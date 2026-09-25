using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for BreedingSystem: the pure Inherit / InheritRarity seams and the
    /// Breed prerequisites and offspring invariants.
    /// </summary>
    public class BreedingSystemTests
    {
        private const int   Seed      = 12345;
        private const float Tolerance = 1e-5f;
        private const float LowTrait  = 0.2f;
        private const float HighTrait = 0.8f;
        private const float MidTrait  = 0.5f;
        private const float NoUpgradeRoll = 0.99f;
        private const float Offset    = 0.5f;
        private const int   MatureA   = 6;
        private const int   MatureB   = 9;
        private const float PriceA    = 80f;
        private const float PriceB    = 120f;

        private Pet _a, _b, _baby;

        /// <summary>Seeds UnityEngine.Random so Breed runs are reproducible.</summary>
        [SetUp]
        public void SetUp() => Random.InitState(Seed);

        /// <summary>Destroys the parents and any offspring.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_a != null)    Object.DestroyImmediate(_a);
            if (_b != null)    Object.DestroyImmediate(_b);
            if (_baby != null) Object.DestroyImmediate(_baby);
        }

        [TestCase(0f,   LowTrait)]
        [TestCase(0.5f, MidTrait)]
        [TestCase(1f,   HighTrait)]
        public void Inherit_NoMutation_LerpsBetweenParents(float t, float expected)
        {
            Assert.AreEqual(expected, BreedingSystem.Inherit(LowTrait, HighTrait, t, null), Tolerance);
        }

        [Test]
        public void Inherit_MutationAboveOne_ClampsToOne()
        {
            Assert.AreEqual(1f, BreedingSystem.Inherit(0.9f, 0.9f, 0f, Offset), Tolerance);
        }

        [Test]
        public void Inherit_MutationBelowZero_ClampsToZero()
        {
            Assert.AreEqual(0f, BreedingSystem.Inherit(0.1f, 0.1f, 0f, -Offset), Tolerance);
        }

        [Test]
        public void InheritRarity_HighRoll_TakesLowerParent()
        {
            Assert.AreEqual(Pet.Rarity.Common,
                BreedingSystem.InheritRarity(Pet.Rarity.Rare, Pet.Rarity.Common, NoUpgradeRoll));
        }

        [Test]
        public void InheritRarity_ZeroRoll_UpgradesOneTier()
        {
            Assert.AreEqual(Pet.Rarity.Rare,
                BreedingSystem.InheritRarity(Pet.Rarity.Rare, Pet.Rarity.Uncommon, 0f));
        }

        [Test]
        public void InheritRarity_RollAtThreshold_DoesNotUpgrade()
        {
            Assert.AreEqual(Pet.Rarity.Common, BreedingSystem.InheritRarity(
                Pet.Rarity.Common, Pet.Rarity.Common, BreedingSystem.RarityUpgradeChance));
        }

        [Test]
        public void InheritRarity_Legendary_NeverExceedsLegendary()
        {
            Assert.AreEqual(Pet.Rarity.Legendary,
                BreedingSystem.InheritRarity(Pet.Rarity.Legendary, Pet.Rarity.Legendary, 0f));
        }

        /// <summary>Creates a test pet with the given species, stage, maturity, price and rarity.</summary>
        private static Pet MakePet(Pet.Species species, Pet.GrowthStage stage, int mature,
                                   float price, Pet.Rarity rarity)
        {
            var p = ScriptableObject.CreateInstance<Pet>();
            p.species = species; p.growthStage = stage; p.daysToMature = mature;
            p.basePrice = price; p.rarity = rarity;
            p.temperament = LowTrait; p.energyLevel = HighTrait; p.friendliness = MidTrait;
            return p;
        }

        [Test]
        public void Breed_TwoAdultsSameSpecies_ProducesValidBaby()
        {
            _a = MakePet(Pet.Species.Cat, Pet.GrowthStage.Adult, MatureA, PriceA, Pet.Rarity.Uncommon);
            _b = MakePet(Pet.Species.Cat, Pet.GrowthStage.Adult, MatureB, PriceB, Pet.Rarity.Rare);
            _baby = BreedingSystem.Breed(_a, _b);

            Assert.IsNotNull(_baby);
            Assert.AreEqual(Pet.GrowthStage.Baby, _baby.growthStage);
            Assert.AreEqual(Pet.Species.Cat, _baby.species);
            Assert.AreEqual(0, _baby.ageDays);
            Assert.AreEqual((PriceA + PriceB) * 0.5f, _baby.basePrice, Tolerance);
            Assert.AreEqual(Mathf.RoundToInt((MatureA + MatureB) / 2f), _baby.daysToMature);
            AssertUnit(_baby.temperament);
            AssertUnit(_baby.energyLevel);
            AssertUnit(_baby.friendliness);
            Assert.GreaterOrEqual((int)_baby.rarity, (int)Pet.Rarity.Uncommon);
            Assert.LessOrEqual((int)_baby.rarity, (int)Pet.Rarity.Uncommon + 1);
        }

        /// <summary>Asserts a trait sits within [0, 1].</summary>
        private static void AssertUnit(float v)
        {
            Assert.GreaterOrEqual(v, 0f);
            Assert.LessOrEqual(v, 1f);
        }

        [Test]
        public void Breed_CrossSpecies_ReturnsNullWithWarning()
        {
            _a = MakePet(Pet.Species.Cat, Pet.GrowthStage.Adult, MatureA, PriceA, Pet.Rarity.Common);
            _b = MakePet(Pet.Species.Dog, Pet.GrowthStage.Adult, MatureB, PriceB, Pet.Rarity.Common);
            LogAssert.Expect(LogType.Warning, new Regex("cross-species"));
            _baby = BreedingSystem.Breed(_a, _b);
            Assert.IsNull(_baby);
        }

        [Test]
        public void Breed_NonAdultParent_ReturnsNullWithWarning()
        {
            _a = MakePet(Pet.Species.Cat, Pet.GrowthStage.Adult,    MatureA, PriceA, Pet.Rarity.Common);
            _b = MakePet(Pet.Species.Cat, Pet.GrowthStage.Juvenile, MatureB, PriceB, Pet.Rarity.Common);
            LogAssert.Expect(LogType.Warning, new Regex("cannot breed"));
            _baby = BreedingSystem.Breed(_a, _b);
            Assert.IsNull(_baby);
        }
    }
}
