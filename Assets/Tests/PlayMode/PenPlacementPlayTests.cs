using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for pen placement: pens are refused inside the shop room, a fresh pen
    /// arrives with its adult breeding pair, and a pen packed away and placed again brings none.
    /// </summary>
    public class PenPlacementPlayTests
    {
        private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
        private const float  RichBalance = 5000f;
        private const Pet.Species PenSpecies = Pet.Species.Dog;
        /// <summary>A yard cell well clear of the default room footprint.</summary>
        private static readonly Vector2Int YardCell   = new(2, 2);
        /// <summary>Offset into the room so the whole pen footprint lies inside it.</summary>
        private static readonly Vector2Int RoomOffset = new(1, 1);
        /// <summary>A yard cell east of the shop, outside the room's x-column and the starter lot.</summary>
        private static readonly Vector2Int BesideShopCell = new(-4, 0);
        /// <summary>A second yard cell, in the back of the yard, for re-placing a packed pen.</summary>
        private static readonly Vector2Int BackYardCell   = new(6, -6);

        private FurniturePrefabs _saved;
        private GameObject       _root;
        private GridManager      _grid;
        private ShopLayout       _layout;
        private FurnitureSupply  _supply;
        private BuildMode        _build;
        private string           _penId;
        private PlacedObjectData _penDef;

        [SetUp]
        public void SetUp()
        {
            _saved = FurnitureFactory.Prefabs;
#if UNITY_EDITOR
            FurnitureFactory.Prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath);
#endif
            if (FurnitureFactory.Prefabs == null) Assert.Ignore("Furniture prefabs are only loadable in the editor.");

            _root   = new GameObject("pen-play-test");
            _grid   = _root.AddComponent<GridManager>();
            _layout = _root.AddComponent<ShopLayout>();
            var shop = _root.AddComponent<ShopManager>();
            shop.SetBalance(RichBalance);

            _supply = new FurnitureSupply();
            _build  = _root.AddComponent<BuildMode>();
            _build.GridManager = _grid; _build.Shop = shop; _build.ObjectRoot = _root.transform;
            _build.Supply = _supply; _build.Layout = _layout;

            _penId  = BuildCatalog.PenIdFor(PenSpecies);
            _penDef = BuildCatalog.Get(_penId);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            FurnitureFactory.Prefabs = _saved;
        }

        /// <summary>A pen aimed inside the shop room is refused with the yard notice and stays in hand.</summary>
        [UnityTest]
        public IEnumerator PlaceHeld_InsideRoom_IsRefused()
        {
            var cell = _layout.RoomCells().position + RoomOffset;
            _grid.EnsureFloor(cell, _penDef.Size);
            string notice = null;
            _build.OnBuildMessage.AddListener(m => notice = m);

            _supply.AddOwned(_penId);
            Assert.IsTrue(_build.EnterPlacement(_penId));
            Assert.IsNull(_build.PlaceHeld(cell));
            yield return null;

            Assert.AreEqual(BuildMode.PenInShopNotice, notice);
            Assert.IsFalse(_grid.TryGetObject(cell, out _));
            Assert.IsTrue(_build.IsHolding);
        }

        /// <summary>A freshly delivered pen placed in the yard holds two adults of its species.</summary>
        [UnityTest]
        public IEnumerator PlaceHeld_FreshPenInYard_HasAdultPair()
        {
            Assert.IsFalse(_layout.IsInsideShop(YardCell, _penDef.Size));
            var pen = PlaceFromInventory();
            yield return null;

            Assert.AreEqual(BuildCatalog.PenStarterPairSize, pen.Count);
            foreach (var pet in pen.Residents)
            {
                Assert.AreEqual(PenSpecies, pet.species);
                Assert.IsTrue(pet.IsAdult);
                Assert.GreaterOrEqual(pet.ageDays, pet.daysToMature);
            }
        }

        /// <summary>Packing an emptied pen away and placing it again adds no animals.</summary>
        [UnityTest]
        public IEnumerator PackAwayAndReplace_AddsNoPets()
        {
            var pen = PlaceFromInventory();
            yield return null;
            foreach (var pet in new System.Collections.Generic.List<Pet>(pen.Residents)) pen.RemovePet(pet);

            _build.RemoveAtWorldPos(_grid.GridToWorld(YardCell));
            yield return null;
            Assert.AreEqual(1, _supply.PackedCount(_penId));

            var again = PlaceFromInventory();
            yield return null;

            Assert.AreEqual(0, again.Count);
            Assert.AreEqual(0, _supply.PackedCount(_penId));
        }

        /// <summary>At the starter lot stage a pen fits on bare yard beside the shop, with no floor laid there.</summary>
        [UnityTest]
        public IEnumerator PlaceHeld_StarterLot_YardBesideShop_IsPlaced()
        {
            _layout.FillFloorGrid(_grid);
            Assert.IsFalse(_grid.HasFloor(BesideShopCell));
            var go = HoldAndPlace(BesideShopCell);
            yield return null;

            Assert.IsNotNull(go);
            Assert.IsTrue(_grid.TryGetObject(BesideShopCell, out var entry));
            Assert.AreEqual(_penId, entry.Data.Id);
        }

        /// <summary>At the starter lot stage a pen aimed inside the shop room is refused with the yard notice.</summary>
        [UnityTest]
        public IEnumerator PlaceHeld_StarterLot_InsideRoom_IsRefused()
        {
            _layout.FillFloorGrid(_grid);
            var cell = _layout.RoomCells().position + RoomOffset;
            AssertRefused(cell);
            yield return null;
        }

        /// <summary>At the starter lot stage a pen hanging past the yard edge is refused.</summary>
        [UnityTest]
        public IEnumerator PlaceHeld_StarterLot_OutsideYard_IsRefused()
        {
            _layout.FillFloorGrid(_grid);
            RectInt yard = _layout.LotStageCells(ShopLayout.FullYardLotStage);
            AssertRefused(new Vector2Int(yard.xMax, 0));
            yield return null;
        }

        /// <summary>At the starter lot stage a packed pen goes back down on another yard cell.</summary>
        [UnityTest]
        public IEnumerator PackAwayAndReplace_StarterLot_OtherYardCell_IsPlaced()
        {
            _layout.FillFloorGrid(_grid);
            var pen = HoldAndPlace(BesideShopCell).GetComponent<PetPen>();
            yield return null;
            foreach (var pet in new System.Collections.Generic.List<Pet>(pen.Residents)) pen.RemovePet(pet);
            _build.RemoveAtWorldPos(_grid.GridToWorld(BesideShopCell));
            yield return null;
            Assert.AreEqual(1, _supply.PackedCount(_penId));

            var again = HoldAndPlace(BackYardCell);
            yield return null;

            Assert.IsNotNull(again);
            Assert.IsFalse(_grid.TryGetObject(BesideShopCell, out _));
            Assert.AreEqual(0, again.GetComponent<PetPen>().Count);
            Assert.AreEqual(0, _supply.PackedCount(_penId));
        }

        /// <summary>Takes one pen into the hand (adding a fresh one if none is owned) and places it at <paramref name="cell"/>, with no floor prepared.</summary>
        private GameObject HoldAndPlace(Vector2Int cell)
        {
            if (_supply.OwnedCount(_penId) == 0 && _supply.PackedCount(_penId) == 0) _supply.AddOwned(_penId);
            Assert.IsTrue(_build.EnterPlacement(_penId));
            return _build.PlaceHeld(cell);
        }

        /// <summary>Asserts a held pen aimed at <paramref name="cell"/> is refused with the yard notice and stays in hand.</summary>
        private void AssertRefused(Vector2Int cell)
        {
            string notice = null;
            _build.OnBuildMessage.AddListener(m => notice = m);
            Assert.IsNull(HoldAndPlace(cell));
            Assert.AreEqual(BuildMode.PenInShopNotice, notice);
            Assert.IsFalse(_grid.TryGetObject(cell, out _));
            Assert.IsTrue(_build.IsHolding);
        }

        /// <summary>Takes one pen from the inventory (adding a fresh one if none is owned) and places it in the yard.</summary>
        private PetPen PlaceFromInventory()
        {
            if (_supply.OwnedCount(_penId) == 0) _supply.AddOwned(_penId);
            _grid.EnsureFloor(YardCell, _penDef.Size);
            Assert.IsTrue(_build.EnterPlacement(_penId));
            var go = _build.PlaceHeld(YardCell);
            Assert.IsNotNull(go);
            return go.GetComponent<PetPen>();
        }
    }
}
