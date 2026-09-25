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
#    ./build.sh look       render screenshots of the running game into Screenshots/
#    ./build.sh assets     import Asset Store packages you've downloaded via Package Manager
#    ./build.sh assets?    report which downloaded packages are present
#
#  Override the editor with:  UNITY=/path/to/Unity ./build.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")" && pwd)"
EDITOR_ROOT="${EDITOR_ROOT:-$HOME/Unity/Hub/Editor}"
LOG_DIR="$PROJECT/Logs/build"
PLAYER="$PROJECT/Build/Linux/PetShopSimulator.x86_64"

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

# run_editor <method> <log-name> [--no-quit]
run_editor() {
    local method="$1" name="$2" quit="-quit"
    [[ "${3:-}" == "--no-quit" ]] && quit=""
    local log="$LOG_DIR/$name.log"

    echo "▶ $method"
    if ! "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
                  -executeMethod "$method" $quit -logFile "$log"; then
        echo "✘ $method failed — compiler errors:" >&2
        grep -E "error CS[0-9]+" "$log" | sort -u | head -40 >&2 || true
        echo "  full log: $log" >&2
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
    echo "▶ headless smoke run (6 short days)"
    timeout 90 "$PLAYER" -batchmode -nographics -daylength 12 -logFile "$log" >/dev/null 2>&1 || true

    if grep -qiE "Exception|NullReferenceException" "$log"; then
        echo "✘ exceptions during the smoke run:" >&2
        grep -iE -A5 "Exception" "$log" | head -30 >&2
        exit 1
    fi
    grep -E "^\[(Game|ShopManager)\]" "$log" | tail -15
    echo "✔ smoke run clean"
}

# EditMode unit tests. No -quit: -runTests exits the editor itself once the run finishes.
# Exit codes: 0 all passed, 2 some tests failed, anything else = the run itself broke.
do_test() {
    local results="$LOG_DIR/editmode-results.xml"
    local log="$LOG_DIR/editmode-tests.log"
    rm -f "$results"

    echo "▶ EditMode tests"
    local rc=0
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
             -runTests -testPlatform EditMode -testResults "$results" \
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
            echo "✔ EditMode tests passed (total ${total:-?}, passed ${passed:-?}, failed ${failed:-?})"
            ;;
        2)
            echo "✘ EditMode tests failed:" >&2
            grep -o '<test-case [^>]*result="Failed"[^>]*>' "$results" \
                | sed -E 's/.*fullname="([^"]*)".*/  \1/' >&2 || true
            echo "  results: $results" >&2
            exit 1
            ;;
        *)
            echo "✘ EditMode test run failed (exit $rc) — compiler errors:" >&2
            grep -E "error CS[0-9]+" "$log" | sort -u | head -40 >&2 || true
            echo "  full log: $log" >&2
            exit 1
            ;;
    esac
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
    look)    do_look ;;
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
