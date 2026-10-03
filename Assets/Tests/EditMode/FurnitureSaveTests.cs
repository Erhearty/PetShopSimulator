using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SaveFurniture"/>: the furniture inventory and pending orders
    /// round-trip through a save, a unit held in build mode is not lost by saving, and crates
    /// waiting on the forecourt are re-announced (respawned) on load.
    /// </summary>
    public class FurnitureSaveTests
    {
        private const string CatalogPath     = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance     = 5000f;
        private const float  MorningProgress = 0.1f;
        private static readonly Vector2Int FloorSize = new(6, 6);

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private ShopManager      _shop;
        private BuildMode        _build;
        private FurnitureSupply  _supply;

        [SetUp]
        public void SetUp()
        {
            _saved = FurnitureFactory.Prefabs;
            FurnitureFactory.Prefabs = AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);

            _root  = new GameObject("furniture-save-test");
            var grid = _root.AddComponent<GridManager>();
            _shop  = _root.AddComponent<ShopManager>();
            _shop.SetBalance(RichBalance);
            grid.FillFloorRect(Vector2Int.zero, FloorSize);

            _supply = new FurnitureSupply();
            _build  = new GameObject("build").AddComponent<BuildMode>();
            _build.GridManager = grid;
            _build.Shop        = _shop;
            _build.ObjectRoot  = _root.transform;
            _build.Supply      = _supply;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_build.gameObject);
            Object.DestroyImmediate(_root);
            FurnitureFactory.Prefabs = _saved;
        }

        /// <summary>Captures into a SaveData, round-trips it through JSON and restores a fresh supply.</summary>
        private FurnitureSupply RoundTrip(out int announced)
        {
            var data = new SaveData();
            SaveFurniture.Capture(data, _supply, _build);
            var restored = new FurnitureSupply();
            int count = 0;
            restored.OnArrived += _ => count++;
            SaveFurniture.Apply(SaveMigrator.Migrate(JsonUtility.ToJson(data)), restored);
            announced = count;
            return restored;
        }

        [Test]
        public void SaveLoad_RoundTripsInventoryAndPendingOrders()
        {
            _supply.AddOwned(BuildCatalog.ShelfSmall, 2);
            _supply.Order(BuildCatalog.Counter, _shop, MorningProgress);
            _supply.ArriveAll();
            _supply.Order(BuildCatalog.DecorBench, _shop, MorningProgress);

            var restored = RoundTrip(out int announced);

            Assert.AreEqual(2, restored.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(2, restored.Pending.Count);
            Assert.AreEqual(BuildCatalog.Counter, restored.Pending[0].CatalogId);
            Assert.IsTrue(restored.Pending[0].Arrived, "crate on the forecourt stays arrived");
            Assert.IsFalse(restored.Pending[1].Arrived, "order still on the road");
            Assert.AreEqual(1, announced, "only the waiting crate is respawned");
        }

        [Test]
        public void Save_WhileHolding_KeepsTheHeldUnit()
        {
            _supply.AddOwned(BuildCatalog.ShelfSmall, 1);
            Assert.IsTrue(_build.EnterPlacement(BuildCatalog.ShelfSmall, null));
            Assert.AreEqual(0, _supply.OwnedCount(BuildCatalog.ShelfSmall), "unit is in hand");

            var restored = RoundTrip(out _);

            Assert.IsFalse(_build.IsHolding, "saving hands the held item back");
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(1, restored.OwnedCount(BuildCatalog.ShelfSmall));
        }

        [Test]
        public void Capture_WithNullInputs_DoesNothing()
        {
            Assert.DoesNotThrow(() => SaveFurniture.Capture(null, _supply, _build));
            Assert.DoesNotThrow(() => SaveFurniture.Apply(null, _supply));
            Assert.DoesNotThrow(() => SaveFurniture.Capture(new SaveData(), null, null));
        }
    }
}
