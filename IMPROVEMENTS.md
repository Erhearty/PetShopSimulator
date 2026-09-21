# Pet Shop Simulator — Improvements Backlog

_Research + code audit, September 2026. Based on genre survey (Supermarket Simulator, Pet Shop Simulator
on Steam) and review of the current codebase._

---

## What the current build already does well

- **Economy has real texture**: demand elasticity (`DemandFactor = (1/multiplier)^1.6`), growing rent
  (+€6/day), daily wages, a six-tier reputation-footfall loop.
- **Checkout queue** is fully modelled: patience scales with queue position, walkaways cost 3.5 rep,
  assistants serve autonomously.
- **Pet care loop** exists end-to-end: health/hunger/cleanliness drain per day, starvation reduces sale
  price to 35%, `E` prioritises service over commerce.
- **Breeding with rarity upgrades** (45% nightly chance, 5% rarity step, trait mutation) gives a long-
  term progression axis.
- **Day results** show a proportion bar by category, actionable advice (up to 3 hints), colour-coded
  profit.
- **Alert strip** auto-surfaces the most urgent issue every 1.5 s.
- **HUD** has a live rent countdown, reputation bar, queue label, clock, and a toast system.

Everything below is what is _missing_ relative to the genre, or is rough in the current build.

---

## UI improvements

### 1. In-world price tags (HIGH) — ✅ DONE (sprint 2)
Genre standard. Every shelf and pen should show a small floating label (TMP in World Space)
with the current price. Players should never have to open the ledger to check prices.
- `ShelfUnit`: add a `WorldSpaceLabel` child at top-front; refresh when `PriceMultiplier` changes.
- `PetPen`: label shows pet species + price, colour-coded by rarity (common=white, uncommon=green,
  rare=blue, legendary=gold). Matches the colour coding used in `UIFactory`.

### 2. Customer thought bubbles (HIGH) — ✅ DONE (sprint 3)
Shop simulators universally show what a customer wants. A small canvas above each customer head
showing a product icon (or text abbreviation) tells the player at a glance what stock to prioritise.
- `CustomerAI` already has a `_targetShelf` / `_targetPet`; expose it via an event.
- `CharacterFactory.Attach` returns a `CharacterVisual`; add a world-space canvas above the head
  driven by `CustomerAI.OnWantChanged`.

### 3. Satisfaction flash on purchase (MEDIUM) — ✅ DONE (sprint 3)
A green "+€X" floating text that rises and fades when a sale completes (customer at till, or pet sold).
Implemented as a pooled `WorldTextParticle` launched from `CheckoutQueue.Complete` and `PetPen.Sell`.

### 4. Shelf stock counter (MEDIUM) — ✅ DONE (folded into the price tag)
Shelves currently show a crate per product line but no quantity. Add a small HUD label or in-world
badge showing `X / maxUnits` so the player can spot nearly-empty shelves at a glance.

### 5. Running income/expense ticker in HUD (MEDIUM) — ✅ DONE (sprint 3)
Current HUD shows balance (static), rent tonight (static). Add a live "today: +€X / -€Y" ticker
updated on every sale and every wage payment. This is the single most-requested feature in reviews
of Supermarket Simulator. `ShopManager.GetCurrentDaySummary()` has all the data.

### 6. Minimap / shop overview (LOW)
A small top-down overlay (Tab → Manage tab, or a dedicated M key) showing the floor plan, shelf
positions, and a dot per customer. Useful once the shop expands beyond one room. Can be rendered
as a second Camera onto a RenderTexture clipped to a circle.

### 7. Pen info tooltip on look (LOW)
When the player looks at a pen without pressing E, show a small non-blocking tooltip at the bottom
centre: species, count, health bar, hunger bar, days-since-serviced. Currently the player must press
E to see any pen data.

---

## Gameplay improvements

