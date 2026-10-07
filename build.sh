#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  build.sh — takes this project from a fresh checkout to a playable build.
#
#    ./build.sh            full pipeline (setup → Linux player → smoke)
#    ./build.sh setup      TMP essentials + Nunito font (if missing) + runtime shaders + repair URP materials
#    ./build.sh font       (re)generate the Nunito TMP font asset Assets/Resources/Fonts/Nunito SDF.asset
#    ./build.sh linux      build the Linux player
#    ./build.sh windows    build the Windows player
#    ./build.sh run        run the Linux player, rebuilding it first if sources are newer
#    ./build.sh smoke      headless multi-day run, fails on any exception (rebuilds if stale)
#    ./build.sh test       run EditMode unit tests headless (results in Logs/build/)
#    ./build.sh playmode   run PlayMode tests headless; KnownIssue tests run too, non-gating
#    ./build.sh soak       seeded 6-day headless economy run at real day length, checked against SoakBands (rebuilds if stale)
#    ./build.sh spawnverify  check spawned pack models' size and grounding (SKIP without packs)
#    ./build.sh playtest   spawnverify + playmode + smoke + soak, with a PASS/FAIL/SKIP summary
#    ./build.sh look       render screenshots of the running game into Screenshots/
#                          (LOOK_LANG=uk|en renders in that UI language, not saved; unset = saved language)
#    ./build.sh look-diff  look, then compare against Tests/Baselines/Screenshots (a diff never
#                          fails; a failed capture or comparison does)
#    ./build.sh look-approve  copy the current Screenshots/ over the baselines
#    ./build.sh assets     import Asset Store packages you've downloaded via Package Manager
#    ./build.sh assets?    report which downloaded packages are present
#    ./build.sh kenney     restore the CC0 Kenney kits (other checkout → Library/kenney-cache → kenney.nl)
#
#  Most targets run the kenney step first; KENNEY_SRC=/path/to/checkout picks where kits are copied from.
#  setup and linux generate the Nunito font asset first when it is missing (it is checked in; regenerate with ./build.sh font).
#
#  Override the editor with:  UNITY=/path/to/Unity ./build.sh
#  soak takes SEED (default 1), SOAK_DAYS (default 6) and SOAK_TIMESCALE (default 8, 1-20) from the
#  environment. Days are the real 540 s, fast-forwarded by SOAK_TIMESCALE, so a run takes about
#  SOAK_DAYS x 540 / SOAK_TIMESCALE seconds (6 days at 8x: ~7 min; 15 days would be ~17 min).
#  run/smoke/soak rebuild the player when anything under Assets/, ProjectSettings/ or Packages/
#  is newer than it; FORCE_BUILD=1 always rebuilds. A failed build never runs the old player.
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
        echo "✘ a Unity Editor or batch run has the project open - close the Editor / wait for the other run (then optionally FORCE_BUILD=1) and retry" >&2
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
    rm -f "$log"   # never judge this run by a previous run's log

    echo "▶ $method"
    local rc=0
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
              -executeMethod "$method" $quit "$@" -logFile "$log" || rc=$?
    if [[ $rc -ne 0 ]]; then
        # Only call it a compile failure when the log says so; otherwise the method itself failed.
        if grep -q "another Unity instance is running" "$log" 2>/dev/null; then
            echo "✘ $method failed — Unity Editor has the project open - close the Editor (then optionally FORCE_BUILD=1) and retry" >&2
        elif grep -qE "error CS[0-9]+" "$log" 2>/dev/null; then
            echo "✘ $method failed — compiler errors:" >&2
            grep -E "error CS[0-9]+" "$log" | sort -u | head -40 >&2 || true
            echo "  full log: $log" >&2
        else
            echo "✘ $method failed (exit $rc) — see $log" >&2
        fi
        exit 1
    fi
    grep -E "^\[(ProjectSetup|ShaderInclusion|GameBuilder|FontAssetBuilder)\]" "$log" | head -5 || true
}

# The Nunito TMP SDF font asset UIFactory loads from Resources; generated from Assets/Fonts/.
FONT_ASSET="$PROJECT/Assets/Resources/Fonts/Nunito SDF.asset"

# (Re)generate the font asset and prove it was written. Exits non-zero otherwise.
build_font() {
    run_editor FontAssetBuilder.BuildNunito font
    if [[ ! -f "$FONT_ASSET" ]]; then
        echo "✘ font build wrote no asset at $FONT_ASSET — see $LOG_DIR/font.log" >&2
        exit 1
    fi
}

# What the font asset is generated from: a change to either means the asset is stale.
FONT_SOURCES=("$PROJECT/Assets/Fonts/Nunito-Regular.ttf" "$PROJECT/Assets/Editor/FontAssetBuilder.cs")
# Committed sha256 of FONT_SOURCES the asset was generated from. Git keeps no mtimes, so content,
# not age, decides staleness. The leading dot makes Unity skip it (no import, no .meta).
FONT_STAMP="$PROJECT/Assets/Resources/Fonts/.Nunito SDF.hash"

