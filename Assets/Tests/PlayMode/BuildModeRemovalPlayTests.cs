using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for the Remove path: packing a stocked shelf back into the furniture inventory (shelf
    /// visuals use Destroy, so this runs in play mode rather than EditMode), and taking away every seeded
    /// room-wall piece of a new game.
    /// </summary>
    public class BuildModeRemovalPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const float  Tolerance   = 1e-3f;
        private const int    StockUnits  = 3;
        private static readonly Vector2Int FloorSize = new(6, 6);
        private static readonly Vector2Int Cell      = new(2, 2);

        private const int   Seed      = 2718;
        private const float TimeScale = 1f;
        private const float DayLength = 900f;
        /// <summary>Metres back from the wall face, away from the floor target, the aim ray starts from.</summary>
        private const float AimStandoff = 2f;
        /// <summary>Cells into the room, past the aimed-at wall, where the shallow aim ray meets the floor.</summary>
        private const int   FloorCellsBeyondWall = 1;
        /// <summary>Metres above the floor the upward (ceiling-aimed) ray starts from.</summary>
        private const float UpwardRayHeight = 1f;
        /// <summary>What <see cref="ShopLayout.BakeNavMesh"/> logs after every bake.</summary>
        private const string BakeLog = "[ShopLayout] NavMesh baked.";
        /// <summary>Editor-only bake noise tolerated while <see cref="LogAssert.ignoreFailingMessages"/> is on.</summary>
        private static readonly Regex ToleratedError = new(@"RuntimeNavMeshBuilder: Source mesh .* does not allow read access");

        private bool _savedIgnore;
        private bool _swappedPrefabs;
        private bool _booted;
        private int  _bakes;
        private int  _removedEvents;
        private readonly List<string> _errors = new();

        [SetUp]
        public void SetUp()
        {
            _savedIgnore   = LogAssert.ignoreFailingMessages;
            _bakes         = 0;
            _removedEvents = 0;
            _errors.Clear();
        }

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private ProductItem      _product;

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            if (_root != null) Object.Destroy(_root);
            if (_product != null) Object.Destroy(_product);
            if (_swappedPrefabs) FurnitureFactory.Prefabs = _saved;
            _swappedPrefabs = false;
            LogAssert.ignoreFailingMessages = _savedIgnore;
            if (_booted) PlaytestHarness.Teardown();
            _booted = false;
        }

        /// <summary>
        /// Every seeded ring piece (walls, window walls, both doorways, corners) is removed through
        /// <see cref="BuildMode.RemoveAtWorldPos"/> at the piece's own position, or refused with a stated reason.
        /// Each removal raises <see cref="BuildMode.OnObjectRemovedVisually"/> once and rebakes the NavMesh once.
        /// </summary>
        [UnityTest]
        public IEnumerator NewGame_EverySeededRingPiece_RemovesOrExplains()
        {
            _booted = true;
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatching(game);

            string message = null;
            game.Build.OnBuildMessage.AddListener(m => message = m);

            var pieces = SeededRingPieces(game);
            Assert.Greater(pieces.Count, 0, "A new game seeded no ring pieces.");
            foreach (var (cell, id, position) in pieces)
            {
                message = null;
                int owned   = game.Furniture.OwnedCount(id);
                int bakes   = _bakes;
                int removes = _removedEvents;
                game.Build.RemoveAtWorldPos(position);
                bool removed = !game.Grid.TryGetObject(cell, out _);
                Assert.IsTrue(removed || !string.IsNullOrEmpty(message),
                              $"Removing {id} at {cell} failed with no reason given.");
                int expected = removed ? 1 : 0;
                Assert.AreEqual(expected, _removedEvents - removes, $"Removed-visually events for {id} at {cell}.");
                Assert.AreEqual(expected, _bakes - bakes, $"NavMesh bakes for removing {id} at {cell}.");
                if (removed)
                    Assert.AreEqual(owned + 1, game.Furniture.OwnedCount(id), $"{id} at {cell} was not returned.");
                yield return null;
            }
            yield return null;
            AssertNoUnexpectedErrors();
        }

        /// <summary>
        /// Aiming at a ring wall's face from outside removes that wall, not the floor cell behind it. The ray is
        /// shallow enough to meet the floor in the next, empty cell into the room, so only the wall's collider
        /// hit can make the wall go.
        /// </summary>
        [UnityTest]
        public IEnumerator RemoveAlongRay_AimedAtWallFace_RemovesThatWall()
        {
            _booted = true;
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            var game = PlaytestHarness.Game;
            StartWatching(game);

            RectInt ring = game.Layout.RoomWallCells();
            var cell = new Vector2Int(ring.xMin, ring.yMin + ring.height / 2);
            Assert.IsTrue(game.Grid.TryGetObject(cell, out var entry) && entry.Instance != null, $"No wall at {cell}.");
            var floorCell = cell + Vector2Int.right * FloorCellsBeyondWall;
            Assert.IsFalse(game.Grid.TryGetObject(floorCell, out _), $"The floor cell {floorCell} behind the wall is not empty.");

            Vector3 target      = entry.Instance.transform.position + Vector3.up * (ShopLayout.WallPieceHeight * 0.5f);
            Vector3 floorTarget = game.Grid.GridToWorld(floorCell);
            floorTarget.y = 0f;
            Vector3 flat = floorTarget - target;
            flat.y = 0f;
            Assert.Greater(flat.magnitude, GridManager.CellSize * 0.5f, "The floor point is not beyond the wall's cell.");
            Assert.AreEqual(floorCell, game.Grid.WorldToGrid(floorTarget), "The floor point is not in the empty cell.");

            Vector3 from = target + (target - floorTarget).normalized * AimStandoff;
            int removes = _removedEvents, bakes = _bakes;

            game.Build.RemoveAlongRay(new Ray(from, (floorTarget - from).normalized));

            Assert.IsFalse(game.Grid.TryGetObject(cell, out _), "Aiming at the wall did not remove it.");
            Assert.AreEqual(1, _removedEvents - removes, "Removing the wall should raise one removed-visually event.");
            Assert.AreEqual(1, _bakes - bakes, "Removing the wall should rebake the NavMesh once.");
            yield return null;
            AssertNoUnexpectedErrors();
        }

        /// <summary>A ray aimed up (at the ceiling, which has no collider) meets nothing and says why.</summary>
        [UnityTest]
        public IEnumerator RemoveAlongRay_AimedUpward_RaisesAimNotice()
        {
            _root = new GameObject("build-remove-aim-test");
            var grid = _root.AddComponent<GridManager>();
            grid.FillFloorRect(Vector2Int.zero, FloorSize);
            var build = _root.AddComponent<BuildMode>();
            build.GridManager = grid; build.ObjectRoot = _root.transform;
            yield return null;

            string message = null;
            build.OnBuildMessage.AddListener(m => message = m);
            Vector3 origin = grid.GridToWorld(Cell) + Vector3.up * UpwardRayHeight;

            build.RemoveAlongRay(new Ray(origin, Vector3.up));

            Assert.AreEqual(BuildMode.AimToRemoveNotice, message);
        }

        /// <summary>Root cell, catalogue id and world position of every building piece on the room's wall ring.</summary>
        private static List<(Vector2Int cell, string id, Vector3 position)> SeededRingPieces(GameManager game)
        {
            var pieces = new List<(Vector2Int, string, Vector3)>();
            foreach (var entry in game.Grid.GetAllPlaced())
            {
                if (entry.Data == null || !BuildCatalog.IsBuildingPiece(entry.Data.Id)) continue;
                if (!game.Layout.IsRoomWallCell(entry.Root)) continue;
                Vector3 position = entry.Instance != null ? entry.Instance.transform.position
                                                          : game.Grid.GridToWorld(entry.Root);
                pieces.Add((entry.Root, entry.Data.Id, position));
            }
            return pieces;
        }

        /// <summary>
        /// Counts removals and NavMesh bakes. Failing log messages are let through only so the editor-only bake
        /// noise does not fail the test; every other error is collected and failed by <see cref="AssertNoUnexpectedErrors"/>.
        /// </summary>
        private void StartWatching(GameManager game)
        {
            game.Build.OnObjectRemovedVisually.AddListener(_ => _removedEvents++);
            LogAssert.ignoreFailingMessages = true;
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

        /// <summary>Removing a stocked shelf returns it to the inventory and its stock to the warehouse.</summary>
        [UnityTest]
        public IEnumerator Remove_StockedShelf_ReturnsShelfAndStock()
        {
            _saved = FurnitureFactory.Prefabs;
            _swappedPrefabs = true;
#if UNITY_EDITOR
            FurnitureFactory.Prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
#endif
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");

            _root = new GameObject("build-play-test");
            var grid = _root.AddComponent<GridManager>();
            var shop = _root.AddComponent<ShopManager>();
            shop.SetBalance(RichBalance);
            grid.FillFloorRect(Vector2Int.zero, FloorSize);

            var supply = new FurnitureSupply();
            var build  = _root.AddComponent<BuildMode>();
            build.GridManager = grid; build.Shop = shop; build.ObjectRoot = _root.transform; build.Supply = supply;

            supply.AddOwned(BuildCatalog.ShelfSmall);
            Assert.IsTrue(build.EnterPlacement(BuildCatalog.ShelfSmall, nameof(ProductCategory.Food)));
            var shelf = build.PlaceHeld(Cell).GetComponent<ShelfUnit>();
            yield return null;

            _product = ProductItem.Create("test_kibble", "Kibble", ProductCategory.Food, 1f, 2f, Color.white);
            Assert.AreEqual(StockUnits, shelf.AddStock(_product, StockUnits));

            build.RemoveAtWorldPos(grid.GridToWorld(Cell));
            yield return null;

            Assert.AreEqual(1, supply.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(StockUnits, shop.Warehouse(ProductCategory.Food));
            Assert.AreEqual(RichBalance, shop.Balance, Tolerance);
            Assert.IsFalse(grid.TryGetObject(Cell, out _));
        }
    }
}
