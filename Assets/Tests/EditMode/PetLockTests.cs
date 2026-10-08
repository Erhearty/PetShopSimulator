using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Customer;
using PetShop.Pets;

namespace PetShop.Tests
{
    public class PetLockTests
    {
        private Pet _pet;

        [SetUp]
        public void SetUp()
        {
            _pet = ScriptableObject.CreateInstance<Pet>();
            _pet.growthStage = Pet.GrowthStage.Adult;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_pet);

        [Test]
        public void Locked_SurvivesSaveRoundTrip()
        {
            _pet.Locked = true;
            var back = SaveSystem.SaveDataToPet(SaveSystem.PetToSaveData(_pet));
            Assert.IsTrue(back.Locked);
            Object.DestroyImmediate(back);
        }

        [Test]
        public void Unlocked_StaysUnlockedAfterRoundTrip()
        {
            var back = SaveSystem.SaveDataToPet(SaveSystem.PetToSaveData(_pet));
            Assert.IsFalse(back.Locked);
            Object.DestroyImmediate(back);
        }

        [Test]
        public void Customers_DoNotAcceptALockedPet()
        {
            var profile = CustomerProfile.For(CustomerArchetype.Regular);
            Assert.IsTrue(profile.Accepts(_pet));
            _pet.Locked = true;
            Assert.IsFalse(profile.Accepts(_pet));
        }
    }
}
