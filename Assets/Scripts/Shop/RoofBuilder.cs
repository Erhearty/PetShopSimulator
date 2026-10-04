using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the shop's flat roof: a slab at <see cref="ShopLayout.WallHeight"/> over the shell footprint,
    /// plus the cells under and enclosed by player-built walls. Rebuilt whenever a wall is placed or
    /// removed. The roof is pure visuals: no colliders, on the Scenery layer, so interaction raycasts
    /// and the NavMesh never see it.
    /// </summary>
    public class RoofBuilder : MonoBehaviour
    {
        private const float Thickness = 0.2f;
        private const float Overhang  = 0.3f;
        private const float CellInset = 0.01f;
        private const string WallType = "wall";

        private ShopLayout   _layout;
        private GridManager  _grid;
        private MeshFilter   _filter;
        private MeshRenderer _renderer;

        /// <summary>The roof renderer, for bounds checks.</summary>
        public MeshRenderer Renderer => _renderer;

        /// <summary>Binds to the layout and grid, builds the roof once and follows wall changes.</summary>
        public void Init(ShopLayout layout, GridManager grid)
        {
            _layout = layout;
            _grid   = grid;

            if (_filter == null)
            {
                _filter   = gameObject.AddComponent<MeshFilter>();
                _renderer = gameObject.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = MaterialFactory.Get("roof", new Color(0.32f, 0.28f, 0.27f), 0f, 0.2f);
                MeshBuilder.StripColliders(gameObject);
                MeshBuilder.SetLayerRecursive(gameObject, GameLayers.Scenery);
            }

            if (_grid != null)
            {
                _grid.OnObjectPlaced.AddListener(OnPlaced);
                _grid.OnObjectRemoved.AddListener(OnRemoved);
            }
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_grid == null) return;
            _grid.OnObjectPlaced.RemoveListener(OnPlaced);
            _grid.OnObjectRemoved.RemoveListener(OnRemoved);
        }

        private void OnPlaced(Vector2Int cell, PlacedObjectData data)
        {
            if (data != null && data.Type == WallType) Rebuild();
        }

        // The removed entry is already gone, so its type is unknown; rebuilding is cheap.
        private void OnRemoved(Vector2Int cell) => Rebuild();

        /// <summary>Regenerates the roof mesh from the shell footprint and the placed walls.</summary>
        public void Rebuild()
        {
            if (_layout == null || _filter == null) return;

            var verts = new List<Vector3>();
            var tris  = new List<int>();

            Vector3 c = _layout.ShopCentre;
            float hw = _layout.RoomWidth * 0.5f + Overhang;
            float hd = _layout.RoomDepth * 0.5f + Overhang;
            float y  = _layout.WallHeight;
            AddBox(verts, tris, c.x - hw, c.x + hw, c.z - hd, c.z + hd, y, y + Thickness);

            float cs = GridManager.CellSize;
            foreach (Vector2Int cell in RoofedCells())
            {
                Vector3 p = _grid.GridToWorld(cell);
                // Cells whose centre is already under the shell slab would only z-fight with it.
                if (Mathf.Abs(p.x - c.x) <= hw && Mathf.Abs(p.z - c.z) <= hd) continue;
                float h = cs * 0.5f;
                AddBox(verts, tris, p.x - h, p.x + h, p.z - h, p.z + h, y - CellInset, y + Thickness - CellInset);
            }

            var mesh = _filter.sharedMesh;
            if (mesh == null) { mesh = new Mesh { name = "Roof" }; _filter.sharedMesh = mesh; }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>Wall cells plus every empty cell the walls fully enclose.</summary>
        private HashSet<Vector2Int> RoofedCells()
        {
            var walls = new HashSet<Vector2Int>();
            if (_grid != null)
                foreach (var entry in _grid.GetAllPlaced())
                {
                    if (entry.Data == null || entry.Data.Type != WallType) continue;
                    for (int x = 0; x < entry.Size.x; x++)
                        for (int z = 0; z < entry.Size.y; z++)
                            walls.Add(entry.Root + new Vector2Int(x, z));
                }
            if (walls.Count == 0) return walls;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var w in walls)
            {
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
            }
            minX--; minY--; maxX++; maxY++;

            // Flood the outside from a corner; whatever it cannot reach is enclosed.
            var outside = new HashSet<Vector2Int>();
            var stack   = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(minX, minY));
            outside.Add(new Vector2Int(minX, minY));
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (n.x < minX || n.x > maxX || n.y < minY || n.y > maxY) continue;
                    if (walls.Contains(n) || !outside.Add(n)) continue;
                    stack.Push(n);
                }
            }

            var roofed = new HashSet<Vector2Int>(walls);
            for (int x = minX; x <= maxX; x++)
                for (int z = minY; z <= maxY; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!outside.Contains(cell)) roofed.Add(cell);
                }
            return roofed;
        }

        private static void AddBox(List<Vector3> v, List<int> t, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            var a = new Vector3(x0, y0, z0); var b = new Vector3(x1, y0, z0);
            var c = new Vector3(x1, y0, z1); var d = new Vector3(x0, y0, z1);
            var e = new Vector3(x0, y1, z0); var f = new Vector3(x1, y1, z0);
            var g = new Vector3(x1, y1, z1); var h = new Vector3(x0, y1, z1);
            Quad(v, t, e, h, g, f);   // top
            Quad(v, t, a, b, c, d);   // bottom
            Quad(v, t, a, e, f, b);   // -z
            Quad(v, t, c, g, h, d);   // +z
            Quad(v, t, d, h, e, a);   // -x
            Quad(v, t, b, f, g, c);   // +x
        }

        // Winding is clockwise seen from outside (Unity front face).
        private static void Quad(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
    }
}
