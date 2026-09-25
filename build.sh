#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  build.sh — takes this project from a fresh checkout to a playable build.
#
#    ./build.sh            full pipeline (setup → scene → Linux player)
#    ./build.sh setup      TMP essentials + runtime shaders + repair URP materials
#    ./build.sh scene      (re)generate Assets/Scenes/MainScene.unity
#    ./build.sh linux      build the Linux player
#    ./build.sh windows    build the Windows player
#    ./build.sh run        run the last Linux build
#    ./build.sh smoke      headless multi-day run, fails on any exception
#    ./build.sh test       run EditMode unit tests headless (results in Logs/build/)
#    ./build.sh playmode   run PlayMode tests headless; KnownIssue tests run too, non-gating
#    ./build.sh soak       seeded 15-day headless economy run, checked against SoakBands
#    ./build.sh spawnverify  check spawned pack models' size and grounding (SKIP without packs)
#    ./build.sh playtest   spawnverify + playmode + smoke + soak, with a PASS/FAIL/SKIP summary
#    ./build.sh look       render screenshots of the running game into Screenshots/
#    ./build.sh look-diff  look, then compare against Tests/Baselines/Screenshots (never fails)
#    ./build.sh look-approve  copy the current Screenshots/ over the baselines
#    ./build.sh assets     import Asset Store packages you've downloaded via Package Manager
#    ./build.sh assets?    report which downloaded packages are present
#
#  Override the editor with:  UNITY=/path/to/Unity ./build.sh
#  soak takes SEED (default 1) and SOAK_DAYS (default 15) from the environment.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")" && pwd)"
EDITOR_ROOT="${EDITOR_ROOT:-$HOME/Unity/Hub/Editor}"
LOG_DIR="$PROJECT/Logs/build"
PLAYER="$PROJECT/Build/Linux/PetShopSimulator.x86_64"
BASELINES="$PROJECT/Tests/Baselines/Screenshots"

if [[ -z "${UNITY:-}" ]]; then
    UNITY=$(find "$EDITOR_ROOT" -maxdepth 3 -name Unity -type f 2>/dev/null | sort -V | tail -1)
fi
if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
    echo "ERROR: no Unity editor found under $EDITOR_ROOT" >&2
    echo "Install one via Unity Hub, or run: UNITY=/path/to/Unity $0" >&2
    exit 1
fi

mkdir -p "$LOG_DIR"

# A killed or timed-out editor leaves Temp/UnityLockfile behind, and every later batch run
# then fails with "another Unity instance is running" — which looks like a compiler error in
# the log. Clear it when nothing actually holds it.
clear_stale_lock() {
    local lock="$PROJECT/Temp/UnityLockfile"
    [ -f "$lock" ] || return 0
    if ps -eo args= | grep -q "[E]ditor/Unity .*$PROJECT"; then
        echo "✘ another Unity instance has this project open — close it first" >&2
        exit 1
    fi
    echo "· clearing stale Unity lockfile"
    rm -f "$lock"
}
clear_stale_lock

# run_editor <method> <log-name> [--no-quit] [extra Unity args...]
run_editor() {
    local method="$1" name="$2" quit="-quit"
    shift 2
    [[ "${1:-}" == "--no-quit" ]] && { quit=""; shift; }
    local log="$LOG_DIR/$name.log"

    echo "▶ $method"
    local rc=0
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
              -executeMethod "$method" $quit "$@" -logFile "$log" || rc=$?
    if [[ $rc -ne 0 ]]; then
        # Only call it a compile failure when the log says so; otherwise the method itself failed.
        if grep -qE "error CS[0-9]+" "$log" 2>/dev/null; then
            echo "✘ $method failed — compiler errors:" >&2
            grep -E "error CS[0-9]+" "$log" | sort -u | head -40 >&2 || true
            echo "  full log: $log" >&2
        else
            echo "✘ $method failed (exit $rc) — see $log" >&2
        fi
        exit 1
    fi
    grep -E "^\[(ProjectSetup|ShaderInclusion|SceneBuilder|GameBuilder)\]" "$log" | head -5 || true
}

do_setup() {
    # TMP's package import completes on a later editor tick, so this one must not -quit.
    run_editor ProjectSetup.ImportTMPEssentials tmp-import --no-quit
    run_editor ShaderInclusion.EnsureIncluded   shaders
    run_editor MaterialRepair.Repair            materials
}

