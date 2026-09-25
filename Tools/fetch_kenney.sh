#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  fetch_kenney.sh <project-dir> — make sure the CC0 Kenney kits are installed.
#
#  The kits live in Assets/Resources/Kenney/<Folder>/ and are gitignored (kept out of the
#  repository to keep it small), so a fresh clone or a new worktree has none of them. They
#  are Kenney's asset kits from https://kenney.nl, released under Creative Commons Zero 1.0
#  (public domain): no attribution required, free to copy and redistribute — which is what
#  lets this script fetch them unattended. See THIRD-PARTY.md.
#
#  A kit counts as installed when <Folder>/License.txt exists and the folder holds at least
#  one .fbx. Each missing kit is restored from the first source that has it:
#    1. Assets/Resources/Kenney/<Folder>/ in $KENNEY_SRC if set, else in the main checkout
#       (the first entry of `git worktree list`), when that is not this project;
#    2. a cached zip in Library/kenney-cache/ — <slug>.zip, or a single zip named after the
#       folder or the slug's stem (e.g. nature.zip);
#    3. a download from the kit's kenney.nl page (the zip URL carries a version hash, so it is
#       scraped from the page each time), saved to Library/kenney-cache/<slug>.zip.
#  Installing from a zip copies 'Models/FBX format/' plus License.txt into the kit folder.
#
#  The Asset Store prefabs in Assets/Resources/Packs/ are copied from the same source checkout
#  too when absent; they are optional, so that step never fails the script.
#
#  Exits 1, listing each kit still missing and where to get it, if any kit could not be restored.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: $0 <project-dir>" >&2
    exit 2
fi

PROJECT="$(cd "$1" && pwd)"
KENNEY="$PROJECT/Assets/Resources/Kenney"
CACHE="$PROJECT/Library/kenney-cache"

# Folder under Assets/Resources/Kenney/ → kenney.nl slug (ModelLibrary.cs, THIRD-PARTY.md).
KITS=(
    "Roads:city-kit-roads"
    "Commercial:city-kit-commercial"
    "Suburban:city-kit-suburban"
    "Cars:car-kit"
    "Nature:nature-kit"
    "Furniture:furniture-kit"
    "Food:food-kit"
)

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Where to copy already-installed kits from: $KENNEY_SRC, else the main checkout.
SRC=""
if [[ -n "${KENNEY_SRC:-}" ]]; then
    SRC="$KENNEY_SRC"
else
    main="$(git -C "$PROJECT" worktree list --porcelain 2>/dev/null \
            | awk '/^worktree /{ print substr($0, 10); exit }')" || main=""
    if [[ -n "$main" && -d "$main" ]]; then
        main="$(cd "$main" && pwd)"
        [[ "$main" != "$PROJECT" ]] && SRC="$main"
    fi
fi

# installed <dir> — License.txt plus at least one .fbx.
installed() {
    [[ -f "$1/License.txt" ]] || return 1
    [[ -n "$(find "$1" -maxdepth 1 -type f -iname '*.fbx' -print -quit 2>/dev/null)" ]]
}

# cached_zip <folder> <slug> — prints the cached zip for this kit, if exactly one fits.
cached_zip() {
    local folder="$1" slug="$2"
    [[ -d "$CACHE" ]] || return 1
    if [[ -f "$CACHE/$slug.zip" ]]; then
        echo "$CACHE/$slug.zip"
        return 0
    fi
    local stem="${slug#city-kit-}"
    stem="${stem%-kit}"
    local want_folder want_stem
    want_folder="$(tr '[:upper:]' '[:lower:]' <<<"$folder")"
    want_stem="$(tr '[:upper:]' '[:lower:]' <<<"$stem")"
    local found=() f name
    shopt -s nullglob nocaseglob
    for f in "$CACHE"/*.zip; do
        name="$(basename "$f")"
        name="$(tr '[:upper:]' '[:lower:]' <<<"${name%.*}")"
        if [[ "$name" == "$want_folder" || "$name" == "$want_stem" || "$name" == "$slug" ]]; then
            found+=("$f")
        fi
    done
    shopt -u nullglob nocaseglob
    [[ ${#found[@]} -eq 1 ]] || return 1
    echo "${found[0]}"
}

# place <src-dir> <dest> — copies <src-dir> into a hidden sibling staging folder, then
# swaps it in with mv, so a copy that fails partway never leaves a half-installed kit
# that installed() would later accept. The leading dot keeps Unity from importing it.
place() {
    local src="$1" dest="$2" stage
    stage="$(dirname "$dest")/.$(basename "$dest").partial"
    rm -rf "$stage"
    mkdir -p "$stage" || return 1
    if ! cp -a "$src/." "$stage/" || ! installed "$stage"; then
        rm -rf "$stage"
        return 1
    fi
    rm -rf "$dest" && mv "$stage" "$dest"
}

# install_zip <zip> <dest> — installs 'Models/FBX format/.' and License.txt as <dest>.
install_zip() {
    local zip="$1" dest="$2" work
    unzip -tq "$zip" >/dev/null 2>&1 || return 1
    work="$(mktemp -d "$TMP/kit.XXXXXX")"
    unzip -q "$zip" -d "$work" || return 1
    local fbx license
    fbx="$(find "$work" -type d -path '*/Models/FBX format' -print -quit)"
    license="$(find "$work" -maxdepth 2 -type f -name 'License.txt' -print -quit)"
    [[ -n "$fbx" && -n "$license" ]] || return 1
    cp "$license" "$fbx/License.txt" || return 1
    place "$fbx" "$dest" || return 1
    rm -rf "$work"
}

