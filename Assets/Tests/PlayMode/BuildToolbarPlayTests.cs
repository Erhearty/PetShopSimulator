using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
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