do_smoke() {
    [[ -x "$PLAYER" ]] || { echo "No build at $PLAYER — run: $0 linux" >&2; exit 1; }
    local log="$LOG_DIR/smoke.log"
    # A throwaway save slot (a directory, so the .bak/.tmp siblings go with it): the smoke run
    # must neither pick up nor overwrite the player's real save.
    local save_dir; save_dir=$(mktemp -d)
    echo "▶ headless smoke run (6 short days)"
    timeout 90 "$PLAYER" -batchmode -nographics -seed 1 -daylength 12 \
        -savepath "$save_dir/save.json" -logFile "$log" >/dev/null 2>&1 || true
    rm -rf "$save_dir"

    # Explicit checks rather than relying on set -e: inside playtest's subshell it is suspended.
    if [[ ! -s "$log" ]]; then
        echo "✘ smoke run wrote no log ($log) — the player did not start" >&2
        exit 1
    fi
    if grep -qiE "Exception|NullReferenceException" "$log"; then
        echo "✘ exceptions during the smoke run:" >&2
        grep -iE -A5 "Exception" "$log" | head -30 >&2
        exit 1
    fi
    local lines
    lines=$(grep -E "^\[(Game|ShopManager)\]" "$log" || true)
    if [[ -z "$lines" ]]; then
        echo "✘ no [Game]/[ShopManager] lines in $log — the game never ran" >&2
        exit 1
    fi
    echo "$lines" | tail -15
    echo "✔ smoke run clean"
}

# Unity tests (EditMode or PlayMode). No -quit: -runTests exits the editor itself once the run finishes.
# Exit codes: 0 all passed, 2 some tests failed, anything else = the run itself broke.
# run_unity_tests <EditMode|PlayMode> [extra Unity args...]
# Results and log go to $LOG_DIR/<label>-results.xml and <label>-tests.log, where the label is
# the lower-cased platform unless TEST_LABEL overrides it.
run_unity_tests() {
    local platform="$1"; shift
    local label="${TEST_LABEL:-${platform,,}}"
    local results="$LOG_DIR/$label-results.xml"
    local log="$LOG_DIR/$label-tests.log"
    rm -f "$results"

    echo "▶ $platform tests${*:+ ($*)}"
    local rc=0
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
             -runTests -testPlatform "$platform" -testResults "$results" "$@" \
             -logFile "$log" || rc=$?

    case "$rc" in
        0)
            if [[ ! -f "$results" ]]; then
                echo "✘ Unity exited 0 but wrote no results ($results) — run unconfirmed" >&2
                echo "  full log: $log" >&2
                exit 1
            fi
            local run attr total="" passed="" failed=""
            run=$(grep -o '<test-run [^>]*>' "$results" | head -1 || true)
            for attr in total passed failed; do
                printf -v "$attr" '%s' "$(echo "$run" | grep -o " $attr=\"[0-9]*\"" | sed -E 's/.*="([0-9]*)"/\1/' || true)"
            done
            echo "✔ $platform tests passed (total ${total:-?}, passed ${passed:-?}, failed ${failed:-?})"
            ;;
        2)
            echo "✘ $platform tests failed:" >&2
            grep -o '<test-case [^>]*result="Failed"[^>]*>' "$results" \
                | sed -E 's/.*fullname="([^"]*)".*/  \1/' >&2 || true
            echo "  results: $results" >&2
            exit 1
            ;;
        *)
            echo "✘ $platform test run failed (exit $rc) — compiler errors:" >&2
            grep -E "error CS[0-9]+" "$log" | sort -u | head -40 >&2 || true
            echo "  full log: $log" >&2
            exit 1
            ;;
    esac
}

# EditMode unit tests.
do_test() {
    run_unity_tests EditMode
}

# PlayMode tests. The gating run excludes [Category("KnownIssue")]; those run afterwards on
# their own and only report — a known issue reproducing is expected, not a failure. Both runs
# happen in subshells so the known-issue run still happens when the gating run fails.
do_playmode() {
    local rc=0
    ( run_unity_tests PlayMode -testCategory '!KnownIssue' ) || rc=$?

    local label="playmode-knownissue"
    local results="$LOG_DIR/$label-results.xml"
    echo "▶ PlayMode known issues (non-gating)"
    ( TEST_LABEL="$label" run_unity_tests PlayMode -testCategory KnownIssue ) >/dev/null 2>&1 || true
    if [[ ! -f "$results" ]]; then
        echo "⚠ known-issue run wrote no results — see $LOG_DIR/$label-tests.log"
    else
        local failures
        failures=$(grep -o '<test-case [^>]*result="Failed"[^>]*>' "$results" \
                       | sed -E 's/.*fullname="([^"]*)".*/\1/' || true)
        if [[ -n "$failures" ]]; then
            echo "$failures" | sed "s|^|⚠ known issue: |"
        elif grep -qE '<test-case [^>]*result="(Inconclusive|Skipped|Ignored)"' "$results" \
             || ! grep -qE '<test-case [^>]*result="(Passed|Failed)"' "$results"; then
            echo "⚠ known issue test did not run (inconclusive)"
        else
            echo "✔ no known issue reproduced"
        fi
    fi

    [[ $rc -eq 0 ]] || exit 1
}

