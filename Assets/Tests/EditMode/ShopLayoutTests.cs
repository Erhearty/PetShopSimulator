using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PetShop.Shop;
using PetShop.Core;
using PetShop.Commerce;

namespace PetShop.Tests
{
    public class ShopLayoutTests
    {
        private GameObject  _go;
        private ShopLayout  _layout;
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _go     = new GameObject("Layout");
            _layout = _go.AddComponent<ShopLayout>();
            _grid   = _go.AddComponent<GridManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void DefaultAnchors_MatchGeneratedLayout()
        {
            Assert.AreEqual(-22f,  _layout.ShopCentre.x, 1e-4f);
            Assert.AreEqual(4f,    _layout.ShopCentre.z, 1e-4f);
            Assert.AreEqual(8.4f,  _layout.DoorPosition.z, 1e-4f);
            Assert.AreEqual(12.5f, _layout.ForecourtPosition.z, 1e-4f);
            Assert.AreEqual(17f,   _layout.YardFrontZ, 1e-4f);
        }

        [Test]
        public void LotStageCells_MatchDefaultDimensions()
        {
            Assert.AreEqual(new RectInt(-15, -5, 28, 13), _layout.LotStageCells(ShopLayout.StarterLotStage));
            Assert.AreEqual(new RectInt(-15, -8, 28, 16), _layout.LotStageCells(ShopLayout.BackStripLotStage));
            Assert.AreEqual(new RectInt(-16, -8, 32, 16), _layout.LotStageCells(ShopLayout.FullYardLotStage));
        }

        [Test]
        public void FillFloorGrid_ClearsDoorway()
        {
            _layout.FillFloorGrid(_grid);
            Assert.IsTrue(_grid.HasFloor(new Vector2Int(-13, 4)));
            Assert.IsTrue(_grid.HasFloor(new Vector2Int(-10, 4)));
            for (int x = -12; x <= -11; x++)
                for (int z = 3; z <= 6; z++)
                    Assert.IsFalse(_grid.HasFloor(new Vector2Int(x, z)), $"doorway cell {x},{z}");
        }

        [Test]
        public void ApplyLotStage_OnlyGrows()
        {
            _layout.FillFloorGrid(_grid);
            Assert.IsFalse(_grid.HasFloor(new Vector2Int(-16, -8)));
            _layout.ApplyLotStage(ShopLayout.FullYardLotStage);
            Assert.IsTrue(_grid.HasFloor(new Vector2Int(-16, -8)));
            Assert.IsFalse(_grid.HasFloor(new Vector2Int(-12, 4)));
        }

        [Test]
        public void StarterLayout_ReadsMarkersAtTheirCells()
        {
            Mark(BuildCatalog.Counter, new Vector2Int(-12, -1), null, 0f);
            Mark(BuildCatalog.ShelfLarge, new Vector2Int(-15, 1), "Food", 90f);
            Mark(BuildCatalog.PetPen, new Vector2Int(2, 1), "Dog", 0f);

            var list = _layout.StarterLayout(_grid);
            Assert.AreEqual(3, list.Count);
            var byId = new Dictionary<string, ShopLayout.FurniturePlacement>();
            foreach (var p in list) byId[p.CatalogId] = p;

            Assert.AreEqual(new Vector2Int(-12, -1), byId[BuildCatalog.Counter].Cell);
            Assert.IsNull(byId[BuildCatalog.Counter].Variant);
            Assert.AreEqual(new Vector2Int(-15, 1), byId[BuildCatalog.ShelfLarge].Cell);
            Assert.AreEqual("Food", byId[BuildCatalog.ShelfLarge].Variant);
            Assert.AreEqual(90f, byId[BuildCatalog.ShelfLarge].Rotation);
            Assert.AreEqual(new Vector2Int(2, 1), byId[BuildCatalog.PetPen].Cell);
            Assert.AreEqual("Dog", byId[BuildCatalog.PetPen].Variant);
        }

        private void Mark(string id, Vector2Int cell, string variant, float rot)
        {
            var go = new GameObject("Starter_" + id);
            go.transform.SetParent(_go.transform, false);
            go.transform.position = _grid.GridToWorld(cell);
            var piece = go.AddComponent<StarterPiece>();
            piece.CatalogId = id;
            piece.Variant   = variant;
            piece.Rotation  = rot;
        }
    }
}
