using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Progression;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// The build view's tool strip in the real world: pick Wall, place it on a free cell (grid entry
    /// and a NavMesh rebake), take it away again with the Remove tool (rebake, piece back in the
    /// furniture inventory), and RMB no longer drops the tool while the overhead view shows.
    /// </summary>
    public class BuildToolbarPlayTests
    {
        private const int    Seed        = 2024;
        private const float  TimeScale   = 1f;
        private const float  DayLength   = 900f;
        private const float  RichBalance = 5000f;
        private const float  Tolerance   = 1e-3f;
        /// <summary>What <see cref="ShopLayout.BakeNavMesh"/> logs after every bake.</summary>
        private const string BakeLog     = "[ShopLayout] NavMesh baked.";
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

        /// <summary>Wall tool places with a rebake; Remove tool takes it away with a rebake and returns it.</summary>
        [UnityTest]
        public IEnumerator BuildView_WallThenRemove_PlacesRebakesAndReturnsPiece()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            var ui   = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui?.Toolbar, "GameUI built no build toolbar.");
            game.Shop.SetBalance(RichBalance);
            ui.BuildView.Enter();
            yield return null;
            Assert.IsTrue(ui.Toolbar.IsVisible, "The toolbar should show in the build view.");

            Assert.IsTrue(ui.Toolbar.SelectPiece(BuildCatalog.Wall));
            var build = game.Build;
            Assert.IsTrue(build.IsActive);
            Assert.AreEqual(BuildCatalog.Wall, build.CurrentItem.Id);
            Assert.IsFalse(build.RightClickCancels, "RMB must not drop the wall tool in the build view.");

            StartWatchingLogs();
            var cell = FreeRoomCell(game);
            var wall = BuildCatalog.Get(BuildCatalog.Wall);
            Assert.IsNotNull(build.Place(cell, wall, null, 0f, charge: true));
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Data.Id == BuildCatalog.Wall);
            Assert.AreEqual(RichBalance - wall.Cost, game.Shop.Balance, Tolerance);
            Assert.AreEqual(1, _bakes, "Placing a wall should rebake the NavMesh.");

            ui.Toolbar.SelectRemove();
            Assert.IsTrue(build.IsActive && build.IsRemoving);
            Assert.IsNull(build.CurrentItem);
            int owned = game.Furniture.OwnedCount(BuildCatalog.Wall);
            build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "The Remove tool left the wall on the grid.");
            Assert.AreEqual(2, _bakes, "Removing a wall should rebake the NavMesh.");
            Assert.AreEqual(owned + 1, game.Furniture.OwnedCount(BuildCatalog.Wall), "The wall was not returned.");
            AssertNoUnexpectedErrors();

            ui.HandleEscape();
            Assert.IsFalse(build.IsActive, "Esc should cancel the Remove tool first.");
            Assert.IsTrue(ui.BuildView.IsActive, "Esc should not leave the view while a tool was active.");
            ui.BuildView.Exit();
            Assert.IsTrue(build.RightClickCancels, "RMB should still cancel in first person.");
        }

        /// <summary>Walls owned up front for the yard-building test: one per target cell.</summary>
        private const int YardWalls = 3;
        /// <summary>Ring row offset of the side wall the yard test lifts and puts back (clear of corners and doors).</summary>
        private const int RingSideOffset = 2;

        /// <summary>
        /// At the starter stage a wall goes on a yard cell behind the room, one beside it (both off the unlocked
        /// lot, so they get floor laid) and on a free wall-ring cell; each placement and removal rebakes the
        /// NavMesh, and removing a yard wall frees the cell and takes back the floor it laid.
        /// </summary>
        [UnityTest]
        public IEnumerator Wall_BuildsAnywhereInYard_AndReleasesFloorOnRemove()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game  = PlaytestHarness.Game;
            var build = game.Build;
            var wall  = BuildCatalog.Get(BuildCatalog.Wall);
            RectInt ring = game.Layout.RoomWallCells();
            game.Furniture.AddOwned(BuildCatalog.Wall, YardWalls);
            StartWatchingLogs();

            var behind = FreeYardCell(game, c => c.y < ring.yMin);
            var beside = FreeYardCell(game, c => (c.x < ring.xMin || c.x >= ring.xMax) && c.y >= ring.yMin && c.y < ring.yMax);
            foreach (var cell in new[] { behind, beside })
            {
                Assert.IsTrue(build.CanPlaceItem(wall, cell, wall.Size), $"The ghost should be valid at yard cell {cell}.");
                PlaceHeldWall(game, cell);

                int bakes = _bakes;
                build.RemoveAtWorldPos(game.Grid.GridToWorld(cell));
                Assert.IsFalse(game.Grid.TryGetObject(cell, out _), $"Removing the yard wall at {cell} left it on the grid.");
                Assert.IsFalse(game.Grid.HasFloor(cell), $"Removing the yard wall at {cell} kept the floor it laid.");
                Assert.Greater(_bakes, bakes, "Removing a yard wall should rebake the NavMesh.");
            }

            var ringCell = new Vector2Int(ring.xMin, ring.yMin + RingSideOffset);
            build.RemoveAtWorldPos(game.Grid.GridToWorld(ringCell));
            Assert.IsFalse(game.Grid.TryGetObject(ringCell, out _), "The side ring wall was not lifted.");
            Assert.IsTrue(build.CanPlaceItem(wall, ringCell, wall.Size), "A wall should fit back on a free ring cell.");
            PlaceHeldWall(game, ringCell);
            AssertNoUnexpectedErrors();
        }

        /// <summary>Takes a wall into the hand and places it at <paramref name="cell"/>, asserting the grid entry and a rebake.</summary>
        private void PlaceHeldWall(GameManager game, Vector2Int cell)
        {
            int bakes = _bakes;
            Assert.IsTrue(game.Build.EnterPlacement(BuildCatalog.Wall), "No wall to take into the hand.");
            Assert.IsNotNull(game.Build.PlaceHeld(cell), $"The wall was not placed at {cell}.");
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Data.Id == BuildCatalog.Wall,
                          $"No wall on the grid at {cell}.");
            Assert.Greater(_bakes, bakes, "Placing a wall should rebake the NavMesh.");
        }

        /// <summary>
        /// A free cell of the full yard matching <paramref name="where"/> that has no floor yet (outside the
        /// starter lot) and is clear of the doorways.
        /// </summary>
        private static Vector2Int FreeYardCell(GameManager game, System.Func<Vector2Int, bool> where)
        {
            RectInt yard = game.Layout.LotStageCells(ProgressionRules.FullYardLotStage);
            foreach (var cell in yard.allPositionsWithin)
                if (where(cell) && !game.Grid.HasFloor(cell) && !game.Grid.TryGetObject(cell, out _) &&
                    !game.Layout.IsDoorwayCell(cell) && !game.Layout.IsRoomWallCell(cell)) return cell;
            Assert.Fail("No free unfloored yard cell found.");
            return default;
        }

        /// <summary>The selected tool's accent button gets the dark on-accent label; the others keep ink.</summary>
        [UnityTest]
        public IEnumerator SelectedTool_LabelUsesOnAccent()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var ui = Object.FindAnyObjectByType<GameUI>();
            PlaytestHarness.Game.Shop.SetBalance(RichBalance);
            ui.BuildView.Enter();
            yield return null;

            Assert.IsTrue(ui.Toolbar.SelectPiece(BuildCatalog.Wall));
            yield return null;   // the strip relights its buttons in Update

            Assert.AreEqual(UIFactory.OnAccent, ToolLabel(ui, BuildCatalog.Wall).color, "selected tool label");
            Assert.AreEqual(UIFactory.Ink, ToolLabel(ui, BuildCatalog.Fence).color, "unselected tool label");
            ui.HandleEscape();
            ui.BuildView.Exit();
        }

        /// <summary>The label of the strip button for <paramref name="id"/>.</summary>
        private static TMPro.TMP_Text ToolLabel(GameUI ui, string id)
        {
            foreach (var button in ui.CanvasRoot.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name == $"Tool_{id}") return button.GetComponentInChildren<TMPro.TMP_Text>();
            Assert.Fail($"No toolbar button for {id}.");
            return null;
        }

        /// <summary>A free, buildable cell inside the shop room.</summary>
        private static Vector2Int FreeRoomCell(GameManager game)
        {
            RectInt room = game.Layout.RoomCells();
            foreach (var cell in room.allPositionsWithin)
                if (game.Grid.CanPlace(cell, Vector2Int.one)) return cell;
            Assert.Fail("No free cell in the shop room.");
            return default;
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