# Economy soak: a seeded multi-day headless run. DayTelemetry appends one JSON line per day
# to soak.jsonl, logs each SoakBands violation as "[Soak] VIOLATION ...", and quits the player
# with exit code 3 when any band was violated (0 when all held).
do_soak() {
    [[ -x "$PLAYER" ]] || { echo "No build at $PLAYER — run: $0 linux" >&2; exit 1; }
    local log="$LOG_DIR/soak.log" jsonl="$LOG_DIR/soak.jsonl" save="$LOG_DIR/soak-save.json"
    # Telemetry appends, and a leftover save changes the run — always start from nothing.
    rm -f "$jsonl" "$save" "$save".*

    echo "▶ headless economy soak (${SOAK_DAYS:-15} days, seed ${SEED:-1})"
    local rc=0
    timeout 400 "$PLAYER" -batchmode -nographics -seed "${SEED:-1}" -daylength 12 \
        -quitafterdays "${SOAK_DAYS:-15}" -savepath "$save" -telemetry "$jsonl" \
        -logFile "$log" >/dev/null 2>&1 || rc=$?

    case "$rc" in
        3)
            echo "✘ soak run out of band:" >&2
            grep -E "\[Soak\] VIOLATION" "$log" | sed "s|^|  |" >&2 || true
            echo "  telemetry: $jsonl" >&2
            exit 1
            ;;
        124)
            echo "✘ soak run timed out after 400 s — see $log" >&2
            exit 1
            ;;
        0) ;;
        *)
            echo "✘ soak player exited with code $rc — see $log" >&2
            exit 1
            ;;
    esac
    if ! grep -q '"summary":true' "$jsonl" 2>/dev/null; then
        echo "✘ soak run wrote no summary line to $jsonl — see $log" >&2
        exit 1
    fi
    if grep -qiE "Exception|NullReferenceException" "$log"; then
        echo "✘ exceptions during the soak run:" >&2
        grep -iE -A5 "Exception" "$log" | head -30 >&2
        exit 1
    fi
    print_soak_table "$jsonl"
    echo "✔ soak run in band"
}

# print_soak_table <soak.jsonl> — one row per day, then the summary; raw lines without jq.
print_soak_table() {
    local jsonl="$1"
    [[ -f "$jsonl" ]] || { echo "⚠ no telemetry written ($jsonl)"; return 0; }
    if ! command -v jq >/dev/null 2>&1; then
        sed "s|^|  |" "$jsonl"
        return 0
    fi
    local fmt='  %4s %6s %9s %9s %8s %7s %7s %9s %6s %9s\n'
    # shellcheck disable=SC2059
    printf "$fmt" day sales revenue checkouts walkouts gaveUp navT/O stranded rep balance
    jq -r 'select(.summary != true)
           | [.day, .sales, (.revenue * 100 | round / 100), .checkouts, .walkoutsEmpty, .gaveUp,
              .navTimeouts, .strandedCheckouts, .reputation, (.balance * 100 | round / 100)]
           | @tsv' "$jsonl" \
        | while IFS=$'\t' read -r -a row; do printf "$fmt" "${row[@]}"; done || true
    jq -r 'select(.summary == true)
           | "  \(.days) day(s), seed \(.seed), \(.violations) violation(s)"' "$jsonl" || true
}

# Spawn the key pack models and check their size and grounding. With no asset pack installed
# at all — the normal state of a fresh clone — SpawnVerify logs "[Verify] SKIPPED" and exits 0.
do_spawnverify() {
    local log="$LOG_DIR/spawnverify.log"
    if ! ( run_editor SpawnVerify.Verify spawnverify ); then
        grep -E "^\[Verify\].*(MISSING|FAILED|<<<|missing|problem)" "$log" | head -30 >&2 || true
        exit 1
    fi
    if spawnverify_skipped; then
        echo "· SpawnVerify skipped — no asset packs installed"
    else
        grep -E "^\[Verify\] [0-9]+ models checked" "$log" || true
    fi
}

spawnverify_skipped() {
    grep -q "^\[Verify\] SKIPPED" "$LOG_DIR/spawnverify.log" 2>/dev/null
}

