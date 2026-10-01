using NUnit.Framework;
using UnityEngine;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="GridManager.EnsureFloor"/>, which keeps saved pieces
    /// placeable when they sit outside the current lot stage.
    /// </summary>
    public class GridManagerTests
    {
        private static readonly Vector2Int Origin    = new(3, -2);
        private static readonly Vector2Int PenSize   = new(2, 3);
        private static readonly Vector2Int Elsewhere = new(10, 10);

        private GameObject  _go;
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _go   = new GameObject("grid");
            _grid = _go.AddComponent<GridManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void OutsideFloor_CannotPlace()
        {
            Assert.IsFalse(_grid.CanPlace(Origin, PenSize));
        }

        [Test]
        public void EnsureFloor_MakesWholeFootprintPlaceable()
        {
            _grid.EnsureFloor(Origin, PenSize);
            Assert.IsTrue(_grid.CanPlace(Origin, PenSize));
            Assert.IsTrue(_grid.HasFloor(Origin + PenSize - Vector2Int.one));
        }

        [Test]
        public void EnsureFloor_LeavesOtherCellsUnbuildable()
        {
            _grid.EnsureFloor(Origin, PenSize);
            Assert.IsFalse(_grid.HasFloor(Elsewhere));
            Assert.IsFalse(_grid.HasFloor(Origin + new Vector2Int(PenSize.x, 0)));
        }

        [Test]
        public void EnsureFloor_DoesNotFreeOccupiedCells()
        {
            _grid.EnsureFloor(Origin, PenSize);
            var def = new PlacedObjectData { Id = "pen", Type = "pen", Size = PenSize };
            Assert.IsTrue(_grid.PlaceObject(Origin, def, PenSize));
            _grid.EnsureFloor(Origin, PenSize);
            Assert.IsFalse(_grid.CanPlace(Origin, PenSize));
        }
    }
}
