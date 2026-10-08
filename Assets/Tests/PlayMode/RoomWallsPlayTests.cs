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
            Assert.AreEqual(perimeter, CountRoomWalls(game), "A new game should wall every border cell of the room including its four corners.");
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

        /// <summary>
        /// A new game's wall ring is closed at all four corners, still closed after a save/load, and no static
        /// corner fillers exist.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomWallCorners_StartClosed_AndStayClosedAfterSaveLoad()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatchingLogs();
            yield return null;
            AssertCornersClosed(game);
            AssertNoCornerFillers();

            Reload(game, SaveAndRead(game));
            yield return null;
            AssertCornersClosed(game);
            AssertNoCornerFillers();
            AssertNoUnexpectedErrors();
        }

        /// <summary>Every seeded wall-ring cell holds a grid piece, and no static corner filler stands anywhere.</summary>
        [UnityTest]
        public IEnumerator NewGame_EveryRingCellHasAGridPiece()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            foreach (var cell in RingCells(game))
            {
                Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry), $"Ring cell {cell} has no grid piece.");
                Assert.IsTrue(entry.Data != null && BuildCatalog.IsBuildingPiece(entry.Data.Id),
                              $"Ring cell {cell} holds {entry.Data?.Id}, not a building piece.");
                Assert.IsNotNull(entry.Instance, $"Ring cell {cell}'s piece has no spawned object.");
            }
            AssertNoCornerFillers();
        }

        /// <summary>
        /// Neighbouring ring pieces meet: along every run and round every corner the renderer bounds of adjacent
        /// pieces touch within <see cref="JoinTolerance"/>, and no corner piece overhangs the adjacent row's outer face.
        /// </summary>
        [UnityTest]
        public IEnumerator NewGame_AdjacentRingPiecesMeet()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var ring = game.Layout.RoomWallCells();
            var cells = RingCells(game).ToList();
            var set = new System.Collections.Generic.HashSet<Vector2Int>(cells);
            int pairs = 0;

            foreach (var cell in cells)
            foreach (var step in new[] { Vector2Int.right, Vector2Int.up })
            {
                var next = cell + step;
                if (!set.Contains(next)) continue;
                Bounds a = PieceBounds(game, cell), b = PieceBounds(game, next);
                float gap = step.x != 0 ? b.min.x - a.max.x : b.min.z - a.max.z;
                Assert.LessOrEqual(gap, JoinTolerance, $"Pieces at {cell} and {next} leave a {gap:F3} m gap.");
                pairs++;
            }
            Assert.Greater(pairs, 0, "No adjacent ring pieces were compared.");
        }

        /// <summary>
        /// A window wall closes its whole cell at glass height: along the wall's run, its child renderers leave no
        /// gap wider than <see cref="WindowSlitTolerance"/> between the cell's two edges (no see-through side slits).
        /// </summary>
        [UnityTest]
        public IEnumerator NewGame_WindowWallCoversItsCellAtGlassHeight()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var corners = new System.Collections.Generic.HashSet<Vector2Int>(Corners(game.Layout.RoomWallCells()));
            var entry = game.Grid.GetAllPlaced().FirstOrDefault(e =>
                e.Data != null && e.Data.Id == BuildCatalog.WallWindow && e.Instance != null && !corners.Contains(e.Root));
            Assert.IsNotNull(entry, "A new game has no window wall off the ring corners.");

            var renderers = entry.Instance.GetComponentsInChildren<Renderer>();
            var glass = renderers.FirstOrDefault(r => r.name == WindowGlassName);
            Assert.IsNotNull(glass, $"The window wall at {entry.Root} has no {WindowGlassName} renderer.");
            float y = glass.bounds.center.y;
            Vector3 axis   = entry.Instance.transform.right;
            Vector3 centre = game.Grid.GridToWorld(entry.Root);
            float half = GridManager.CellSize * 0.5f;

            var spans = new System.Collections.Generic.List<(float from, float to)>();
            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                if (y < b.min.y || y > b.max.y) continue;
                float a = Vector3.Dot(b.min - centre, axis), c = Vector3.Dot(b.max - centre, axis);
                spans.Add((Mathf.Min(a, c), Mathf.Max(a, c)));
            }
            Assert.Greater(spans.Count, 0, "No renderer of the window wall reaches glass height.");

            float reach = -half;
            foreach (var (from, to) in spans.OrderBy(s => s.from))
            {
                Assert.LessOrEqual(from - reach, WindowSlitTolerance,
                                   $"The window wall at {entry.Root} leaves a {from - reach:F3} m gap at {reach:F3} m along its run.");
                reach = Mathf.Max(reach, to);
            }
            Assert.LessOrEqual(half - reach, WindowSlitTolerance,
                               $"The window wall at {entry.Root} leaves a {half - reach:F3} m gap at its far edge.");
        }

        /// <summary>Name of a window wall's glass child, whose height is where the side slits would show.</summary>
        private const string WindowGlassName = "Glass";
        /// <summary>Widest gap, in metres, a window wall may leave along its cell at glass height.</summary>
        private const float WindowSlitTolerance = 0.02f;

        /// <summary>Metres two adjacent wall pieces' bounds may stand apart and still count as joined.</summary>
        private const float JoinTolerance = 0.02f;
        /// <summary>How far a wall piece's skirting stands proud of its face, in metres.</summary>
        private const float SkirtingOverhang = 0.025f;

        /// <summary>Every seeded cell of the room's wall ring: corners included.</summary>
        private static System.Collections.Generic.IEnumerable<Vector2Int> RingCells(GameManager game)
        {
            RectInt ring = game.Layout.RoomWallCells();
            foreach (var cell in ring.allPositionsWithin)
                if (game.Layout.IsRoomWallCell(cell)) yield return cell;
        }

        private static Vector2Int[] Corners(RectInt ring) => new[]
        {
            new Vector2Int(ring.xMin, ring.yMin), new Vector2Int(ring.xMax - 1, ring.yMin),
            new Vector2Int(ring.xMin, ring.yMax - 1), new Vector2Int(ring.xMax - 1, ring.yMax - 1),
        };

        /// <summary>Combined bounds of the active renderers of the piece on <paramref name="cell"/>.</summary>
        private static Bounds PieceBounds(GameManager game, Vector2Int cell)
        {
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Instance != null, $"No piece at {cell}.");
            var renderers = entry.Instance.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, $"The piece at {cell} has no renderers.");
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void AssertNoCornerFillers()
        {
            int fillers = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                                .Count(t => t.name == ShopLayout.RoomWallCornerName);
            Assert.AreEqual(0, fillers, "Static RoomWallCorner fillers should no longer exist.");
        }

        /// <summary>Asserts a piece stands on, and a collider fills the strip of, each of the ring's four corner cells.</summary>
        private static void AssertCornersClosed(GameManager game)
        {
            foreach (var corner in Corners(game.Layout.RoomWallCells()))
            {
                Assert.IsTrue(game.Grid.TryGetObject(corner, out _), $"No piece stands on corner cell {corner}.");
                Assert.IsTrue(CornerHit(game, corner), $"The wall corner at cell {corner} is open.");
            }
        }

        /// <summary>True when a collider fills the strip between corner cell <paramref name="corner"/>'s centre and its room-side X edge.</summary>
        private static bool CornerHit(GameManager game, Vector2Int corner)
        {
            Physics.SyncTransforms();
            RectInt ring = game.Layout.RoomWallCells();
            float cs = GridManager.CellSize;
            Vector3 centre = game.Grid.GridToWorld(corner);
            float inward = corner.x == ring.xMin ? 1f : -1f;
            var gap = new Vector3(centre.x + inward * (0.12f + (cs * 0.5f - 0.12f) * 0.5f), 1f, centre.z);
            return Physics.CheckBox(gap, new Vector3(0.3f, 0.5f, 0.05f), Quaternion.identity,
                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