# download <slug> — scrapes the zip URL from the asset page into the cache; prints its path.
download() {
    local slug="$1" url
    command -v curl >/dev/null 2>&1 || return 1
    url="$(curl -s --max-time 60 "https://kenney.nl/assets/$slug" \
           | grep -oE 'https://kenney.nl/media/pages/assets/[^"]+\.zip' | head -1)" || url=""
    [[ -n "$url" ]] || return 1
    mkdir -p "$CACHE"
    curl --fail --max-time 120 -sL -o "$CACHE/$slug.zip.part" "$url" || {
        rm -f "$CACHE/$slug.zip.part"
        return 1
    }
    mv "$CACHE/$slug.zip.part" "$CACHE/$slug.zip"
    echo "$CACHE/$slug.zip"
}

missing=()
for kit in "${KITS[@]}"; do
    folder="${kit%%:*}"
    slug="${kit#*:}"
    dest="$KENNEY/$folder"

    if installed "$dest"; then
        echo "  $folder: present"
        continue
    fi

    # 1. another checkout
    if [[ -n "$SRC" ]] && installed "$SRC/Assets/Resources/Kenney/$folder"; then
        mkdir -p "$KENNEY"
        if place "$SRC/Assets/Resources/Kenney/$folder" "$dest"; then
            echo "  $folder: copied from $SRC"
            continue
        fi
    fi

    have_unzip=0
    command -v unzip >/dev/null 2>&1 && have_unzip=1

    # 2. cached zip
    if [[ $have_unzip -eq 1 ]] && zip="$(cached_zip "$folder" "$slug")" \
            && install_zip "$zip" "$dest"; then
        echo "  $folder: from cache"
        continue
    fi

    # 3. download
    if [[ $have_unzip -eq 1 ]] && zip="$(download "$slug")" \
            && install_zip "$zip" "$dest"; then
        echo "  $folder: downloaded"
        continue
    fi

    missing+=("$folder:$slug")
done

# Optional: the relocated Asset Store prefabs. Never fails the script.
PACKS="$PROJECT/Assets/Resources/Packs"
if [[ ! -e "$PACKS" && -n "$SRC" && -d "$SRC/Assets/Resources/Packs" ]]; then
    PACKS_STAGE="$PROJECT/Assets/Resources/.Packs.partial"
    rm -rf "$PACKS_STAGE"
    if cp -a "$SRC/Assets/Resources/Packs" "$PACKS_STAGE" 2>/dev/null \
            && mv "$PACKS_STAGE" "$PACKS"; then
        [[ -f "$SRC/Assets/Resources/Packs.meta" ]] \
            && cp -a "$SRC/Assets/Resources/Packs.meta" "$PACKS.meta" 2>/dev/null || true
        echo "  Packs: copied from $SRC"
    else
        rm -rf "$PACKS_STAGE"
        echo "  Packs: copy from $SRC failed (optional, continuing)" >&2
    fi
fi

if [[ ${#missing[@]} -eq 0 ]]; then
    echo "✔ Kenney kits present"
    exit 0
fi

echo "✘ Kenney kits missing — download each and see THIRD-PARTY.md to install by hand:" >&2
for kit in "${missing[@]}"; do
    echo "  ${kit%%:*}: https://kenney.nl/assets/${kit#*:}" >&2
done
command -v unzip >/dev/null 2>&1 \
    || echo "  (unzip is not installed, so the cache and download sources were skipped)" >&2
command -v curl >/dev/null 2>&1 \
    || echo "  (curl is not installed, so nothing could be downloaded)" >&2
exit 1
