# Pet Shop Simulator — Feature Specifications

_Spec for Claude Code. Treat every section as a work order. Cross-reference CLAUDE.md for architecture
rules, IMPROVEMENTS.md for backlog context._

---

## 1 · Sims-like Building Mechanic

### What it is
Wall segments are **drawn by dragging**, not placed one cell at a time. When a set of wall segments
forms a closed loop, the enclosed floor auto-fills. Furniture placement stays grid-snapped as it is
now but gains a category palette modelled on The Sims build panel.

### Input model — `WallTool`

Add `WallTool.cs` in `Assets/Scripts/Shop/`. `BuildMode` delegates to it when the active catalog
item is any `IsBuildingPiece` id.

```
MouseDown on floor  → record StartCell, enter drag state
MouseDrag           → Bresenham-walk grid cells from StartCell to current cell;
                      render a chain of ghost wall pieces along the path
MouseUp             → commit: call BuildMode.Place() for each cell in the chain
                      (charge only once per segment, total cost = count × unit cost)
R during drag       → toggle wall type in the chain (wall → wall_window → wall_door)
RMB / Esc           → cancel drag, destroy ghosts
```

The ghost chain uses the existing `_ghostMat` (valid/invalid colour) on `MeshBuilder.CreateBox` per
cell. Invalid cells (occupied, out of bounds) colour red; the rest colour the valid tint.

### Room detection — `RoomDetector`

New static class `Assets/Scripts/Shop/RoomDetector.cs`.

```csharp
// Call after any wall is placed or removed.
public static List<RectInt> DetectRooms(GridManager grid, HashSet<Vector2Int> wallCells)
```

Algorithm:
1. Build adjacency: a cell is "enclosed" if BFS from it cannot reach the yard boundary without
   crossing a wall cell.
2. Flood-fill from every non-wall cell. Each isolated interior region = one room.
3. Return the list of room cell sets (not necessarily rectangular).

On detection, for each **new** room:
- Fill interior cells with the current `FloorMaterial` (stored per-room in `GridManager`).
- Raise `OnRoomCreated(RoomData room)` on `GameManager`.
- The room's area contributes to customer navigation: `BakeNavMesh()` is called automatically.

When a wall is removed and two regions merge, remove the room records and raise `OnRoomDestroyed`.

### Build panel — category tabs

Replace the current flat `HotkeyOrder` bar with a **tab strip** across the top of the build panel:

