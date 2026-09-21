# Pet Shop Simulator — CLAUDE.md

Handoff doc for Claude Code. Read this before touching any file.

## Project snapshot

Unity **6000.6.2f1** on Linux (Nobara/Fedora), **Built-in Render Pipeline**, **old Input Manager**
(`activeInputHandler: 0`).

Art is **procedural geometry plus the CC0 Kenney model kits** in `Assets/Resources/Kenney/`
(see `THIRD-PARTY.md`). Audio is still fully synthesised in `AudioManager.cs` — there are no
audio files. The earlier "no external assets at all" rule was lifted deliberately when the
street was added; the procedural paths all still exist as fallbacks, and **deleting
`Assets/Resources/Kenney/` must leave the game running**, just plainer. Keep it that way:
every `ModelLibrary.Spawn` call can return null and every caller has to cope.

This is a **3D** game. Do not reintroduce `SpriteRenderer`, `Camera.orthographic`, or 2D physics.

## How to run

**From a terminal (no editor interaction needed):**

```bash
./build.sh          # setup → scene → Linux player → headless smoke test
./build.sh run      # launch the built player
```

**From the editor:** open `Assets/Scenes/MainScene.unity` and press Play. The scene contains only a
`Bootstrap` GameObject with `GameBootstrapper` on it; everything else is generated at runtime.

`build.sh` sub-commands: `setup`, `scene`, `linux`, `windows`, `run`, `smoke`.

## Controls — first person

| Key | Action |
|-----|--------|
| WASD / Arrows | Move |
| Mouse | Look (cursor is locked; it frees while a panel is open) |
| Space | Jump |
| E | Interact — restock a shelf, feed/buy a pet, **serve the queue** at the counter |
| 1–8 or B | Build mode (shelf, large shelf, pen, counter, wall, window wall, doorway, fence) |
| LMB | Place · **R** rotate · **middle-click / Delete** remove (50% refund) |
| RMB | Leave build mode |
| Tab | The ledger — shelves, animals, catalogue, **manage** (prices and staff) |
| Esc | Cancel placement → close popup → close ledger → pause menu (in that order) |
| Enter | Close up early |
| H | Toggle the controls panel |
| F5 | Save |

## Command-line flags (player build)

| Flag | Effect |
|------|--------|
| `-daylength <seconds>` | Trading-day length (default 210) |
| `-screenshot <path>` | Save PNGs of the running game, then quit |
| `-screenshotdelay/-screenshotevery/-screenshotcount` | Tune the above |

`-batchmode` runs auto-continue past the day-results panel, so a headless run exercises the whole
day loop. That is what `./build.sh smoke` relies on.

## Level design — read the plan view first

`./build.sh look` writes `11_plan_yard`, `12_plan_block` and `13_plan_city`. Faults that are
invisible at eye level are obvious there, and every layout problem so far was found that
way: a city that was one thin strip of randomly-rotated towers, a yard that was a featureless
green field, a paddock four times the size of the pens inside it.

**The yard is zoned, not scattered.** `ShopGenerator.DressYard` lays down surfacing first
and props second:

| Zone | What it is |
|---|---|
| Forecourt | paving in front of the shop, `ShopFrontMargin` deep (7 m) |
| Spine | paved path: gate → shop door → across to the paddock |
| Paddock | `PaddockArea`, sanded and post-and-rail fenced; the starter pens go inside it |
| Garden | planted bed with kerb, tree and benches, filling the gap between shop and paddock |
| Beds | hedging along the back and west walls |

`PaddockArea` must stay sized to the pens. `StarterLayout` places them on a 4-cell pitch
from its corner, so if you change one, check the other.

**Asset traps found the hard way, both invisible in code review:**

- `Packs/Street/StreetProps/TraficLights/LampPost_*` are **traffic lights**, whatever the
  file is called. The actual lamp post is `ParkLamp`. One traffic light every 19 m along a
  high street looked absurd.
