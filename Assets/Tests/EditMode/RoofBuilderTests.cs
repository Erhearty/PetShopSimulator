using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for <see cref="RoofBuilder"/>: it grows over built walls and never collides.</summary>
    public class RoofBuilderTests
    {
        private GameObject  _go;
        private GridManager _grid;
        private RoofBuilder _roof;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("roofTest");
            var layout = _go.AddComponent<ShopLayout>();
            _grid = _go.AddComponent<GridManager>();
            _grid.FillFloorRect(new Vector2Int(-40, -40), new Vector2Int(80, 80));
            _roof = new GameObject("Roof").AddComponent<RoofBuilder>();
            _roof.Init(layout, _grid);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_roof.gameObject);
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void PlacingWallOutsideShell_GrowsRoofBounds()
        {
            Bounds before = _roof.Renderer.bounds;
            var wall = new PlacedObjectData { Id = "wall", Type = "wall", Size = Vector2Int.one };

            Assert.IsTrue(_grid.PlaceObject(new Vector2Int(20, 0), wall, Vector2Int.one));

            Bounds after = _roof.Renderer.bounds;
            Assert.Greater(after.size.x, before.size.x);
            Assert.GreaterOrEqual(after.max.x, _grid.GridToWorld(new Vector2Int(20, 0)).x);
        }

        [Test]
        public void Roof_HasNoColliders_AndIsOnScenery()
        {
            var wall = new PlacedObjectData { Id = "wall", Type = "wall", Size = Vector2Int.one };
            _grid.PlaceObject(new Vector2Int(20, 0), wall, Vector2Int.one);

            Assert.AreEqual(0, _roof.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(GameLayers.Scenery, _roof.gameObject.layer);
        }

        [Test]
        public void RoofBottom_SitsOnWallPieceTops()
        {
            Assert.AreEqual(ShopLayout.WallPieceHeight, _roof.Renderer.bounds.min.y, 1e-4f);
        }

        [Test]
        public void PlacingWindowWall_RebuildsRoof()
        {
            Bounds before = _roof.Renderer.bounds;
            var window = new PlacedObjectData
                { Id = BuildCatalog.WallWindow, Type = BuildCatalog.WallWindow, Size = Vector2Int.one };

            Assert.IsTrue(_grid.PlaceObject(new Vector2Int(20, 0), window, Vector2Int.one));

            Assert.Greater(_roof.Renderer.bounds.size.x, before.size.x);
        }

        [Test]
        public void BuildView_HidesRoof_AndExitRestoresIt()
        {
            var camGo = new GameObject("BuildCam") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            var viewGo = new GameObject("BuildView");
            var view = viewGo.AddComponent<BuildCamera>();
            try
            {
                Assert.AreSame(_roof, RoofBuilder.Instance);
                view.Init(null, null, null);

                view.Enter();
                Assert.IsTrue(view.IsActive);
                Assert.IsFalse(_roof.Renderer.enabled);

                view.Exit();
                Assert.IsFalse(view.IsActive);
                Assert.IsTrue(_roof.Renderer.enabled);
            }
            finally
            {
                Object.DestroyImmediate(viewGo);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
