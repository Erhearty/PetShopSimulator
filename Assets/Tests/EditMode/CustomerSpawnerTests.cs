using NUnit.Framework;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>Checks the furniture gate that keeps customers out of a shop that cannot trade.</summary>
    public class CustomerSpawnerTests
    {
        private const int None = 0;
        private const int One  = 1;
        private const int Many = 3;

        /// <summary>An empty shop never trades.</summary>
        [Test]
        public void CanTrade_EmptyShop_IsFalse() =>
            Assert.IsFalse(CustomerSpawner.CanTrade(false, None, None));

        /// <summary>Shelves and pens without a counter leave nowhere to pay.</summary>
        [Test]
        public void CanTrade_NoCounter_IsFalse() =>
            Assert.IsFalse(CustomerSpawner.CanTrade(false, Many, Many));

        /// <summary>A counter alone leaves nothing to sell.</summary>
        [Test]
        public void CanTrade_CounterOnly_IsFalse() =>
            Assert.IsFalse(CustomerSpawner.CanTrade(true, None, None));

        /// <summary>A counter plus one shelf opens the shop.</summary>
        [Test]
        public void CanTrade_CounterAndShelf_IsTrue() =>
            Assert.IsTrue(CustomerSpawner.CanTrade(true, One, None));

        /// <summary>A counter plus one pen opens the shop.</summary>
        [Test]
        public void CanTrade_CounterAndPen_IsTrue() =>
            Assert.IsTrue(CustomerSpawner.CanTrade(true, None, One));

        /// <summary>A fully furnished shop trades.</summary>
        [Test]
        public void CanTrade_CounterShelvesAndPens_IsTrue() =>
            Assert.IsTrue(CustomerSpawner.CanTrade(true, Many, Many));

        private const int MinPerDay = 3;
        private const int MaxPerDay = 18;

        /// <summary>Reputation lerps the target between the day's minimum and maximum.</summary>
        [TestCase(0f,   3)]
        [TestCase(40f,  9)]
        [TestCase(100f, 18)]
        public void TargetFor_NoEvent_FollowsReputation(float reputation, int expected) =>
            Assert.AreEqual(expected, CustomerSpawner.TargetFor(reputation, 1f, 1f, MinPerDay, MaxPerDay));

        /// <summary>The event target multiplier scales the target.</summary>
        [Test]
        public void TargetFor_TargetMultiplier_Scales() =>
            Assert.AreEqual(36, CustomerSpawner.TargetFor(100f, 2f, 1f, MinPerDay, MaxPerDay));

        /// <summary>A shorter interval (busier) is inverted into more customers: 0.6 => 18 / 0.6 = 30.</summary>
        [Test]
        public void TargetFor_BusierInterval_RaisesTheTarget() =>
            Assert.AreEqual(30, CustomerSpawner.TargetFor(100f, 1f, 0.6f, MinPerDay, MaxPerDay));

        /// <summary>A quieter interval lowers the target, but never the hourly floor of guaranteed buyers.</summary>
        [Test]
        public void TargetFor_QuieterInterval_NeverLowersTheFloor()
        {
            int target = CustomerSpawner.TargetFor(0f, 1f, 4f, MinPerDay, MaxPerDay);
            Assert.Less(target, MinPerDay);
            Assert.AreEqual(ArrivalSchedule.MinTotal, ArrivalSchedule.TotalFor(target));
            foreach (int count in ArrivalSchedule.CountsPerHour(target))
                Assert.AreEqual(ArrivalSchedule.MinQueueJoinsPerHour, ArrivalSchedule.GuaranteedIn(count));
        }
    }
}
