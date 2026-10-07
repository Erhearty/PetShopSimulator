using NUnit.Framework;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>
    /// Regression for the soak's "ran out of time walking to forecourt": a shopper spawned at the far end
    /// of the pavement has a ~52 m walk to the forecourt, which the old flat 20 s limit cut off.
    /// </summary>
    public class CustomerWalkTimeoutTests
    {
        /// <summary>Customer walking speed (CustomerAI.MoveSpeed default), m/s.</summary>
        private const float WalkSpeed = 2.4f;

        /// <summary>
        /// Observed in Logs/build/soak.log (seed 1, 2026-10-05): from (28.80, 21.00) along the pavement
        /// to the forecourt at (-22.88, 18.50), corners (-22.40, 19.68) and (-22.88, 19.20) - about 52.4 m.
        /// </summary>
        private const float ObservedForecourtWalkMetres = 52.4f;

        /// <summary>Expected limit for the observed walk: 52.4 m / 2.4 m/s × 1.5 slack.</summary>
        private const float ExpectedForecourtLimitSeconds = 32.75f;

        /// <summary>Tolerance for float comparisons of the limit, seconds.</summary>
        private const float LimitToleranceSeconds = 0.01f;

        /// <summary>A short in-shop walk, well inside the minimum limit.</summary>
        private const float ShortWalkMetres = 5f;

        [Test]
        public void FarPavementSpawn_GetsLongerThanTheIdealWalkTime()
        {
            float ideal = ObservedForecourtWalkMetres / WalkSpeed;
            Assert.Greater(ideal, CustomerAI.MinWalkTimeoutSeconds, "the observed walk outlasts the old flat limit");
            Assert.Greater(CustomerAI.WalkTimeout(ObservedForecourtWalkMetres, WalkSpeed), ideal);
            Assert.AreEqual(ExpectedForecourtLimitSeconds,
                            CustomerAI.WalkTimeout(ObservedForecourtWalkMetres, WalkSpeed), LimitToleranceSeconds);
        }

        [Test]
        public void ShortWalk_KeepsTheMinimumLimit()
        {
            Assert.AreEqual(CustomerAI.MinWalkTimeoutSeconds, CustomerAI.WalkTimeout(ShortWalkMetres, WalkSpeed));
        }

        [TestCase(float.PositiveInfinity, WalkSpeed)]
        [TestCase(float.NaN, WalkSpeed)]
        [TestCase(ShortWalkMetres, 0f)]
        public void UnknownLengthOrNoSpeed_GetsTheMaximum(float metres, float speed)
        {
            Assert.AreEqual(CustomerAI.MaxWalkTimeoutSeconds, CustomerAI.WalkTimeout(metres, speed));
        }

        [Test]
        public void VeryLongWalk_IsCappedAtTheMaximum()
        {
            float huge = CustomerAI.MaxWalkTimeoutSeconds * WalkSpeed * CustomerAI.WalkTimeoutSlack;
            Assert.AreEqual(CustomerAI.MaxWalkTimeoutSeconds, CustomerAI.WalkTimeout(huge, WalkSpeed));
        }
    }
}