# Every stage runs even if an earlier one failed: each runs in a subshell, because the do_*
# functions exit on failure. Inside `( do_x ) || rc=$?` set -e is suspended, so a failing
# command no longer aborts the stage — every do_* stage must fail through an explicit exit.
do_playtest() {
    local stage rc failed=0
    local -a summary=()
    for stage in spawnverify playmode smoke soak; do
        echo ""
        rc=0
        ( "do_$stage" ) || rc=$?
        if [[ $rc -ne 0 ]]; then
            summary+=("FAIL  $stage"); failed=1
        elif [[ $stage == spawnverify ]] && spawnverify_skipped; then
            summary+=("SKIP  $stage")
        else
            summary+=("PASS  $stage")
        fi
    done

    echo ""
    echo "── playtest ──"
    printf '  %s\n' "${summary[@]}"
    if [[ $failed -ne 0 ]]; then
        echo "✘ playtest failed" >&2
        exit 1
    fi
    echo "✔ playtest passed"
}

# Non-gating visual check: a diff is for a human to review, so this never fails the build.
do_look_diff() {
    ( do_look ) || echo "⚠ screenshot capture failed — comparing whatever is in Screenshots/"
    if ( run_editor ScreenshotDiff.Compare screenshot-diff \
             -baseline "$BASELINES" -current "$PROJECT/Screenshots" ); then
        grep -E "^\[ScreenshotDiff\]" "$LOG_DIR/screenshot-diff.log" | sed "s|^|  |" || true
        echo "  diff images: $PROJECT/Logs/screenshot-diff/"
    else
        echo "⚠ screenshot comparison did not run — see $LOG_DIR/screenshot-diff.log"
    fi
    return 0
}

# Photograph the running game so it can be reviewed without sitting in front of it.
# Deliberately NOT -nographics: the editor needs a real graphics device to render, and it
# gets one from the DRM render node given DISPLAY and XAUTHORITY — even where opening an
# actual game window from a non-desktop shell fails. No -quit either: play mode has to run,
# and SceneShot exits the editor itself once the tour is done.
do_look() {
    local out="$PROJECT/Screenshots"
    rm -rf "$out"; mkdir -p "$out"

    : "${DISPLAY:=:0}"
    if [ -z "${XAUTHORITY:-}" ]; then
        XAUTHORITY=$(find /run/user/"$(id -u)" -maxdepth 1 -name 'xauth_*' 2>/dev/null | head -1)
    fi
    export DISPLAY XAUTHORITY

    echo "▶ rendering the game (DISPLAY=$DISPLAY)"
    if ! timeout 600 "$UNITY" -batchmode -projectPath "$PROJECT" \
            -executeMethod SceneShot.Capture -tour "$out" \
            -logFile "$LOG_DIR/look.log"; then
        echo "✘ capture failed — see $LOG_DIR/look.log" >&2
        grep -E "^\[SceneShot\]|^\[Tour\]|error CS" "$LOG_DIR/look.log" | tail -10 >&2 || true
        exit 1
    fi
    grep -E "^\[Tour\] done" "$LOG_DIR/look.log" || true
    ls "$out"/*.png 2>/dev/null | sed "s|^|  |"
}

case "${1:-all}" in
    setup)   do_setup ;;
    scene)   run_editor SceneBuilder.BuildMainScene scene ;;
    linux)   run_editor GameBuilder.BuildLinux   player-linux ;;
    windows) run_editor GameBuilder.BuildWindows player-windows ;;
    smoke)   do_smoke ;;
    test)    do_test ;;
    playmode)    do_playmode ;;
    soak)        do_soak ;;
    spawnverify) do_spawnverify ;;
    playtest)    do_playtest ;;
    look)    do_look ;;
    look-diff)   do_look_diff; exit 0 ;;
    look-approve)
        run_editor ScreenshotDiff.Approve screenshot-approve \
            -baseline "$BASELINES" -current "$PROJECT/Screenshots"
        grep -E "^\[ScreenshotDiff\]" "$LOG_DIR/screenshot-approve.log" || true
        ;;
    assets)
        # Unity's own package importer cannot be driven from batch mode: any package
        # containing C# triggers a domain reload mid-import, which destroys the state
        # tracking progress and leaves the editor hanging forever. Extracting the archives
        # ourselves (a .unitypackage is a gzipped tar) sidesteps that — Unity then imports
        # the resulting files as ordinary assets, which it handles fine.
        python3 "$PROJECT/Tools/unpack_unitypackage.py" "$PROJECT" || exit 1
        python3 "$PROJECT/Tools/organise_packs.py"       "$PROJECT" || exit 1
        run_editor MaterialRepair.Repair materials
        echo "✔ asset packages extracted and materials repaired"
        ;;
    assets?) run_editor AssetStoreImporter.Report    assetstore-report ;;
    run)     exec "$PLAYER" ;;
    all)
        do_setup
        run_editor SceneBuilder.BuildMainScene scene
        run_editor GameBuilder.BuildLinux player-linux
        do_smoke
        echo ""
        echo "✔ Done. Play it with:  $PLAYER"
        ;;
    *) echo "unknown target '$1' — see the header of $0" >&2; exit 1 ;;
esac
