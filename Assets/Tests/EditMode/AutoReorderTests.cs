using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for auto-reorder decisions and ShopManager.PendingUnits.</summary>
    public class AutoReorderTests
    {
        private const float Balance = 1000f;
        private const float Reserve = 200f;
        private const float Cost    = 100f;

        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static List<(ProductCategory, int)> Run(AutoReorder ar, int onHand, float cost,
                                                        float balance = Balance)
        {
            return ar.Decide(_ => onHand, _ => cost, balance, Reserve).ToList();
        }

        [Test]
        public void DefaultRules_OnePerCategory_Disabled()
        {
            var ar = new AutoReorder();
            Assert.AreEqual(System.Enum.GetValues(typeof(ProductCategory)).Length, ar.Rules.Count);
            foreach (var r in ar.Rules)
            {
                Assert.IsFalse(r.Enabled);
                Assert.AreEqual(AutoReorder.DefaultThreshold, r.Threshold);
                Assert.AreEqual(AutoReorder.DefaultUnits, r.Units);
            }
        }

        [Test]
        public void DisabledRule_NeverOrders()
        {
            Assert.IsEmpty(Run(new AutoReorder(), 0, Cost));
        }

        [Test]
        public void BelowThreshold_OrdersUnits()
        {
            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Food).Enabled = true;
            var orders = Run(ar, AutoReorder.DefaultThreshold - 1, Cost);
            Assert.AreEqual(1, orders.Count);
            Assert.AreEqual((ProductCategory.Food, AutoReorder.DefaultUnits), orders[0]);
        }

        [Test]
        public void AtOrAboveThreshold_DoesNotOrder()
        {
            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Food).Enabled = true;
            Assert.IsEmpty(Run(ar, AutoReorder.DefaultThreshold, Cost));
        }

        [Test]
        public void PendingUnits_CountedInOnHand_NoDoubleOrder()
        {
            _go = new GameObject("ShopManagerTest");
            var shop = _go.AddComponent<ShopManager>();
            shop.SetBalance(Balance);
            Assert.IsNotNull(shop.PlaceOrder(ProductCategory.Food, AutoReorder.DefaultUnits, 1f, 0f));
            Assert.AreEqual(AutoReorder.DefaultUnits, shop.PendingUnits(ProductCategory.Food));
            Assert.AreEqual(0, shop.PendingUnits(ProductCategory.Toy));

            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Food).Enabled = true;
            var orders = ar.Decide(c => shop.Warehouse(c) + shop.PendingUnits(c), _ => Cost,
                                   Balance, Reserve).ToList();
            Assert.IsEmpty(orders);
        }

        [Test]
        public void BreachingReserve_SkippedAndReported()
        {
            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Toy).Enabled = true;
            var orders = Run(ar, 0, Cost, Reserve + Cost - 1f);
            Assert.IsEmpty(orders);
            CollectionAssert.AreEqual(new[] { ProductCategory.Toy }, ar.LastSkipped);
        }

        [Test]
        public void TwoCategories_RespectCombinedBalance()
        {
            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Food).Enabled = true;
            ar.Rule(ProductCategory.Toy).Enabled  = true;
            // Enough for one order above the reserve, not two.
            var orders = Run(ar, 0, Cost, Reserve + Cost * 1.5f);
            Assert.AreEqual(1, orders.Count);
            Assert.AreEqual(ProductCategory.Food, orders[0].Item1);
            CollectionAssert.AreEqual(new[] { ProductCategory.Toy }, ar.LastSkipped);
        }
    }
}
