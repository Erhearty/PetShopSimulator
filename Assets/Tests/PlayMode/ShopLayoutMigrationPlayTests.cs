using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
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
            data.PlacedObjects.Add(new SaveData.PlacedItem
                { catalogId = BuildCatalog.ShelfSmall, cellX = OffYardShelfCell.x, cellY = OffYardShelfCell.y });
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
        }

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
