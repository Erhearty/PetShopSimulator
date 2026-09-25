using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for SaveSystem: JSON round-trips through a throwaway temp file (never the
    /// real save slot) and the Pet &lt;-&gt; PetSaveData mapping.
    /// </summary>
    public class SaveSystemTests
    {
        private const float Tolerance = 1e-5f;
        private const int   OutdatedVersion = 1;

        private string _dir;
        private string _path;
        private Pet _source;
        private Pet _restored;

        /// <summary>Creates a fresh temp directory and save path inside it for each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "petshop_test.json");
        }

        /// <summary>Removes the temp directory and any pets the test created.</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
            if (_source != null)   Object.DestroyImmediate(_source);
            if (_restored != null) Object.DestroyImmediate(_restored);
        }

        /// <summary>A small, fully populated save to round-trip.</summary>
        private static SaveData BuildSample()
        {
            var data = new SaveData
            {
                Version = 2, Balance = 1234.5f, Reputation = 67.25f, Day = 9,
                Staff = 3, PriceMultiplier = 1.25f,
            };
            data.Stock.Add(new SaveData.StockEntry { id = "kibble", qty = 12 });
            var placed = new SaveData.PlacedItem { catalogId = "pen_small", cellX = 4, cellY = 7 };
            placed.pets.Add(new SaveData.PetSaveData { species = "Cat", petName = "Pip", ageDays = 2 });
            placed.pets.Add(new SaveData.PetSaveData { species = "Dog", petName = "Miso", ageDays = 5 });
            data.PlacedObjects.Add(placed);
            return data;
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            var original = BuildSample();
            SaveSystem.Save(original, _path);
            var loaded = SaveSystem.Load(_path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.Version, loaded.Version);
            Assert.AreEqual(original.Balance, loaded.Balance, Tolerance);
            Assert.AreEqual(original.Reputation, loaded.Reputation, Tolerance);
            Assert.AreEqual(original.Day, loaded.Day);
            Assert.AreEqual(original.Staff, loaded.Staff);
            Assert.AreEqual(original.PriceMultiplier, loaded.PriceMultiplier, Tolerance);
            Assert.AreEqual(1, loaded.Stock.Count);
            Assert.AreEqual("kibble", loaded.Stock[0].id);
            Assert.AreEqual(12, loaded.Stock[0].qty);
            AssertPlacedMatches(original.PlacedObjects, loaded);
        }

        /// <summary>Checks the single placed item and its two nested pets survived.</summary>
        private static void AssertPlacedMatches(System.Collections.Generic.List<SaveData.PlacedItem> expected,
                                                SaveData loaded)
        {
            Assert.AreEqual(1, loaded.PlacedObjects.Count);
            var item = loaded.PlacedObjects[0];
            Assert.AreEqual(expected[0].catalogId, item.catalogId);
            Assert.AreEqual(expected[0].cellX, item.cellX);
            Assert.AreEqual(expected[0].cellY, item.cellY);
            Assert.AreEqual(2, item.pets.Count);
            for (int i = 0; i < item.pets.Count; i++)
            {
                Assert.AreEqual(expected[0].pets[i].species, item.pets[i].species);
                Assert.AreEqual(expected[0].pets[i].petName, item.pets[i].petName);
                Assert.AreEqual(expected[0].pets[i].ageDays, item.pets[i].ageDays);
            }
        }

        /// <summary>An adult dog with every persisted field off its default.</summary>
        private static Pet BuildPet()
        {
            var p = ScriptableObject.CreateInstance<Pet>();
            p.species = Pet.Species.Dog;   p.petName = "Biscuit";
            p.growthStage = Pet.GrowthStage.Adult; p.rarity = Pet.Rarity.Rare;
            p.coat = new Color(0.1f, 0.2f, 0.3f);
            p.temperament = 0.11f; p.energyLevel = 0.22f; p.friendliness = 0.33f;
            p.ageDays = 12; p.daysToMature = 9; p.basePrice = 140f;
            return p;
        }

        [Test]
        public void Save_ValidPath_ReturnsTrueAndWritesFile()
        {
            Assert.IsTrue(SaveSystem.Save(BuildSample(), _path));
            Assert.IsTrue(File.Exists(_path));
        }

        [Test]
        public void Save_MissingDirectory_ReturnsFalseWithError()
        {
            var badPath = Path.Combine(_dir, "does_not_exist", "petshop_test.json");
            LogAssert.Expect(LogType.Error, new Regex("Save failed"));
            Assert.IsFalse(SaveSystem.Save(BuildSample(), badPath));
            Assert.IsFalse(File.Exists(badPath));
        }

        [Test]
        public void PetRoundTrip_PreservesPersistedFields()
        {
            _source   = BuildPet();
            _restored = SaveSystem.SaveDataToPet(SaveSystem.PetToSaveData(_source));

            Assert.AreEqual(_source.species, _restored.species);
            Assert.AreEqual(_source.petName, _restored.petName);
            Assert.AreEqual(_source.growthStage, _restored.growthStage);
            Assert.AreEqual(_source.rarity, _restored.rarity);
            Assert.AreEqual(_source.coat.r, _restored.coat.r, Tolerance);
            Assert.AreEqual(_source.coat.g, _restored.coat.g, Tolerance);
            Assert.AreEqual(_source.coat.b, _restored.coat.b, Tolerance);
            Assert.AreEqual(_source.temperament, _restored.temperament, Tolerance);
            Assert.AreEqual(_source.energyLevel, _restored.energyLevel, Tolerance);
            Assert.AreEqual(_source.friendliness, _restored.friendliness, Tolerance);
            Assert.AreEqual(_source.ageDays, _restored.ageDays);
            Assert.AreEqual(_source.daysToMature, _restored.daysToMature);
            Assert.AreEqual(_source.basePrice, _restored.basePrice, Tolerance);
        }

        [Test]
        public void SaveDataToPet_ZeroDaysToMature_BecomesOne()
        {
            var d = new SaveData.PetSaveData { species = "Cat", petName = "Nala", daysToMature = 0, basePrice = 10f };
            _restored = SaveSystem.SaveDataToPet(d);
            Assert.AreEqual(1, _restored.daysToMature);
        }

        [Test]
        public void SaveDataToPet_ZeroBasePrice_FallsBackToSpeciesPrice()
        {
            var d = new SaveData.PetSaveData { species = "Horse", petName = "Comet", daysToMature = 5, basePrice = 0f };
            _restored = SaveSystem.SaveDataToPet(d);
            Assert.AreEqual(Pet.SpeciesBasePrice(Pet.Species.Horse), _restored.basePrice, Tolerance);
        }

        [Test]
        public void Load_OutdatedVersion_ReturnsNullWithWarning()
        {
            File.WriteAllText(_path, JsonUtility.ToJson(new SaveData { Version = OutdatedVersion }));
            LogAssert.Expect(LogType.Warning, new Regex("older build"));
            Assert.IsNull(SaveSystem.Load(_path));
        }

        [Test]
        public void Load_MissingFile_ReturnsNull()
        {
            Assert.IsFalse(File.Exists(_path));
            Assert.IsNull(SaveSystem.Load(_path));
        }
    }
}
