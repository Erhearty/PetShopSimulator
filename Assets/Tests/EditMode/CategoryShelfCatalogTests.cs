using NUnit.Framework;
using PetShop.Commerce;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>The toy, accessory and medicine shelves are catalogued as shelves bound to their category.</summary>
    public class CategoryShelfCatalogTests
    {
        [TestCase(BuildCatalog.ShelfToys,        ProductCategory.Toy)]
        [TestCase(BuildCatalog.ShelfAccessories, ProductCategory.Accessory)]
        [TestCase(BuildCatalog.ShelfMedicine,    ProductCategory.Medicine)]
        public void CategoryShelf_IsListedShelfWithMatchingCategory(string id, ProductCategory category)
        {
            var def = BuildCatalog.Get(id);
            Assert.IsNotNull(def);
            Assert.AreEqual("shelf", def.Type);
            Assert.IsFalse(def.Hidden);
            Assert.Greater(def.Cost, 0f);
            Assert.AreEqual(category, BuildCatalog.ShelfCategoryFor(id));
        }

        [Test]
        public void PlainShelves_HaveNoFixedCategory()
        {
            Assert.IsNull(BuildCatalog.ShelfCategoryFor(BuildCatalog.ShelfSmall));
            Assert.IsNull(BuildCatalog.ShelfCategoryFor(BuildCatalog.ShelfLarge));
        }
    }
}
