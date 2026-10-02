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
    }
}
