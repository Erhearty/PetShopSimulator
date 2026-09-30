#!/usr/bin/env python3
"""Extract .unitypackage files straight into a Unity project.

Usage:  python3 Tools/unpack_unitypackage.py <projectDir> [package.unitypackage ...]
        With no packages listed, everything in the Asset Store download cache is used.

A .unitypackage is a gzipped tar of one directory per asset GUID, each holding
`pathname` (the destination under Assets/), `asset` (the bytes) and `asset.meta`
(which carries the GUID). Extracting it ourselves keeps the original GUIDs and
avoids Unity's asynchronous package importer, which cannot survive the domain
reload that importing C# scripts triggers.

Legacy packages (exported by Unity 4.x) ship no `asset.meta` at all — only a binary
`metaData` — so Unity would hand every extracted file a fresh random GUID and break
every prefab reference into the pack. For those entries a minimal .meta is synthesised
from the GUID folder name (plus `folderAsset: yes` for folder entries). An existing
.meta is only overwritten when its `guid:` line differs from the package GUID, or when
it lacks a mesh name table the new one would carry, so re-running the unpacker leaves
Unity's own import settings alone.

Legacy prefabs also reference model sub-assets by Unity 4 fileIDs (4300000, 4300002, ...
for Meshes), whereas Unity 6 hashes internal IDs for a bare meta. For model files the
`metaData` blob - a Unity 4.6 SerializedFile of the imported objects - is scanned for
its Mesh objects, and the pathID -> name table is written as the ModelImporter's
legacy `fileIDToRecycleName`, which Unity migrates on import so those IDs resolve.
"""
import json, os, re, struct, sys, tarfile

MESH_CLASS = 43
# Editor-serialized Unity 4 objects start with m_ObjectHideFlags, m_PrefabParentObject and
# m_PrefabInternal (4 + 8 + 8 bytes) before m_Name; accept a name at either position.
NAME_SKIPS = (0, 20)
MODEL_EXTS = (".fbx", ".obj", ".dae", ".3ds", ".blend")
# A table header followed by at least one indented row (an empty header doesn't count).
NAME_TABLE = re.compile(r"^[ \t]*(fileIDToRecycleName|internalIDToNameTable):[ \t]*\n[ \t]+\S", re.M)

def meta_guid(meta_path):
    """The GUID declared by an existing .meta file, or None if there is none."""
    if not os.path.isfile(meta_path):
        return None
    with open(meta_path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None

def has_name_table(text):
    """True if meta text carries a non-empty fileIDToRecycleName/internalIDToNameTable block."""
    return bool(text) and NAME_TABLE.search(text) is not None

def read_text(path):
    """A file's text, or '' if it does not exist."""
    if not os.path.isfile(path):
        return ""
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return f.read()

def data_offset(blob):
    """dataOffset from a SerializedFile's big-endian header, or None if implausible."""
    if len(blob) < 20:
        return None
    offset = struct.unpack_from(">IIII", blob, 0)[3]
    return offset if 0 < offset < len(blob) else None

def read_name(blob, pos):
    """The int32-length-prefixed printable-ASCII name at pos, or None if there is none."""
    if pos < 0 or pos + 4 > len(blob):
        return None
    (n,) = struct.unpack_from("<i", blob, pos)
    raw = blob[pos + 4:pos + 4 + n] if 1 <= n <= 128 else b""
    if len(raw) != n or not all(32 <= b < 127 for b in raw):
        return None
    return raw.decode("ascii")

def mesh_names(blob):
    """Legacy Mesh pathID -> m_Name, found by scanning a Unity 4.x metaData object table.

    Table entries are 20 bytes: pathID, byteOffset, byteSize, typeID, classID (LE).
    Rather than walk the type tree, look for typeID == classID == 43 and validate.
    """
    base, names = data_offset(blob), {}
    marker = struct.pack("<ii", MESH_CLASS, MESH_CLASS)
    pos = blob.find(marker, 12) if base is not None else -1
    while pos != -1:
        path_id, offset = struct.unpack_from("<iI", blob, pos - 12)
        if 4300000 <= path_id <= 4399998 and path_id % 2 == 0:
            name = next(filter(None, (read_name(blob, base + offset + s) for s in NAME_SKIPS)), None)
            if name:
                names.setdefault(path_id, name)
        pos = blob.find(marker, pos + 1)
    return names

def meta_text(guid, path, is_folder, blob):
    """Synthesised .meta text: GUID, plus a legacy mesh name table for model files."""
    text = f"fileFormatVersion: 2\nguid: {guid}\n"
    if is_folder:
        return text + "folderAsset: yes\n"
    names = mesh_names(blob) if blob and path.lower().endswith(MODEL_EXTS) else {}
    if not names:
        return text
    # Names are JSON-quoted (a valid YAML double-quoted scalar), so '#', ':' or '*' survive.
    rows = "".join(f"    {pid}: {json.dumps(names[pid])}\n" for pid in sorted(names))
    return (text + "ModelImporter:\n  serializedVersion: 18\n"
            "  fileIDToRecycleName:\n" + rows)

def legacy_meta(tar, parts, guid, path):
    """Build the .meta text for a legacy entry from its GUID and binary metaData."""
    is_folder = "asset" not in parts
    blob = tar.extractfile(parts["metaData"]).read() if "metaData" in parts else b""
    return meta_text(guid, path, is_folder, blob)

def synthesise_meta(meta_path, guid, text):
    """Write the synthesised .meta; False if the existing one has that GUID and is no poorer.

    An existing meta with the right GUID is still rewritten when it lacks a mesh name
    table that the new text carries, so earlier bare metas get upgraded.
    """
    if meta_guid(meta_path) == guid and (has_name_table(read_text(meta_path))
                                         or not has_name_table(text)):
        return False
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(text)
    return True

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
            if kind in ("pathname", "asset", "asset.meta", "metaData"):
                entries.setdefault(guid, {})[kind] = m

        written = skipped = synthesised = 0
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
            elif synthesise_meta(dest + ".meta", guid, legacy_meta(tar, parts, guid, path)):
                synthesised += 1   # legacy package: no asset.meta
    return written, skipped, synthesised

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
        written, skipped, synthesised = unpack(pkg, project)
        total += written
        print(f"{written:5d} files  {os.path.basename(pkg)}" + (f"   ({skipped} skipped)" if skipped else "")
              + (f"   ({synthesised} .meta synthesised)" if synthesised else ""))
    print(f"{total} files extracted into {project}/Assets")
