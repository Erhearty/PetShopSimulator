#!/usr/bin/env bash
#
# purge_history.sh — remove non-redistributable Asset Store packs and a stray
# built Linux player from the ENTIRE git history of this repository.
#
# Usage (run from the repository top level):
#   Tools/purge_history.sh                       dry run: per-path history counts,
#                                                pack size and the planned steps.
#                                                Changes nothing.
#   Tools/purge_history.sh --verify              scan all history for any purged
#                                                path; exit 1 if any survive.
#   Tools/purge_history.sh --confirm             rewrite history (git filter-repo).
#   Tools/purge_history.sh --confirm --remote <url>
#                                                ...then add <url> as origin and
#                                                push master (never --force).
#   Tools/purge_history.sh --help                this text.
#
# --confirm refuses to run unless: it is at the repo top level, the working tree
# is clean, there is exactly one worktree, no purge path is tracked at HEAD, and
# git-filter-repo is installed. It first writes a verified backup bundle to
# ../<repo>-pre-purge-<timestamp>.bundle. The list below must match .gitignore
# and THIRD-PARTY.md. Files on disk are never touched (they are ignored).

set -euo pipefail

readonly PURGE_PATHS=(
    "Assets/Resources/Packs"
    "Assets/Resources/Packs.meta"
    "Assets/SimplePoly City - Low Poly Assets"
    "Assets/SimplePoly City - Low Poly Assets.meta"
    "Assets/Low-Poly Furniture Kit - Stylized Wooden Set"
    "Assets/Low-Poly Furniture Kit - Stylized Wooden Set.meta"
    "Assets/LowpolyStreetPack"
    "Assets/LowpolyStreetPack.meta"
    "Assets/SimpleNaturePack"
    "Assets/SimpleNaturePack.meta"
    "Assets/100 People - Animated Characters Pack"
    "Assets/100 People - Animated Characters Pack.meta"
    "Assets/DenysAlmaral"
    "Assets/DenysAlmaral.meta"
    "Assets/ithappy"
    "Assets/ithappy.meta"
    "Assets/CuteMagic_CubeAnimals_Free"
    "Assets/CuteMagic_CubeAnimals_Free.meta"
    "PetShop.x86_64"
    "PetShop_Data"
    "UnityPlayer.so"
    "libdecor-0.so.0"
    "libdecor-cairo.so"
    "PetShop_BackUpThisFolder_ButDontShipItWithYourGame"
)

readonly EXIT_OK=0
readonly EXIT_FOUND=1
readonly EXIT_USAGE=2
readonly EXIT_PRECONDITION=3
readonly EXPECTED_WORKTREES=1
readonly MAX_SHOWN_PER_PATH=5
readonly BRANCH="master"
readonly REMOTE_NAME="origin"

TMP_FILE=""

usage() {
    sed -n '3,22p' "$0" | sed 's/^# \{0,1\}//'
}

die() {
    local code="$1"
    shift
    echo "ABORT: $*" >&2
    exit "$code"
}

cleanup() {
    if [[ -n "$TMP_FILE" && -f "$TMP_FILE" ]]; then
        rm -f -- "$TMP_FILE"
    fi
}
trap cleanup EXIT

history_count() {
    local path="$1"
    git log --all --format= --name-only -- "$path" | sort -u | sed '/^$/d' | wc -l
}

# Every path that has ever existed, from commit logs and reachable objects.
all_history_paths() {
    {
        git log --all --format= --name-only
        git rev-list --all --objects | cut -s -d' ' -f2-
    } | sed '/^$/d' | sort -u
}

# Reads paths on stdin; prints those equal to or under a PURGE_PATHS entry,
# at most MAX_SHOWN_PER_PATH per entry plus a "... and N more" line.
match_purged() {
    awk -v max="$MAX_SHOWN_PER_PATH" '
        NR == FNR { purge[++n] = $0; next }
        {
            for (i = 1; i <= n; i++) {
                if ($0 == purge[i] || index($0, purge[i] "/") == 1) {
                    hits[i]++
                    if (hits[i] <= max) print "  " $0
                    break
                }
            }
        }
        END {
            for (i = 1; i <= n; i++)
                if (hits[i] > max) printf "  ... and %d more under %s\n", hits[i] - max, purge[i]
        }
    ' <(printf '%s\n' "${PURGE_PATHS[@]}") -
}

do_verify() {
    local offenders
    offenders="$(all_history_paths | match_purged)"
    if [[ -n "$offenders" ]]; then
        echo "Purged paths still present in history:"
        echo "$offenders"
        return "$EXIT_FOUND"
    fi
    echo "History is clean: no purge path found in any commit or object."
    return "$EXIT_OK"
}

print_plan() {
    echo "Planned steps for --confirm:"
    echo "  1. check: repo top level, clean tree, single worktree,"
    echo "     no purge path tracked at HEAD, git-filter-repo installed"
    echo "  2. git bundle create ../<repo>-pre-purge-<timestamp>.bundle --all (+ verify)"
    echo "  3. git filter-repo --force --invert-paths --paths-from-file <list>"
    echo "  4. re-scan history (--verify); abort if anything survives"
    echo "  5. with --remote <url>: git remote add $REMOTE_NAME <url>; git push -u $REMOTE_NAME $BRANCH"
}

