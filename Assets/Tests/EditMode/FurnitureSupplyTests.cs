using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FurnitureSupply"/>: ordering charges up front, crates arrive
    /// on time, collecting fills the inventory, placing draws it down, and saves round-trip.
    /// </summary>
    public class FurnitureSupplyTests
    {
        private const float Tolerance = 1e-3f;
        private const float RichBalance = 5000f;
        private const float MorningProgress = 0.1f;
        private const float EndOfDay = 1f;

        private GameObject      _go;
        private ShopManager     _shop;
        private FurnitureSupply _supply;

        [SetUp]
        public void SetUp()
        {
            _go     = new GameObject("shop");
            _shop   = _go.AddComponent<ShopManager>();
            _shop.SetBalance(RichBalance);
            _supply = new FurnitureSupply();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Order_ChargesCatalogPriceAndQueues()
        {
            var order = _supply.Order(BuildCatalog.Counter, _shop, MorningProgress);

            Assert.IsNotNull(order);
            Assert.AreEqual(RichBalance - BuildCatalog.Get(BuildCatalog.Counter).Cost, _shop.Balance, Tolerance);
            Assert.AreEqual(1, _supply.Pending.Count);
            Assert.IsFalse(order.Arrived);
            Assert.GreaterOrEqual(order.ArrivalProgress, MorningProgress + FurnitureSupply.MinDeliveryDelay - Tolerance);
            Assert.LessOrEqual(order.ArrivalProgress, FurnitureSupply.LatestArrival);
        }

        [Test]
        public void Order_Unaffordable_IsRefusedAndFree()
        {
            _shop.SetBalance(1f);

            Assert.IsNull(_supply.Order(BuildCatalog.PetPen, _shop, MorningProgress));
            Assert.AreEqual(1f, _shop.Balance, Tolerance);
            Assert.AreEqual(0, _supply.Pending.Count);
        }

        [Test]
        public void Order_UnknownId_IsRefused()
        {
            Assert.IsNull(_supply.Order("no_such_thing", _shop, MorningProgress));
            Assert.AreEqual(RichBalance, _shop.Balance, Tolerance);
        }

        [Test]
        public void Tick_LandsOnlyOrdersWhoseTimeHasCome()
        {
            var order = _supply.Order(BuildCatalog.ShelfSmall, _shop, MorningProgress);
            FurnitureOrder landed = null;
            _supply.OnArrived += o => landed = o;

            _supply.Tick(MorningProgress);
            Assert.IsNull(landed);

            _supply.Tick(EndOfDay);
            Assert.AreSame(order, landed);
            Assert.IsTrue(order.Arrived);
        }

        [Test]
        public void ArriveAll_LandsEverythingOvernight()
        {
            _supply.Order(BuildCatalog.ShelfSmall, _shop, MorningProgress);
            _supply.Order(BuildCatalog.Counter, _shop, MorningProgress);
            int arrivals = 0;
            _supply.OnArrived += _ => arrivals++;

            _supply.ArriveAll();

            Assert.AreEqual(2, arrivals);
        }

        [Test]
        public void Collect_MovesArrivedCrateIntoInventory()
        {
            var order = _supply.Order(BuildCatalog.ShelfLarge, _shop, MorningProgress);
            Assert.IsFalse(_supply.Collect(order), "an order still on the road cannot be collected");

            _supply.ArriveAll();

            Assert.IsTrue(_supply.Collect(order));
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.ShelfLarge));
            Assert.AreEqual(0, _supply.Pending.Count);
            Assert.IsFalse(_supply.Collect(order), "a crate is collected once");
        }

        [Test]
        public void TakeOwned_DrawsDownAndRefusesWhenEmpty()
        {
            _supply.AddOwned(BuildCatalog.Wall, 2);

            Assert.IsTrue(_supply.TakeOwned(BuildCatalog.Wall));
            Assert.IsTrue(_supply.TakeOwned(BuildCatalog.Wall));
            Assert.IsFalse(_supply.TakeOwned(BuildCatalog.Wall));
            Assert.AreEqual(0, _supply.Owned.Count);
        }

        [Test]
        public void CaptureRestore_RoundTripsInventoryAndOrders()
        {
            _supply.AddOwned(BuildCatalog.Counter, 1);
            _supply.AddOwned(BuildCatalog.Fence, 3);
            var landed = _supply.Order(BuildCatalog.PetPen, _shop, MorningProgress);
            _supply.ArriveAll();
            _supply.Order(BuildCatalog.ShelfSmall, _shop, MorningProgress);

            var data = new SaveData();
            _supply.Capture(data);
            var json     = JsonUtility.ToJson(data);
            var restored = new FurnitureSupply();
            restored.Restore(SaveMigrator.Migrate(json));

            Assert.AreEqual(1, restored.OwnedCount(BuildCatalog.Counter));
            Assert.AreEqual(3, restored.OwnedCount(BuildCatalog.Fence));
            Assert.AreEqual(2, restored.Pending.Count);
            Assert.AreEqual(BuildCatalog.PetPen, restored.Pending[0].CatalogId);
            Assert.IsTrue(restored.Pending[0].Arrived);
            Assert.AreEqual(landed.ArrivalProgress, restored.Pending[0].ArrivalProgress, Tolerance);
            Assert.IsFalse(restored.Pending[1].Arrived);
        }

        [Test]
        public void Restore_SkipsUnknownIds()
        {
            var data = new SaveData();
            data.FurnitureInventory.Add(new SaveData.StockEntry { id = "retired_item", qty = 2 });
            data.PendingFurnitureOrders.Add(new SaveData.FurnitureOrderSave { catalogId = "retired_item" });

            _supply.Restore(data);

            Assert.AreEqual(0, _supply.Owned.Count);
            Assert.AreEqual(0, _supply.Pending.Count);
        }

        [Test]
        public void RespawnArrived_AnnouncesOnlyWaitingCrates()
        {
            _supply.Order(BuildCatalog.Counter, _shop, MorningProgress);
            _supply.ArriveAll();
            _supply.Order(BuildCatalog.Wall, _shop, MorningProgress);
            int announced = 0;
            _supply.OnArrived += _ => announced++;

            _supply.RespawnArrived();

            Assert.AreEqual(1, announced);
        }
    }
}
