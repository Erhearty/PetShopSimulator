using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for the furniture supply chain as wired through <see cref="GameManager"/>
    /// and the <see cref="FurnitureCatalogPanel"/>: ordering charges and queues, arrival drops a
    /// crate, unpacking it fills the inventory, and Place picks an owned unit up.
    /// </summary>
    public class FurnitureCatalogPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const float  PoorBalance = 1f;
        private const float  Tolerance   = 1e-3f;
        private static readonly Vector2Int FloorSize = new(6, 6);

        private FurniturePrefabs      _saved;
        private GameObject            _root, _canvas;
        private GameManager           _game;
        private ShopManager           _shop;
        private BuildMode             _build;
        private FurnitureCatalogPanel _panel;

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
            _panel  = _canvas.AddComponent<FurnitureCatalogPanel>();
            _panel.Build(_canvas.transform, _game, _build);
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
            Assert.IsFalse(_game.IsModalOpen);
            Assert.AreEqual(0, _game.Furniture.OwnedCount(BuildCatalog.ShelfSmall));
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
