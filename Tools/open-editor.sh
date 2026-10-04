#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  open-editor.sh — open the Unity editor on THIS checkout (worktree) and prove it.
#
#    Tools/open-editor.sh [--fresh] [--approve-mcp] [--force-other]
#
#  Run it from inside the worktree you want open. See --help for the flags.
#  Override the editor with:  UNITY=/path/to/Unity Tools/open-editor.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

readonly VERIFY_TIMEOUT_SECONDS=180
readonly VERIFY_INTERVAL_SECONDS=3
readonly EXIT_USAGE=2

FRESH=0
APPROVE_MCP=0
FORCE_OTHER=0
LAUNCHED_PID=""   # set only when this script launches the editor

usage() {
    cat <<'EOF'
Usage: Tools/open-editor.sh [--fresh] [--approve-mcp] [--force-other]

Opens the Unity editor on the git worktree you run it from, detached from the
terminal, logging to Logs/editor.log, then verifies the editor really has THIS
path open. If an editor already has this worktree open it is not relaunched.

  --fresh         Delete Library/ and Temp/ first, forcing a full reimport (slow).
                  Library/ is gitignored, so a new worktree already starts empty.
                  Refused while an editor has this worktree open.
  --approve-mcp   Before launching, run a batch editor that sets the Unity MCP
                  server's "direct" connection policy to allowed / no approval
                  (PetShop.EditorTools.McpAccess.AllowDirect). EditorPrefs are
                  MACHINE-WIDE, so this affects every project on this machine.
                  Refused while an editor has this worktree open.
  --force-other   Launch even though an editor is open on a different project.
  -h, --help      Show this help.

Environment: UNITY=/path/to/Unity overrides the editor binary;
EDITOR_ROOT (default ~/Unity/Hub/Editor) is where editors are looked for.
EOF
}

parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --fresh)       FRESH=1 ;;
            --approve-mcp) APPROVE_MCP=1 ;;
            --force-other) FORCE_OTHER=1 ;;
            -h|--help)     usage; exit 0 ;;
            *)             echo "unknown option '$1'" >&2; usage >&2; exit "$EXIT_USAGE" ;;
        esac
        shift
    done
}

# (1) The worktree root. Must be a Unity project checkout.
resolve_root() {
    if ! ROOT=$(git rev-parse --show-toplevel 2>/dev/null); then
        echo "✘ not inside a git repository — run this from the worktree to open" >&2
        exit 1
    fi
    if [[ ! -d "$ROOT/Assets" || ! -d "$ROOT/ProjectSettings" ]]; then
        echo "✘ $ROOT has no Assets/ and ProjectSettings/ — not a Unity project" >&2
        exit 1
    fi
    echo "· worktree: $ROOT"
}

