#!/usr/bin/env python3
"""Move art-pack prefabs under Assets/Resources/Packs so the runtime loader can find them.

Everything in this game is spawned at runtime by Resources path, so prefabs have to live
under a Resources folder. Their meshes, materials and textures stay where they are and come
along as dependencies.

Idempotent on purpose: re-extracting a .unitypackage restores the pack's original folders,
and leaving both copies in place gives Unity two assets with the same GUID. If the
destination already exists, the freshly extracted duplicate is deleted instead.
"""
import os, shutil, sys

MOVES = [
    ("Low-Poly Furniture Kit - Stylized Wooden Set/Prefabs", "Furniture"),
    ("LowpolyStreetPack/Prefabs",                            "Street"),
    ("SimplePoly City - Low Poly Assets/Prefab",             "City"),
    ("100 People - Animated Characters Pack/1.Prefabs",      "People"),
    ("SimpleNaturePack/Prefabs",                             "Nature"),
    ("DenysAlmaral/CityPeople/Prefabs",                      "CityPeople"),
    ("CuteMagic_CubeAnimals_Free/CubeAnimals_Free/Prefab_1", "Animals"),
]

# Animal packs vary in folder layout, so any top-level folder whose name mentions animals
# has its prefabs merged into Packs/Animals. PetPen looks models up by species name, so
# dropping a new animal pack in is enough — no code change needed.
ANIMAL_HINTS = ("animal", "pets", "creature")


def main(project="."):
    assets = os.path.join(project, "Assets")
    packs  = os.path.join(assets, "Resources", "Packs")
    os.makedirs(packs, exist_ok=True)

    for src_rel, name in MOVES:
        src  = os.path.join(assets, src_rel)
        dest = os.path.join(packs, name)

        if not os.path.isdir(src):
            status = "already organised" if os.path.isdir(dest) else "not installed"
            print(f"  {name:12s} {status}")
            continue

        if os.path.isdir(dest):
            shutil.rmtree(src)
            meta = src + ".meta"
            if os.path.exists(meta):
                os.remove(meta)
            print(f"  {name:12s} duplicate removed (kept Resources/Packs/{name})")
        else:
            shutil.move(src, dest)
            meta = src + ".meta"
            if os.path.exists(meta):
                shutil.move(meta, dest + ".meta")
            n = sum(1 for _, _, fs in os.walk(dest) for f in fs if f.endswith(".prefab"))
            print(f"  {name:12s} moved, {n} prefabs")


def merge_animal_packs(assets, packs):
    """Merge any animal pack's prefabs into Packs/Animals.

    Publishers bury their models at unpredictable depths — ithappy ships
    Assets/ithappy/Animals_FREE/Prefabs — so the whole tree is searched for a directory
    whose path mentions animals, rather than just top-level folder names.
    """
    dest = os.path.join(packs, "Animals")
    os.makedirs(dest, exist_ok=True)
    merged = 0

    for dirpath, dirnames, files in os.walk(assets):
        rel = os.path.relpath(dirpath, assets).replace(os.sep, "/")
        if rel.startswith("Resources") or rel.startswith("Scripts") or rel.startswith("Editor"):
            dirnames[:] = []
            continue
        if not any(hint in rel.lower() for hint in ANIMAL_HINTS):
            continue

        for f in files:
            if not f.endswith(".prefab"):
                continue
            target = os.path.join(dest, f)
            if os.path.exists(target):
                continue
            shutil.move(os.path.join(dirpath, f), target)
            meta = os.path.join(dirpath, f + ".meta")
            if os.path.exists(meta):
                shutil.move(meta, target + ".meta")
            merged += 1

    if merged:
        print(f"  {'Animals':12s} merged {merged} prefab(s) from animal packs")
    return merged


if __name__ == "__main__":
    project = sys.argv[1] if len(sys.argv) > 1 else "."
    main(project)
    merge_animal_packs(os.path.join(project, "Assets"),
                       os.path.join(project, "Assets", "Resources", "Packs"))