### 1. Restocking orders / supplier system (HIGH) — ✅ DONE (sprint 4)
This is the core loop of Supermarket Simulator and Pet Shop Simulator alike. Rather than restocking
instantly with `E`, the player should:
1. Open the Catalogue tab in the ledger and place an order (quantity × product).
2. A delivery van appears outside after N minutes of game time (1–3 in-game hours).
3. Player collects boxes from the forecourt and unpacks them onto shelves (or an auto-restock
   assistant does it).

This changes restocking from a chore into a planning puzzle (ordering before you run out vs. cash
flow), and gives the exterior yard a purpose beyond pets.

Implementation sketch:
- Add `SupplierOrder { ProductId, Qty, ArrivalDayProgress }` to `ShopManager`.
- `GameManager.Update` polls `DayProgress`; on arrival, spawn a crate prop at the forecourt and
  raise `OnDeliveryArrived`.
- `InteractionSystem` handles E on the crate: transfers stock to a pending-inventory count.
- `ShelfUnit.Restock(pendingInventory)` pulls from pending rather than creating from nothing.

### 2. Visible pen health bars in world (HIGH) — ✅ DONE (sprint 2)
`PetPen` already tracks `FoodLevel` and `Cleanliness`. Render two small bar meshes (thin boxes,
green → red) above each pen gate so the player can read the whole yard at a glance.
`PetVisual` updates them each frame. No UI canvas needed — two `MeshBuilder.CreateBox` calls with
`MaterialFactory.Get("penBar_food", color)`.

### 3. Customer preferences (MEDIUM) — ✅ DONE (sprint 5, pulled forward)
Currently a customer browses any shelf and has a flat 12% chance to want a pet. Add a lightweight
preference profile generated at spawn:
- `PreferredCategory` (Animal / Food / Toy / Accessory) — drives which shelf they visit first.
- `BudgetCap` — won't buy if `DemandFactor`-adjusted price exceeds it.
- `WantsPet` bool (30% of customers) — makes them head to a pen before the till.

This makes pricing meaningful per-category rather than as a global slider, and makes the day-results
bar breakdown feel earned.