# (2) The editor binary — same rules as build.sh, but preferring the project's own version.
resolve_unity() {
    local editor_root="${EDITOR_ROOT:-$HOME/Unity/Hub/Editor}"
    if [[ -z "${UNITY:-}" ]]; then
        local version=""
        version=$(sed -nE 's/^m_EditorVersion:[[:space:]]*([^[:space:]]+).*/\1/p' \
                      "$ROOT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | head -1 || true)
        if [[ -n "$version" && -x "$editor_root/$version/Editor/Unity" ]]; then
            UNITY="$editor_root/$version/Editor/Unity"
        else
            UNITY=$(find "$editor_root" -maxdepth 3 -name Unity -type f 2>/dev/null | sort -V | tail -1 || true)
        fi
    fi
    if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
        echo "ERROR: no Unity editor found under $editor_root" >&2
        echo "Install one via Unity Hub, or run: UNITY=/path/to/Unity $0" >&2
        exit 1
    fi
    echo "· editor: $UNITY"
}

# Prints "<pid> <project path>" for every running Unity editor started with -projectPath.
list_unity_editors() {
    ps -eo pid,args | awk '
        NR > 1 && ($2 ~ /\/Unity$/ || $2 == "Unity") {
            for (i = 3; i < NF; i++) {
                if (tolower($i) == "-projectpath") { print $1, $(i + 1); break }
            }
        }' || true
}

# Absolute, trailing-slash-free form of a -projectPath, resolved against that process's cwd.
normalise_path() {
    local pid="$1" path="$2"
    [[ "$path" == /* ]] || path="/proc/$pid/cwd/$path"
    path=$(readlink -f "$path" 2>/dev/null || echo "$path")
    echo "${path%/}"
}

# Sets RUNNING_PID (editor on ROOT) and OTHER_EDITORS ("<pid> <path>" lines for other projects).
scan_editors() {
    RUNNING_PID=""
    OTHER_EDITORS=""
    local pid path
    while read -r pid path; do
        [[ -n "$pid" ]] || continue
        path=$(normalise_path "$pid" "$path")
        if [[ "$path" == "$ROOT" ]]; then
            RUNNING_PID="$pid"
        else
            OTHER_EDITORS+="$pid $path"$'\n'
        fi
    done < <(list_unity_editors)
}

# (3) Reports running editors; fails on another project's editor unless --force-other.
detect_running() {
    scan_editors
    local pid path
    while read -r pid path; do
        if [[ -n "$pid" ]]; then
            echo "⚠ Unity editor PID $pid has a different project open: $path" >&2
        fi
    done <<< "$OTHER_EDITORS"
    if [[ -n "$RUNNING_PID" ]]; then
        echo "· Unity editor PID $RUNNING_PID already has this worktree open"
    elif [[ -n "$OTHER_EDITORS" && $FORCE_OTHER -eq 0 ]]; then
        echo "✘ another project is open in Unity — close it, or pass --force-other" >&2
        exit 1
    fi
}

# Mirrors build.sh: a killed editor leaves Temp/UnityLockfile behind; clear it when nothing holds it.
clear_stale_lock() {
    local lock="$ROOT/Temp/UnityLockfile"
    [ -f "$lock" ] || return 0
    scan_editors
    if [[ -n "$RUNNING_PID" ]]; then
        echo "✘ Unity editor PID $RUNNING_PID has this project open — close it first" >&2
        exit 1
    fi
    echo "· clearing stale Unity lockfile"
    rm -f "$lock"
}

do_fresh() {
    if [[ -n "$RUNNING_PID" ]]; then
        echo "✘ --fresh refused: editor PID $RUNNING_PID has this worktree open — close it first" >&2
        exit 1
    fi
    echo "⚠ --fresh: deleting Library/ and Temp/ — the next open is a full reimport and is slow."
    echo "  (Library/ is gitignored, so a brand-new worktree already starts without one.)"
    rm -rf "$ROOT/Library" "$ROOT/Temp"
}

do_approve_mcp() {
    if [[ -n "$RUNNING_PID" ]]; then
        echo "✘ --approve-mcp refused: editor PID $RUNNING_PID has this worktree open — close it first" >&2
        exit 1
    fi
    mkdir -p "$ROOT/Logs/build"
    local log="$ROOT/Logs/build/mcp-access.log"
    echo "▶ allowing direct MCP connections (machine-wide EditorPrefs)"
    local rc=0
    "$UNITY" -batchmode -nographics -projectPath "$ROOT" \
             -executeMethod PetShop.EditorTools.McpAccess.AllowDirect -quit -logFile "$log" || rc=$?
    if [[ $rc -ne 0 ]]; then
        echo "✘ McpAccess.AllowDirect failed (exit $rc) — see $log" >&2
        exit 1
    fi
    grep -E "^\[McpAccess\]" "$log" | head -5 || true
}

# (5) Detached launch: survives the terminal closing.
launch_editor() {
    mkdir -p "$ROOT/Logs"
    rm -f "$ROOT/Logs/editor.log"   # so verification cannot match a previous session's log
    nohup setsid "$UNITY" -projectPath "$ROOT" -logFile "$ROOT/Logs/editor.log" >/dev/null 2>&1 &
    LAUNCHED_PID=$!
    echo "▶ launched Unity (PID $LAUNCHED_PID), log: $ROOT/Logs/editor.log"
}

# ps must show an editor on ROOT; for an editor THIS script launched, its log must also name ROOT.
# A pre-existing (e.g. Hub-launched) editor logs elsewhere, so it is checked by ps only.
editor_has_root() {
    scan_editors
    [[ -n "$RUNNING_PID" ]] || return 1
    [[ -z "$LAUNCHED_PID" ]] && return 0
    grep -qF "$ROOT" "$ROOT/Logs/editor.log" 2>/dev/null
}

# (6) Poll until editor_has_root confirms the editor on ROOT.
verify_editor() {
    local waited=0
    echo "· verifying (up to ${VERIFY_TIMEOUT_SECONDS}s)..."
    while (( waited < VERIFY_TIMEOUT_SECONDS )); do
        if editor_has_root; then
            local how="confirmed in Logs/editor.log"
            [[ -z "$LAUNCHED_PID" ]] && how="pre-existing editor, confirmed via ps"
            echo "✔ OK: editor PID $RUNNING_PID has $ROOT open ($how)"
            return 0
        fi
        sleep "$VERIFY_INTERVAL_SECONDS"
        waited=$(( waited + VERIFY_INTERVAL_SECONDS ))
    done
    echo "✘ FAIL: no editor confirmed on $ROOT after ${VERIFY_TIMEOUT_SECONDS}s — see $ROOT/Logs/editor.log" >&2
    exit 1
}

print_hints() {
    echo ""
    echo "Agent-side check (this script cannot call MCP — the relay is a stdio MCP server owned"
    echo "by the agent client): run Unity MCP RunCommand logging UnityEngine.Application.dataPath"
    echo "and confirm it equals: $ROOT/Assets"
    [[ $APPROVE_MCP -eq 1 ]] && return 0
    echo ""
    echo "One-time MCP approval: Project Settings > AI > Unity MCP Server > Pending Connections > Allow."
    echo "UserSettings/ is gitignored, so each worktree starts without UserSettings/mcp.json;"
    echo "this script does not copy it. (Or rerun with --approve-mcp — machine-wide, opt-in.)"
}

main() {
    parse_args "$@"
    resolve_root
    resolve_unity
    detect_running
    if [[ $FRESH -eq 1 ]]; then do_fresh; fi
    if [[ -n "$RUNNING_PID" ]]; then
        [[ $APPROVE_MCP -eq 1 ]] && do_approve_mcp   # refuses: editor is open on ROOT
        echo "· not launching another editor"
    else
        clear_stale_lock
        if [[ $APPROVE_MCP -eq 1 ]]; then do_approve_mcp; clear_stale_lock; fi
        launch_editor
    fi
    verify_editor
    print_hints
}

main "$@"
