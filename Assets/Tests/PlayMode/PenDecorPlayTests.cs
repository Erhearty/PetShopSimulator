using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for pen dressing: every species' pen gets its bed, both bowls and a species
    /// prop, none of it has a collider, and the bowls show food only while the pen is fed.
    /// </summary>
    public class PenDecorPlayTests
    {
        /// <summary>Pen edge length used for the test pens, metres.</summary>
        private const float TestPenSize = 2.5f;
        /// <summary>A larger size, to prove the dressing is rebuilt on resize.</summary>
        private const float ResizedPenSize = 3.4f;
        /// <summary>Food level low enough that the pen needs feeding.</summary>
        private const float HungryFoodLevel = 0.1f;

        private GameObject _pen;

        [TearDown]
        public void TearDown()
        {
            if (_pen != null) Object.Destroy(_pen);
        }

        /// <summary>Creates a pen of <paramref name="species"/> and lets its Start run.</summary>
        private IEnumerator MakePen(Pet.Species species)
        {
            if (_pen != null) Object.Destroy(_pen);
            _pen = MeshBuilder.CreatePetPen(TestPenSize);
            var pen = _pen.AddComponent<PetPen>();
            pen.PenSize    = TestPenSize;
            pen.PenSpecies = species;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySpeciesPen_HasBedBowlsAndProp_WithoutColliders()
        {
            foreach (Pet.Species species in System.Enum.GetValues(typeof(Pet.Species)))
            {
                yield return MakePen(species);
                var decor = _pen.GetComponent<PetPen>().DecorRoot;
                Assert.IsNotNull(decor, $"{species}: no decor");

                Assert.IsNotNull(decor.Find(PetPen.DecorBedName), $"{species}: no bed");
                Assert.IsNotNull(decor.Find(PetPen.DecorFoodBowlName), $"{species}: no food bowl");
                Assert.IsNotNull(decor.Find(PetPen.DecorWaterBowlName), $"{species}: no water bowl");
                var prop = decor.Find(PetPen.DecorSpeciesPropName);
                Assert.IsNotNull(prop, $"{species}: no species prop");
                Assert.Greater(prop.GetComponentsInChildren<MeshRenderer>().Length, 0, $"{species}: empty prop");
                foreach (var t in _pen.GetComponentsInChildren<Transform>(true))
                    Assert.IsFalse(t.name.Contains("Plant") || t.name.StartsWith("Grass"), $"{species}: vegetation '{t.name}'");
                Assert.IsNotNull(decor.Find(PetPen.DecorTrimName), $"{species}: no trim");

                Assert.AreEqual(0, decor.GetComponentsInChildren<Collider>(true).Length,
                                $"{species}: decor must not have colliders");
            }
        }

        [UnityTest]
        public IEnumerator BowlFill_FollowsFeeding()
        {
            yield return MakePen(Pet.Species.Rabbit);
            var pen = _pen.GetComponent<PetPen>();
            Assert.IsTrue(pen.FoodBowlFilled && pen.WaterBowlFilled, "a fresh pen starts fed");

            pen.FoodLevel = HungryFoodLevel;
            pen.AdvanceDay();
            Assert.IsFalse(pen.FoodBowlFilled, "a hungry pen shows an empty food bowl");
            Assert.IsFalse(pen.WaterBowlFilled, "a hungry pen shows an empty water bowl");

            Assert.IsTrue(pen.Service());
            Assert.IsTrue(pen.FoodBowlFilled && pen.WaterBowlFilled, "servicing refills the bowls");
        }

        [UnityTest]
        public IEnumerator Decor_IsRebuiltOnResize()
        {
            yield return MakePen(Pet.Species.Cat);
            var pen = _pen.GetComponent<PetPen>();
            var before = pen.DecorRoot;

            pen.PenSize   = ResizedPenSize;
            pen.FoodLevel = HungryFoodLevel;
            Assert.IsTrue(pen.Service(), "servicing a hungry pen refreshes its visuals");
            yield return null;

            Assert.IsTrue(before == null, "the old dressing is destroyed");
            Assert.IsNotNull(pen.DecorRoot);
            Assert.AreNotSame(before, pen.DecorRoot);
        }
    }
}