- `PlantPot_A` is a wide shallow trough. Sizing it with `Fit.Height` turns it into a mound
  of earth taller than the doorway — fit wide props by `Fit.Width`.

## Layout — a shop in a yard

The world is an open lot with a small shop building in its near-left corner, matching the
brief's red/green sketch: **red = the open yard, green = the shop**.

```
yard    64 x 34 m, centred on the origin, entirely buildable
shop    16 x 12 m, near-left corner: ShopSideMargin (2 m) from the west wall,
        ShopFrontMargin (7 m) from the street so there is a real forecourt
street  beyond the yard's front edge at z = +17
```

Shelves and the till go **indoors**; pet pens go **outdoors in the yard**. Both use the same
placement path, so the only difference is which cells the starter layout picks.

`ShopGenerator.StarterLayout(grid)` derives every cell from the live geometry rather than
hard-coding, so resizing the yard or shop does not break it.

**The street's cross-section is measured from `YardFrontZ`.** `ShopGenerator.Generate` sets
`Street.KerbZ` and `Street.FarPavementZ` explicitly — forgetting that left the road sitting
on top of the yard, with parked cars in the shop doorway and the player spawning on tarmac.

## First person

`FirstPersonCamera` owns facing: it drives the body's yaw and keeps pitch to itself, so
movement, the interaction SphereCast and the build raycast all agree with where you look.
`PlayerController.SteerTowardsMovement` is therefore **off** — leave it off unless you put
the orbit camera back.

The player model stays in the scene but renders **shadows-only** in first person: you cast a
shadow without the inside of your own head filling the screen.

The cursor is locked, so **build mode aims from the screen centre**, not the pointer —
see `BuildMode.RaycastFloor`. Placement is clamped to `MaxPlacementDistance` so looking at
the horizon does not drop a shelf on the far side of the map.

`ThirdPersonCamera` is still in the project and still works, but nothing creates it.

## Building — walls as well as furniture

`BuildCatalog` carries four building pieces alongside the furniture: **wall, window wall,
doorway, fence**. All are one cell, span the full cell width so a row joins seamlessly, and
rotate with **R**. `BuildCatalog.IsBuildingPiece(id)` distinguishes them.

Walls change navigation, so the NavMesh is rebaked **as soon as one is placed or removed**,
not on build-mode exit — otherwise customers walk through a wall you just built.

The shop's own shell is still fixed geometry; only placed pieces can be modified.

## Characters

The player and every customer are built by `CharacterFactory.Attach`, which pulls one of
100 rigged bodies from the character pack and adds `CharacterVisual` to drive its animator.
That controller exposes `move` (0 idle / 1 walk / 2 run) and `speed` (playback multiplier);
`CharacterVisual` reads a velocity delegate — `NavMeshAgent.velocity` for customers,
`CharacterController.velocity` for the player — so the walk cycle matches real travel speed
instead of skating. `applyRootMotion` is off: movement comes from the agent, never the
animation. Without the pack it falls back to `MeshBuilder.CreateHumanoid`.

## Looking at the game — `./build.sh look`

**The game can be photographed head-lessly.** This is the single most useful tool in the
repo; use it after any visual change instead of guessing.

```bash
./build.sh look          # renders Screenshots/00..10_*.png of a freshly generated shop
```

How it works, and why each part is load-bearing:

- The editor runs **without `-nographics`**. With it, Unity uses a null device and cannot
  render at all. Without it — given `DISPLAY` and an `XAUTHORITY` cookie from
  `/run/user/<uid>/xauth_*` — it gets a real OpenGL device off the DRM render node. Opening
  an actual *game window* from a non-desktop shell still hangs, which is why the player
  build cannot be launched this way; the editor's offscreen path can.
- `SceneShot.Capture` opens the scene, **deletes the save**, and enters play mode. Deleting
  matters: without it the tour photographs whatever day the last run left behind, and an
  early set of "empty pens" screenshots was really a day-5 save whose animals had all sold.
