using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Progression;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for the furniture supply chain as wired through <see cref="GameManager"/>
    /// and the <see cref="FurnitureCatalogPanel"/> hosted on the Shop book's Build page: ordering charges
    /// and queues, arrival drops a crate, unpacking it fills the inventory, and Place picks an owned
    /// unit up and closes the book.
    /// </summary>
    public class FurnitureCatalogPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const float  PoorBalance = 1f;
        private const float  Tolerance   = 1e-3f;
        /// <summary>Furthest a crate may land from the forecourt centre (the origin with no layout), in metres.</summary>
        private const float  ForecourtReach = 3f;
        private static readonly Vector2Int FloorSize = new(6, 6);
        private const int WallsPlaced = 2;
        private static readonly Vector2Int WallCell  = new(1, 1);
        private static readonly Vector2Int NextWallCell = new(2, 1);

        private FurniturePrefabs      _saved;
        private GameObject            _root, _canvas;
        private GameManager           _game;
        private ShopManager           _shop;
        private BuildMode             _build;
        private FurnitureCatalogPanel _panel;
        private StatsPanel            _stats;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saved = FurnitureFactory.Prefabs;
#if UNITY_EDITOR
            FurnitureFactory.Prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
#endif
            _root = new GameObject("catalogue-play-test");
            var grid = _root.AddComponent<GridManager>();
            _shop = _root.AddComponent<ShopManager>();
            grid.FillFloorRect(Vector2Int.zero, FloorSize);

            _game = new GameObject("GameManager").AddComponent<GameManager>();
            _build = _root.AddComponent<BuildMode>();
            _build.GridManager = grid; _build.Shop = _shop; _build.ObjectRoot = _root.transform;
            _build.Supply = _game.Furniture;
            _game.Grid = grid; _game.Shop = _shop; _game.Build = _build;

            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _stats  = _canvas.AddComponent<StatsPanel>();
            _stats.Build(_canvas.transform, _game, null, _build);
            _panel  = _stats.Catalogue;
            yield return null;   // let Start run before fixing the balance
            _shop.SetBalance(RichBalance);
        }

        [TearDown]
        public void TearDown()
        {
            if (_build != null) _build.ExitBuildMode();
            // Immediate: GameManager is a singleton, and a deferred destroy would make the next
            // test's fresh manager see this one as Instance and destroy itself.
            foreach (var crate in Object.FindObjectsByType<DeliveryCrate>(FindObjectsSortMode.None))
                Object.DestroyImmediate(crate.gameObject);
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_root);
            if (_game != null) Object.DestroyImmediate(_game.gameObject);
            FurnitureFactory.Prefabs = _saved;
        }

        private static DeliveryCrate FindFurnitureCrate()
        {
            foreach (var crate in Object.FindObjectsByType<DeliveryCrate>(FindObjectsSortMode.None))
                if (crate.IsFurniture) return crate;
            return null;
        }

        [Test]
        public void Order_ChargesCostAndAddsPending()
        {
            Assert.IsTrue(_panel.Order(BuildCatalog.Counter));

            Assert.AreEqual(RichBalance - BuildCatalog.Get(BuildCatalog.Counter).Cost, _shop.Balance, Tolerance);
            Assert.AreEqual(1, _game.Furniture.Pending.Count);
            Assert.AreEqual(BuildCatalog.Counter, _game.Furniture.Pending[0].CatalogId);
        }

        [Test]
        public void Order_Unaffordable_IsRefusedWithMessage()
        {
            _shop.SetBalance(PoorBalance);

            Assert.IsFalse(_panel.Order(BuildCatalog.PetPen));
            Assert.AreEqual(PoorBalance, _shop.Balance, Tolerance);
            Assert.AreEqual(0, _game.Furniture.Pending.Count);
            StringAssert.Contains("Not enough money", _panel.Status);
        }

        [UnityTest]
        public IEnumerator Arrival_SpawnsCrate_AndCollectingFillsInventory()
        {
            Assert.IsTrue(_game.OrderFurniture(BuildCatalog.ShelfLarge));
            Assert.IsNull(FindFurnitureCrate(), "no crate before the van arrives");

            _game.Furniture.ArriveAll();
            var crate = FindFurnitureCrate();
            Assert.IsNotNull(crate, "arrival drops a crate");

            _game.CollectDelivery(crate);
            yield return null;

            Assert.AreEqual(1, _game.Furniture.OwnedCount(BuildCatalog.ShelfLarge));
            Assert.AreEqual(0, _game.Furniture.Pending.Count);
            Assert.IsNull(FindFurnitureCrate(), "the crate is cleared away");
        }

        [Test]
        public void Place_EntersPlacementAndClosesPanel()
        {
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");
            _panel.Show();
            Assert.IsFalse(_panel.Place(BuildCatalog.ShelfSmall), "nothing owned yet");
            Assert.IsTrue(_panel.IsOpen);

            _game.Furniture.AddOwned(BuildCatalog.ShelfSmall);
            Assert.IsTrue(_panel.Place(BuildCatalog.ShelfSmall));

            Assert.IsTrue(_build.IsHolding);
            Assert.IsFalse(_panel.IsOpen);
            Assert.IsFalse(_stats.IsOpen, "picking an item closes the ledger");
            Assert.IsFalse(_game.IsModalOpen);
            Assert.AreEqual(0, _game.Furniture.OwnedCount(BuildCatalog.ShelfSmall));
        }

        [Test]
        public void Show_OpensLedgerOnBuildTab_AndOtherTabsHideIt()
        {
            _panel.Show();
            Assert.IsTrue(_stats.IsOpen);
            Assert.IsTrue(_panel.IsOpen);
            Assert.IsTrue(_game.IsModalOpen);

            _stats.ShowTab(0);
            Assert.IsTrue(_stats.IsOpen);
            Assert.IsFalse(_panel.IsOpen, "the Shelves tab hides the catalogue");

            _stats.ShowTab(StatsPanel.BuildTab);
            Assert.IsTrue(_panel.IsOpen);

            _stats.Hide();
            Assert.IsFalse(_panel.IsOpen);
            Assert.IsFalse(_game.IsModalOpen);
        }

        [Test]
        public void ShopBook_HasSixPages_AndOpensOnOverview()
        {
            Assert.AreEqual(6, _stats.TabCount);
            _stats.ShowTab(StatsPanel.GuideTab);

            _stats.Show();
            Assert.AreEqual(StatsPanel.OverviewTab, _stats.CurrentTab);
            Assert.IsTrue(_game.IsModalOpen);
        }

        [Test]
        public void ShopBook_EmptyShop_HasNothingUrgent()
        {
            var todos = _stats.Todos();

            Assert.AreEqual(1, todos.Count);
            Assert.AreEqual(StatsPanel.NothingUrgent, todos[0]);
        }

        [Test]
        public void ShopBook_BillMoreThanBalance_IsAToDo()
        {
            _shop.SetBalance(PoorBalance);
            if (_shop.DailyOutgoings <= PoorBalance) Assert.Ignore("No outgoings in this fixture.");

            var todos = _stats.Todos();

            Assert.LessOrEqual(todos.Count, StatsPanel.MaxTodos);
            StringAssert.Contains("Tonight's bill", todos[0]);
        }

        [Test]
        public void Order_Wall_CreatesNoDeliveryAndChargesOnPlace()
        {
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");
            var wall = BuildCatalog.Get(BuildCatalog.Wall);
            _panel.Show();

            Assert.IsTrue(_panel.Order(BuildCatalog.Wall));
            Assert.AreEqual(0, _game.Furniture.Pending.Count, "walls are not delivered");
            Assert.AreEqual(RichBalance, _shop.Balance, Tolerance, "nothing is charged up front");
            Assert.IsFalse(_panel.IsOpen);
            Assert.IsFalse(_stats.IsOpen);
            Assert.IsTrue(_build.IsActive);
            Assert.IsFalse(_build.IsHolding, "paid placement, not from the inventory");
            Assert.AreSame(wall, _build.CurrentItem);

            Assert.IsNotNull(_build.Place(WallCell, wall, null, 0f, charge: true));
            Assert.IsNotNull(_build.Place(NextWallCell, wall, null, 0f, charge: true));

            Assert.AreEqual(RichBalance - WallsPlaced * wall.Cost, _shop.Balance, Tolerance);
            Assert.IsTrue(_build.IsActive, "still placing walls");
        }

        [Test]
        public void Order_LockedPen_IsRefused()
        {
            string tigerPen = BuildCatalog.PenIdFor(Pet.Species.Tiger);
            Assert.IsFalse(ProgressionRules.IsPenUnlocked(tigerPen, ProgressionRules.CornerShopTier));

            Assert.IsFalse(_panel.Order(tigerPen));

            Assert.AreEqual(0, _game.Furniture.Pending.Count);
            Assert.AreEqual(RichBalance, _shop.Balance, Tolerance);
            StringAssert.Contains(ProgressionRules.TierName(ProgressionRules.TigerTier), _panel.Status);
        }

        [Test]
        public void Order_WithoutShop_SaysThereIsNoShop()
        {
            _game.Shop = null;

            Assert.IsFalse(_panel.Order(BuildCatalog.Counter));
            Assert.AreEqual(0, _game.Furniture.Pending.Count);
            StringAssert.Contains("no shop", _panel.Status);
            StringAssert.DoesNotContain("Not enough money", _panel.Status);
        }

        [Test]
        public void Load_WithCrateOnForecourt_RespawnsTheCrate()
        {
            Assert.IsTrue(_game.OrderFurniture(BuildCatalog.Counter));
            _game.Furniture.ArriveAll();
            Object.DestroyImmediate(FindFurnitureCrate().gameObject);   // a fresh scene has no crates
            var data = new SaveData();
            SaveFurniture.Capture(data, _game.Furniture, _build);

            SaveFurniture.Apply(data, _game.Furniture);

            var crate = FindFurnitureCrate();
            Assert.IsNotNull(crate, "loading respawns the waiting crate");
            Assert.AreEqual(BuildCatalog.Counter, crate.FurnitureOrder.CatalogId);
            var flat = new Vector3(crate.transform.position.x, 0f, crate.transform.position.z);
            Assert.LessOrEqual(flat.magnitude, ForecourtReach, "the crate lands on the forecourt");
        }

        [Test]
        public void Escape_ClosesCatalogueBeforeBuildMode()
        {
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");
            var ui = _canvas.AddComponent<GameUI>();
            ui.WireForTests(_game, _build, _panel);
            _game.Furniture.AddOwned(BuildCatalog.ShelfSmall);
            Assert.IsTrue(_build.EnterPlacement(BuildCatalog.ShelfSmall, null));
            _panel.Show();

            ui.HandleEscape();
            Assert.IsFalse(_panel.IsOpen, "Esc closes the catalogue first");
            Assert.IsTrue(_game.IsBuildModeActive, "build mode survives the first Esc");
            Assert.IsFalse(_game.IsModalOpen);

            ui.HandleEscape();
            Assert.IsFalse(_game.IsBuildModeActive, "the second Esc leaves build mode");
            Assert.IsFalse(_game.IsModalOpen);
        }

        [Test]
        public void BuildKey_TogglesCatalogueClosed()
        {
            Assert.IsFalse(GameBootstrapper.RouteCatalogueInput(_panel, true), "a closed catalogue leaves the key alone");
            _panel.Show();

            Assert.IsTrue(GameBootstrapper.RouteCatalogueInput(_panel, false));
            Assert.IsTrue(_panel.IsOpen, "no key press, still open");

            Assert.IsTrue(GameBootstrapper.RouteCatalogueInput(_panel, true));
            Assert.IsFalse(_panel.IsOpen);
            Assert.IsFalse(_game.IsModalOpen);
        }

        [Test]
        public void ChangePage_ClampsAtFirstAndLastPage()
        {
            _panel.SelectTab(BuildCategory.Decoration);
            int last = _panel.PageCount - 1;
            Assert.Greater(last, 0, "decorations span several pages");

            _panel.ChangePage(-1);
            Assert.AreEqual(0, _panel.Page, "clamped at the first page");

            for (int i = 0; i <= _panel.PageCount; i++) _panel.ChangePage(1);
            Assert.AreEqual(last, _panel.Page, "clamped at the last page");
        }

        [Test]
        public void Open_SelectsTheItemsCategoryTab()
        {
            _panel.Open(BuildCatalog.DecorLeadRail);

            Assert.AreEqual(BuildCategory.Decoration, _panel.Tab);
            Assert.Greater(_panel.PageCount, 1, "13 decorations need more than one page");
            Assert.AreEqual(_panel.PageCount - 1, _panel.Page);
            Assert.IsTrue(_game.IsModalOpen);
        }
    }
}
