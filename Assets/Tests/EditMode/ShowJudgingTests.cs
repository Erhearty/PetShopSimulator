using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for closing-time show judging and its persistence.</summary>
    public class ShowJudgingTests
    {
        private const int ShowDay = 7;

        private Pet _pet;
        private GameObject _go;
        private ShopManager _shop;
        private PetShow _show;

        [SetUp]
        public void SetUp()
        {
            _pet = ScriptableObject.CreateInstance<Pet>();
            _pet.id = BreedingSystem.NewId();
            _pet.species = Pet.Species.Cat;
            _pet.rarity = Pet.Rarity.Legendary;
            _pet.growthStage = Pet.GrowthStage.Adult;
            _pet.basePrice = Pet.SpeciesBasePrice(Pet.Species.Cat);
            _go = new GameObject("ShopManagerTest");
            _shop = _go.AddComponent<ShopManager>();
            _shop.SetBalance(500f);
            _show = new PetShow();
            _show.Restore(_pet);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_pet);
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Run_NotShowDay_KeepsEntry()
        {
            ShowJudging.Run(_show, _shop, ShowDay - 1, new[] { _pet }, null);
            Assert.AreEqual(_pet.id, _show.EntryPetId);
            Assert.AreEqual(500f, _shop.Balance, 1e-3f);
        }

        [Test]
        public void Run_ShowDay_PaysOnceAndClearsEntry()
        {
            var pets = new[] { _pet };
            ShowJudging.Run(_show, _shop, ShowDay, pets, null);
            float after = _shop.Balance;
            Assert.IsNull(_show.EntryPetId);
            Assert.IsTrue(_show.LastResult.HasValue);
            Assert.AreEqual(500f + _show.LastResult.Value.prize, after, 1e-3f);
            ShowJudging.Run(_show, _shop, ShowDay, pets, null);
            Assert.AreEqual(after, _shop.Balance, 1e-3f);
        }

        [Test]
        public void Run_PlacedInTopThree_AddsRibbon()
        {
            ShowJudging.Run(_show, _shop, ShowDay, new[] { _pet }, null);
            int place = _show.LastResult.Value.placement;
            Assert.AreEqual(place >= 1 && place <= 3 ? 1 : 0, _pet.ribbons);
        }

        [Test]
        public void Run_SoldEntry_VoidsWithoutPayment()
        {
            var messages = new List<string>();
            Assert.DoesNotThrow(() =>
                ShowJudging.Run(_show, _shop, ShowDay, new List<Pet>(), messages.Add));
            Assert.IsNull(_show.EntryPetId);
            Assert.AreEqual(500f, _shop.Balance, 1e-3f);
            Assert.AreEqual(1, messages.Count);
        }

        [Test]
        public void SaveRoundTrip_KeepsRibbonsAndEntryId()
        {
            _pet.ribbons = 2;
            var data = new SaveData { ShowEntryPetId = _show.EntryPetId };
            data.PlacedObjects.Add(new SaveData.PlacedItem());
            data.PlacedObjects[0].pets.Add(SaveSystem.PetToSaveData(_pet));
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var back = SaveSystem.SaveDataToPet(loaded.PlacedObjects[0].pets[0]);
            Assert.AreEqual(2, back.ribbons);
            Assert.AreEqual(_pet.id, loaded.ShowEntryPetId);
            Object.DestroyImmediate(back);
        }

        [Test]
        public void OldSave_WithoutFields_HasNoRibbonsOrEntry()
        {
            var save = JsonUtility.FromJson<SaveData>("{\"Day\":2}");
            Assert.IsTrue(string.IsNullOrEmpty(save.ShowEntryPetId));
            var d = JsonUtility.FromJson<SaveData.PetSaveData>("{\"species\":\"Dog\"}");
            var pet = SaveSystem.SaveDataToPet(d);
            Assert.AreEqual(0, pet.ribbons);
            Object.DestroyImmediate(pet);
        }
    }
}
