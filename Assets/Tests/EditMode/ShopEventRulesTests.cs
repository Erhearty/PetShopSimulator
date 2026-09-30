using NUnit.Framework;
using PetShop.Events;

namespace PetShop.Tests
{
    /// <summary>Deterministic tests for <see cref="ShopEventRules"/>.</summary>
    public class ShopEventRulesTests
    {
        private const float Tolerance = 0.001f;
        private const int   LastDayChecked = 56;
        private static readonly int[] Seeds = { 1, 7, 42, 1234, 98765, -31337 };

        [Test]
        public void Roll_IsDeterministic_ForSameSeedAndDay()
        {
            foreach (int seed in Seeds)
                for (int day = 1; day <= LastDayChecked; day++)
                    Assert.AreEqual(ShopEventRules.Roll(seed, day), ShopEventRules.Roll(seed, day));
        }

        [Test]
        public void Roll_NeverHeatwave_OutsideSummer()
        {
            foreach (int seed in Seeds)
                for (int day = 1; day <= LastDayChecked; day++)
                {
                    if (ShopEventRules.IsSummer(day)) continue;
                    Assert.AreNotEqual(ShopEventKind.Heatwave, ShopEventRules.Roll(seed, day),
                                       $"seed {seed}, day {day}");
                }
        }

        [Test]
        public void None_AllMultipliersAreOne()
        {
            Assert.AreEqual(1f, ShopEventRules.PriceMultiplier(ShopEventKind.None), Tolerance);
            Assert.AreEqual(1f, ShopEventRules.CareDrainMultiplier(ShopEventKind.None), Tolerance);
            Assert.AreEqual(1f, ShopEventRules.IntervalMultiplier(ShopEventKind.None), Tolerance);
            Assert.AreEqual(1f, ShopEventRules.TargetMultiplier(ShopEventKind.None), Tolerance);
        }

        [Test]
        public void EventKinds_ApplyTheirOwnMultiplier()
        {
            Assert.AreEqual(0.75f, ShopEventRules.PriceMultiplier(ShopEventKind.SupplierSale), Tolerance);
            Assert.AreEqual(1.5f, ShopEventRules.CareDrainMultiplier(ShopEventKind.Heatwave), Tolerance);
            Assert.AreEqual(0.6f, ShopEventRules.IntervalMultiplier(ShopEventKind.StreetFestival), Tolerance);
            Assert.AreEqual(1.5f, ShopEventRules.TargetMultiplier(ShopEventKind.StreetFestival), Tolerance);
            Assert.AreEqual(1f, ShopEventRules.PriceMultiplier(ShopEventKind.Heatwave), Tolerance);
        }

        [Test]
        public void SeasonForDay_Boundaries()
        {
            Assert.AreEqual(0, ShopEventRules.SeasonForDay(1));
            Assert.AreEqual(0, ShopEventRules.SeasonForDay(7));
            Assert.AreEqual(1, ShopEventRules.SeasonForDay(8));
            Assert.AreEqual(2, ShopEventRules.SeasonForDay(15));
            Assert.AreEqual(3, ShopEventRules.SeasonForDay(22));
            Assert.AreEqual(0, ShopEventRules.SeasonForDay(29));
            Assert.AreEqual(0, ShopEventRules.SeasonForDay(0));
            Assert.IsTrue(ShopEventRules.IsSummer(8));
            Assert.IsFalse(ShopEventRules.IsSummer(7));
        }

        [Test]
        public void DurationFor_EachKind()
        {
            Assert.AreEqual(0, ShopEventRules.DurationFor(ShopEventKind.None));
            Assert.AreEqual(1, ShopEventRules.DurationFor(ShopEventKind.SupplierSale));
            Assert.AreEqual(2, ShopEventRules.DurationFor(ShopEventKind.Heatwave));
            Assert.AreEqual(1, ShopEventRules.DurationFor(ShopEventKind.StreetFestival));
        }

        [Test]
        public void DisplayNameAndAnnouncement_NotEmptyForEvents()
        {
            foreach (ShopEventKind kind in System.Enum.GetValues(typeof(ShopEventKind)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(ShopEventRules.DisplayName(kind)));
                if (kind != ShopEventKind.None)
                    Assert.IsFalse(string.IsNullOrEmpty(ShopEventRules.Announcement(kind)));
            }
        }
    }
}