do_dry_run() {
    local path
    echo "DRY RUN — nothing will be changed."
    echo "Historical file count per purge path:"
    for path in "${PURGE_PATHS[@]}"; do
        printf '  %6d  %s\n' "$(history_count "$path")" "$path"
    done
    echo "Current pack size:"
    git count-objects -vH | grep '^size-pack' | sed 's/^/  /'
    print_plan
    return "$EXIT_OK"
}

check_filter_repo() {
    if ! git filter-repo --version >/dev/null 2>&1; then
        die "$EXIT_PRECONDITION" "git-filter-repo is not installed. Install it with" \
            "'pip install --user git-filter-repo' or your distro package 'git-filter-repo'."
    fi
}

check_preconditions() {
    local top worktrees tracked
    top="$(git rev-parse --show-toplevel)"
    [[ "$top" == "$(pwd -P)" ]] || die "$EXIT_PRECONDITION" "run from the repo top level ($top)."
    # Untracked files are ignored here: filter-repo's final reset leaves them alone.
    [[ -z "$(git status --porcelain --untracked-files=no)" ]] \
        || die "$EXIT_PRECONDITION" "working tree has uncommitted changes; commit or stash first."
    worktrees="$(git worktree list | wc -l)"
    [[ "$worktrees" -eq "$EXPECTED_WORKTREES" ]] \
        || die "$EXIT_PRECONDITION" "found $worktrees worktrees; remove extra ones (git worktree list) first."
    tracked="$(git ls-tree -r --name-only HEAD -- "${PURGE_PATHS[@]}")"
    [[ -z "$tracked" ]] || die "$EXIT_PRECONDITION" "purge paths still tracked at HEAD; untrack and commit them first."
    check_filter_repo
}

make_bundle() {
    local bundle
    bundle="../$(basename "$(pwd -P)")-pre-purge-$(date +%Y%m%d-%H%M%S).bundle"
    git bundle create "$bundle" --all
    git bundle verify "$bundle" >/dev/null || die "$EXIT_PRECONDITION" "backup bundle failed to verify: $bundle"
    echo "Backup bundle written and verified: $bundle"
}

# Echoes the purge paths that currently exist on disk.
present_on_disk() {
    local path
    for path in "${PURGE_PATHS[@]}"; do
        if [[ -e "$path" ]]; then
            echo "$path"
        fi
    done
}

warn_vanished() {
    local before="$1" path
    while IFS= read -r path; do
        if [[ -n "$path" && ! -e "$path" ]]; then
            echo "WARNING: local copy vanished from disk: $path (re-run ./build.sh assets)" >&2
        fi
    done <<< "$before"
}

run_filter_repo() {
    TMP_FILE="$(mktemp)"
    printf '%s\n' "${PURGE_PATHS[@]}" > "$TMP_FILE"
    git filter-repo --force --invert-paths --paths-from-file "$TMP_FILE"
}

publish() {
    local url="$1"
    if [[ -z "$url" ]]; then
        echo "Next, publish the rewritten history:"
        echo "  git remote add $REMOTE_NAME \"<url>\""
        echo "  git push -u $REMOTE_NAME $BRANCH"
        return "$EXIT_OK"
    fi
    if git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
        die "$EXIT_PRECONDITION" "remote '$REMOTE_NAME' already exists; refusing to push over it."
    fi
    git remote add "$REMOTE_NAME" "$url"
    git push -u "$REMOTE_NAME" "$BRANCH"
}

do_confirm() {
    local url="$1" old_head new_head on_disk
    # filter-repo deletes 'origin' itself, so this must be checked before the rewrite.
    if [[ -n "$url" ]] && git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
        die "$EXIT_PRECONDITION" "remote '$REMOTE_NAME' already exists; refusing to push over it."
    fi
    check_preconditions
    make_bundle
    old_head="$(git rev-parse HEAD)"
    on_disk="$(present_on_disk)"
    run_filter_repo
    do_verify || die "$EXIT_FOUND" "purge paths survived the rewrite; restore from the bundle."
    warn_vanished "$on_disk"
    publish "$url"
    new_head="$(git rev-parse HEAD)"
    echo "HEAD: $old_head -> $new_head"
}

main() {
    case "${1:-}" in
        "")        do_dry_run ;;
        --verify)  [[ $# -eq 1 ]] || { usage; exit "$EXIT_USAGE"; }; do_verify ;;
        --confirm) parse_confirm "$@" ;;
        -h|--help) usage ;;
        *)         usage; exit "$EXIT_USAGE" ;;
    esac
}

parse_confirm() {
    if [[ $# -eq 1 ]]; then
        do_confirm ""
    elif [[ $# -eq 3 && "$2" == "--remote" && -n "$3" ]]; then
        do_confirm "$3"
    else
        usage
        exit "$EXIT_USAGE"
    fi
}

main "$@"
