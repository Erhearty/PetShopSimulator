using NUnit.Framework;
using PetShop.Commerce;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the pure reorder-rule sizing in <see cref="SoakSteward"/>.</summary>
    public class SoakStewardTests
    {
        [Test]
        public void ShelfCapacity_IsLinesTimesMaxPerLine()
        {
            Assert.AreEqual(15, SoakSteward.ShelfCapacity(3, 5));
            Assert.AreEqual(0,  SoakSteward.ShelfCapacity(0, 5));
        }

        [Test]
        public void CapacityByCategory_SumsShelvesOfTheSameCategory()
        {
            var caps = SoakSteward.CapacityByCategory(new[]
            {
                (ProductCategory.Food, 3, 5),
                (ProductCategory.Food, 2, 4),
                (ProductCategory.Toy,  3, 6),
            });

            Assert.AreEqual(2,  caps.Count);
            Assert.AreEqual(23, caps[ProductCategory.Food]);
            Assert.AreEqual(18, caps[ProductCategory.Toy]);
        }

        [Test]
        public void CapacityByCategory_NullOrEmpty_GivesNoCategories()
        {
            Assert.AreEqual(0, SoakSteward.CapacityByCategory(null).Count);
            Assert.AreEqual(0, SoakSteward.CapacityByCategory(
                new (ProductCategory, int, int)[0]).Count);
        }

        [Test]
        public void Threshold_IsTheShelfCapacity()
        {
            Assert.AreEqual(18, SoakSteward.ThresholdFor(18));
        }

        [Test]
        public void Units_AreAtLeastTheDefaultBatch()
        {
            Assert.AreEqual(AutoReorder.DefaultUnits, SoakSteward.UnitsFor(AutoReorder.DefaultUnits - 1));
            Assert.AreEqual(AutoReorder.DefaultUnits + 6, SoakSteward.UnitsFor(AutoReorder.DefaultUnits + 6));
        }
    }
}
