using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for placing furniture from the hand: the prefab ghost, rotation steps,
    /// the rotated footprint and its save flag, free placement and removal into the inventory.
    /// </summary>
    public class BuildModeHeldTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const float  Tolerance   = 1e-3f;
        private const float  QuarterTurn = 90f;
        private static readonly Vector2Int FloorOrigin = new(0, 0);
        private static readonly Vector2Int FloorSize   = new(10, 10);
        private static readonly Vector2Int Cell        = new(3, 3);
        private static readonly Vector2Int OtherCell   = new(6, 6);

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private GridManager      _grid;
        private ShopManager      _shop;
        private BuildMode        _build;
        private FurnitureSupply  _supply;

        [SetUp]
        public void SetUp()
        {
            _saved = FurnitureFactory.Prefabs;
            FurnitureFactory.Prefabs = AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
            Assert.IsNotNull(FurnitureFactory.Prefabs, $"No FurniturePrefabs asset at {CatalogPath}.");

            _root  = new GameObject("build-test");
            _grid  = _root.AddComponent<GridManager>();
            _shop  = _root.AddComponent<ShopManager>();
            _shop.SetBalance(RichBalance);
            _grid.FillFloorRect(FloorOrigin, FloorSize);

            _supply = new FurnitureSupply();
            _build  = new GameObject("build").AddComponent<BuildMode>();
            _build.GridManager = _grid;
            _build.Shop        = _shop;
            _build.ObjectRoot  = _root.transform;
            _build.Supply      = _supply;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_build.gameObject);
            Object.DestroyImmediate(_root);
            FurnitureFactory.Prefabs = _saved;
        }

        private void Hold(string id, int owned = 1)
        {
            _supply.AddOwned(id, owned);
            Assert.IsTrue(_build.EnterPlacement(id, null), $"EnterPlacement({id})");
        }

        [Test]
        public void Ghost_IsAStrippedCopyOfThePrefab()
        {
            Hold(BuildCatalog.ShelfSmall);
            var prefab = FurnitureFactory.Prefabs.Get(BuildCatalog.ShelfSmall);
            var ghost  = _build.Ghost;

            Assert.IsNotNull(ghost);
            CollectionAssert.AreEquivalent(Meshes(prefab), Meshes(ghost), "ghost meshes are not the prefab's");
            Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>(true), "colliders");
            Assert.IsEmpty(ghost.GetComponentsInChildren<MonoBehaviour>(true), "behaviours");

            var renderers = ghost.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            Material shared = renderers[0].sharedMaterial;
            foreach (var r in renderers)
            {
                Assert.AreEqual(GameLayers.Ghost, r.gameObject.layer, r.name);
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, r.name);
                foreach (var m in r.sharedMaterials) Assert.AreSame(shared, m, r.name);
            }
        }

        private static List<Mesh> Meshes(GameObject root)
        {
            var list = new List<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) list.Add(mf.sharedMesh);
            return list;
        }

        [Test]
        public void RotationStep_Is45ForDecorAnd90ForFurniture()
        {
            Hold(BuildCatalog.DecorFishTank);
            Assert.AreEqual(BuildMode.DecorRotationStep, _build.RotationStep);
            _build.Rotate(1);
            Assert.AreEqual(45f, _build.CurrentRotation, Tolerance);
            _build.Rotate(-1);
            _build.Rotate(-1);
            Assert.AreEqual(315f, _build.CurrentRotation, Tolerance);
            StringAssert.Contains("315", _build.PlacementHint);

            Hold(BuildCatalog.ShelfSmall);
            Assert.AreEqual(BuildMode.DefaultRotationStep, _build.RotationStep);
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.DecorFishTank), "switching returns the held item");
        }

        [Test]
        public void HandPlaced2x1_At90_OccupiesSwappedCellsAndSavesFlag()
        {
            var def = BuildCatalog.Get(BuildCatalog.DecorFishTank);
            Assert.AreEqual(new Vector2Int(2, 1), def.Size);
            Hold(BuildCatalog.DecorFishTank);
            _build.Rotate(1);
            _build.Rotate(1);
            Assert.AreEqual(QuarterTurn, _build.CurrentRotation, Tolerance);

            var go = _build.PlaceHeld(Cell);
            Assert.IsNotNull(go);
            Assert.IsTrue(_grid.TryGetObject(Cell + Vector2Int.up, out var entry), "swapped cell free");
            Assert.IsFalse(_grid.TryGetObject(Cell + Vector2Int.right, out _), "unswapped cell taken");
            Assert.AreEqual(new Vector2Int(1, 2), entry.Size);
            Assert.AreEqual(_grid.FootprintCenter(Cell, entry.Size), go.transform.position);
            Assert.IsTrue(BuildMode.IsFootprintRotated(entry));
            Assert.IsTrue(SaveEntry(entry).footprintRotated, "save flag");
        }

        [Test]
        public void LegacyPlacement_KeepsCatalogFootprint()
        {
            var def = BuildCatalog.Get(BuildCatalog.DecorFishTank);
            var go  = _build.Place(Cell, def, null, QuarterTurn, charge: false);

            Assert.IsNotNull(go);
            Assert.IsTrue(_grid.TryGetObject(Cell + Vector2Int.right, out var entry));
            Assert.AreEqual(def.Size, entry.Size);
            Assert.IsFalse(SaveEntry(entry).footprintRotated);

            var old = JsonUtility.FromJson<SaveData.PlacedItem>(
                $"{{\"catalogId\":\"{BuildCatalog.DecorFishTank}\",\"rotation\":90}}");
            Assert.IsFalse(old.footprintRotated, "old saves default to the catalogue footprint");
            Assert.AreEqual(def.Size, BuildMode.FootprintSize(def, old.footprintRotated));
        }

        /// <summary>Runs SaveLoadController's (internal) per-entry serialiser.</summary>
        private static SaveData.PlacedItem SaveEntry(GridEntry entry)
        {
            var type   = typeof(GameManager).Assembly.GetType("PetShop.Core.SaveLoadController");
            var method = type.GetMethod("BuildPlacedItem", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "SaveLoadController.BuildPlacedItem not found");
            return (SaveData.PlacedItem)method.Invoke(null, new object[] { entry });
        }

        [Test]
        public void PlacingHeld_ConsumesInventoryForFree_ThenExits()
        {
            Hold(BuildCatalog.ShelfSmall, owned: 2);
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.ShelfSmall));

            Assert.IsNotNull(_build.PlaceHeld(Cell));
            Assert.IsTrue(_build.IsHolding, "takes the next owned unit");
            Assert.AreEqual(0, _supply.OwnedCount(BuildCatalog.ShelfSmall));

            Assert.IsNotNull(_build.PlaceHeld(OtherCell));
            Assert.IsFalse(_build.IsHolding);
            Assert.IsFalse(_build.IsActive, "exits when none are left");
            Assert.AreEqual(0, _supply.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(RichBalance, _shop.Balance, Tolerance);
        }

        [Test]
        public void Cancel_ReturnsHeldItem()
        {
            Hold(BuildCatalog.Counter);
            Assert.AreEqual(0, _supply.OwnedCount(BuildCatalog.Counter));
            _build.ExitBuildMode();
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.Counter));
            Assert.IsFalse(_build.EnterPlacement(BuildCatalog.ShelfLarge, null), "none owned");
        }

        [Test]
        public void Remove_WithSupply_ReturnsItemToInventoryWithoutRefund()
        {
            Hold(BuildCatalog.ShelfSmall);
            Assert.IsNotNull(_build.PlaceHeld(Cell));

            _build.RemoveAtWorldPos(_grid.GridToWorld(Cell));

            Assert.IsFalse(_grid.TryGetObject(Cell, out _));
            Assert.AreEqual(1, _supply.OwnedCount(BuildCatalog.ShelfSmall));
            Assert.AreEqual(RichBalance, _shop.Balance, Tolerance);
        }

        [Test]
        public void HeldItemLayer_ExistsAndIsExcludedFromCameraBlocker()
        {
            int layer = LayerMask.NameToLayer(GameLayers.HeldItemLayerName);
            Assert.GreaterOrEqual(layer, 0);
            Assert.AreEqual(layer, GameLayers.HeldItem);
            Assert.AreEqual(0, GameLayers.CameraBlocker.value & (1 << layer));
            Assert.AreEqual(0, GameLayers.InteractMask.value & (1 << layer));
        }

        [Test]
        public void FootprintSwap_RoundsToNearestQuarterTurn()
        {
            Assert.IsFalse(BuildMode.SwapsFootprint(0f));
            Assert.IsTrue(BuildMode.SwapsFootprint(90f));
            Assert.IsTrue(BuildMode.SwapsFootprint(270f));
            Assert.IsTrue(BuildMode.SwapsFootprint(-90f));
            Assert.IsFalse(BuildMode.SwapsFootprint(180f));
        }
    }
}