- `CameraTour` renders each viewpoint through a **RenderTexture**, not `ScreenCapture` —
  there is no game view in batch mode. It must not use `WaitForEndOfFrame` either; that
  never fires in batch mode and hangs the coroutine.
- It exits by writing `_done.txt`, which `SceneShot` polls for. `CameraTour` must not call
  `Application.Quit` in the editor — that tears the windowing system down underneath Unity
  and segfaults it on the way out.

A player build also honours `-tour <dir>` and `-screenshot <path>`.

## The world outside

`StreetGenerator` builds everything beyond the shop window. Its cross-section, running out
from the shopfront along +Z:

```
z 8.1        shop front wall (glazed — this is what makes the street worth building)
z 7 – 14     near pavement    walkable; customers spawn here and walk in
z 14 – 22    road             NOT walkable: it has no collider, so it bakes no NavMesh
z 22 – 26    far pavement     decoration only
z 26 +       block opposite, then the skyline
```

Two rules keep this cheap and predictable:

- **Kenney models never carry colliders.** `KenneyModelPostprocessor` disables them. All
  collision is procedural boxes placed by `StreetGenerator`, so what is walkable is decided
  explicitly rather than inherited from art.
- **The pavement is drawn full-length but only collides within `WalkableHalf` (26 m).**
  The NavMesh bake volume comes from colliders, so this is what keeps the bake small. If you
  widen the walkable area, expect the bake to get slower.

The barriers fencing the pavement in sit on the **Ignore Raycast** layer: they still collide
and still bake, but the camera can see through them instead of being shoved about by an
invisible wall.

## World-space labels

Price tags, pen signs, thought bubbles and delivery labels are all `WorldLabel` — a TextMeshPro
in world space, not a per-object canvas, so they occlude correctly behind walls.

Two rules, both learned the hard way:

- **`size` is the cap height in metres.** `TextMeshPro.fontSize` runs at roughly ten units per
  metre, so `Create` sets `fontSize = size * 10f`. Passing a "font size" straight through gives
  price tags a metre tall.
- **Billboarding runs off `Camera.onPreCull`, per rendering camera** — never once a frame against
  `Camera.main`. The screenshot tour renders through its own untagged camera, and a label turned
  to face the player is edge-on, i.e. invisible, from anywhere else. Labels that "do not render"
  are almost always rendering perfectly, facing somewhere else.

Labels hide below `MinVisibleDistance` (0.8 m) as well as beyond `MaxVisibleDistance`, so standing
next to a sign does not black out the screen.

## Restocking and supplier orders

Two ways to get stock, and the difference between them is the planning game:

- **Order ahead** (ledger → Catalogue → *Order 12 X*): charged at `WholesaleDiscount` (0.78) per
  unit up front, arrives 0.10–0.22 of a day later as a `DeliveryCrate` on the forecourt. `E` on the
  crate moves the units into the stockroom.
- **Cash-and-carry**: `E` on a shelf with an empty stockroom buys on the spot at `EmergencyMarkup`
  (1.45). Always available, so a player who never orders can still trade — just at far worse margins.

`ShelfUnit.Restock` drains the stockroom first and returns a `RestockResult` (units, how many came
from the stockroom, what was spent). Orders still in transit at close of business arrive overnight
rather than being lost; the stockroom persists via `SaveData.Warehouse`.

## Model scaling

The Kenney kits are authored at wildly different scales — a furniture bookcase measures ~8.8
units tall, a whole city building ~1.3. **Never hard-code a scale factor.** `ModelLibrary`
measures each model's combined mesh bounds off the asset and scales it so a chosen axis hits
a target size in metres:

```csharp
ModelLibrary.Spawn(ModelLibrary.Commercial + "building-f", parent, pos,
                    yRotation: 180f, ModelLibrary.Fit.Height, targetSize: 9f);
```

`Spawn` also drops the model onto Y = 0 and centres it on X/Z. Models whose pivot means
something — a street light whose arm reaches out from the post — pass
`centreHorizontally: false`. `Logs/kenney-bounds.txt` (regenerate with
`KenneyBoundsDump.Dump`) is the measured reference table for every imported model.

