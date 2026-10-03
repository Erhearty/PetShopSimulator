using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="DeliveryCrate"/>: a stock pallet still goes into the
    /// stockroom, a furniture crate is not emptied by the stock path, and a stale furniture crate
    /// (its order no longer pending) is cleared away without adding anything.
    /// </summary>
    public class DeliveryCratePlayTests
    {
        private const ProductCategory Category = ProductCategory.Food;
        private const int             Units    = 12;

        private GameObject  _root;
        private ShopManager _shop;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("delivery-crate-test");
            _shop = _root.AddComponent<ShopManager>();
            yield return null;   // let Start run before reading the stockroom
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var crate in Object.FindObjectsByType<DeliveryCrate>(FindObjectsSortMode.None))
                Object.DestroyImmediate(crate.gameObject);
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator StockPallet_Collect_AddsUnitsToStockroom()
        {
            int before = _shop.Warehouse(Category);
            var crate  = DeliveryCrate.Spawn(Vector3.zero, Category, Units);
            Assert.IsFalse(crate.IsFurniture);

            Assert.AreEqual(Units, crate.Collect(_shop));
            yield return null;

            Assert.AreEqual(before + Units, _shop.Warehouse(Category));
            Assert.IsTrue(crate == null, "the pallet is cleared away");
        }

        [UnityTest]
        public IEnumerator FurnitureCrate_StockCollect_ReturnsZeroAndLeavesIt()
        {
            int before = _shop.Warehouse(Category);
            var order  = new FurnitureOrder { CatalogId = BuildCatalog.Counter, Arrived = true };
            var crate  = DeliveryCrate.SpawnFurniture(Vector3.zero, order);

            Assert.AreEqual(0, crate.Collect(_shop));
            yield return null;

            Assert.AreEqual(before, _shop.Warehouse(Category));
            Assert.IsTrue(crate != null, "a furniture crate is left alone");
        }

        [UnityTest]
        public IEnumerator StaleFurnitureCrate_IsClearedWithoutAddingItems()
        {
            var supply = new FurnitureSupply();
            var order  = new FurnitureOrder { CatalogId = BuildCatalog.Counter, Arrived = true };
            var crate  = DeliveryCrate.SpawnFurniture(Vector3.zero, order);   // order not in supply.Pending

            Assert.IsFalse(crate.CollectFurniture(supply));
            yield return null;

            Assert.IsTrue(crate == null, "the stale crate is cleared away");
            Assert.AreEqual(0, supply.OwnedCount(BuildCatalog.Counter));
            Assert.AreEqual(0, supply.Pending.Count);
        }
    }
}