# sha256 of the font sources' contents, independent of where the project is checked out.
font_sources_hash() {
    local src
    for src in "${FONT_SOURCES[@]}"; do
        sha256sum < "$src"
    done | sha256sum | cut -d' ' -f1
}

# Generate the font asset when it is missing or its stamp differs from the sources' hash, then
# write the stamp. An existing asset with no stamp is trusted once: only the stamp is written.
ensure_font() {
    local hash
    hash=$(font_sources_hash)
    if [[ ! -f "$FONT_ASSET" ]]; then
        echo "· Nunito font asset is missing — generating it"
        build_font
    elif [[ ! -f "$FONT_STAMP" ]]; then
        echo "· Nunito font asset has no source stamp — trusting it and writing $(basename "$FONT_STAMP")"
    elif [[ "$(cat "$FONT_STAMP")" != "$hash" ]]; then
        echo "· Nunito font sources changed since the asset was generated — regenerating it"
        build_font
    else
        return 0   # a bare return would pass on the failed [[ ]] status and trip set -e
    fi
    echo "$hash" > "$FONT_STAMP"
}

do_setup() {
    # TMP's package import completes on a later editor tick, so this one must not -quit.
    run_editor ProjectSetup.ImportTMPEssentials tmp-import --no-quit
    ensure_font
    run_editor ShaderInclusion.EnsureIncluded   shaders
    run_editor MaterialRepair.Repair            materials
}

# Unity's incremental player build does not necessarily rewrite the executable (it is a copied
# engine stub), so its mtime says nothing about freshness. build_linux instead touches this stamp
# when a build STARTS and keeps it only if the build succeeds; sources edited mid-build stay newer.
BUILD_STAMP="$PROJECT/Build/Linux/.build-stamp"

# True when the Linux player or its build stamp is missing, or any project source is newer than
# the last successful build.
player_is_stale() {
    [[ -x "$PLAYER" && -f "$BUILD_STAMP" ]] || return 0
    [[ -n "$(find "$PROJECT/Assets" "$PROJECT/ProjectSettings" "$PROJECT/Packages" \
                 -type f -newer "$BUILD_STAMP" ! -name '*.meta' -print -quit 2>/dev/null)" ]]
}

# Build the Linux player and prove it happened: this run's log (run_editor deletes the old one)
# must report "[GameBuilder] Build succeeded" and no build failure, and the player must exist.
# Exits non-zero otherwise. Compiler errors in assemblies the player does not include (e.g. test
# assemblies) do not fail the build; errors in player code make BuildPlayer itself fail.
build_linux() {
    local log="$LOG_DIR/player-linux.log" pending="$LOG_DIR/.build-stamp.pending"
    # Before the pending stamp is touched, so a freshly generated font is older than the stamp.
    ensure_font
    rm -f "$BUILD_STAMP"
    touch "$pending"
    run_editor GameBuilder.BuildLinux player-linux
    if grep -qE "Build failed|BuildFailedException|\[GameBuilder\] Build (Failed|Cancelled|Unknown)" "$log" 2>/dev/null; then
        echo "✘ Linux build failed:" >&2
        grep -E "Build failed|BuildFailedException|error CS[0-9]+|\[GameBuilder\]" "$log" | sort -u | head -20 >&2 || true
        echo "  full log: $log" >&2
        exit 1
    fi
    if ! grep -q "^\[GameBuilder\] Build succeeded" "$log" 2>/dev/null || [[ ! -x "$PLAYER" ]]; then
        echo "✘ Linux build did not report success or produced no player at $PLAYER — see $log" >&2
        exit 1
    fi
    mv -f "$pending" "$BUILD_STAMP" || { echo "✘ could not write build stamp $BUILD_STAMP" >&2; exit 1; }
}

# Rebuild the player when it is stale (or FORCE_BUILD=1), so nothing ever runs an old binary.
ensure_fresh_player() {
    if [[ "${FORCE_BUILD:-0}" == 1 ]] || player_is_stale; then
        echo "· Linux player is missing or older than the sources — rebuilding"
        ensure_kenney
        build_linux
    fi
}

