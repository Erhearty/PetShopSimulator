using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for unsnapped placement: free pieces keep their exact position and yaw, hold no
    /// grid cells, and survive the save format.
    /// </summary>
    public class FreePlacementTests
    {
        private GameObject  _go;
        private GridManager _grid;

        [SetUp]
        public void SetUp()
        {
            _go   = new GameObject("Grid");
            _grid = _go.AddComponent<GridManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static PlacedObjectData Def() => new() { Id = "test_shelf", Size = new Vector2Int(2, 1) };

        [Test]
        public void PlaceFree_KeepsExactPositionAndYaw_AndHoldsNoCells()
        {
            var entry = _grid.PlaceFree(Def(), new Vector3(3.37f, 5f, 1.21f), 37.5f);

            Assert.IsTrue(entry.IsFree);
            Assert.AreEqual(3.37f, entry.Position.x, 1e-4f);
            Assert.AreEqual(0f,    entry.Position.y, 1e-4f, "free pieces stand on the floor");
            Assert.AreEqual(1.21f, entry.Position.z, 1e-4f);
            Assert.AreEqual(37.5f, entry.Yaw, 1e-4f);
            Assert.AreEqual(new Vector2Int(1, 0), entry.Root);
            Assert.IsFalse(_grid.TryGetObject(entry.Root, out _), "free pieces hold no grid cell");
            CollectionAssert.Contains(_grid.GetAllPlaced(), entry);
        }

        [Test]
        public void RemoveFree_DropsThePiece_ClearAllToo()
        {
            var a = _grid.PlaceFree(Def(), Vector3.one, 10f);
            _grid.PlaceFree(Def(), new Vector3(8f, 0f, 8f), 20f);

            Assert.IsTrue(_grid.RemoveFree(a));
            Assert.AreEqual(1, _grid.GetAllPlaced().Count);
            _grid.ClearAll();
            Assert.AreEqual(0, _grid.GetAllPlaced().Count);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(33f)]
        public void FreeContains_FollowsTheTurnedFootprint(float yaw)
        {
            var entry = _grid.PlaceFree(Def(), new Vector3(10f, 0f, 10f), yaw);
            var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;   // the 2-cell (4 m) side
            var across = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; // the 1-cell (2 m) side

            Assert.IsTrue(BuildMode.FreeContains(entry,  entry.Position + along * 1.9f));
            Assert.IsFalse(BuildMode.FreeContains(entry, entry.Position + along * 2.1f));
            Assert.IsTrue(BuildMode.FreeContains(entry,  entry.Position + across * 0.9f));
            Assert.IsFalse(BuildMode.FreeContains(entry, entry.Position + across * 1.1f));
        }

        [Test]
        public void FreeCells_CoverTheTurnedFootprint()
        {
            // A 4 m x 2 m footprint centred on a cell corner at yaw 0 touches four cells in a row pair.
            var cells = BuildMode.FreeCells(new Vector3(4f, 0f, 4f), 0f, new Vector2Int(2, 1));
            CollectionAssert.AreEquivalent(new[]
            {
                new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2),
            }, cells);
        }

        [Test]
        public void PlacedItem_RoundTripsFreePositionAndRotation()
        {
            var item = new SaveData.PlacedItem
            {
                catalogId = "test_shelf", free = true, posX = 7.31f, posZ = -2.64f, rotation = 123.4f,
            };

            var back = JsonUtility.FromJson<SaveData.PlacedItem>(JsonUtility.ToJson(item));

            Assert.IsTrue(back.free);
            Assert.AreEqual(7.31f,  back.posX,     1e-4f);
            Assert.AreEqual(-2.64f, back.posZ,     1e-4f);
            Assert.AreEqual(123.4f, back.rotation, 1e-3f);
        }

        [Test]
        public void IsFreeItem_IncludesBuildingPieces()
        {
            Assert.IsTrue(BuildMode.IsFreeItem(Def()));
            Assert.IsTrue(BuildMode.IsFreeItem(BuildCatalog.Get(BuildCatalog.Wall)));
            Assert.IsTrue(BuildMode.IsFreeItem(BuildCatalog.Get(BuildCatalog.Fence)));
            Assert.IsTrue(BuildMode.IsFreeItem(BuildCatalog.Get(BuildCatalog.WallDoor)));
            Assert.IsFalse(BuildMode.IsFreeItem(null));
        }

        [Test]
        public void FreeWall_ReportsThinFootprintAndCentreLineCells()
        {
            var wall = BuildCatalog.Get(BuildCatalog.Wall);
            var entry = _grid.PlaceFree(wall, new Vector3(4f, 0f, 4.5f), 0f);

            // A 2 m wall along X centred at (4, 4.5) runs through cells x=1 and x=2 on row 2 only.
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 2), new Vector2Int(2, 2) },
                                           BuildMode.FreeCellsFor(wall, entry.Position, entry.Yaw));
            Assert.IsTrue(BuildMode.FreeContains(entry,  entry.Position + Vector3.right * 0.9f));
            Assert.IsFalse(BuildMode.FreeContains(entry, entry.Position + Vector3.forward * 1f), "walls are thin");
        }

        [Test]
        public void FreeWall_DoesNotBlockCellsForOtherPieces()
        {
            _grid.PlaceFree(BuildCatalog.Get(BuildCatalog.Wall), new Vector3(1f, 0f, 1f), 33f);
            Assert.IsFalse(_grid.CoveredByFree(new Vector2Int(0, 0)));

            _grid.PlaceFree(Def(), new Vector3(1f, 0f, 1f), 0f);
            Assert.IsTrue(_grid.CoveredByFree(new Vector2Int(0, 0)));
        }

        [Test]
        public void FreePlacement_RaisesGridEvents_SoTheRoofCanFollow()
        {
            int placed = 0, removed = 0;
            _grid.OnObjectPlaced.AddListener((_, __) => placed++);
            _grid.OnObjectRemoved.AddListener(_ => removed++);

            var entry = _grid.PlaceFree(BuildCatalog.Get(BuildCatalog.Wall), new Vector3(3.3f, 0f, 2.2f), 12f);
            Assert.AreEqual(1, placed);
            Assert.IsTrue(_grid.RemoveFree(entry));
            Assert.AreEqual(1, removed);
            Assert.IsFalse(_grid.RemoveFree(entry));
            Assert.AreEqual(1, removed);
        }
    }
}