| Tab | Items |
|-----|-------|
| 🏗 Structure | wall, wall_window, wall_door, fence |
| 🪑 Furniture | shelf_small, shelf_large, counter |
| 🐾 Pens | pen_cat, pen_dog, pen_rabbit, pen_small, pen_bird, pen_reptile |
| 🎨 Floors | (material swatches for the active room's floor) |

Keys 1–8 still jump to items within the active tab. Tab key cycles build tabs (not the ledger tab —
that is already Shift+Tab or separate).

`BuildCatalog` gains `string Category` per entry and a `GetByCategory(string cat)` method.
`ShopHUD` rebuilds the build bar when the active category changes.

### Undo / redo — `BuildHistory`

New `BuildHistory` component on the Shop root, capacity 20 actions.

```csharp
void Push(BuildAction action)   // called by BuildMode.Place() and BuildMode.Remove()
void Undo()                     // Ctrl+Z
void Redo()                     // Ctrl+Y
```

`BuildAction` is a struct: `{ string Id, Vector2Int Cell, float Rotation, bool IsRemoval }`.
Undo of a placement removes the object and refunds cost. Undo of a removal re-places the object
and charges cost. Does **not** undo NavMesh bake (it rebakes automatically).

---

## 2 · Level-up Gates

### Shop level

`ShopManager` gains a computed property:

```csharp
public int ShopLevel => Reputation switch {
    >= 90 => 5,
    >= 70 => 4,
    >= 50 => 3,
    >= 30 => 2,
    _     => 1,
};
```

`OnShopLevelUp` Unity event fires when level increases. `ShopHUD` toasts "Shop level up! Now level X"
and plays the level-up audio clip.

### Unlock registry — `UnlockRegistry.cs`

New file `Assets/Scripts/Commerce/UnlockRegistry.cs`. Static class with a single `GetUnlocks(int
level)` that returns `UnlockSet`.

```csharp
public struct UnlockSet {
    public Pet.Species[]      Pets;
    public ProductCategory[]  ProductCategories;
    public string[]           FurnitureIds;   // BuildCatalog ids
    public int                MaxStaff;
}
```

Full table:

| Level | Pets unlocked | Products unlocked | Furniture | Max staff |
|-------|--------------|-------------------|-----------|-----------|
| 1 | Hamster, Rabbit, Fish, Chicken | Food | shelf_small, counter, pen_small, fence | 1 |
| 2 | Cat, Dog, Parrot | Food, Toy | shelf_large, pen_cat, pen_dog, pen_bird | 2 |
| 3 | Fox, Deer | Toy, Accessory | wall_window, pen_rabbit (enriched) | 3 |
| 4 | Penguin, Horse | Accessory, Medicine | pen_reptile, grooming_bay | 4 |
| 5 | Tiger (exotic licence) | All + Premium | all | unlimited |

`UnlockRegistry.IsUnlocked(string furnitureId, int level)` and `.IsSpeciesUnlocked(Species s, int
level)` are the query points.

### Lock display

**Build catalog:** locked items render at 50% opacity in the build bar with a 🔒 overlay and a
tooltip: `"Unlocks at shop level X · Reach {rep} reputation"`. Hovering shows the tooltip
(existing `InfoPanel` mechanic). Clicking a locked item plays the locked audio clip and shows the
tooltip; it does not enter placement mode.

**Ledger catalogue tab:** locked products show with a padlock badge. The player can still read the
product description (it acts as an incentive) but cannot add it to a shelf.

**Breeding panel (future):** locked species rows greyed out.

`ItemDatabase.IsAvailable(ProductItem item, int shopLevel)` wraps the check.
`BuildCatalog.IsAvailable(string id, int shopLevel)` likewise.

---

## 3 · In-game Guide Articles

### Data — `GuideArticle.cs`

```csharp
// Assets/Scripts/UI/Guide/GuideArticle.cs
[CreateAssetMenu(menuName = "PetShop/Guide Article")]
public class GuideArticle : ScriptableObject {
    public string       id;
    public string       title;
    public GuideCategory category;
    public string       bodyRichText;   // TMP rich text
    public string       unlockEvent;    // empty = always visible; "first_sale", "first_breed", etc.
}

public enum GuideCategory { Basics, Building, Pets, Commerce, Breeding, Advanced }
```

Place articles as assets in `Assets/Resources/Guide/`. `GuideLibrary.cs` loads them all at startup
with `Resources.LoadAll<GuideArticle>("Guide")`.

### Initial article set (12 articles)

| id | Title | Category | Unlocks |
|----|-------|----------|---------|
| basics_welcome | Welcome to Your Pet Shop | Basics | always |
| basics_day | A Day in the Life | Basics | always |
| building_walls | Drawing Walls | Building | always |
| building_furniture | Placing Furniture | Building | always |
| commerce_pricing | Setting Prices | Commerce | always |
| commerce_shelves | Stocking Shelves | Commerce | first_restock |
| commerce_queue | The Checkout Queue | Commerce | first_customer |
| pets_care | Caring for Your Pets | Pets | first_pen_placed |
| pets_pens | Choosing the Right Pen | Pets | first_pen_placed |
| breeding_intro | Breeding Basics | Breeding | first_breed |
| breeding_rarity | Rare Traits & Rarity | Breeding | first_rare_birth |
| advanced_expansion | Growing Your Shop | Advanced | shop_level_3 |

Body text uses TMP rich text. Example formatting conventions:
- `<b>Bold</b>` for game terms (Balance, Reputation, Demand Factor).
- `<color=#F5A623>amber</color>` for currency values and key numbers.
- `<size=120%><b>Heading</b></size>` for in-article sub-sections.
- `<i>Italic</i>` for tips / asides.

Article bodies should be 80–200 words. Short, scannable, practical.

### UI — `GuidePanel.cs`

Accessible via **G key** (new binding in `GameUI.HandleInput`) or Tab → Guide tab.
`GuidePanel` is a modal like `StatsPanel` — sets `GameManager.IsModalOpen = true`.

Layout (all `UIFactory`):
```
┌────────────────────────────────────────┐
│  GUIDE                            [X]  │
├──────────┬─────────────────────────────┤
│ Basics   │  <article title>            │
│ Building │                             │
│ Pets     │  <bodyRichText TMP scroll>  │
│ Commerce │                             │
│ Breeding │                             │
│ Advanced │                             │
└──────────┴─────────────────────────────┘
```

Left column: category buttons (`UIFactory.Button`, width 120). Clicking a category refreshes the
right list. Right panel: article title buttons, then selected article body in a `ScrollRect` with
`TextMeshProUGUI`.

Locked articles show in italic grey; hovering shows "Complete: [unlock condition]".

### Contextual links

`ShopHUD.ShowAlert(text, articleId)` — when `articleId` is non-null, the alert strip appends
`" [?]"` as a small button. Pressing it opens `GuidePanel` directly to that article.

Apply this to the existing alerts:
- Hungry pens → `pets_care`
- Low reputation → `commerce_pricing`
- Empty shelves → `commerce_shelves`
- Queue growing → `commerce_queue`

`GameManager` raises `OnGuideUnlock(string articleId)` when an unlock event fires. `GuideLibrary`
listens and marks the article available. `ShopHUD` toasts "New guide article: [title]".

---

## 4 · Unified Art Style

All visual output — procedural geometry, Kenney models, Asset Store packs — must read as one system.

### Palette

Define in `MaterialFactory.Palette` (static readonly `Dictionary<string, Color>`):

```csharp
// Surfaces
"wall_interior"   = new Color(0.96f, 0.93f, 0.87f)   // warm cream
"wall_exterior"   = new Color(0.82f, 0.76f, 0.65f)   // sand render
"floor_wood"      = new Color(0.58f, 0.40f, 0.24f)   // dark oak
"floor_tile"      = new Color(0.88f, 0.86f, 0.82f)   // light stone
"floor_yard"      = new Color(0.45f, 0.52f, 0.35f)   // sage green
"floor_paddock"   = new Color(0.72f, 0.62f, 0.42f)   // sand
// Furniture
"wood_light"      = new Color(0.80f, 0.62f, 0.38f)   // pine
"wood_dark"       = new Color(0.40f, 0.26f, 0.14f)   // walnut
"metal_frame"     = new Color(0.55f, 0.57f, 0.60f)   // brushed steel
"pen_bar"         = new Color(0.30f, 0.33f, 0.38f)   // dark iron
// Accents
"accent_gold"     = new Color(0.93f, 0.76f, 0.25f)   // shop sign / rare badges
"accent_green"    = new Color(0.28f, 0.72f, 0.45f)   // positive UI
"accent_red"      = new Color(0.87f, 0.27f, 0.27f)   // negative UI / danger
"accent_blue"     = new Color(0.27f, 0.53f, 0.87f)   // info / links
```

### Material rules

All `MaterialFactory.Get()` calls must enforce:

```
Smoothness : 0.10 – 0.25  (matte — no mirror reflections)
Metallic   : 0.00          (except pen bars / hardware: 0.45)
```

Add `MaterialFactory.Matte(string key, Color col)` which sets these values. Replace all existing
`new Material(shader)` calls in `MeshBuilder` with `MaterialFactory.Matte(...)`.

### Geometry conventions

- All `MeshBuilder.CreateBox` calls: pass `roundness: 0.04f` (a new optional float that offsets
  corner vertices outward by that amount, creating a subtle bevel). Implement in `MeshBuilder.Bevel`.
- Furniture rests **1 mm above the floor** (`localPosition.y += 0.001f`) to prevent Z-fighting.
- Pen fences use cylinders with radius 0.03 m, not boxes, so bars cast round shadows.

### `StyleValidator` editor tool

`Assets/Editor/StyleValidator.cs` — menu item **PetShop → Validate Style**.

Scans all `Renderer` components in the active scene. Flags:
- Smoothness > 0.35 → "too shiny"
- Metallic > 0.55 → "too metallic"
- Material shader not `Standard` and not `TextMeshPro/...` → "wrong shader"

Prints a report to the console. Does not auto-fix (preserves intentional exceptions).

---

## 5 · Species-specific Pens

### `PetPenProfile` — new ScriptableObject

```csharp
// Assets/Scripts/Pets/PetPenProfile.cs
[CreateAssetMenu(menuName = "PetShop/Pet Pen Profile")]
public class PetPenProfile : ScriptableObject {
    public Pet.Species  Species;
    public PenShape     Shape;           // Cage, Kennel, Hutch, Tank, Aviary, Terrarium, Open
    public Color        FrameColor;
    public Color        BedColor;
    public Color        AccentColor;
    public string[]     PropIds;         // ModelLibrary path suffixes for props in the pen
    public float        WallHeight;
    public bool         HasRoof;
    public bool         TransparentFront; // glass tank / aviary front
}
```

Create one asset per species in `Assets/Resources/PenProfiles/`. `PetPen.Build()` loads the profile
for `PenSpecies` at `Awake`.

### Pen geometry per species

`MeshBuilder` gains these new methods (all use the matte palette and bevel rules above):

| Method | Used by |
|--------|---------|
| `CreateBarWall(w, h, barCount, barRadius)` | Cat cage, aviary, small animal front |
| `CreateKennelRoof(w, d)` | Dog, rabbit hutch |
| `CreateGlassPanel(w, h, opacity)` | Fish tank, hamster tank, reptile front |
| `CreateHayPile(scale)` | Rabbit, hamster substrate |
| `CreateWaterBowl(radius)` | Dog, cat |
| `CreatePerch(h)` | Bird, parrot |
| `CreateHeatLamp(height)` | Reptile |
| `CreateWheel(radius)` | Hamster |

### Species pen designs

| Species | Shape | Key props | Frame colour |
|---------|-------|-----------|-------------|
| **Cat** | Cage (bars 3 sides, open front) | Scratch post, soft bed mat | dark iron (`pen_bar`) |
| **Dog** | Kennel (open front, sloped roof) | Water bowl, rubber toy | wood_dark |
| **Rabbit** | Hutch (raised box + wire run below) | Hay pile, salt lick | wood_light |
| **Hamster** | Tank (glass 3 sides, bar front) | Wheel, wood chips, hide | metal_frame |
| **Parrot** | Tall aviary (bar all sides, perch cross) | Branch perch, seed cup | accent_gold |
| **Fish** | Aquarium (glass box, no floor wander) | Bubble stream (particle), gravel base | metal_frame |
| **Chicken** | Open yard pen (low fence, dirt floor) | Feed trough, perch beam | wood_light |
| **Fox / Deer** | Large open pen (post-and-rail × 2 h) | Tree stump, water trough | pen_bar |
| **Horse** | Paddock stall (wood 3 sides, open front) | Hay net, bucket | wood_dark |
| **Penguin** | Pool pen (low walls, blue rubber floor) | Water trough at ground level | metal_frame |
| **Tiger** | Reinforced cage (double bar layer, lock icon) | Large platform, meat prop | pen_bar + accent_red |

### `PetPen.Build(PetPenProfile profile)`

Replace the current single `MeshBuilder.CreateBox` pen body with:

```
1. Floor slab  → MeshBuilder.CreateBox(w, 0.06f, d) — BedColor
2. Back wall   → shape-specific (bar wall / solid / glass)
3. Side walls  × 2
4. Front wall  → open / bar / glass per species
5. Roof        → if profile.HasRoof, MeshBuilder.CreateKennelRoof or flat slab
6. Props       → foreach PropId: ModelLibrary.Spawn(id, pen, propPos)
```

All geometry as children of the pen root, so `FurnitureFactory.Spawn` and `GridManager` still track
the pen at its root position.

### Ambient pen animations

Each `PetVisual` component gains `PenAmbient` coroutine started by `PetPen` at spawn:
- Cat / Dog / Rabbit: wander existing path (unchanged).
- Hamster: `PetVisual.RunWheel()` — rotates the wheel transform at `WheelRPM` proportional to speed.
- Bird / Parrot: `PetVisual.Perch()` — bob head (rotate ±5° around X) every 1.2 s.
- Fish: `PetVisual.Swim()` — slow figure-eight path inside the tank bounds.
- Chicken: existing wander with occasional peck (crouch animation).
- Horse / Deer: slow pace along the back wall.

---

## 6 · Skippable Tutorial

### `TutorialManager.cs`

New `Assets/Scripts/Core/TutorialManager.cs`. Instantiated by `GameBootstrapper` on first
new-game only (`SaveSystem.TutorialComplete == false`).

### Steps

| # | Title | Trigger to advance | World arrow target |
|---|-------|--------------------|-------------------|
| 1 | Place a shelf | `BuildMode.OnFurniturePlaced` where `id == "shelf_small"` | Build bar shelf button |
| 2 | Stock the shelf | `ShelfUnit.OnRestocked` | The placed shelf |
| 3 | Adjust your price | `ShopManager.OnPriceMultiplierChanged` | Ledger → Manage tab |
| 4 | Serve a customer | `CheckoutQueue.OnCustomerServed` | Counter object in world |
| 5 | End the day | `GameManager.OnDayEnded` (auto) | Clock progress bar |

After step 5 completes: set `SaveSystem.TutorialComplete = true`, save, call
`TutorialManager.Teardown()` (destroys arrows, removes HUD panel).

### World arrow

`TutorialArrow.cs` — simple `MeshBuilder.CreateBox` (0.15 × 0.5 × 0.15, amber) on the Ghost layer
with a `Bounce` coroutine (±0.2 m Y, 0.8 s period). Positioned:
- For UI elements: above the screen-space rect, projected to world at 2 m from camera.
- For world objects: 1.5 m above the object's transform position.

Arrow tracks the target each frame in case the player moves furniture.

### HUD tutorial panel

`TutorialHUD.cs` — a small `UIFactory.Panel` anchored top-left (width 300, height 80).
Shows: step number badge + step title + body line (one sentence).
"**Skip tutorial →**" button right-aligned, styled as a ghost button (transparent background,
grey border, small font). On click: `TutorialManager.Skip()`.

`Skip()` marks `TutorialComplete = true`, shows a toast "Tutorial skipped — press G for the Guide",
and tears down. The Guide articles remain available.

### Integration points

```csharp
// GameBootstrapper.WireUI():
if (!SaveSystem.Load().TutorialComplete)
    _tutorial = gameObject.AddComponent<TutorialManager>();
    _tutorial.Init(_build, _shop, _game, _ui.HUD);

// GameUI.HandleEscape():
// Esc during tutorial does NOT open pause menu — it shows "Press Esc again to skip tutorial"
// Second Esc within 2 s calls TutorialManager.Skip().
```

Do not block input during tutorial steps. The player can freely explore between steps; the arrow
just pulses until the trigger fires.

---

## Delivery checklist for Claude Code

Work in this order to avoid breaking the running game:

- [ ] **StyleValidator** + palette constants (no runtime change, safe first)
- [ ] **PetPenProfile** assets + `PetPen.Build(profile)` — replaces existing pen body, species by
  species; run `./build.sh look` after each species
- [ ] **UnlockRegistry** + `ShopManager.ShopLevel` + lock display in HUD/catalogue
- [ ] **GuideArticle** assets + `GuidePanel` + contextual alert links
- [ ] **WallTool** (drag-draw) + `RoomDetector` — keep existing single-cell placement as fallback
- [ ] **BuildHistory** undo/redo
- [ ] **Build panel category tabs**
- [ ] **TutorialManager** + `TutorialHUD` + `TutorialArrow`

Run `./build.sh look` after each item and check the plan-view screenshots.
