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
            Assert.AreEqual(new RectInt(-15, -1, 8, 9), _layout.LotStageCells(ShopLayout.StarterLotStage));
            Assert.AreEqual(new RectInt(-15, -8, 8, 16), _layout.LotStageCells(ShopLayout.BackStripLotStage));
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
                {
                    var cell = new Vector2Int(x, z);
                    // Where the doorway crosses the wall ring the floor stays, so a doorway piece can stand there.
                    bool ring = _layout.IsRoomWallCell(cell);
                    Assert.AreEqual(ring, _grid.HasFloor(cell), $"doorway cell {x},{z} (wall ring: {ring})");
                }
        }

        [Test]
        public void FillFloorGrid_FloorsTheWholeWallRing()
        {
            _layout.FillFloorGrid(_grid);
            RectInt ring = _layout.RoomWallCells();
            for (int x = ring.xMin; x < ring.xMax; x++)
                for (int z = ring.yMin; z < ring.yMax; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (_layout.IsRoomWallCell(cell)) Assert.IsTrue(_grid.HasFloor(cell), $"wall ring cell {x},{z}");
                }
        }

        [Test]
        public void CanPlacePen_YardBesideShop_IsAllowedAtStarterStage()
        {
            _layout.FillFloorGrid(_grid);
            var cell = new Vector2Int(-4, 0);
            Assert.IsFalse(_grid.HasFloor(cell));
            Assert.IsTrue(_layout.CanPlacePen(_grid, cell, new Vector2Int(2, 2)));
        }

        [Test]
        public void CanPlacePen_RoomDoorwayOrOutsideYard_IsRefused()
        {
            Assert.IsFalse(_layout.CanPlacePen(_grid, new Vector2Int(-14, 0), new Vector2Int(2, 2)), "room");
            Assert.IsFalse(_layout.CanPlacePen(_grid, new Vector2Int(-12, 5), Vector2Int.one), "doorway");
            Assert.IsFalse(_layout.CanPlacePen(_grid, new Vector2Int(15, 0), new Vector2Int(2, 2)), "east edge");
            Assert.IsFalse(_layout.CanPlacePen(_grid, new Vector2Int(0, -9), Vector2Int.one), "behind yard");
        }

        [Test]
        public void CanPlacePen_OccupiedCell_IsRefused()
        {
            var cell = new Vector2Int(2, 2);
            _grid.EnsureFloor(cell, Vector2Int.one);
            Assert.IsTrue(_grid.PlaceObject(cell, new PlacedObjectData { Id = "x" }, Vector2Int.one));
            Assert.IsFalse(_layout.CanPlacePen(_grid, new Vector2Int(1, 1), new Vector2Int(2, 2)));
            Assert.IsTrue(_layout.CanPlacePen(_grid, new Vector2Int(3, 3), new Vector2Int(2, 2)));
        }

        [Test]
        public void ReleasePenFloor_DropsFloorOutsideUnlockedLot_KeepsLotFloor()
        {
            _layout.FillFloorGrid(_grid);
            var yardCell = new Vector2Int(-4, 0);
            var lotCell  = new Vector2Int(-10, 0);
            _grid.EnsureFloor(yardCell, new Vector2Int(2, 2));

            _layout.ReleasePenFloor(_grid, yardCell, new Vector2Int(2, 2));
            _layout.ReleasePenFloor(_grid, lotCell, Vector2Int.one);

            Assert.IsFalse(_grid.HasFloor(yardCell), "yard cell outside the starter lot");
            Assert.IsFalse(_grid.CanPlace(yardCell, Vector2Int.one), "no other furniture there");
            Assert.IsTrue(_grid.HasFloor(lotCell), "starter lot keeps its floor");
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
    }
}
