# Third-party assets

> **This project is no longer freely redistributable.** The Asset Store packs below are
> *Extension Assets* under the Unity Asset Store EULA: they are tied to the account that
> claimed them and may not be redistributed in project source. Do not commit
> `Assets/Resources/Packs/`, `Assets/100 People - Animated Characters Pack/`,
> `Assets/LowpolyStreetPack/`, `Assets/SimpleNaturePack/`,
> `Assets/SimplePoly City - Low Poly Assets/` or
> `Assets/Low-Poly Furniture Kit - Stylized Wooden Set/` to a public repository.
> The Kenney kits are CC0 and remain safe to share.

## Unity Asset Store packs — Standard EULA, Extension Asset

Claimed to the project owner's Unity account and downloaded through Package Manager →
My Assets. Prefabs were moved under `Assets/Resources/Packs/` so the runtime loader can
reach them; their meshes, materials and textures stay in each pack's own folder.

| Pack | Publisher | Used for |
|---|---|---|
| 100 People – Animated Characters | Marcin's Assets | the shopkeeper and every customer — 100 bodies, 17 animations, one shared animator controller |
| SimplePoly City | VenCreations | the parade of shops, the block opposite, parked cars, skyline |
| Low Poly Street Pack | Dynamic Art | road tiles, crossing, lamp posts, benches, street props, trees |
| Low-Poly Furniture Kit – Stylized Wooden Set | Morphara Studio | shop counter, wall shelving, seating, tables |
| Low-Poly Simple Nature Pack | JustCreate | street trees and planting |

Re-importing on a fresh machine: download the five via Package Manager → My Assets, then
`./build.sh assets`.

## Kenney asset kits — CC0 1.0 (public domain)

The 3D models under `Assets/Resources/Kenney/` come from Kenney's asset kits and are
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

Each kit's own `License.txt` is kept next to its models.

Only a curated subset of each kit is imported — roughly 123 models out of several hundred —
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
