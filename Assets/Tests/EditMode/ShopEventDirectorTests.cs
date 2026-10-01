using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Events;

namespace PetShop.Tests
{
    /// <summary>Save round-trip and countdown tests for <see cref="ShopEventDirector"/>.</summary>
    public class ShopEventDirectorTests
    {
        private const float Tolerance = 0.001f;

        private GameObject        _go;
        private ShopEventDirector _director;
        private ShopManager       _shop;

        [SetUp]
        public void SetUp()
        {
            _go       = new GameObject("EventDirectorTest");
            _shop     = _go.AddComponent<ShopManager>();
            _director = _go.AddComponent<ShopEventDirector>();
            _director.Init(null, _shop, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void RestoreThenCapture_RoundTrips()
        {
            var saved = new SaveData { EventSeed = 12345, ActiveEventId = "Heatwave", EventDaysLeft = 2 };
            _director.Restore(saved);

            var captured = new SaveData();
            _director.Capture(captured);

            Assert.AreEqual(12345, captured.EventSeed);
            Assert.AreEqual("Heatwave", captured.ActiveEventId);
            Assert.AreEqual(2, captured.EventDaysLeft);
            Assert.AreEqual(1.5f, _director.CareDrainMultiplier, Tolerance);
        }

        [Test]
        public void Restore_UnknownOrNullId_IsNone()
        {
            _director.Restore(new SaveData { EventSeed = 5, ActiveEventId = "Blizzard", EventDaysLeft = 3 });
            Assert.AreEqual(ShopEventKind.None, _director.Active);

            _director.Restore(new SaveData { EventSeed = 5, ActiveEventId = null, EventDaysLeft = 3 });
            Assert.AreEqual(ShopEventKind.None, _director.Active);
        }

        [Test]
        public void Restore_ZeroSeed_GetsNonzeroSeed()
        {
            _director.Restore(new SaveData { EventSeed = 0 });
            Assert.AreNotEqual(0, _director.Seed);
        }

        [Test]
        public void Restore_ActiveWithNoDaysLeft_Clears()
        {
            _director.Restore(new SaveData { EventSeed = 9, ActiveEventId = "SupplierSale", EventDaysLeft = 0 });
            Assert.AreEqual(ShopEventKind.None, _director.Active);
            Assert.AreEqual(1f, _shop.SupplierPriceMultiplier, Tolerance);
        }

        [Test]
        public void EndDay_CountsDownAndClears_ResettingShopMultiplier()
        {
            _director.Restore(new SaveData { EventSeed = 9, ActiveEventId = "SupplierSale", EventDaysLeft = 2 });
            Assert.AreEqual(0.75f, _shop.SupplierPriceMultiplier, Tolerance);

            var first = new DaySummary();
            _director.EndDay(first);
            Assert.AreEqual(ShopEventKind.SupplierSale, _director.Active);
            Assert.AreEqual(1, _director.DaysLeft);
            Assert.AreEqual(1, first.Headlines.Count);

            _director.EndDay(new DaySummary());
            Assert.AreEqual(ShopEventKind.None, _director.Active);
            Assert.AreEqual(0, _director.DaysLeft);
            Assert.AreEqual(1f, _shop.SupplierPriceMultiplier, Tolerance);

            Assert.DoesNotThrow(() => _director.EndDay(null));
        }
    }
}