## Two non-obvious build requirements

**1. Shaders must be in "Always Included Shaders".** Every material is created at runtime with
`Shader.Find`, so nothing in the project references those shaders and the build stripper drops
them — materials then come out null and the game throws on the first `new Material(null)`.
`ShaderInclusion.EnsureIncluded` (run by `build.sh setup`) adds them.
`MaterialFactory.LitShader` also falls back to borrowing the shader off a throwaway primitive.

**2. Imported packs are authored for URP; this project is Built-in.** URP/Lit is simply
absent, so materials fall back to the magenta error shader — that is the "purple furniture".
`MaterialRepair.Repair` (run by `build.sh setup` and `build.sh assets`) repoints them onto
Standard, carrying textures and colours across by reading the material's *serialised*
properties, which survive even when the shader is missing. It catches three cases:

- shader missing or the error shader;
- shader name starting `Universal Render Pipeline/` or `HDRP/`;
- **a project-authored `.shader` tagged `"RenderPipeline" = "UniversalPipeline"`** — this one
  compiles without error, so Unity reports nothing, but nothing matches it and it renders
  magenta anyway. The animal pack's `ShaderMaster` is exactly this.

It also flattens metallic/smoothness on pack materials: left PBR-shiny under a sky ambient,
models mirror the sky and come out uniformly blue, which reads as a broken texture.

**3. TMP essential resources are not in the repo.** `ProjectSetup.ImportTMPEssentials` imports them
headlessly. The package import finishes on a *later* editor tick, so that method must run
**without `-quit`** — it exits the editor itself once the import lands.

## Assembly definition

`Assets/Scripts/PetShop.asmdef` references three GUIDs:

```
GUID:6055be8ebefd69e48b49212b09b47b2f  → TextMeshPro (bundled in com.unity.ugui 2.6.0)
GUID:2bafac87e7f4b9b418d9448d219b01ab  → UGUI
GUID:8c4dd21966739024fbd72155091d199e  → AI Navigation (NavMeshSurface)
```

Add GUIDs, not package names — Unity 6 asmdef requires GUIDs.
`Assets/Editor/` has no asmdef and lands in `Assembly-CSharp-Editor`, which auto-references `PetShop`.

## Layers

`TagManager.asset` defines layer **8 = Character**, **9 = Furniture** and **10 = Scenery**.
Resolve them through `GameLayers` (which looks them up by name), never by hard-coded index.

- Player and customers → `Character`: excluded from camera collision and the NavMesh bake.
- Shelves, pens, counter → `Furniture`: this is what the interaction SphereCast looks for.
  Only this layer is interactable, which is why the street can never be pressed E at.
- The street and interior dressing → `Scenery`.
- The build ghost and the pavement barriers → `Ignore Raycast`.

## File map

