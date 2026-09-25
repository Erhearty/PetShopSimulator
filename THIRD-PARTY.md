# Third-party assets

> **The Asset Store packs below are not in this repository.** They are *Extension Assets*
> under the Unity Asset Store EULA (or, where marked, of unverified licence), tied to the
> account that claimed them and not redistributable in project source. Their folders are
> gitignored and have been purged from the repository history.
>
> The CC0 Kenney kits are not in the repository either (kept out to keep it small), so a
> fresh clone builds and runs on procedural geometry only. To restore the full art, download
> each pack through Package Manager → My Assets, then run `./build.sh assets`
> (`./build.sh assets?` reports which packs are present); restore the Kenney kits as
> described under *Kenney asset kits* below.

## Unity Asset Store packs — Standard EULA, Extension Asset

Claimed to the project owner's Unity account and downloaded through Package Manager →
My Assets. Prefabs were moved under `Assets/Resources/Packs/` so the runtime loader can
reach them; their meshes, materials and textures stay in each pack's own folder.

| Pack | Publisher | Folder | Licence | Used for |
|---|---|---|---|---|
| 100 People – Animated Characters | Marcin's Assets | `Assets/100 People - Animated Characters Pack` | Standard EULA | the shopkeeper and every customer — 100 bodies, 17 animations, one shared animator controller |
| SimplePoly City | VenCreations | `Assets/SimplePoly City - Low Poly Assets` | Standard EULA | the parade of shops, the block opposite, parked cars, skyline |
| Low Poly Street Pack | Dynamic Art | `Assets/LowpolyStreetPack` | Standard EULA | road tiles, crossing, lamp posts, benches, street props, trees |
| Low-Poly Furniture Kit – Stylized Wooden Set | Morphara Studio | `Assets/Low-Poly Furniture Kit - Stylized Wooden Set` | Standard EULA | shop counter, wall shelving, seating, tables |
| Low-Poly Simple Nature Pack | JustCreate | `Assets/SimpleNaturePack` | Standard EULA | street trees and planting |
| City People | Denys Almaral | `Assets/DenysAlmaral` | licence unverified — treated as non-redistributable | customer and shopkeeper character models (`Packs/CityPeople/…`, preferred by `CharacterFactory` when installed) |
| Animals FREE | ithappy | `Assets/ithappy` | licence unverified — treated as non-redistributable | animal models for the pets in the pens |
| Cube Animals Free | CuteMagic | `Assets/CuteMagic_CubeAnimals_Free` | licence unverified — treated as non-redistributable | cube-style animal models (candidate pet models) |

The relocated prefabs live under `Assets/Resources/Packs/`, which is likewise not in the
repository.

Re-importing on a fresh machine: download all eight via Package Manager → My Assets, then
`./build.sh assets`. `./build.sh assets?` reports which packs are present.

## Repository history

Earlier commits contained the pack files above and a stray built Linux player in the
project root (`PetShop.x86_64`, `PetShop_Data/`, `UnityPlayer.so`, …), and the Kenney
kits under `Assets/Resources/Kenney/`. All of them were removed
from **all** history with `Tools/purge_history.sh` (`--verify` re-checks it). Clones made
before that rewrite have different commit SHAs from the first pack commit onward (and still
hold the purged files), so they must be deleted and re-cloned — never merged or pushed back.

## Kenney asset kits — CC0 1.0 (public domain)

**Not in this repository.** The kits are CC0 and free to share, but they were removed from
the repository and its history to keep it small; `Assets/Resources/Kenney/` is gitignored.

The 3D models the game loads from `Assets/Resources/Kenney/` come from Kenney's asset kits and are
released under [Creative Commons Zero 1.0](http://creativecommons.org/publicdomain/zero/1.0/).
CC0 waives all copyright: no attribution is required, no licence file has to ship with the
game, and the assets may be used commercially. The credit below is given because it is the
decent thing to do, not because the licence demands it.

| Kit | Used for | Source |
|-----|----------|--------|
| City Kit (Roads) | road tiles, crossings, street lights, traffic lights, signs, dumpster | https://kenney.nl/assets/city-kit-roads |
| City Kit (Commercial) | the terrace the shop sits in, the block opposite, the skyline | https://kenney.nl/assets/city-kit-commercial |
| City Kit (Suburban) | planters, fences, street trees | https://kenney.nl/assets/city-kit-suburban |
| Car Kit | parked cars | https://kenney.nl/assets/car-kit |
| Nature Kit | trees, bushes, grass | https://kenney.nl/assets/nature-kit |
| Furniture Kit | counter, benches, plants, rugs, boxes, ceiling lamps, wall shelving | https://kenney.nl/assets/furniture-kit |
| Food Kit | the products that sit on the shop shelves | https://kenney.nl/assets/food-kit |

Restoring them on a fresh clone is automatic: `build.sh` runs `Tools/fetch_kenney.sh` before
every target that needs the art (or on its own with `./build.sh kenney`). Each missing kit is
copied from another checkout (`$KENNEY_SRC`, else the main checkout of a git worktree), else
unpacked from a zip cached in `Library/kenney-cache/`, else downloaded from its URL above into
that cache. Installing from a zip copies the kit's whole `Models/FBX format/` folder plus its
`License.txt`.

Manual fallback (e.g. offline with no cache): download each kit from its URL above, and copy the
`.fbx` models you need, plus the kit's `License.txt`, into `Assets/Resources/Kenney/<Kit>/`. The
folders are `Cars`, `Commercial`, `Food`, `Furniture`, `Nature`, `Roads` and `Suburban`.
Models are loaded by file name, and any that are missing fall back to procedural geometry.

Only a curated subset of each kit was used — roughly 123 models out of several hundred —
to keep import and build times down. To add more, drop the `.fbx` into the right
`Assets/Resources/Kenney/<Kit>/` folder; `KenneyModelPostprocessor` applies the import
settings automatically and `KenneyLibrary` can load it by name with no other changes.

## Why not the Unity Asset Store?

Asset Store packages are tied to a Unity account: they must be claimed in a browser while
signed in and then pulled through the Package Manager's *My Assets* tab. There is no
unauthenticated download, so they cannot be fetched from a terminal or a build script.

If you want to use an Asset Store package instead, claim it in the Unity Hub and import it,
then either:

- drop its prefabs under `Assets/Resources/<YourKit>/` and load them through
  `KenneyLibrary` — it takes any Resources path and sizes models by measured bounds, so it
  is not Kenney-specific; or
- assign them to the serialized fields on `ShopGenerator` / `StreetGenerator`.

Nothing in the game hard-depends on any art pack. `ModelLibrary.FirstAvailable` picks the
best installed model and every spawn is allowed to return null, so the chain degrades
Asset Store pack → Kenney kit → procedural geometry. Delete all of them and the game still
builds and runs, just plainer.

## TextMesh Pro essentials

`Assets/TextMesh Pro/` is imported from the `com.unity.ugui` package by
`ProjectSetup.ImportTMPEssentials` and is covered by the Unity Companion License.
