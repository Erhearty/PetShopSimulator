using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for PetShow rules, judging and ribbons.</summary>
    public class PetShowTests
    {
        private const int   TestDay   = 7;
        private const float Tolerance = 1e-3f;

        private Pet _pet;
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_pet != null) Object.DestroyImmediate(_pet);
            if (_go != null)  Object.DestroyImmediate(_go);
        }

        private Pet MakePet(Pet.Species species, Pet.Rarity rarity, Pet.GrowthStage stage)
        {
            _pet = ScriptableObject.CreateInstance<Pet>();
            _pet.species = species; _pet.rarity = rarity; _pet.growthStage = stage;
            _pet.basePrice = Pet.SpeciesBasePrice(species);
            return _pet;
        }

        [Test]
        public void IsShowDay_OnlyDays7And14InFirstTwoWeeks()
        {
            var days = Enumerable.Range(1, 14).Where(PetShow.IsShowDay).ToArray();
            CollectionAssert.AreEqual(new[] { 7, 14 }, days);
        }

        [Test]
        public void ThemeFor_SameDay_IsStable()
        {
            var owned = new[] { Pet.Species.Cat, Pet.Species.Dog, Pet.Species.Fox };
            var a = PetShow.ThemeFor(TestDay, owned);
            var b = PetShow.ThemeFor(TestDay, owned);
            Assert.AreEqual(a.species, b.species);
            Assert.AreEqual(a.coatName, b.coatName);
        }

        [Test]
        public void Judge_LegendaryThrivingMatch_PlacesFirst()
        {
            var pet = MakePet(Pet.Species.Cat, Pet.Rarity.Legendary, Pet.GrowthStage.Adult);
            var theme = new ShowTheme(Pet.Species.Cat, pet.CoatName);
            Assert.AreEqual(1, new PetShow().Judge(TestDay, pet, theme).placement);
        }

        [Test]
        public void Enter_WrongSpecies_IsRejected()
        {
            var pet = MakePet(Pet.Species.Dog, Pet.Rarity.Common, Pet.GrowthStage.Adult);
            _go = new GameObject("ShopManagerTest");
            var shop = _go.AddComponent<ShopManager>();
            shop.SetBalance(500f);
            var theme = new ShowTheme(Pet.Species.Cat, pet.CoatName);
            Assert.IsFalse(new PetShow().Enter(pet, shop, theme));
            Assert.AreEqual(500f, shop.Balance, Tolerance);
        }

        [Test]
        public void Judge_FirstPlace_PrizeIsTwiceSpeciesBasePrice()
        {
            var pet = MakePet(Pet.Species.Cat, Pet.Rarity.Legendary, Pet.GrowthStage.Adult);
            var theme = new ShowTheme(Pet.Species.Cat, pet.CoatName);
            var r = new PetShow().Judge(TestDay, pet, theme);
            Assert.AreEqual(2f * Pet.SpeciesBasePrice(Pet.Species.Cat), r.prize, Tolerance);
        }

        [Test]
        public void SellPrice_OneRibbon_IsOnePointFifteenTimes()
        {
            var pet = MakePet(Pet.Species.Cat, Pet.Rarity.Rare, Pet.GrowthStage.Adult);
            float before = pet.SellPrice();
            pet.ribbons = 1;
            Assert.AreEqual(before * 1.15f, pet.SellPrice(), 0.02f);
        }
    }
}
