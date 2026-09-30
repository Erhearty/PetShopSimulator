using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for lineage ids, the registry, coat names and pairing odds.</summary>
    public class BreedingGeneticsTests
    {
        private const int   Seed      = 4242;
        private const float Tolerance = 1e-4f;
        private const int   BabyCount = 20;

        private readonly List<Pet> _pets = new List<Pet>();

        /// <summary>Seeds Random and clears the registry.</summary>
        [SetUp]
        public void SetUp()
        {
            Random.InitState(Seed);
            LineageRegistry.Reset();
        }

        /// <summary>Destroys every pet created by the test and clears the registry.</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var p in _pets) if (p != null) Object.DestroyImmediate(p);
            _pets.Clear();
            LineageRegistry.Reset();
        }

        private Pet Adult(Pet.Rarity rarity, Color coat, int generation = 0)
        {
            var p = BreedingSystem.GenerateRandom(Pet.Species.Cat);
            p.rarity = rarity; p.coat = coat; p.generation = generation;
            _pets.Add(p);
            return p;
        }

        [Test]
        public void Breed_SetsParentIdsAndGeneration()
        {
            var a = Adult(Pet.Rarity.Common, Color.white, 1);
            var b = Adult(Pet.Rarity.Common, Color.white, 3);
            var baby = BreedingSystem.Breed(a, b);
            _pets.Add(baby);
            Assert.AreEqual(a.id, baby.parentAId);
            Assert.AreEqual(b.id, baby.parentBId);
            Assert.AreEqual(4, baby.generation);
            Assert.IsNotEmpty(baby.id);
        }

        [Test]
        public void Breed_IdsAreUnique()
        {
            var a = Adult(Pet.Rarity.Common, Color.white);
            var b = Adult(Pet.Rarity.Common, Color.white);
            var ids = new HashSet<string> { a.id, b.id };
            for (int i = 0; i < BabyCount; i++)
            {
                var baby = BreedingSystem.Breed(a, b);
                _pets.Add(baby);
                Assert.IsTrue(ids.Add(baby.id));
            }
        }

        [Test]
        public void Registry_KeepsEntryAfterPetDropped()
        {
            var a = Adult(Pet.Rarity.Common, Color.white);
            string id = a.id;
            Object.DestroyImmediate(a);
            var entry = LineageRegistry.Get(id);
            Assert.IsNotNull(entry);
            Assert.AreEqual(id, entry.id);
        }

        [Test]
        public void Classify_MapsSamples()
        {
            Assert.AreEqual("White", CoatColours.Classify(Color.white));
            Assert.AreEqual("Black", CoatColours.Classify(Color.black));
            Assert.AreEqual("Ginger", CoatColours.Classify(new Color(0.88f, 0.45f, 0.18f)));
        }

        [Test]
        public void EstimateOdds_IdenticalCoats_GivesThatCoat()
        {
            var a = Adult(Pet.Rarity.Common, Color.black);
            var b = Adult(Pet.Rarity.Common, Color.black);
            var odds = BreedingSystem.EstimateOdds(a, b);
            Assert.AreEqual(1f, odds.Coats["Black"], Tolerance);
        }

        [Test]
        public void EstimateOdds_RareRare_IsRareOrLegendary()
        {
            var odds = BreedingSystem.EstimateOdds(Adult(Pet.Rarity.Rare, Color.white),
                                                   Adult(Pet.Rarity.Rare, Color.white));
            Assert.AreEqual(0.88f, odds.Rarities[Pet.Rarity.Rare], Tolerance);
            Assert.AreEqual(0.12f, odds.Rarities[Pet.Rarity.Legendary], Tolerance);
        }

        [Test]
        public void EstimateOdds_CommonRare_IsCommonOrUncommon()
        {
            var odds = BreedingSystem.EstimateOdds(Adult(Pet.Rarity.Common, Color.white),
                                                   Adult(Pet.Rarity.Rare, Color.white));
            Assert.AreEqual(0.95f, odds.Rarities[Pet.Rarity.Common], Tolerance);
            Assert.AreEqual(0.05f, odds.Rarities[Pet.Rarity.Uncommon], Tolerance);
        }
    }
}
