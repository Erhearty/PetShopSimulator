using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// The shop room's walls are ordinary placed pieces: a new game lays them on the room's border
    /// cells (with a doorway on the shopfront), the Remove tool takes one away with a NavMesh rebake,
    /// a save keeps it gone, and a save from before room walls gets them laid exactly once.
    /// </summary>
    public class RoomWallsPlayTests
    {
        private const int    Seed      = 3141;
        private const float  TimeScale = 1f;
        private const float  DayLength = 900f;
        /// <summary>What <see cref="ShopLayout.BakeNavMesh"/> logs after every bake.</summary>
        private const string BakeLog   = "[ShopLayout] NavMesh baked.";
        /// <summary>Editor-only bake noise the harness also tolerates.</summary>
        private static readonly Regex ToleratedError = new(@"RuntimeNavMeshBuilder: Source mesh .* does not allow read access");

        private int  _bakes;
        private bool _savedIgnore;
        private readonly System.Collections.Generic.List<string> _errors = new();

        [SetUp]
        public void SetUp()
        {
            _savedIgnore = LogAssert.ignoreFailingMessages;
            _bakes = 0;
            _errors.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = _savedIgnore;
            PlaytestHarness.Teardown();
        }

        /// <summary>New game walls the room; one removed wall rebakes, returns to stock and stays gone after save/load.</summary>
        [UnityTest]
        public IEnumerator NewGame_RoomWallRemoved_RebakesAndStaysRemovedAfterSaveLoad()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            int perimeter = PerimeterCellCount(game.Layout.RoomWallCells());
            Assert.AreEqual(perimeter, CountRoomWalls(game), "A new game should wall every border cell of the room.");
            Assert.Greater(CountPieces(game, BuildCatalog.WallDoor), 0, "The shopfront has no doorway piece.");

            StartWatchingLogs();
            var cell  = SideWallCell(game);
            int owned = game.Furniture.OwnedCount(BuildCatalog.Wall);
            game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "The Remove tool left the room wall on the grid.");
            Assert.AreEqual(1, _bakes, "Removing a room wall should rebake the NavMesh.");
            Assert.AreEqual(owned + 1, game.Furniture.OwnedCount(BuildCatalog.Wall), "The wall was not returned.");

            var data = SaveAndRead(game);
            Assert.IsTrue(data.roomWallsSeeded, "The save should record that the room walls were laid.");
            Reload(game, data);
            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "The removed wall came back after loading.");
            Assert.AreEqual(perimeter - 1, CountRoomWalls(game));
            AssertNoUnexpectedErrors();
        }

        /// <summary>A save without the flag gets the walls once; a later load of the re-saved game does not re-lay them.</summary>
        [UnityTest]
        public IEnumerator LoadingUnseededSave_SeedsRoomWallsExactlyOnce()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            int perimeter = PerimeterCellCount(game.Layout.RoomWallCells());
            StartWatchingLogs();

            var old = SaveAndRead(game);
            old.roomWallsSeeded = false;
            old.PlacedObjects.RemoveAll(p => BuildCatalog.IsBuildingPiece(p.catalogId));
            Reload(game, old);
            Assert.AreEqual(perimeter, CountRoomWalls(game), "Loading an old save should lay the room walls.");

            var cell = SideWallCell(game);
            game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            var resaved = SaveAndRead(game);
            Assert.IsTrue(resaved.roomWallsSeeded);
            Assert.AreEqual(perimeter - 1, resaved.PlacedObjects.Count(p => BuildCatalog.IsBuildingPiece(p.catalogId)));

            Reload(game, resaved);
            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "The second load laid the room walls again.");
            Assert.AreEqual(perimeter - 1, CountRoomWalls(game));
            AssertNoUnexpectedErrors();
        }

        /// <summary>A removed doorway is still doorway ground after save/load: a doorway piece goes back, nothing else does.</summary>
        [UnityTest]
        public IEnumerator DoorwayRemovedThenReloaded_TakesOnlyADoorwayPiece()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatchingLogs();

            var cell = DoorwayWallCell(game);
            game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            Reload(game, SaveAndRead(game));
            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "The removed doorway came back after loading.");

            AssertRefused(game, BuildCatalog.ShelfSmall, cell);
            AssertRefused(game, BuildCatalog.Wall, cell);
            AssertPutsBack(game, BuildCatalog.WallDoor, cell);
            AssertNoUnexpectedErrors();
        }

        /// <summary>A removed side wall can be put back after a load that rebuilt the floor; furniture cannot take its cell.</summary>
        [UnityTest]
        public IEnumerator SideWallRemovedThenReloaded_TakesOnlyBuildingPieces()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatchingLogs();

            var cell = SideWallCell(game);
            game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            var data = SaveAndRead(game);
            game.Grid.SetFloor(cell, false);   // a fresh boot only lays the unlocked lot's floor
            Reload(game, data);
            Assert.IsTrue(game.Grid.HasFloor(cell), "Loading left the wall ring cell without floor.");

            AssertRefused(game, BuildCatalog.ShelfSmall, cell);
            AssertPutsBack(game, BuildCatalog.Wall, cell);
            AssertNoUnexpectedErrors();
        }

        /// <summary>Laying the whole wall ring — by seeding or by loading an old save — bakes the NavMesh once.</summary>
        [UnityTest]
        public IEnumerator SeedingAndLoadingRoomWalls_BakeTheNavMeshOnce()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatchingLogs();

            var old = SaveAndRead(game);
            old.roomWallsSeeded = false;
            old.PlacedObjects.RemoveAll(p => BuildCatalog.IsBuildingPiece(p.catalogId));
            ClearGrid(game);
            _bakes = 0;
            LoadSave(game, old);
            Assert.AreEqual(1, _bakes, "Loading a save and laying its walls should bake once.");

            ClearGrid(game);
            _bakes = 0;
            Assert.Greater(game.Layout.SeedRoomWalls(game.Build), 0, "Seeding laid no walls.");
            Assert.AreEqual(1, _bakes, "Seeding the room walls should bake once.");
            AssertNoUnexpectedErrors();
        }

        /// <summary>The doorway piece on the room's wall ring.</summary>
        private static Vector2Int DoorwayWallCell(GameManager game)
        {
            var entry = game.Grid.GetAllPlaced().FirstOrDefault(e =>
                e.Data != null && e.Data.Id == BuildCatalog.WallDoor && game.Layout.IsRoomWallCell(e.Root));
            Assert.IsNotNull(entry, "No doorway piece on the wall ring.");
            Assert.IsTrue(game.Layout.IsDoorwayCell(entry.Root), $"{entry.Root} is not a doorway cell.");
            return entry.Root;
        }

        /// <summary>Asserts build mode would refuse catalogue item <paramref name="id"/> at <paramref name="cell"/>.</summary>
        private static void AssertRefused(GameManager game, string id, Vector2Int cell)
        {
            var def = BuildCatalog.Get(id);
            Assert.IsFalse(game.Build.CanPlaceItem(def, cell, def.Size), $"{id} was allowed at wall ring cell {cell}.");
        }

        /// <summary>Takes <paramref name="id"/> from stock and places it at <paramref name="cell"/> as the player would.</summary>
        private static void AssertPutsBack(GameManager game, string id, Vector2Int cell)
        {
            Assert.IsTrue(game.Build.EnterPlacement(id), $"No {id} in stock to put back.");
            Assert.IsNotNull(game.Build.PlaceHeld(cell), $"{id} could not be put back at {cell}.");
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Data.Id == id, $"No {id} at {cell}.");
        }

        /// <summary>Cells of the room's wall ring: the whole outer ring of <paramref name="room"/>.</summary>
        private static int PerimeterCellCount(RectInt room) => 2 * (room.width + room.height) - 4;

        /// <summary>A wall cell in the middle of the room's low-X side.</summary>
        private static Vector2Int SideWallCell(GameManager game)
        {
            RectInt room = game.Layout.RoomWallCells();
            var cell = new Vector2Int(room.xMin, room.yMin + room.height / 2);
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Data.Id == BuildCatalog.Wall,
                          $"No room wall at {cell}.");
            return cell;
        }

        /// <summary>Wall, window and doorway pieces rooted on the room's border cells.</summary>
        private static int CountRoomWalls(GameManager game) => game.Grid.GetAllPlaced().Count(e =>
            e.Data != null && BuildCatalog.IsBuildingPiece(e.Data.Id) && game.Layout.IsRoomWallCell(e.Root));

        private static int CountPieces(GameManager game, string id) =>
            game.Grid.GetAllPlaced().Count(e => e.Data != null && e.Data.Id == id);

        private static SaveData SaveAndRead(GameManager game)
        {
            Assert.IsTrue(game.SaveGame(), "Saving failed.");
            var data = SaveSystem.Load();
            Assert.IsNotNull(data, "The save could not be read back.");
            return data;
        }

        /// <summary>Clears every placed piece through the Remove path, then loads <paramref name="data"/> as at boot.</summary>
        private static void Reload(GameManager game, SaveData data)
        {
            ClearGrid(game);
            LoadSave(game, data);
        }

        /// <summary>Removes every placed piece through the Remove path.</summary>
        private static void ClearGrid(GameManager game)
        {
            foreach (var entry in game.Grid.GetAllPlaced().ToList())
                game.Build.RemoveAtWorldPos(game.Grid.GridToWorld(entry.Root));
            Assert.AreEqual(0, game.Grid.GetAllPlaced().Count(), "The grid was not cleared before loading.");
        }

        /// <summary>Loads <paramref name="data"/> through the game's save/load controller, as at boot.</summary>
        private static void LoadSave(GameManager game, SaveData data)
        {
            var field = typeof(GameManager).GetField("_saveLoad", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "GameManager has no save/load controller.");
            ((SaveLoadController)field.GetValue(game)).LoadGame(data);
        }

        private void StartWatchingLogs()
        {
            LogAssert.ignoreFailingMessages = true;   // checked by AssertNoUnexpectedErrors instead
            Application.logMessageReceived += OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition == BakeLog) _bakes++;
            if (type == LogType.Log || type == LogType.Warning || ToleratedError.IsMatch(condition)) return;
            _errors.Add($"{type}: {condition}");
        }

        private void AssertNoUnexpectedErrors()
        {
            if (_errors.Count > 0) Assert.Fail(string.Join("\n", _errors));
        }
    }
}