### 4. Breeding panel (MEDIUM)
`BreedingSystem.AdvanceDay` currently pairs at random. Add a modal (Tab → Animals → Breed) showing
all adult pets. Player drags two compatible adults into slots; the pairing is locked in for tonight.
Unpaired adults still breed randomly. Shows the expected offspring traits (midpoint of parents').
This is the feature most requested in the Pet Shop Simulator community.

### 5. Staff hiring UI (MEDIUM)
`GameManager._assistants` exists, but the player has no way to hire or fire. Add a "Staff" section
to the Manage tab: a pool of 3 candidate cards (randomised name, service speed, daily wage), a hire
button (deducts sign-on fee), a fire button (costs 0.5 rep per assistant — they talk). Show the
current total daily wage and how many are on shift.

### 6. Shop expansion / upgrade system (MEDIUM)
Currently the floor rect is fixed. Add an "Expand" button in the Manage tab that unlocks one of:
- **East wing** (+8 × 12 m interior) — €1,800 one-time.
- **Grooming bay** (+4 × 6 m side room, unlocks Grooming service) — €1,200.
- **Outdoor paddock upgrade** (larger paddock fence, +2 pen slots) — €950.

Implementation: `ShopGenerator.ExpandShop(ExpandType)` regenerates geometry for the new zone,
rebakes NavMesh, and updates `GridManager` cell count. Save/load must write the expansion state.

### 7. Pet trait visibility (MEDIUM)
Traits (`Pet.Traits` float array) are currently invisible in-game. Surface two key traits in the
pen tooltip and the breeding panel: **Temperament** (0=shy→1=friendly, affects customer attraction
chance +15% at max) and **Coat Quality** (0=plain→1=show-quality, adds a price premium up to +30%).
Renders as star icons in the UI.

### 8. Reputation events (LOW)
Random events that fire 0–1 times per day once reputation > 60:
- _Local newspaper feature_ (+8 rep, +15% footfall for 2 days) — triggered by streak of 5 profitable days.
- _Social media post_ (+5 rep spike) — triggered by a customer buying a legendary-rarity pet.
- _Inspection visit_ (inspector NPC checks pen cleanliness; pass=+3 rep, fail=-10 rep and €200 fine).

Adds mid-game narrative texture without a full quest system. Implemented as a `ReputationEvent` 
ScriptableObject list checked by `GameManager.StartNewDay()`.

### 9. Closing-time rush (LOW)
At `DayProgress > 0.85`, mark remaining shelf-browsing customers as "bargain hunters": they accept
a lower price (DemandFactor cap raised to 2.0) but won't queue more than 2 deep. Creates a useful
pressure moment near the end of each day without a new system.

### 10. Tutorial sequence (LOW)
New-game first day: a sequence of three prompted actions (place a shelf → stock it → serve a
customer). `TutorialManager` registers for `GameManager` events and shows a large pulsing arrow
(a stretched `MeshBuilder.CreateBox` in the Ghost layer). Skippable via Esc.

---

## Free Unity assets that fit the aesthetic

All entries are free or open-licence; "Free tier" means the publisher offers a free cut of a paid
pack. Confirm current availability on the Unity Asset Store before importing.

| Asset | Publisher | Why it fits | Notes |
|-------|-----------|-------------|-------|
| **Kenney Mini-Hex Tiles** | Kenney (CC0) | Extra floor textures for expansion zones | Already using Kenney; same import pipeline |
| **Kenney Food Kit** | Kenney (CC0) | Shelf product visuals: cans, produce, bags | Drop into `Assets/Resources/Kenney/Food/` — `ModelLibrary` already has the path constant |
| **Kenney Furniture Kit** | Kenney (CC0) | Tables, chairs, rugs for the grooming bay / waiting area | Same |
| **Stylized Grass & Ground Textures** | Lofty Sky (free tier) | Better yard surface material | Built-in compatible; swap `MaterialFactory` yard colour |
| **Low Poly Modular Interiors** | PolygonBlackcat (free sample) | Interior wall panels, ceiling tiles | Check Built-in compatibility; run `MaterialRepair` |
| **Simple Coins SFX Pack** | Dustyroom (free) | Register/sale sound effects | Drop into `AudioManager.SfxClips` to override the synth clip |
| **8-Bit & Chiptune SFX** | SketchyLogic (free) | Ambient shop sounds, door bell | Same |
| **Modular Village Pack (demo)** | Synty Studios (free demo) | Opposite-block building variety | URP materials — run `MaterialRepair.Repair`; check facing with `FacingProbe` |

**Paid but worth budgeting:**

- **Polygon Pet Store** (Synty, ~€80) — exact genre match; 200+ models including cages, grooming
  tables, pet accessories. `organise_packs.py` will place animals under `Packs/Animals/`
  automatically.
- **City People** (already referenced in `CharacterFactory.cs`) — 100 rigged characters.

---

## Bugs to fix before adding features

_All five closed. `Screenshots/16_hud.png` now shows the HUD; `NavProbe.Run()` runs on every tour and
reports every leg `PathComplete` (pavement → till 36.1 m), so bug 2 did not reproduce — the volume
already spanned the doorway._

1. ~~**HUD not visible in screenshots**~~ ✅ — 16 screenshots show no HUD. `ShopHUD.Build(canvas, ...)` is
   called after `GameBootstrapper` wires UI; check `Canvas.sortingOrder` and `renderMode`
   (must be `ScreenSpaceOverlay` or `ScreenSpaceCamera` with assigned camera).

2. ~~**NavMesh gap at shop door**~~ ✅ — customers reach the pavement but not the shop interior.
   Add a `NavMeshLink` bridging the doorstep, or ensure `NavMeshSurface` volume spans from
   `ShopFrontZ` through the interior. `./build.sh look` then check `03_plan_yard` for the
   walkable area outline.

3. ~~**Interior prop contamination**~~ ✅ — cactus, café chairs visible in screenshots. `DressInterior`
   filter needs tightening: only `pet_themed` category props inside; everything else is yard or street.

4. ~~**`casual_Female_G` bikini variant**~~ ✅ — renders as swimwear under indoor lighting.
   Remove that path from `CharacterFactory.CityPeople` string array.

5. ~~**City skyline hard clip**~~ ✅ — far building row has a visible flat edge at the horizon.
   Add a distance-fade material (`MaterialFactory` fog plane) or extend the skyline by 2–3
   extra building rows (cheap: they are all the same Kenney model).

---

## Suggested sprint order

| Sprint | Focus |
|--------|-------|
| 1 | ✅ Fix HUD visibility + NavMesh door gap (blockers for everything else) |
| 2 | ✅ In-world price tags + pen health bars (visual polish, high payoff) |
| 3 | ✅ Customer thought bubbles + satisfaction flash (feel) |
| 4 | ✅ Restocking order system (core loop depth) |
| 5 | Breeding panel ← **next** (customer preferences ✅) |
| 6 | Staff hiring UI + shop expansion (late-game) |

### Sprint 2–3 implementation notes

- `Core/WorldLabel.cs` — `WorldLabel` (billboarded world-space TMP on a dark plate, yaw-only so text
  stays upright, culled past `MaxVisibleDistance`) and `FloatingText` (rises, holds, fades, self-destroys).
  Both are the shared primitive for every in-world readout below.
- `ShelfUnit` — price tag above the top shelf: category, marked-up price range, `X / capacity in stock`.
  Red when empty, amber under 30%. Refreshed from `AddStock`/`TakeOne`/`Restock` and `OnPriceChanged`.
- `PetPen` — gate sign (species, top pet's price, `count/capacity · rarity`, tinted by rarity) plus two
  mesh bars for `FoodLevel` and `Cleanliness`, left-aligned and tinted red→green with a
  MaterialPropertyBlock (no material duplication).
- `CustomerAI` — `PreferredCategory` / `WantsPet` (30%) / `BudgetCap` generated at spawn. The bubble over
  the head shows the want, then reacts: *got X* (green), *too expensive* (red, and the item goes back on
  the shelf), *waiting to pay*, *gave up!*, *thanks!*. Sales and walkouts also launch a `FloatingText`.
- `ShopManager` — `EarnedToday` / `SpentToday` accumulated in `ChangeBalance`, reset by `CloseDay`;
  `ShopHUD` shows them as a live `today +€X / −€Y = net` ticker under the balance.
- **World labels billboard per rendering camera** (`Camera.onPreCull`), not once a frame against
  `Camera.main`. A label turned to face the player is edge-on — effectively invisible — to any other
  viewpoint, which is why the first pen signs did not show up in the tour screenshots at all.

### Sprint 4 implementation notes — supplier orders

The loop is: order ahead cheaply, or pay a premium at the door.

- `ShopManager.PlaceOrder(category, units, unitCost, dayProgress)` charges
  `unitCost × WholesaleDiscount (0.78)` per unit up front and schedules arrival 0.10–0.22 of a day
  later. `PollDeliveries(dayProgress)`, driven from `GameManager.Update`, raises `OnDeliveryArrived`.
- `DeliveryCrate.Spawn` puts a labelled pallet on the forecourt (one box per six units). `E` on it
  calls `GameManager.CollectDelivery`, which moves the units into the stockroom.
- `ShelfUnit.Restock` now returns a `RestockResult` and takes stockroom units first — already paid
  for — falling back to cash-and-carry at `EmergencyMarkup (1.45)`. Restocking never hard-fails, so
  a player who ignores ordering can still trade, just at ~46% worse margins.
- Orders still in transit at close of business are delivered to the stockroom overnight rather than
  being lost. Stockroom contents persist through `SaveData.Warehouse`.
- Ledger → Catalogue shows stockroom counts, order price vs. door price, and vans in transit with an
  arrival time; one order button per aisle (12 units).
- The HUD alert strip promotes an uncollected delivery above every other hint.
- Tour shot 29 (`29_delivery.png`) orders two pallets and forces them to land, so the mechanic is
  visible in the screenshot set.