do_smoke() {
    ensure_fresh_player
    local log="$LOG_DIR/smoke.log"
    rm -f "$log"   # a leftover log from an earlier run must not pass for this one
    # A throwaway save slot (a directory, so the .bak/.tmp siblings go with it): the smoke run
    # must neither pick up nor overwrite the player's real save.
    local save_dir; save_dir=$(mktemp -d)
    echo "▶ headless smoke run (6 short days)"
    timeout 90 "$PLAYER" -batchmode -nographics -seed 1 -daylength 12 -furnish \
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
    rm -f "$results" "$log"

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
            if [[ -z "${total:-}" || "$total" == 0 ]]; then
                echo "✘ $platform run reported no tests — nothing was verified ($results)" >&2
                exit 1
            fi
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
            if grep -q "another Unity instance is running" "$log" 2>/dev/null; then
                echo "✘ $platform test run failed — Unity Editor has the project open - close the Editor and retry" >&2
                exit 1
            fi
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

# Economy soak: a seeded multi-day headless run at the real day length, fast-forwarded with
# -timescale. DayTelemetry appends one JSON line per day to soak.jsonl, logs each SoakBands and
# hourly queue-join violation as "[Soak] VIOLATION ...", and quits the player with exit code 3
# when any band was violated (0 when all held). A bankrupt shop ("[Game] GAME OVER") also ends
# the run early, with exit code 0 when no band broke, so that is checked separately.
SOAK_DAY_LENGTH=540
do_soak() {
    local days="${SOAK_DAYS:-6}" scale="${SOAK_TIMESCALE:-8}" seed="${SEED:-1}"
    if ! [[ "$days" =~ ^[1-9][0-9]*$ ]]; then
        echo "✘ SOAK_DAYS must be a positive integer, got '$days'" >&2
        exit 1
    fi
    if ! [[ "$scale" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
        echo "✘ SOAK_TIMESCALE must be a number (1-20), got '$scale'" >&2
        exit 1
    fi
    # The game clamps -timescale to 1-20; budget for the speed it will actually run at, plus a
    # quarter for slow frames and two minutes for boot, NavMesh bake and day transitions.
    local limit
    limit=$(awk -v d="$days" -v l="$SOAK_DAY_LENGTH" -v s="$scale" \
        'BEGIN { if (s < 1) s = 1; if (s > 20) s = 20; printf "%d", d * l / s * 1.25 + 120 }')

    ensure_fresh_player
    local log="$LOG_DIR/soak.log" jsonl="$LOG_DIR/soak.jsonl" save="$LOG_DIR/soak-save.json"
    # Telemetry appends, and a leftover save changes the run — always start from nothing.
    rm -f "$log" "$jsonl" "$save" "$save".*

    echo "▶ headless economy soak ($days days of ${SOAK_DAY_LENGTH}s at ${scale}x, seed $seed, timeout ${limit}s)"
    local rc=0
    timeout "$limit" "$PLAYER" -batchmode -nographics -seed "$seed" \
        -daylength "$SOAK_DAY_LENGTH" -timescale "$scale" -furnish \
        -quitafterdays "$days" -savepath "$save" -telemetry "$jsonl" \
        -logFile "$log" >/dev/null 2>&1 || rc=$?

    grep -E "^\[Soak\] Finished" "$log" 2>/dev/null | tail -1 || true

    # Any violation fails the run, whatever the exit code; hourly queue-join violations
    # ("hour=H queueJoins=N") are among them.
    local violations
    violations=$(grep -E "\[Soak\] VIOLATION" "$log" 2>/dev/null || true)
    local failed=0
    if [[ $rc -eq 124 ]]; then
        echo "✘ soak run timed out after ${limit}s ($days days at ${scale}x) — see $log" >&2
        failed=1
    fi
    if grep -qE "^\[Game\] GAME OVER" "$log" 2>/dev/null; then
        echo "✘ soak shop went bankrupt before $days days: $(grep -E "^\[Game\] GAME OVER" "$log" | head -1)" >&2
        failed=1
    fi
    if [[ -n "$violations" ]]; then
        echo "✘ soak run out of band ($(echo "$violations" | wc -l) violation(s)):" >&2
        echo "$violations" | sed "s|^|  |" >&2
        failed=1
    fi
    if [[ $rc -ne 0 && $rc -ne 3 && $rc -ne 124 ]]; then
        echo "✘ soak player exited with code $rc — see $log" >&2
        failed=1
    elif [[ $rc -eq 3 && -z "$violations" ]]; then
        echo "✘ soak player reported violations (exit 3) but none were logged — see $log" >&2
        failed=1
    fi
    if [[ $failed -ne 0 ]]; then
        echo "  telemetry: $jsonl" >&2
        print_soak_table "$jsonl" >&2
        exit 1
    fi
    if ! grep -q '"summary":true' "$jsonl" 2>/dev/null; then
        echo "✘ soak run wrote no summary line to $jsonl — see $log" >&2
        exit 1
    fi
    if grep -qiE "Exception|NullReferenceException" "$log"; then
        echo "✘ exceptions during the soak run:" >&2
        grep -iE -A5 "Exception" "$log" | head -30 >&2
        exit 1
    fi
    local done_days
    done_days=$(grep '"summary":true' "$jsonl" | tail -1 | sed -nE 's/.*"days":([0-9]+).*/\1/p')
    if [[ "${done_days:-0}" -lt "$days" ]]; then
        echo "✘ soak run ended after ${done_days:-0} of $days days — see $log" >&2
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
    jq -r 'select(.summary != true and .walkout != true)
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

# Visual check: a diff is for a human to review, so a difference never fails the build — but a
# capture or comparison that did not run verified nothing, and that does fail.
do_look_diff() {
    if ! ( do_look ); then
        echo "✘ screenshot capture failed — nothing current to compare" >&2
        exit 1
    fi
    if ( run_editor ScreenshotDiff.Compare screenshot-diff \
             -baseline "$BASELINES" -current "$PROJECT/Screenshots" ); then
        grep -E "^\[ScreenshotDiff\]" "$LOG_DIR/screenshot-diff.log" | sed "s|^|  |" || true
        echo "  diff images: $PROJECT/Logs/screenshot-diff/"
    else
        echo "✘ screenshot comparison did not run — see $LOG_DIR/screenshot-diff.log" >&2
        exit 1
    fi
}

# Photograph the running game so it can be reviewed without sitting in front of it.
# Deliberately NOT -nographics: the editor needs a real graphics device to render, and it
# gets one from the DRM render node given DISPLAY and XAUTHORITY — even where opening an
# actual game window from a non-desktop shell fails. No -quit either: play mode has to run,
# and SceneShot exits the editor itself once the tour is done.
do_look() {
    local out="$PROJECT/Screenshots"
    rm -rf "$out"; mkdir -p "$out"
    rm -f "$LOG_DIR/look.log"

    : "${DISPLAY:=:0}"
    if [ -z "${XAUTHORITY:-}" ]; then
        XAUTHORITY=$(find /run/user/"$(id -u)" -maxdepth 1 -name 'xauth_*' 2>/dev/null | head -1)
    fi
    export DISPLAY XAUTHORITY

    echo "▶ rendering the game (DISPLAY=$DISPLAY)"
    if ! timeout 600 "$UNITY" -batchmode -projectPath "$PROJECT" \
            -executeMethod SceneShot.Capture -tour "$out" \
            ${LOOK_LANG:+-lang "$LOOK_LANG"} \
            -logFile "$LOG_DIR/look.log"; then
        echo "✘ capture failed — see $LOG_DIR/look.log" >&2
        grep -E "^\[SceneShot\]|^\[Tour\]|error CS" "$LOG_DIR/look.log" | tail -10 >&2 || true
        exit 1
    fi
    grep -E "^\[Tour\] done" "$LOG_DIR/look.log" || true
    if ! compgen -G "$out/*.png" >/dev/null; then
        echo "✘ capture wrote no screenshots to $out — see $LOG_DIR/look.log" >&2
        exit 1
    fi
    ls "$out"/*.png | sed "s|^|  |"

    # A glyph the font lacks draws as an empty box; TMP only says so in the log.
    local missing
    missing=$(grep -E "was not found in the|[Cc]haracter with (ASCII|Unicode) value" \
                   "$LOG_DIR/look.log" | sort -u || true)
    if [[ -n "$missing" ]]; then
        echo "✘ TMP reported missing glyphs during the tour — see $LOG_DIR/look.log:" >&2
        echo "$missing" | head -40 | sed "s|^|  |" >&2
        exit 1
    fi
}

# The Kenney kits are gitignored, so a fresh clone or worktree has none; restore them first.
ensure_kenney() {
    bash "$PROJECT/Tools/fetch_kenney.sh" "$PROJECT" || exit 1
}

case "${1:-all}" in
    setup|linux|windows|test|playmode|soak|spawnverify|playtest|look|look-diff|all|assets)
        ensure_kenney ;;
esac

case "${1:-all}" in
    setup)   do_setup ;;
    linux)   build_linux ;;
    font)    build_font ;;
    windows) run_editor GameBuilder.BuildWindows player-windows ;;
    # Retired: MainScene.unity is authored in the editor now. Kept as a no-op so old
    # check commands and scripts that still call it do not fail; it never touches the scene.
    scene)   echo "▶ scene: no-op — MainScene.unity is hand-authored, nothing is generated" ;;
    smoke)   do_smoke ;;
    test)    do_test ;;
    playmode)    do_playmode ;;
    soak)        do_soak ;;
    spawnverify) do_spawnverify ;;
    playtest)    do_playtest ;;
    look)    do_look ;;
    look-diff)   do_look_diff ;;
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
    run)     ensure_fresh_player; exec "$PLAYER" ;;
    kenney)  ensure_kenney ;;
    all)
        do_setup
        build_linux
        do_smoke
        echo ""
        echo "✔ Done. Play it with:  $PLAYER"
        ;;
    *) echo "unknown target '$1' — see the header of $0" >&2; exit 1 ;;
esac