```
Assets/
├── Editor/
│   ├── ProjectSetup.cs             ← headless TMP essentials import (run without -quit)
│   ├── ShaderInclusion.cs          ← adds runtime shaders to Always Included Shaders
│   ├── KenneyModelPostprocessor.cs ← import settings for the model kits (no colliders, no rigs)
│   ├── KenneyAssetReport.cs        ← verifies every model imported with a material
│   ├── KenneyBoundsDump.cs         ← writes Logs/kenney-bounds.txt
│   ├── AssetStoreImporter.cs       ← imports downloaded Asset Store packs (one per run)
│   ├── SpawnVerify.cs              ← checks spawned models for size/orientation/grounding
│   ├── FacingProbe.cs              ← works out which way a building model faces
│   ├── SceneBuilder.cs             ← generates Assets/Scenes/MainScene.unity
│   └── GameBuilder.cs              ← headless Linux/Windows player builds
│
├── Resources/
│   ├── Kenney/                     ← CC0 model kits
│   └── Packs/                      ← Asset Store packs (NOT redistributable)
│
└── Scripts/
    ├── PetShop.asmdef
    │
    ├── Core/
    │   ├── GameBootstrapper.cs     ← entry point; builds systems → world → UI → wiring
    │   ├── GameManager.cs          ← singleton; day cycle, save/load, interactions, catalog
    │   ├── GameLayers.cs           ← named layer lookups + masks
    │   ├── CharacterFactory.cs     ← City People bodies; CharacterVisual animator bridge; procedural humanoid fallback
│   ├── ModelLibrary.cs         ← path constants for all Kenney + Asset Store packs; Prefab(path) with Fit scaling
    │   ├── MaterialFactory.cs      ← material cache, palette, build-safe shader resolution
    │   ├── MeshBuilder.cs          ← all procedural geometry (furniture, humanoids, pets)
    │   ├── AudioManager.cs         ← synthesises every clip at startup; no audio assets
    │   ├── WorldLabel.cs           ← billboarded world-space TMP labels + FloatingText pops
    │   └── SaveSystem.cs           ← JSON save incl. full furniture layout
    │
    ├── Shop/
    │   ├── ShopGenerator.cs        ← room, shopfront, facade, lighting, player, NavMesh, layout
    │   ├── StreetGenerator.cs      ← pavement, road, terrace, block opposite, skyline, props
    │   ├── GridManager.cs          ← CellSize 2 m, XZ plane, footprint helpers
    │   ├── FurnitureFactory.cs     ← BuildCatalog (ids/costs/sizes) + Spawn()
    │   └── BuildMode.cs            ← floor-plane cursor, ghost, placement, refunds
    │
    ├── Player/
    │   ├── PlayerController.cs     ← CharacterController, camera-relative WASD
    │   ├── ThirdPersonCamera.cs    ← RMB orbit, scroll zoom, Linecast collision
    │   └── InteractionSystem.cs    ← SphereCast on the Furniture layer; CounterInteractable
    │
    ├── Customer/
    │   ├── CustomerAI.cs           ← NavMeshAgent shopper: browse → basket → till → leave
    │   └── CustomerSpawner.cs      ← footfall scales with reputation; closes doors near closing
    │
    ├── Commerce/
    │   ├── ShopManager.cs          ← balance, reputation, sales log, rent, orders, CloseDay()
    │   ├── DeliveryCrate.cs        ← forecourt pallet from a supplier order; E to collect
    │   ├── ShelfUnit.cs            ← product lines, in-world price tag, stockroom restocking
    │   ├── ProductItem.cs          ← id, name, category, unitCost, basePrice
    │   └── ItemDatabase.cs         ← 14-product catalog built in code; asset is optional
    │
    ├── Pets/
    │   ├── Pet.cs                  ← ScriptableObject; species, growth, rarity, traits, pricing
    │   ├── PetPen.cs               ← residents + 3D bodies that wander (PetVisual)
    │   └── BreedingSystem.cs       ← Breed(), GenerateRandom(), AdvanceDay(pens)
    │
    ├── UI/
    │   ├── GameUI.cs               ← owns every panel; single place Esc/Tab are routed
    │   ├── UIFactory.cs            ← palette + Node/Panel/Label/Button helpers
    │   ├── ShopHUD.cs              ← status bar, clock, shopper count, alerts, build palette
    │   ├── TitleScreen.cs          ← boot screen; Continue / New shop / Quit
    │   ├── PauseMenu.cs            ← Esc menu; sets Time.timeScale = 0
    │   ├── StatsPanel.cs           ← the ledger (Tab): shelves, animals, catalogue
    │   ├── DayResultsPanel.cs      ← end-of-day books, revenue breakdown, advice
    │   ├── InfoPanel.cs            ← shelf / pen / books popup
    │   └── GameOverPanel.cs        ← bankruptcy screen with Restart
    │
    └── Dev/
        └── ScreenshotCapture.cs    ← -screenshot support
```

## Key architecture rules

