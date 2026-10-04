using NUnit.Framework;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>Checks the minimum real-time gap between customers scales with the day length.</summary>
    public class CustomerCadenceTests
    {
        private const float DefaultDayLength = 540f;
        private const float HalfDayLength    = 270f;
        private const float DefaultGap       = 30f;
        private const float HalfGap          = 15f;
        private const float Tolerance        = 0.0001f;

        /// <summary>A 540 s day maps 30 game minutes to 30 real seconds.</summary>
        [Test]
        public void MinGapSeconds_DefaultDay_IsThirtySeconds() =>
            Assert.AreEqual(DefaultGap, CustomerSpawner.MinGapSeconds(DefaultDayLength), Tolerance);

        /// <summary>Halving the day halves the gap.</summary>
        [Test]
        public void MinGapSeconds_HalfDay_IsFifteenSeconds() =>
            Assert.AreEqual(HalfGap, CustomerSpawner.MinGapSeconds(HalfDayLength), Tolerance);
    }
}
