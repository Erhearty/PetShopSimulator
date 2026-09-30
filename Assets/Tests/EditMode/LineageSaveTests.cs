using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for persisting pet ids, parents and the lineage registry.</summary>
    public class LineageSaveTests
    {
        private const float Tolerance = 1e-5f;
        private const int   BornDay   = 3;

        private Pet _parentA, _parentB, _child;

        [SetUp]
        public void SetUp()
        {
            LineageRegistry.Reset();
            _parentA = MakePet("Ann", 0, null, null);
            _parentB = MakePet("Bob", 0, null, null);
            _child   = MakePet("Cy", 1, _parentA.id, _parentB.id);
        }

        [TearDown]
        public void TearDown()
        {
            LineageRegistry.Reset();
            foreach (var p in new[] { _parentA, _parentB, _child })
                if (p != null) Object.DestroyImmediate(p);
        }

        private static Pet MakePet(string name, int generation, string a, string b)
        {
            var pet = ScriptableObject.CreateInstance<Pet>();
            pet.id = BreedingSystem.NewId();
            pet.petName = name;
            pet.generation = generation;
            pet.parentAId = a;
            pet.parentBId = b;
            pet.coat = new Color(0.25f, 0.5f, 0.75f);
            LineageRegistry.Register(pet, BornDay);
            return pet;
        }

        [Test]
        public void PetRoundTrip_KeepsIdParentsAndGeneration()
        {
            var back = SaveSystem.SaveDataToPet(SaveSystem.PetToSaveData(_child));
            Assert.AreEqual(_child.id, back.id);
            Assert.AreEqual(_parentA.id, back.parentAId);
            Assert.AreEqual(_parentB.id, back.parentBId);
            Assert.AreEqual(1, back.generation);
            Object.DestroyImmediate(back);
        }

        [Test]
        public void RegistryRoundTrip_ThroughJson_RestoresFamily()
        {
            var data = new SaveData();
            SaveLineage.Capture(data);
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            LineageRegistry.Reset();
            SaveLineage.Apply(loaded, null, BornDay);

            Assert.AreEqual(3, LineageRegistry.Entries.Count);
            var entry = LineageRegistry.Get(_child.id);
            Assert.AreEqual(1, entry.generation);
            Assert.AreEqual(0.5f, entry.coat.g, Tolerance);
            Assert.AreEqual(2, LineageRegistry.Parents(_child.id).Count);
        }

        [Test]
        public void OldSaveJson_WithoutNewFields_LoadsWithGeneratedIds()
        {
            const string json = "{\"species\":\"Dog\",\"petName\":\"Rex\",\"growthStage\":\"Adult\"," +
                                "\"rarity\":\"Common\",\"coat_r\":1,\"coat_g\":0,\"coat_b\":0}";
            var d = JsonUtility.FromJson<SaveData.PetSaveData>(json);
            var a = SaveSystem.SaveDataToPet(d);
            var b = SaveSystem.SaveDataToPet(d);
            Assert.IsFalse(string.IsNullOrEmpty(a.id));
            Assert.AreNotEqual(a.id, b.id);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);

            var save = JsonUtility.FromJson<SaveData>("{\"Day\":2}");
            Assert.IsNotNull(save.Lineage);
            Assert.AreEqual(0, save.Lineage.Count);
        }

        [Test]
        public void Apply_RegistersLoadedPetsMissingFromSave()
        {
            LineageRegistry.Reset();
            SaveLineage.Apply(new SaveData(), new[] { _child }, BornDay);
            Assert.IsNotNull(LineageRegistry.Get(_child.id));
            Assert.AreEqual(1, LineageRegistry.Entries.Count);
        }

        [Test]
        public void Apply_ResetsStaleEntries()
        {
            SaveLineage.Apply(new SaveData(), null, BornDay);
            Assert.AreEqual(0, LineageRegistry.Entries.Count);
            Assert.IsNull(LineageRegistry.Get(_parentA.id));
        }
    }
}
