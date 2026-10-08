using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Player;
using PetShop.Progression;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FurnitureFactory.Spawn"/> instantiating the baked furniture
    /// prefabs. No scene is loaded: the catalogue asset is supplied through AssetDatabase.
    /// </summary>
    public class FurnitureFactoryTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const string DecorFolder = "Assets/Prefabs/Decor/";
        private const float  MinDecorCost = 20f;
        private const float  MaxDecorCost = 120f;
        private const int    DefaultLayer = 0;
        private static readonly Vector2Int Cell = new(2, -1);
        private const float Yaw = 90f;

        private GameObject       _gridGo;
        private GridManager      _grid;
        private FurniturePrefabs _saved;
        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            _saved  = FurnitureFactory.Prefabs;
            _gridGo = new GameObject("grid");
            _grid   = _gridGo.AddComponent<GridManager>();
            FurnitureFactory.Prefabs = AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
            Assert.IsNotNull(FurnitureFactory.Prefabs, $"No FurniturePrefabs asset at {CatalogPath}.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            Object.DestroyImmediate(_gridGo);
            FurnitureFactory.Prefabs = _saved;
        }

        private GameObject Spawn(string id, string variant = null, float yaw = 0f)
        {
            var go = FurnitureFactory.Spawn(BuildCatalog.Get(id), Cell, variant, _grid, null, yaw);
            if (go != null) _spawned.Add(go);
            return go;
        }

        [Test]
        public void EveryCatalogItem_HasABakedPrefab()
        {
            foreach (string id in BuildCatalog.Items.Keys)
                Assert.IsNotNull(FurnitureFactory.Prefabs.Get(id), $"No prefab for '{id}'.");
        }

        [Test]
        public void Spawn_EveryFurnitureItem_IsPlacedRotatedAndOnTheFurnitureLayer()
        {
            foreach (var def in BuildCatalog.Items.Values)
            {
                if (def.Category == BuildCategory.Decoration) continue;
                var go = Spawn(def.Id, null, Yaw);
                Assert.IsNotNull(go, $"{def.Id}: Spawn returned null.");
                Assert.AreEqual($"Placed_{def.Id}_{Cell.x}_{Cell.y}", go.name);
                Assert.AreEqual(_grid.FootprintCenter(Cell, def.Size), go.transform.position, $"{def.Id}: position.");
                Assert.AreEqual(Yaw, go.transform.eulerAngles.y, 0.01f, $"{def.Id}: rotation.");
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    Assert.AreEqual(GameLayers.Furniture, t.gameObject.layer, $"{def.Id}: layer of {t.name}.");
                Assert.IsNotEmpty(go.GetComponentsInChildren<Renderer>(true), $"{def.Id}: no visuals.");
            }
        }

        [Test]
        public void DecorEntries_AreCheapDescribedDecorations()
        {
            int decor = 0;
            foreach (var def in BuildCatalog.Items.Values)
            {
                Assert.IsNotEmpty(def.Description, $"{def.Id}: description.");
                if (def.Category != BuildCategory.Decoration) continue;
                decor++;
                Assert.AreEqual(BuildCatalog.DecorationType, def.Type, $"{def.Id}: type.");
                Assert.That(def.Cost, Is.InRange(MinDecorCost, MaxDecorCost), $"{def.Id}: cost.");
                Assert.IsTrue(BuildCatalog.IsDecoration(def.Id));
            }
            Assert.Greater(decor, 0, "no decoration entries");
        }

        [Test]
        public void EveryDecorItem_HasAPrefabInTheDecorFolder()
        {
            foreach (var def in BuildCatalog.Items.Values)
            {
                if (def.Category != BuildCategory.Decoration) continue;
                var prefab = FurnitureFactory.Prefabs.Get(def.Id);
                Assert.IsNotNull(prefab, $"No prefab for '{def.Id}'.");
                Assert.AreEqual($"{DecorFolder}{def.Id}.prefab", AssetDatabase.GetAssetPath(prefab), def.Id);
            }
        }

        [Test]
        public void Spawn_Decoration_IsDefaultLayerWithoutBehaviour()
        {
            foreach (var def in BuildCatalog.Items.Values)
            {
                if (def.Category != BuildCategory.Decoration) continue;
                var go = Spawn(def.Id, null, Yaw);
                Assert.IsNotNull(go, $"{def.Id}: Spawn returned null.");
                Assert.AreEqual(_grid.FootprintCenter(Cell, def.Size), go.transform.position, $"{def.Id}: position.");
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    Assert.AreEqual(DefaultLayer, t.gameObject.layer, $"{def.Id}: layer of {t.name}.");
                Assert.IsEmpty(go.GetComponentsInChildren<ShelfUnit>(true), $"{def.Id}: ShelfUnit.");
                Assert.IsEmpty(go.GetComponentsInChildren<PetPen>(true), $"{def.Id}: PetPen.");
                Assert.IsEmpty(go.GetComponentsInChildren<CounterInteractable>(true), $"{def.Id}: counter.");
                Assert.IsNotEmpty(go.GetComponentsInChildren<Renderer>(true), $"{def.Id}: no visuals.");
            }
        }

        [Test]
        public void Spawn_Shelf_AppliesCategoryVariant()
        {
            var small = Spawn(BuildCatalog.ShelfSmall, nameof(ProductCategory.Toy));
            var large = Spawn(BuildCatalog.ShelfLarge);

            var unit = small.GetComponent<ShelfUnit>();
            Assert.IsNotNull(unit);
            Assert.AreEqual(ProductCategory.Toy, unit.Category);
            Assert.AreEqual(4, unit.MaxPerLine);

            var largeUnit = large.GetComponent<ShelfUnit>();
            Assert.IsNotNull(largeUnit);
            Assert.AreEqual(ProductCategory.Food, largeUnit.Category, "default category");
            Assert.AreEqual(6, largeUnit.MaxPerLine);
        }

        [Test]
        public void Spawn_Pen_AppliesSpeciesVariant()
        {
            var pen = Spawn(BuildCatalog.PetPen, nameof(Pet.Species.Dog)).GetComponent<PetPen>();
            Assert.IsNotNull(pen);
            Assert.AreEqual(Pet.Species.Dog, pen.PenSpecies);
            Assert.AreEqual(4, pen.Capacity);

            var fallback = Spawn(BuildCatalog.PetPen).GetComponent<PetPen>();
            Assert.AreEqual(Pet.Species.Rabbit, fallback.PenSpecies, "default species");
        }

        [Test]
        public void SpeciesPens_ExistForEveryPickableSpecies_WithPairPricing()
        {
            foreach (var species in ProgressionRules.PickableSpecies(ProgressionRules.MaxTier))
            {
                string id = BuildCatalog.PenIdFor(species);
                Assert.AreEqual(BuildCatalog.SpeciesPenPrefix + species.ToString().ToLowerInvariant(), id);
                var def = BuildCatalog.Get(id);
                Assert.IsNotNull(def, $"{id}: missing.");
                Assert.AreEqual(BuildCatalog.PenType, def.Type, $"{id}: type.");
                Assert.AreEqual(BuildCategory.Furniture, def.Category, $"{id}: category.");
                Assert.AreEqual(BuildCatalog.PenFootprintFor(species), def.Size, $"{id}: size.");
                Assert.IsFalse(def.Hidden, $"{id}: hidden.");
                float expected = BuildCatalog.Get(BuildCatalog.PetPen).Cost + 2 * Pet.WholesalePrice(species);
                Assert.AreEqual(expected, def.Cost, 0.001f, $"{id}: cost.");
                StringAssert.Contains("breeding pair", def.Description, $"{id}: description.");
                Assert.AreEqual(species, ProgressionRules.PenSpeciesFor(id), $"{id}: species.");
            }
        }

        [Test]
        public void SpeciesPens_HaveDistinctTints()
        {
            var tints = new HashSet<Color>();
            foreach (var species in ProgressionRules.PickableSpecies(ProgressionRules.MaxTier))
                Assert.IsTrue(tints.Add(BuildCatalog.Get(BuildCatalog.PenIdFor(species)).Tint), $"{species}: tint reused.");
        }

        [Test]
        public void LegacyPen_IsHiddenAndHasNoSpeciesFromId()
        {
            Assert.IsTrue(BuildCatalog.Get(BuildCatalog.PetPen).Hidden);
            Assert.IsNull(ProgressionRules.PenSpeciesFor(BuildCatalog.PetPen));
            Assert.IsNull(ProgressionRules.PenSpeciesFor(BuildCatalog.Counter));
        }

        [Test]
        public void Spawn_SpeciesPen_TakesSpeciesFromId()
        {
            var pen = Spawn(BuildCatalog.PenIdFor(Pet.Species.Deer)).GetComponent<PetPen>();
            Assert.IsNotNull(pen);
            Assert.AreEqual(Pet.Species.Deer, pen.PenSpecies);
            Assert.AreEqual(4, pen.Capacity);
        }

        [Test]
        public void IsPenUnlocked_FollowsTiers()
        {
            string dog = BuildCatalog.PenIdFor(Pet.Species.Dog);
            string horse = BuildCatalog.PenIdFor(Pet.Species.Horse);
            string tiger = BuildCatalog.PenIdFor(Pet.Species.Tiger);
            Assert.IsTrue(ProgressionRules.IsPenUnlocked(dog, ProgressionRules.CornerShopTier));
            Assert.IsFalse(ProgressionRules.IsPenUnlocked(horse, ProgressionRules.HorseTier - 1));
            Assert.IsTrue(ProgressionRules.IsPenUnlocked(horse, ProgressionRules.HorseTier));
            Assert.IsFalse(ProgressionRules.IsPenUnlocked(tiger, ProgressionRules.TigerTier - 1));
            Assert.IsTrue(ProgressionRules.IsPenUnlocked(tiger, ProgressionRules.TigerTier));
        }

        [Test]
        public void Spawn_Counter_IsInteractable()
        {
            Assert.IsNotNull(Spawn(BuildCatalog.Counter).GetComponent<CounterInteractable>());
        }

        [Test]
        public void Spawn_WithoutCatalog_LogsErrorAndReturnsNull()
        {
            FurnitureFactory.Prefabs = null;
            LogAssert.Expect(LogType.Error, new Regex("No baked prefab"));
            Assert.IsNull(Spawn(BuildCatalog.Wall));
        }
    }
}