**One path for all furniture.** The starter shop, player placement and save loading *all* go through
`BuildMode.Place()` → `FurnitureFactory.Spawn()`. Never spawn furniture any other way — the grid
registration, layer assignment and `GameManager` bookkeeping all hang off that path.

**MaterialFactory is the single source of truth for colours.** Never `new Material()` in game logic —
call `MaterialFactory.Get(key, color)`. Same key + same colour = same instance.

**MeshBuilder never adds behaviour components.** It returns a GameObject with only
renderer/collider; the caller attaches `ShelfUnit`, `PetPen`, etc.

**MeshBuilder geometry sits on Y = 0.** `CreateBox`/`CreateCylinder`/`CreateSphere` place the *base*
at local Y = 0, not the centre. If you override `localPosition` afterwards, re-add the half-height
or the object sinks into the floor.

**GridManager is 3D.** `WorldToGrid` uses `x`/`z`; `GridToWorld` returns Y = 0. For multi-cell
footprints use `FootprintCenter(cell, size)`, not `GridToWorld` — the latter is the *root cell*
centre and a 2×1 shelf placed there is off by half a cell.

**NavMesh is baked at runtime** by `ShopGenerator.BakeNavMesh()` — `CollectObjects.Children` over the
`Shop` root, using **PhysicsColliders**, excluding the Character layer. It is rebaked whenever build
mode exits. New static geometry that is not a child of `ShopRoot` will not be in the NavMesh.

**Day flow:** clock runs out (or Enter) → `GameManager.EndDaySequence()` → spawner stops →
`BreedingSystem.AdvanceDay(pens)` ages pets and breeds → `ShopManager.CloseDay()` charges rent and
returns the summary → save → `OnDayEnded` → `DayResultsPanel` (modal) → Continue → `StartNewDay()`.
A negative closing balance ends the game instead.

**Modals block input.** `GameManager.IsModalOpen` is set by the results, game-over, pause,
ledger and title panels; `PlayerController`, `InteractionSystem` and `GameManager.Update` all
honour it. `BuildMode` also ignores mouse clicks when `EventSystem.IsPointerOverGameObject()` —
otherwise clicking the build bar would also place furniture.

**Escape is routed in exactly one place** — `GameUI.HandleEscape`, most-transient first. Do not
add another `KeyCode.Escape` check anywhere; that is why one press used to both cancel a
placement and close a panel.

## Shop mechanics

Four systems carry the game loop. They interlock, so changing one in isolation usually
breaks the balance of another.

**The checkout queue** (`CheckoutQueue`). Customers with a basket join a line at the till
and wait; nobody pays themselves any more. The player serves the front of the queue by
pressing E at the counter. Patience is `PatienceSeconds + place * PatiencePerPlace` — people
accept a wait if they can see a line — and walking out costs 3.5 reputation and the sale.

**Staff** (`Assistant`). Hired assistants stand behind the till and serve the queue on their
own, at `ServiceSeconds` each (the player is instant), for a daily wage. This exists because
the queue would otherwise pin you behind the counter forever. **A new game starts with one
assistant** — with nobody on the till a shop cannot trade at all while you are out in the
yard, and a headless run would show zero revenue.

**Pet care** (`Pet.health/hunger/happiness`, `PetPen.FoodLevel/Cleanliness`). Feed and
bedding drain each day in proportion to how many animals live in a pen. Neglect costs
condition, and condition multiplies sale price (`Mathf.Lerp(0.35f, 1.1f, health)`) — a
starved animal is worth about a third of a thriving one. Servicing a pen costs
`4 + count * 3.5`. Pen state is visible in world: a feed heap that shrinks, muck that
appears.

**Pricing** (`ShopManager.PriceMultiplier`, 0.6–1.8). Set from the ledger's Manage tab.
`DemandFactor = (1 / multiplier)^1.6` gates both shelf and pet purchases, so greed shows up
as people leaving empty-handed rather than as a hidden number.

`E` on a pen is context-sensitive and **prioritises care over commerce**: service it if it
needs it, otherwise buy an animal, otherwise report.

