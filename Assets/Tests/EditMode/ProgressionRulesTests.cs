using NUnit.Framework;
using PetShop.Pets;
using PetShop.Progression;

namespace PetShop.Tests
{
    /// <summary>Deterministic tests for <see cref="ProgressionRules"/>.</summary>
    public class ProgressionRulesTests
    {
        private const float JustBelow = 0.01f;
        private const float MaxReputation = 100f;

        private LocTestScope _loc;

        [SetUp]
        public void SetUp() => _loc = LocTestScope.Begin();

        [TearDown]
        public void TearDown() => _loc.End();

        [Test]
        public void TierForReputation_BelowFirstThreshold_IsZero()
        {
            Assert.AreEqual(0, ProgressionRules.TierForReputation(0f));
            Assert.AreEqual(0, ProgressionRules.TierForReputation(ProgressionRules.LocalFavouriteReputation - JustBelow));
        }

        [Test]
        public void TierForReputation_AtEachThreshold_ReachesThatTier()
        {
            Assert.AreEqual(1, ProgressionRules.TierForReputation(55f));
            Assert.AreEqual(2, ProgressionRules.TierForReputation(70f));
            Assert.AreEqual(3, ProgressionRules.TierForReputation(85f));
        }

        [Test]
        public void TierForReputation_JustBelowThresholds_StaysOnPreviousTier()
        {
            Assert.AreEqual(0, ProgressionRules.TierForReputation(55f - JustBelow));
            Assert.AreEqual(1, ProgressionRules.TierForReputation(70f - JustBelow));
            Assert.AreEqual(2, ProgressionRules.TierForReputation(85f - JustBelow));
        }

        [Test]
        public void TierForReputation_Maximum_IsMaxTier()
        {
            Assert.AreEqual(ProgressionRules.MaxTier, ProgressionRules.TierForReputation(MaxReputation));
        }

        [Test]
        public void TierName_MatchesTable()
        {
            Assert.AreEqual("Corner Shop",     ProgressionRules.TierName(0));
            Assert.AreEqual("Local Favourite", ProgressionRules.TierName(1));
            Assert.AreEqual("Trusted Name",    ProgressionRules.TierName(2));
            Assert.AreEqual("Town Landmark",   ProgressionRules.TierName(3));
        }

        [Test]
        public void TryAdvance_RaisesAndReportsRise()
        {
            var rules = new ProgressionRules();
            Assert.IsTrue(rules.TryAdvance(70f, out int tier));
            Assert.AreEqual(2, tier);
            Assert.AreEqual(2, rules.HighestTier);
            Assert.IsFalse(rules.TryAdvance(70f, out _));
        }

        [Test]
        public void TryAdvance_NeverLowers()
        {
            var rules = new ProgressionRules(3);
            Assert.IsFalse(rules.TryAdvance(0f, out int tier));
            Assert.AreEqual(3, tier);
            Assert.AreEqual(3, rules.HighestTier);
        }

        [Test]
        public void SetTier_RestoresSavedTier()
        {
            var rules = new ProgressionRules();
            rules.SetTier(2);
            Assert.AreEqual(2, rules.HighestTier);
        }

        [Test]
        public void LotStageForTier_GrowsWithTier()
        {
            Assert.AreEqual(0, ProgressionRules.LotStageForTier(0));
            Assert.AreEqual(1, ProgressionRules.LotStageForTier(1));
            Assert.AreEqual(1, ProgressionRules.LotStageForTier(2));
            Assert.AreEqual(2, ProgressionRules.LotStageForTier(3));
        }

        [Test]
        public void PickableSpecies_Tier0_IsStarterSix()
        {
            CollectionAssert.AreEquivalent(
                new[] { Pet.Species.Dog, Pet.Species.Cat, Pet.Species.Fox,
                        Pet.Species.Chicken, Pet.Species.Penguin, Pet.Species.Deer },
                ProgressionRules.PickableSpecies(0));
        }

        [Test]
        public void PickableSpecies_HorseOnlyFromTier2()
        {
            CollectionAssert.DoesNotContain(ProgressionRules.PickableSpecies(1), Pet.Species.Horse);
            CollectionAssert.Contains(ProgressionRules.PickableSpecies(2), Pet.Species.Horse);
        }

        [Test]
        public void PickableSpecies_TigerOnlyFromTier3()
        {
            CollectionAssert.DoesNotContain(ProgressionRules.PickableSpecies(2), Pet.Species.Tiger);
            CollectionAssert.Contains(ProgressionRules.PickableSpecies(3), Pet.Species.Tiger);
        }

        [Test]
        public void PenVariants_MatchSpeciesNames()
        {
            var species  = ProgressionRules.PickableSpecies(ProgressionRules.MaxTier);
            var variants = ProgressionRules.PenVariantsForTier(ProgressionRules.MaxTier);
            Assert.AreEqual(species.Count, variants.Count);
            for (int i = 0; i < species.Count; i++)
                Assert.AreEqual(species[i].ToString(), variants[i]);
        }
    }
}
