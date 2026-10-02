using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode test for packing a stocked shelf back into the furniture inventory: shelf
    /// visuals use Destroy, so this runs in play mode rather than EditMode.
    /// </summary>
    public class BuildModeRemovalPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const float  Tolerance   = 1e-3f;
        private const int    StockUnits  = 3;
        private static readonly Vector2Int FloorSize = new(6, 6);
        private static readonly Vector2Int Cell      = new(2, 2);

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private ProductItem      _product;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (_product != null) Object.Destroy(_product);
            FurnitureFactory.Prefabs = _saved;
        }

        /// <summary>Removing a stocked shelf returns it to the inventory and its stock to the warehouse.</summary>
        [UnityTest]
        public IEnumerator Remove_StockedShelf_ReturnsShelfAndStock()
        {
            _saved = FurnitureFactory.Prefabs;
#if UNITY_EDITOR
            FurnitureFactory.Prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
#endif
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");

            _root = new GameObject("build-play-test");
            var grid = _root.AddComponent<GridManager>();
            var shop = _root.AddComponent<ShopManager>();
            shop.SetBalance(RichBalance);
            grid.FillFloorRect(Vector2Int.zero, FloorSize);

            var supply = new FurnitureSupply();
            var build  = _root.AddComponent<BuildMode>();
            build.GridManager = grid; build.Shop = shop; build.ObjectRoot = _root.transform; build.Supply = supply;

            supply.AddOwned(BuildCatalog.ShelfSmall);
            Assert.IsTrue(build.EnterPlacement(BuildCatalog.ShelfSmall, nameof(ProductCategory.Food)));
            var shelf = build.PlaceHeld(Cell).GetComponent<ShelfUnit>();
            yield return null;

            _product = ProductItem.Create("test_kibble", "Kibble", ProductCategory.Food, 1f, 2f, Color.white);
            Assert.AreEqual(StockUnits, shelf.AddStock(_product, StockUnits));

            build.RemoveAtWorldPos(grid.GridToWorld(Cell));
            yield return null;

            Assert.AreEqual(1, supply.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(StockUnits, shop.Warehouse(ProductCategory.Food));
            Assert.AreEqual(RichBalance, shop.Balance, Tolerance);
            Assert.IsFalse(grid.TryGetObject(Cell, out _));
        }
    }
}
