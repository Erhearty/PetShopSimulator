using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests wiring <see cref="FeedbackFX"/> to a real <see cref="BuildMode"/>: placement
    /// done the way load / seeding / dev furnishing do it raises no effects, and a player placement's
    /// pop leaves the collider geometry (what the NavMesh bake reads) at full size.
    /// </summary>
    public class FeedbackFXBuildPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  Tolerance   = 1e-4f;
        private static readonly Vector2Int FloorSize  = new(10, 10);
        private static readonly Vector2Int SeedCell   = new(2, 2);
        private static readonly Vector2Int PlayerCell = new(6, 6);

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private FeedbackFX       _fx;
        private BuildMode        _build;
        private GridManager      _grid;
        private FurnitureSupply  _supply;
        private bool             _previousReduceMotion;

        [SetUp]
        public void SetUp()
        {
            _previousReduceMotion = GameSettings.ReduceMotion;
            GameSettings.ReduceMotion = false;
            _saved = FurnitureFactory.Prefabs;
#if UNITY_EDITOR
            FurnitureFactory.Prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
#endif
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");
            BuildRig();
        }

        private void BuildRig()
        {
            _root = new GameObject("fx-build-play-test");
            _grid = _root.AddComponent<GridManager>();
            _grid.FillFloorRect(Vector2Int.zero, FloorSize);
            _supply = new FurnitureSupply();
            _build  = _root.AddComponent<BuildMode>();
            _build.GridManager = _grid; _build.ObjectRoot = _root.transform; _build.Supply = _supply;
            _build.Shop = _root.AddComponent<ShopManager>();
            _fx = new GameObject("fx").AddComponent<FeedbackFX>();
            _fx.enabled = false; // only manual Tick() advances time
            _fx.Attach(null, _build);
        }

        [TearDown]
        public void TearDown()
        {
            if (_fx != null) Object.Destroy(_fx.gameObject);
            if (_root != null) Object.Destroy(_root);
            FurnitureFactory.Prefabs = _saved;
            GameSettings.ReduceMotion = _previousReduceMotion;
        }

        private GameObject PlaceLikeLoad(Vector2Int cell) =>
            _build.Place(cell, BuildCatalog.Get(BuildCatalog.ShelfSmall), nameof(ProductCategory.Food), 0f, charge: false);

        private GameObject PlaceLikePlayer(Vector2Int cell)
        {
            _supply.AddOwned(BuildCatalog.ShelfSmall);
            Assert.IsTrue(_build.EnterPlacement(BuildCatalog.ShelfSmall, nameof(ProductCategory.Food)));
            return _build.PlaceHeld(cell);
        }

        private static Vector3 ColliderSize(GameObject go)
        {
            Physics.SyncTransforms();
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var c in go.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            return bounds.size;
        }

        /// <summary>Load / seed / dev-furnish placement (BuildMode.Place) raises no dust and no pop.</summary>
        [UnityTest]
        public IEnumerator NonInteractivePlace_TriggersNoEffects()
        {
            Assert.IsNotNull(PlaceLikeLoad(SeedCell));
            Assert.AreEqual(0, _fx.ActiveFurnitureTweenCount);
            Assert.AreEqual(0, _fx.Dust.particleCount);
            yield return null;
        }

        /// <summary>A player placement pops a clone, but the real object's colliders stay at full size.</summary>
        [UnityTest]
        public IEnumerator PlayerPlace_Pops_WithoutShrinkingColliders()
        {
            var seeded = PlaceLikeLoad(SeedCell);
            Vector3 expected = ColliderSize(seeded);
            Assume.That(expected.sqrMagnitude, Is.GreaterThan(0f), "shelf has colliders");

            var placed = PlaceLikePlayer(PlayerCell);
            Assert.AreEqual(1, _fx.ActiveFurnitureTweenCount);
            Assert.AreEqual(Vector3.one, placed.transform.localScale);
            Assert.AreEqual(expected.x, ColliderSize(placed).x, Tolerance);
            Assert.AreEqual(expected.z, ColliderSize(placed).z, Tolerance);

            _fx.Tick(FeedbackFX.PopDuration * 0.5f);
            Assert.AreEqual(expected.x, ColliderSize(placed).x, Tolerance);
            Assert.AreEqual(0, _fx.GetComponentsInChildren<Collider>(true).Length, "clone is visual-only");
            yield return null;
        }

        /// <summary>Removing an object leaves a shrinking visual-only clone that cleans itself up.</summary>
        [UnityTest]
        public IEnumerator Remove_ShrinksVisualClone_ThenCleansUp()
        {
            PlaceLikePlayer(PlayerCell);
            _fx.Tick(FeedbackFX.PopDuration + FeedbackFX.PopDuration);
            _build.RemoveAtWorldPos(_grid.GridToWorld(PlayerCell));
            Assert.AreEqual(1, _fx.ActiveFurnitureTweenCount);
            Assert.AreEqual(0, _fx.GetComponentsInChildren<Collider>(true).Length);

            _fx.Tick(FeedbackFX.ShrinkDuration + FeedbackFX.ShrinkDuration);
            Assert.AreEqual(0, _fx.ActiveFurnitureTweenCount);
            yield return null;
        }
    }
}
