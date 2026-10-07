using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// A save written against the old layout (room standing back from the street, layout version 0) loads
    /// with every piece moved onto the current layout: furniture lands back inside the room, walls on the ring.
    /// </summary>
    public class ShopLayoutMigrationPlayTests
    {
        private const int   Seed      = 4242;
        private const float TimeScale = 1f;
        private const float DayLength = 900f;
        private const int   OldLayoutVersion = 0;
        /// <summary>A layout-0 front-yard cell that the layout shift moves past the yard's street edge.</summary>
        private static readonly Vector2Int OffYardShelfCell = new(-11, 7);
        /// <summary>A layout-0 cell whose 2x2 pen covers the old front rows y 6..7, which the shift pushes off the yard.</summary>
        private static readonly Vector2Int OffYardPenCell = new(-11, 6);
        /// <summary>Species of the legacy pen the fixtures add.</summary>
        private const Pet.Species FixturePenSpecies = Pet.Species.Rabbit;
        /// <summary>Pets living in the occupied fixture pen.</summary>
        private const int FixturePenPets = 2;
        /// <summary>Units of stock on the fixture shelf pushed off the yard.</summary>
        private const int FixtureShelfUnits = 3;
        /// <summary>Opening words of the one notice a load that moved or packed pieces shows.</summary>
        private const string RebuiltNotice = "Your shop was rebuilt at the front of the yard";
        /// <summary>Opening words of the notice naming pets sold back because no pen could hold them.</summary>
        private const string RefundNotice = "No room was left for a pen pushed off the yard, so its pets were sold back";
        /// <summary>
        /// A layout-0 cell in the open front yard, well clear of the room, whose 2x2 pen the shift pushes off the
        /// yard's street edge; its unshifted cells are ordinary yard the fixture can block.
        /// </summary>
        private static readonly Vector2Int ExposedPenOldCell = new(6, 6);
        /// <summary>Current-layout cell of the full pen of the fixture species in the back corner of the yard.</summary>
        private static readonly Vector2Int FullPenCell = new(12, -8);

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        /// <summary>An old-layout fixture's counter and walls come back at the shifted cells, and re-saving stamps the version.</summary>
        [UnityTest]
        public IEnumerator LoadingOldLayoutSave_ShiftsPiecesIntoTheMovedRoom()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            RectInt room = game.Layout.RoomCells();
            var counterCell = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
            Assert.IsNotNull(game.Build.Place(counterCell, BuildCatalog.Get(BuildCatalog.Counter), null, 0f, charge: false));
            int walls = CountRingWalls(game);

            var data = SaveAndRead(game);
            Assert.AreEqual(ShopLayout.CurrentLayoutVersion, data.layoutVersion, "A new save should write the current layout.");
            MakeOldLayoutFixture(data);
            ClearGrid(game);
            LoadSave(game, data);

            Assert.IsTrue(game.Grid.TryGetObject(counterCell, out var entry) && entry.Data.Id == BuildCatalog.Counter,
                          $"The old save's counter is not at its shifted cell {counterCell}.");
            Assert.IsTrue(room.Contains(entry.Root), "The counter landed outside the moved room.");
            Assert.AreEqual(walls, CountRingWalls(game), "The old save's walls should stand on the moved ring.");
            Assert.AreEqual(ShopLayout.CurrentLayoutVersion, SaveAndRead(game).layoutVersion);
        }

        /// <summary>
        /// A layout-0 shelf on the old front-yard row the shift pushes past the yard's street edge (onto the
        /// pavement and the forecourt spot) is not placed: nothing ends up outside the yard and the shelf is back
        /// in the furniture inventory.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingOldLayoutSave_PacksPiecesShiftedOffTheYard()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            MakeOldLayoutFixture(data);
            int ownedBefore = data.FurnitureInventory.Where(e => e.id == BuildCatalog.ShelfSmall).Sum(e => e.qty);
            var product = game.Catalog.All[0];
            ProductCategory category = product.category;
            int warehouseBefore = game.Shop.Warehouse(category) +
                                  data.Warehouse.Where(e => e.id == category.ToString()).Sum(e => e.qty);
            var shelf = new SaveData.PlacedItem
            {
                catalogId = BuildCatalog.ShelfSmall, variant = category.ToString(),
                cellX = OffYardShelfCell.x, cellY = OffYardShelfCell.y,
            };
            shelf.shelfStock.Add(new SaveData.StockEntry { id = product.id, qty = FixtureShelfUnits });
            data.PlacedObjects.Add(shelf);
            ClearGrid(game);
            LoadSave(game, data);

            RectInt yard = game.Layout.LotStageCells(ShopLayout.FullYardLotStage);
            foreach (var entry in game.Grid.GetAllPlaced())
            {
                var footprint = new RectInt(entry.Root, entry.Size);
                Assert.IsTrue(yard.Contains(footprint.min) && yard.Contains(footprint.max - Vector2Int.one),
                              $"{entry.Data?.Id} at {entry.Root} stands outside the yard {yard}.");
            }
            Assert.AreEqual(ownedBefore + 1, game.Furniture.OwnedCount(BuildCatalog.ShelfSmall),
                            "The shelf pushed off the yard should be back in the furniture inventory.");
            Assert.AreEqual(warehouseBefore + FixtureShelfUnits, game.Shop.Warehouse(category),
                            "The packed shelf's stock should have gone to the warehouse.");
        }

        /// <summary>
        /// A layout-0 pen with pets on the old front rows the shift pushes off the yard cannot be packed (its pets
        /// would be lost): it is placed in the yard instead, both pets alive in it and kept by the next save, and
        /// the load shows one notice saying so.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingOldLayoutSave_MovesOccupiedPenShiftedOffTheYardIntoTheYard()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            MakeOldLayoutFixture(data);
            var pen = OffYardPen();
            for (int i = 0; i < FixturePenPets; i++)
                pen.pets.Add(SaveSystem.PetToSaveData(BreedingSystem.GenerateRandom(FixturePenSpecies)));
            var petIds = pen.pets.Select(p => p.id).ToList();
            data.PlacedObjects.Add(pen);
            ClearGrid(game);

            var notices = new List<string>();
            UnityAction<string> listen = notices.Add;
            game.OnNotification.AddListener(listen);
            try { LoadSave(game, data); }
            finally { game.OnNotification.RemoveListener(listen); }

            var entry = game.Grid.GetAllPlaced()
                            .FirstOrDefault(e => e.Instance != null && e.Instance.GetComponent<PetPen>() != null);
            Assert.IsNotNull(entry, "The occupied pen pushed off the yard was not placed.");
            CollectionAssert.AreEquivalent(petIds, entry.Instance.GetComponent<PetPen>().Residents.Select(p => p.id).ToList(),
                                           "The relocated pen should hold both of its pets.");
            RectInt yard = game.Layout.LotStageCells(ShopLayout.FullYardLotStage);
            var footprint = new RectInt(entry.Root, entry.Size);
            Assert.IsTrue(yard.Contains(footprint.min) && yard.Contains(footprint.max - Vector2Int.one),
                          $"The relocated pen at {entry.Root} stands outside the yard {yard}.");
            Assert.IsFalse(game.Layout.IsInsideShop(entry.Root, entry.Size), "The relocated pen stands in the shop room.");

            var savedIds = SaveAndRead(game).PlacedObjects.SelectMany(i => i.pets).Select(p => p.id).ToList();
            CollectionAssert.IsSubsetOf(petIds, savedIds, "The relocated pen's pets were dropped from the next save.");
            var rebuilt = notices.Where(n => n.StartsWith(RebuiltNotice)).ToList();
            Assert.AreEqual(1, rebuilt.Count, "The load should show exactly one rebuilt-shop notice.");
            StringAssert.Contains("1 pen moved", rebuilt[0]);
        }

        /// <summary>An empty layout-0 pen the shift pushes off the yard is packed away, like the Remove tool does.</summary>
        [UnityTest]
        public IEnumerator LoadingOldLayoutSave_PacksEmptyPenShiftedOffTheYard()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            MakeOldLayoutFixture(data);
            int ownedBefore  = (data.FurnitureInventory ?? new List<SaveData.StockEntry>())
                               .Where(e => e.id == BuildCatalog.PetPen).Sum(e => e.qty);
            int packedBefore = (data.PackedFurniture ?? new List<SaveData.StockEntry>())
                               .Where(e => e.id == BuildCatalog.PetPen).Sum(e => e.qty);
            data.PlacedObjects.Add(OffYardPen());
            ClearGrid(game);
            LoadSave(game, data);

            Assert.IsFalse(game.Grid.GetAllPlaced().Any(e => e.Data != null && e.Data.Id == BuildCatalog.PetPen),
                           "The empty pen pushed off the yard should not be placed.");
            Assert.AreEqual(ownedBefore + 1, game.Furniture.OwnedCount(BuildCatalog.PetPen),
                            "The empty pen should be back in the furniture inventory.");
            Assert.AreEqual(packedBefore + 1, game.Furniture.PackedCount(BuildCatalog.PetPen),
                            "The empty pen should count as packed away.");
        }

        /// <summary>
        /// A layout-0 pen with pets that the shift pushes off the yard, when the yard has no free pen spot left, the
        /// only other pen of its species is full, and its old unshifted cells are taken by another saved piece:
        /// no pet vanishes. Each is either kept in a pen or sold back, the balance rises by the sold pets' value,
        /// and a notice names them and the refund.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingOldLayoutSave_SellsBackPetsOfAPenWithNowhereToGo()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var data = SaveAndRead(game);
            var layout = game.Layout;

            // Current-layout cells: a full pen of the species, then a fence on one cell of every 2x2 square of the
            // yard that is still a pen spot, so no pen fits anywhere. MakeOldLayoutFixture shifts them all back.
            var fullPen = new SaveData.PlacedItem
            {
                catalogId = BuildCatalog.PetPen, variant = FixturePenSpecies.ToString(),
                cellX = FullPenCell.x, cellY = FullPenCell.y,
            };
            for (int i = 0; i < BuildCatalog.PenCapacity; i++)
                fullPen.pets.Add(NamedPet($"Full{i}"));
            data.PlacedObjects.Add(fullPen);
            BlockEveryPenSpot(data, layout);
            MakeOldLayoutFixture(data);

            var pen = new SaveData.PlacedItem
            {
                catalogId = BuildCatalog.PetPen, variant = FixturePenSpecies.ToString(),
                cellX = ExposedPenOldCell.x, cellY = ExposedPenOldCell.y,
            };
            for (int i = 0; i < FixturePenPets; i++)
                pen.pets.Add(NamedPet($"Stray{i}"));
            data.PlacedObjects.Add(pen);

            var oldCells = new RectInt(ExposedPenOldCell, BuildCatalog.Get(BuildCatalog.PetPen).Size);
            Assert.IsTrue(data.PlacedObjects.Any(i => i != pen && ShiftedFootprint(i).Overlaps(oldCells)),
                          "Fixture: another saved piece should stand on the pen's old unshifted cells.");

            var allPets = fullPen.pets.Concat(pen.pets).ToList();
            float balanceBefore = data.Balance;
            ClearGrid(game);

            var notices = new List<string>();
            UnityAction<string> listen = notices.Add;
            game.OnNotification.AddListener(listen);
            try { LoadSave(game, data); }
            finally { game.OnNotification.RemoveListener(listen); }

            var keptIds = game.Grid.GetAllPlaced()
                              .Where(e => e.Instance != null && e.Instance.GetComponent<PetPen>() != null)
                              .SelectMany(e => e.Instance.GetComponent<PetPen>().Residents.Select(p => p.id))
                              .ToList();
            var soldBack = allPets.Where(p => !keptIds.Contains(p.id)).ToList();
            Assert.IsNotEmpty(soldBack, "Fixture: the pen should have found no spot and no room for its pets.");

            string refund = notices.FirstOrDefault(n => n.StartsWith(RefundNotice));
            foreach (var pet in soldBack)
                Assert.IsTrue(refund != null && refund.Contains(pet.petName),
                              $"Pet '{pet.petName}' vanished: it is in no pen and no notice says it was sold back.");

            float value = soldBack.Sum(p => SaveSystem.SaveDataToPet(p).SellPrice());
            Assert.AreEqual(balanceBefore + value, game.Shop.Balance, 0.01f,
                            "The balance should rise by the value of every pet sold back.");
            StringAssert.Contains($"€{value:N2}", refund, "The notice should name the refund.");
        }

        /// <summary>A pet of the fixture species named <paramref name="name"/>, as saved.</summary>
        private static SaveData.PetSaveData NamedPet(string name)
        {
            var saved = SaveSystem.PetToSaveData(BreedingSystem.GenerateRandom(FixturePenSpecies));
            saved.petName = name;
            return saved;
        }

        /// <summary>
        /// Adds a fence on every yard cell whose x and y share the yard corner's parity (one cell of every 2x2
        /// square), skipping cells that are already no pen spot: the room, its wall ring, the doorways and saved
        /// pieces. Cells are in <paramref name="data"/>'s current layout.
        /// </summary>
        private static void BlockEveryPenSpot(SaveData data, ShopLayout layout)
        {
            RectInt yard = layout.LotStageCells(ShopLayout.FullYardLotStage);
            RectInt ring = layout.RoomWallCells();
            var taken = data.PlacedObjects.Where(i => BuildCatalog.Get(i.catalogId) != null)
                            .Select(i => new RectInt(new Vector2Int(i.cellX, i.cellY),
                                                     BuildMode.FootprintSize(BuildCatalog.Get(i.catalogId), i.footprintRotated)))
                            .ToList();
            for (int x = yard.xMin; x < yard.xMax; x += 2)
            for (int y = yard.yMin; y < yard.yMax; y += 2)
            {
                var cell = new RectInt(x, y, 1, 1);
                if (ring.Overlaps(cell) || layout.IsInsideShop(cell.position, cell.size) ||
                    layout.OverlapsDoorway(cell) || taken.Any(t => t.Overlaps(cell))) continue;
                data.PlacedObjects.Add(new SaveData.PlacedItem { catalogId = BuildCatalog.Fence, cellX = x, cellY = y });
            }
        }

        /// <summary>The cells a layout-0 <paramref name="item"/> covers once shifted onto the current layout.</summary>
        private static RectInt ShiftedFootprint(SaveData.PlacedItem item)
        {
            var def = BuildCatalog.Get(item.catalogId);
            if (def == null) return new RectInt(0, 0, 0, 0);
            return new RectInt(new Vector2Int(item.cellX, item.cellY) + ShopLayout.LegacyLayoutCellShift,
                               BuildMode.FootprintSize(def, item.footprintRotated));
        }

        /// <summary>An empty legacy pen saved at <see cref="OffYardPenCell"/>.</summary>
        private static SaveData.PlacedItem OffYardPen() => new()
        {
            catalogId = BuildCatalog.PetPen, variant = FixturePenSpecies.ToString(),
            cellX = OffYardPenCell.x, cellY = OffYardPenCell.y,
        };

        /// <summary>Turns a current save into a layout-0 save: version 0, every cell where the old room stood.</summary>
        private static void MakeOldLayoutFixture(SaveData data)
        {
            data.layoutVersion = OldLayoutVersion;
            foreach (var item in data.PlacedObjects)
            {
                item.cellX -= ShopLayout.LegacyLayoutCellShift.x;
                item.cellY -= ShopLayout.LegacyLayoutCellShift.y;
            }
        }

        private static int CountRingWalls(GameManager game) => game.Grid.GetAllPlaced().Count(e =>
            e.Data != null && BuildCatalog.IsBuildingPiece(e.Data.Id) && game.Layout.IsRoomWallCell(e.Root));

        private static SaveData SaveAndRead(GameManager game)
        {
            Assert.IsTrue(game.SaveGame(), "Saving failed.");
            var data = SaveSystem.Load();
            Assert.IsNotNull(data, "The save could not be read back.");
            return data;
        }

        /// <summary>Removes every placed piece through the Remove path.</summary>
        private static void ClearGrid(GameManager game)
        {
            foreach (var entry in game.Grid.GetAllPlaced().ToList())
                game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(entry.Root));
        }

        /// <summary>Loads <paramref name="data"/> through the game's save/load controller, as at boot.</summary>
        private static void LoadSave(GameManager game, SaveData data)
        {
            var field = typeof(GameManager).GetField("_saveLoad", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "GameManager has no save/load controller.");
            ((SaveLoadController)field.GetValue(game)).LoadGame(data);
        }
    }
}
