#!/usr/bin/env python3
"""Extract .unitypackage files straight into a Unity project.

Usage:  python3 Tools/unpack_unitypackage.py <projectDir> [package.unitypackage ...]
        With no packages listed, everything in the Asset Store download cache is used.

A .unitypackage is a gzipped tar of one directory per asset GUID, each holding
`pathname` (the destination under Assets/), `asset` (the bytes) and `asset.meta`
(which carries the GUID). Extracting it ourselves keeps the original GUIDs and
avoids Unity's asynchronous package importer, which cannot survive the domain
reload that importing C# scripts triggers.
"""
import os, sys, tarfile

def unpack(pkg, project, dry=False):
    entries = {}
    with tarfile.open(pkg, "r:gz") as tar:
        for m in tar.getmembers():
            if not m.isfile():
                continue
            parts = m.name.split("/")
            if len(parts) < 2:
                continue
            guid, kind = parts[-2], parts[-1]
            if kind in ("pathname", "asset", "asset.meta"):
                entries.setdefault(guid, {})[kind] = m

        written = skipped = 0
        for guid, parts in entries.items():
            if "pathname" not in parts:
                continue
            path = tar.extractfile(parts["pathname"]).read().decode("utf-8").split("\n")[0].strip()
            if not path.startswith("Assets/") or ".." in path:
                skipped += 1
                continue

            dest = os.path.join(project, path)
            if "asset" in parts:
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                with open(dest, "wb") as f:
                    f.write(tar.extractfile(parts["asset"]).read())
                written += 1
            else:
                os.makedirs(dest, exist_ok=True)   # folder entry

            if "asset.meta" in parts:
                with open(dest + ".meta", "wb") as f:
                    f.write(tar.extractfile(parts["asset.meta"]).read())
    return written, skipped

def cache_root():
    home = os.path.expanduser("~")
    for candidate in (os.environ.get("ASSETSTORE_CACHE_PATH"),
                      os.path.join(home, ".local/share/unity3d/Asset Store-5.x"),
                      os.path.join(home, "Library/Unity/Asset Store-5.x")):
        if candidate and os.path.isdir(candidate):
            return candidate
    return None


if __name__ == "__main__":
    project  = sys.argv[1] if len(sys.argv) > 1 else "."
    packages = list(sys.argv[2:])

    if not packages:
        root = cache_root()
        if not root:
            print("No Asset Store download cache found. Download packages via "
                  "Package Manager > My Assets first.", file=sys.stderr)
            sys.exit(1)
        for dirpath, _, files in os.walk(root):
            packages += [os.path.join(dirpath, f) for f in files if f.endswith(".unitypackage")]

    if not packages:
        print("Nothing to extract.", file=sys.stderr)
        sys.exit(1)

    total = 0
    for pkg in sorted(packages):
        written, skipped = unpack(pkg, project)
        total += written
        print(f"{written:5d} files  {os.path.basename(pkg)}" + (f"   ({skipped} skipped)" if skipped else ""))
    print(f"{total} files extracted into {project}/Assets")
