# Pet Shop Simulator

A cozy first-person 3D management sim: lay out your shop, keep the shelves stocked, breed animals, and make
rent — in a pet shop on a proper street corner, with traffic going past the window.

The shop, the street and the whole city block are assembled at runtime from Unity Asset Store
low-poly packs, the CC0 [Kenney](https://kenney.nl) kits, and procedural geometry — in that
order of preference, with each tier falling back to the next. All audio is synthesised at
startup; there are no sound files.

> The Asset Store packs are licensed to the project owner's Unity account and are **not**
> redistributable — see [`THIRD-PARTY.md`](THIRD-PARTY.md) before sharing this repo.

![Unity 6000.6.2f1](https://img.shields.io/badge/Unity-6000.6.2f1-black) ![Built-in RP](https://img.shields.io/badge/pipeline-Built--in-blue)

## Quick start

```bash
./build.sh            # sets up, builds the scene and a Linux player, then smoke-tests it
./build.sh run        # play it
./build.sh look       # render screenshots of the running game into Screenshots/
```

Or open the project in Unity, load `Assets/Scenes/MainScene.unity`, and press Play.

A fresh clone needs one setup pass (`./build.sh setup`) before the first editor Play session,
because TextMesh Pro's essential resources are not checked in.

## Controls

| Key | Action |
|-----|--------|
| `WASD` | Move |
| Mouse | Look |
| `Space` | Jump |
| `Space` | Jump |
| `E` | Interact — restock a shelf, buy a pet, check the books |
| `1`–`8` / `B` | Build: shelf, large shelf, pen, counter, wall, window wall, doorway, fence |
| `LMB` | Place · `R` rotate · middle-click or `Delete` remove (50% back) |
| `RMB` | Leave build mode |
| `Tab` | The ledger — shelves, animals, catalogue |
| `Esc` | Cancel / close / pause |
| `Enter` | Close up early |
| `H` | Toggle the controls panel |
| `F5` | Save |

## How the game plays

The shop opens at 09:00 and closes at 18:00 — about 3½ real minutes. Customers walk up the
pavement outside, come in through the door, browse the shelves and pens, and pay at the till.
Footfall is set by your reputation.

- **Customers queue at the till.** Nobody pays themselves: either you serve them (press `E`
  behind the counter) or an assistant does, slower, for a daily wage. Keep them waiting and
  they walk out — with your reputation.
- **The animals need looking after.** Feed and bedding drain every day. A neglected animal
  loses condition and sells for about a third of what a thriving one does, and you can see
  the state of a pen from across the yard.
- **You set the prices.** Mark up for margin or undercut for footfall; the Manage tab shows
  how willing shoppers are at your current markup.
- **Stock sells out.** Walk up to a shelf and press `E` to refill every line on it; you pay the
  wholesale cost up front. A customer who finds nothing worth buying leaves, and your reputation
  dips.
- **Animals are the big-ticket item** but a finite one. Press `E` at a pen to buy a young animal
  from the breeder at 55% of its value. Any pen holding two adults with a spare slot may produce a
  baby overnight; babies inherit blended traits, a mutated coat, and occasionally a higher rarity
  tier than either parent.
- **Rent is due every night** — €45 on day one, rising €6 a day. Close the books with a negative
  balance and the shop shuts for good.
- **Build to grow.** Shelves widen your range, pens add species, and everything you place is saved
  with the game.

## The shop and its yard

The shop is a small building in the corner of a large open lot. Supplies are sold indoors;
the animals live in pens **outside** in the yard. Both are placed through the same build
system, so you can rearrange either.

## What's outside

The shopfront is glazed, so the street is part of the game whether or not you step out onto it.
`StreetGenerator` lays down the pavement, a two-lane road with a crossing at the door, the
terrace of shops the pet shop belongs to, a block of buildings opposite, parked cars, street
lights, trees and a skyline behind it all — about 140 objects.

Only the near pavement is walkable. The road has no collider at all, which is what keeps
customers on the pavement and the NavMesh bake small.

Customers and the shopkeeper are rigged, animated characters drawn from a pool of 100 bodies;
their walk cycles are driven from actual travel speed, so nobody skates.

## Architecture

`GameBootstrapper` is the whole entry point. Dropped on an empty GameObject, it builds the systems,
generates the room and the player, assembles the UI, and hands control to `GameManager`.

```
Bootstrap ─▶ systems (grid · shop · build · spawner · audio · manager)
          ─▶ ShopGenerator  room shell, lighting, player, camera, NavMesh
          ─▶ UI             status bar, build palette, panels
          ─▶ GameManager    starter layout or save, then the day loop
```

Three things are worth knowing before you change anything:

1. **All furniture goes through one path** — `BuildMode.Place()` → `FurnitureFactory.Spawn()`. The
   starter shop, player placements and save loading share it, so they can never drift apart.
2. **Shaders are resolved at runtime**, so they must be listed in Always Included Shaders or the
   build stripper removes them. `./build.sh setup` handles this.
3. **`MeshBuilder` geometry sits on Y = 0** — primitives are positioned by their base, not their
   centre.
4. **Model kits are sized by measured bounds, never by a hard-coded scale.** The Kenney kits
   are authored at wildly different scales, so `KenneyLibrary.Spawn` takes a target size in
   metres and works out the factor itself.
5. **The Kenney kits are optional.** Delete `Assets/Resources/Kenney/` and the game still runs
   on its procedural fallbacks.

`CLAUDE.md` has the full file map and the rest of the rules.

## Project layout

```
Assets/
├── Editor/     headless setup, scene generation and player builds
├── Resources/
│   └── Kenney/     CC0 model kits — see THIRD-PARTY.md
└── Scripts/
    ├── Core/       bootstrap, game manager, mesh/material/model factories, audio, save
    ├── Shop/       grid, build mode, furniture catalog, shop and street generators
    ├── Player/     controller, third-person camera, interaction
    ├── Customer/   shopper AI and spawner
    ├── Commerce/   shop manager, shelves, products, catalog
    ├── Pets/       pet data, pens, breeding
    ├── UI/         title, HUD, ledger, pause, day results, popups
    └── Dev/        screenshot capture
```

## Assets

3D models are Kenney's CC0 kits (city, roads, cars, nature, furniture, food) — public domain,
no attribution required, credited anyway in [`THIRD-PARTY.md`](THIRD-PARTY.md). Unity Asset
Store packages are *not* used: they can only be downloaded through an authenticated Unity
account, so they cannot be fetched by a build script. `THIRD-PARTY.md` explains how to drop one
in yourself if you want to.

## Command-line flags

| Flag | Effect |
|------|--------|
| `-daylength <seconds>` | Length of a trading day (default 210) |
| `-screenshot <path>` | Capture PNGs of the running game, then quit |

## Licence

Personal project — not for distribution.
