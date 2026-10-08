using NUnit.Framework;
using UnityEngine;
using PetShop.Pets;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>Species visual scales, the deer pen's doubled area, and the pen info panel's lock toggle.</summary>
    public class PenScaleTests
    {
        [TestCase(Pet.Species.Penguin, 1.5f)]
        [TestCase(Pet.Species.Chicken, 0.5f)]
        [TestCase(Pet.Species.Dog,     1.2f)]
        [TestCase(Pet.Species.Cat,     0.7f)]
        [TestCase(Pet.Species.Deer,    2f)]
        [TestCase(Pet.Species.Rabbit,  1f)]
        public void VisualScale_MatchesSpec(Pet.Species species, float expected) =>
            Assert.AreEqual(expected, Pet.VisualScale(species), 0.0001f);

        [Test]
        public void DeerPen_HasTwiceTheArea_AndFitsItsFootprint()
        {
            float side = BuildCatalog.PenSideScale(Pet.Species.Deer);
            Assert.AreEqual(2f, side * side, 0.0001f, "area ratio");
            Assert.AreEqual(1f, BuildCatalog.PenSideScale(Pet.Species.Dog), 0.0001f);

            var deer = BuildCatalog.PenFootprintFor(Pet.Species.Deer);
            Assert.GreaterOrEqual(deer.x, BuildCatalog.PenFootprintCells * side, "deer pen must fit its footprint");
        }

        [Test]
        public void ToggleLock_FlipsPetLocked()
        {
            var pet = ScriptableObject.CreateInstance<Pet>();
            Assert.IsTrue(PenInfoPanel.ToggleLock(pet));
            Assert.IsTrue(pet.Locked);
            Assert.IsFalse(PenInfoPanel.ToggleLock(pet));
            Assert.IsFalse(PenInfoPanel.ToggleLock(null));
            Object.DestroyImmediate(pet);
        }
    }
}