## Economy

- Shelf goods: €2.60–€16.50, bought at ~45% of sale price. Restocking a shelf fills every line.
- Pets: `SpeciesBasePrice` × rarity (1 / 1.5 / 2.5 / 5) × 1.2 if adult. Bought from the breeder at
  55% of base by interacting with a pen — **this is what stops a sold-out pen being a dead end.**
- A customer buys at most one pet per visit (12% chance per browsing stop).
- Rent: €45 on day 1, +€6 per day. Wages: €55 per assistant per day. Going negative at
  close is the fail state, and the game-over text names both.
- Leaving empty-handed costs 0.6 reputation; reputation drives footfall.

## Known issues / not yet done

- `Assets/Sprites/`, `Assets/Prefabs/`, `Assets/ScriptableObjects/` and `Assets/Audio/` are empty
  leftovers from the 2D version. Nothing references them.
- `ShopManager._stock` (the string-keyed ledger) is vestigial — real inventory lives on `ShelfUnit`.
  It is still saved/loaded for compatibility.
- A customer already inside at closing time checks out into the *next* day's sales log.
- No shop-upgrade system: the floor rect is fixed at 20 × 16 m.
- `MaterialFactory.ForPet` / `ForProduct` are unused now that colours come from pet coats and
  `ProductItem.fallbackColor`. Kept as a palette reference.
- **Nothing has been looked at.** GUI launch fails from a headless shell on this machine (the
  player hangs at window creation under XWayland without the desktop session's `XAUTHORITY`),
  so everything has been verified by measurement, logs and clean smoke runs rather than by
  eye. `SpawnVerify` confirms sizes/orientation and `FacingProbe` settled building facing,
  but **composition** — whether the street reads well, whether props sit at sensible spots,
  whether UI panels overlap — is unchecked. Start there.
- `StreetGenerator.BuildingFacing` was derived from the city pack's demo scene (+Z won 28
  votes to 15 across 56 buildings). A plurality, not a certainty — if the parade shows its
  back, flip that one constant.
- **A killed editor leaves `Temp/UnityLockfile` behind**, and every later batch run then
  fails with what looks like a compiler error. `build.sh` clears it when nothing holds it.
- **Asset Store packages are extracted, not imported by Unity.** `build.sh assets` runs
  `Tools/unpack_unitypackage.py` (a `.unitypackage` is a gzipped tar) and then
  `Tools/organise_packs.py`. Unity's own `AssetDatabase.ImportPackage` cannot be driven from
  batch mode: any package containing C# — City People and the characters pack both do —
  triggers a domain reload mid-import that destroys the state tracking progress and hangs
  the editor forever. Do not "simplify" this back to `ImportPackage`.
- `organise_packs.py` also merges any folder whose name mentions animals into
  `Packs/Animals/`, and `PetPen` looks models up by species name with aliases (Cat→Kitty
  etc.), so dropping in a new animal pack needs no code change. It moves pack prefabs under
  `Assets/Resources/Packs/` and is **idempotent**:
  re-extracting restores a pack's original folders, and keeping both copies gives Unity two
  assets with the same GUID.
- The far pavement and the skyline have no colliders, so they are scenery the player can
  never reach. That is deliberate.
- Each pack ships a demo scene (and the nature pack shipped nested HDRP/URP `.unitypackage`
  files, since deleted). The demo scenes are harmless but are included in the project.

## What to work on next (suggested priority)

1. **Eyeball the rendered game** — `./build.sh run`, check proportions, lighting and UI scale.
2. **Shop upgrades** — spend money to widen the floor rect and add room sections.
3. **A breeding panel** — choose which two adults pair up rather than leaving it to chance.
4. **Customer patience** — queue at the till, and lose reputation when the queue is long.
5. **Staff** — hire an assistant who restocks shelves automatically.
6. **Authored audio** — drop AudioClips into `AudioManager.SfxClips`; they override the synth ones.
