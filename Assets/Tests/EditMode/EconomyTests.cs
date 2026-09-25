using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for the economy seams: ShopManager rent and demand curves, and the
    /// CustomerAI purchase decision that consumes them.
    /// </summary>
    public class EconomyTests
    {
        private const float Tolerance      = 1e-5f;
        private const float BaseRent       = 45f;
        private const float RentGrowth     = 6f;
        private const float MinDemand      = 0.25f;
        private const float MaxDemand      = 1.6f;
        private const float NormalPrice    = 1f;
        private const float CheapPrice     = 0.6f;
        private const float GreedyPrice    = 1.8f;
        private const float AbsurdlyCheap  = 0.1f;
        private const float AbsurdlyDear   = 10f;
        private const float ShelfChance    = 0.7f;
        private const float ModerateRoll   = 0.6f;
        private const float HighRoll       = 0.8f;

        private static readonly float[] AscendingPrices = { 0.6f, 0.8f, 1.0f, 1.2f, 1.5f, 1.8f };

        private GameObject _go;

        /// <summary>Destroys any ShopManager host created by a test.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [TestCase(1,  45f)]
        [TestCase(2,  51f)]
        [TestCase(10, 99f)]
        public void ComputeRent_GrowsLinearlyFromDayOne(int day, float expected)
        {
            Assert.AreEqual(expected, ShopManager.ComputeRent(BaseRent, RentGrowth, day), Tolerance);
        }

        [Test]
        public void ShopManager_Defaults_ChargeBaseRentOnDayOne()
        {
            _go = new GameObject("ShopManagerTest");
            var shop = _go.AddComponent<ShopManager>();

            Assert.AreEqual(BaseRent, shop.BaseDailyRent, Tolerance);
            Assert.AreEqual(RentGrowth, shop.RentGrowthPerDay, Tolerance);
            Assert.AreEqual(1, shop.Day);
            Assert.AreEqual(BaseRent, shop.DailyRent, Tolerance);
        }

        [Test]
        public void ComputeDemandFactor_NormalPrice_IsOne()
        {
            Assert.AreEqual(1f, ShopManager.ComputeDemandFactor(NormalPrice), Tolerance);
        }

        [Test]
        public void ComputeDemandFactor_StrictlyDecreasesAsPriceRises()
        {
            for (int i = 1; i < AscendingPrices.Length; i++)
            {
                float cheaper = ShopManager.ComputeDemandFactor(AscendingPrices[i - 1]);
                float dearer  = ShopManager.ComputeDemandFactor(AscendingPrices[i]);
                Assert.Greater(cheaper, dearer, $"at multiplier {AscendingPrices[i]}");
            }
        }

        [Test]
        public void ComputeDemandFactor_ClampsAtExtremes()
        {
            Assert.AreEqual(MaxDemand, ShopManager.ComputeDemandFactor(AbsurdlyCheap), Tolerance);
            Assert.AreEqual(MinDemand, ShopManager.ComputeDemandFactor(AbsurdlyDear), Tolerance);
        }

        [Test]
        public void WillBuy_NormalPrice_BuysOnModerateRoll()
        {
            // 0.7 * 1.0 = 0.7 > 0.6
            Assert.IsTrue(CustomerAI.WillBuy(ModerateRoll, ShelfChance, ShopManager.ComputeDemandFactor(NormalPrice)));
        }

        [Test]
        public void WillBuy_GreedyPrice_RefusesModerateRoll()
        {
            // 0.7 * (1/1.8)^1.6 ≈ 0.7 * 0.39 ≈ 0.27 < 0.6
            Assert.IsFalse(CustomerAI.WillBuy(ModerateRoll, ShelfChance, ShopManager.ComputeDemandFactor(GreedyPrice)));
        }

        [Test]
        public void WillBuy_CheapPrice_BuysOnHighRoll()
        {
            // (1/0.6)^1.6 ≈ 2.26, clamped to 1.6; 0.7 * 1.6 = 1.12 > 0.8
            Assert.IsTrue(CustomerAI.WillBuy(HighRoll, ShelfChance, ShopManager.ComputeDemandFactor(CheapPrice)));
        }
    }
}
