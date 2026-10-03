using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Dev
{
    /// <summary>
    /// Dev/test-only helper that furnishes the empty shop of a new game the way the old starter
    /// shop was: a counter, a food and a toy shelf (stocked), and a dog and a cat pen with two
    /// pets each. Headless smoke/soak runs (<c>-furnish</c>), the camera tour and PlayMode tests
    /// use it so customers have something to trade with.
    ///
    /// This is not a runtime generator of world content: it only places existing catalogue
    /// prefabs through the normal <see cref="BuildMode.Place"/> path (grid occupancy,
    /// registration and saves behave exactly like real placement), and never runs in normal play.
    /// Cells are picked by a deterministic scan of lot stage 0; pets come from a fixed seed.
    /// </summary>
    public static class DevFurnisher
    {
        /// <summary>Seed for the pets' random traits when the caller gives none.</summary>
        public const int DefaultSeed = 1234;

        /// <summary>Metres beside the till spot (along +x) where the counter goes.</summary>
        private const float CounterBesideTill = 2.5f;
        /// <summary>Cells behind the till (towards the back wall) kept free for the staff station.</summary>
        private const int StaffCellsBehindTill = 1;
        /// <summary>Cells in front of the till (the queue line, +z) kept free.</summary>
        private const int QueueCellsInFrontOfTill = 3;
        /// <summary>Share of the room width each shelf sits from the shop centre.</summary>
        private const float ShelfSideShare = 0.3f;
        /// <summary>Metres either side of the pen row's centre the two pens sit.</summary>
        private const float PenSpacing = 3f;
        /// <summary>Share of the room depth, from the shop centre towards the front door, where the pens sit.</summary>
        private const float PenRowOffsetShare = 0.1f;
        /// <summary>Pets put in each pen.</summary>
        private const int PetsPerPen = 2;
        /// <summary>Everything is placed unrotated, so catalogue footprints apply as-is.</summary>
        private const float NoRotation = 0f;

        /// <summary>
        /// Places the starter furniture and returns how many pieces were placed. Places nothing
        /// (returns 0) when the game is not set up or the shop already has a shelf, pen or counter.
        /// UnityEngine.Random is seeded with <paramref name="seed"/> while pets are generated and
        /// restored afterwards, so the caller's random stream is unaffected.
        /// </summary>
        public static int Furnish(GameManager game, int seed = DefaultSeed)
        {
            if (!CanFurnish(game)) return 0;
            if (IsFurnished(game))
            {
                Debug.Log("[DevFurnisher] The shop already has furniture; nothing placed.");
                return 0;
            }

            var saved = Random.state;
            Random.InitState(seed);
            int placed;
            try { placed = PlaceAll(game); HireStarterCashier(game); }
            finally { Random.state = saved; }

            game.Layout.BakeNavMesh();
            Debug.Log($"[DevFurnisher] Placed {placed} pieces of starter furniture (seed {seed}).");
            return placed;
        }

        /// <summary>
        /// A new game has no staff, but headless smoke/soak runs have no player to serve the till,
        /// so the dev furnisher hires one free cashier (dev/test paths only, never in NewGame).
        /// </summary>
        private static void HireStarterCashier(GameManager game)
        {
            if (game.StaffCount > 0) return;
            var cashier = StaffCandidate.Generate();
            cashier.SignOnFee = 0f;
            cashier.Role      = StaffRole.Cashier;
            game.HireCandidate(cashier);
        }

        /// <summary>True when the shop already has a shelf, a pen or a counter.</summary>
        public static bool IsFurnished(GameManager game) =>
            game != null && (game.Shelves.Count > 0 || game.Pens.Count > 0
                             || (game.Spawner != null && game.Spawner.HasCounter));

        private static bool CanFurnish(GameManager game)
        {
            if (game != null && game.Build != null && game.Build.GridManager != null && game.Layout != null)
                return true;
            Debug.LogWarning("[DevFurnisher] No game, build mode or layout; nothing placed.");
            return false;
        }

        private static int PlaceAll(GameManager game)
        {
            ShopLayout layout = game.Layout;
            Vector3 shop    = layout.ShopCentre;
            Vector3 till    = layout.TillPosition;
            Vector3 penRow  = shop + Vector3.forward * (layout.RoomDepth * PenRowOffsetShare);
            Vector3 side    = Vector3.right * (layout.RoomWidth * ShelfSideShare);
            // The till follows the counter, so the queue lane is kept clear in front of the counter
            // itself (and the staff strip behind it), wherever the scan put it.
            int placed = 0;
            RectInt keepClear = default;
            var counterDef = BuildCatalog.Get(BuildCatalog.Counter);
            if (counterDef != null && TryFindCell(game, counterDef.Size, till + Vector3.right * CounterBesideTill,
                                                  default, out Vector2Int counterCell))
            {
                placed += Count(game.Build.Place(counterCell, counterDef, null, NoRotation, charge: false));
                keepClear = TillLane(counterCell, counterDef.Size);
            }
            placed += Count(PlaceShelf(game, ProductCategory.Food, shop - side, keepClear));
            placed += Count(PlaceShelf(game, ProductCategory.Toy,  shop + side, keepClear));
            placed += Count(PlacePen(game, Pet.Species.Dog, penRow + Vector3.left  * PenSpacing, keepClear));
            placed += Count(PlacePen(game, Pet.Species.Cat, penRow + Vector3.right * PenSpacing, keepClear));
            return placed;
        }

        /// <summary>
        /// The counter's columns from the staff strip (behind it, -z) through the queue line
        /// (in front, +z): furniture there would trap the assistant or block the queue.
        /// </summary>
        private static RectInt TillLane(Vector2Int counterCell, Vector2Int counterSize) =>
            new(counterCell.x, counterCell.y - StaffCellsBehindTill,
                counterSize.x, StaffCellsBehindTill + counterSize.y + QueueCellsInFrontOfTill);

        private static GameObject PlaceShelf(GameManager game, ProductCategory category, Vector3 target,
                                             RectInt keepClear)
        {
            var go = PlaceNear(game, BuildCatalog.ShelfLarge, category.ToString(), target, keepClear);
            if (go != null) Stock(go.GetComponent<ShelfUnit>(), game.Catalog);
            return go;
        }

        /// <summary>Fills the shelf as the old SeedShelf did: one full line per product, up to MaxLines.</summary>
        private static void Stock(ShelfUnit shelf, ItemDatabase catalog)
        {
            if (shelf == null || catalog == null) return;
            var products = catalog.GetByCategory(shelf.Category);
            for (int i = 0; i < products.Count && i < shelf.MaxLines; i++)
                shelf.AddStock(products[i], shelf.MaxPerLine);
        }

        private static GameObject PlacePen(GameManager game, Pet.Species species, Vector3 target,
                                           RectInt keepClear)
        {
            var go  = PlaceNear(game, BuildCatalog.PetPen, species.ToString(), target, keepClear);
            var pen = go != null ? go.GetComponent<PetPen>() : null;
            if (pen == null) return go;
            for (int i = 0; i < PetsPerPen; i++) pen.AddPet(BreedingSystem.GenerateRandom(species));
            return go;
        }

        /// <summary>Places <paramref name="id"/> on the free lot-0 cell nearest <paramref name="target"/>.</summary>
        private static GameObject PlaceNear(GameManager game, string id, string variant, Vector3 target,
                                            RectInt keepClear)
        {
            var def = BuildCatalog.Get(id);
            if (def == null) return null;
            if (!TryFindCell(game, def.Size, target, keepClear, out Vector2Int cell))
            {
                Debug.LogWarning($"[DevFurnisher] No free cell for {id} in the starter lot.");
                return null;
            }
            return game.Build.Place(cell, def, variant, NoRotation, charge: false);
        }

        /// <summary>
        /// Deterministic scan of lot stage 0: the placeable cell whose footprint centre is nearest
        /// <paramref name="target"/>, never overlapping <paramref name="keepClear"/>. Ties keep the first found.
        /// </summary>
        private static bool TryFindCell(GameManager game, Vector2Int size, Vector3 target,
                                        RectInt keepClear, out Vector2Int best)
        {
            GridManager grid = game.Build.GridManager;
            RectInt lot = game.Layout.LotStageCells(ShopLayout.StarterLotStage);
            target.y = 0f;
            best = default;
            float bestDistance = float.MaxValue;
            foreach (Vector2Int cell in lot.allPositionsWithin)
            {
                if (!grid.CanPlace(cell, size) || new RectInt(cell, size).Overlaps(keepClear)) continue;
                float distance = (grid.FootprintCenter(cell, size) - target).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = cell;
            }
            return bestDistance < float.MaxValue;
        }

        private static int Count(GameObject placed) => placed != null ? 1 : 0;
    }
}
