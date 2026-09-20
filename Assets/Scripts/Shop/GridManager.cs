using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PetShop.Shop
{
    /// <summary>
    /// Core grid system — 3D, XZ plane. CellSize is in world-space metres and Y is
    /// always floor level (0).
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public const float CellSize = 2f;

        public UnityEvent<Vector2Int, PlacedObjectData> OnObjectPlaced  = new();
        public UnityEvent<Vector2Int>                   OnObjectRemoved = new();
        public UnityEvent<Vector2Int, bool>             OnFloorChanged  = new();

        private readonly Dictionary<Vector2Int, GridEntry> _grid       = new();
        private readonly HashSet<Vector2Int>               _floorCells = new();

        // ── Coordinate conversion ───────────────────────────────────────────────

        public Vector2Int WorldToGrid(Vector3 worldPos) => new(
            Mathf.FloorToInt(worldPos.x / CellSize),
            Mathf.FloorToInt(worldPos.z / CellSize));

        /// <summary>Centre of a single cell, at floor level.</summary>
        public Vector3 GridToWorld(Vector2Int cell) => new(
            cell.x * CellSize + CellSize * 0.5f,
            0f,
            cell.y * CellSize + CellSize * 0.5f);

        /// <summary>Centre of a multi-cell footprint rooted at <paramref name="cell"/>.</summary>
        public Vector3 FootprintCenter(Vector2Int cell, Vector2Int size)
        {
            Vector3 root = GridToWorld(cell);
            return new Vector3(
                root.x + (size.x - 1) * CellSize * 0.5f,
                0f,
                root.z + (size.y - 1) * CellSize * 0.5f);
        }

        // ── Queries ─────────────────────────────────────────────────────────────

        public bool HasFloor(Vector2Int cell) => _floorCells.Contains(cell);

        public bool TryGetObject(Vector2Int cell, out GridEntry entry)
            => _grid.TryGetValue(cell, out entry);

        public bool CanPlace(Vector2Int cell, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (_grid.ContainsKey(check))     return false;
                if (!_floorCells.Contains(check)) return false;
            }
            return true;
        }

        /// <summary>Every placed object, each listed once at its root cell.</summary>
        public List<GridEntry> GetAllPlaced()
        {
            var seen   = new HashSet<Vector2Int>();
            var result = new List<GridEntry>();
            foreach (var entry in _grid.Values)
                if (seen.Add(entry.Root)) result.Add(entry);
            return result;
        }

        // ── Mutation ────────────────────────────────────────────────────────────

        public bool PlaceObject(Vector2Int cell, PlacedObjectData data, Vector2Int size)
        {
            if (!CanPlace(cell, size)) return false;
            var entry = new GridEntry(cell, data, size);
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                _grid[cell + new Vector2Int(x, y)] = entry;
            OnObjectPlaced.Invoke(cell, data);
            return true;
        }

        public bool RemoveObject(Vector2Int cell)
        {
            if (!_grid.TryGetValue(cell, out var entry)) return false;
            var root = entry.Root;
            var size = entry.Size;
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                _grid.Remove(root + new Vector2Int(x, y));
            OnObjectRemoved.Invoke(root);
            return true;
        }

        public void ClearAll()
        {
            _grid.Clear();
        }

        public void SetFloor(Vector2Int cell, bool value)
        {
            if (value) _floorCells.Add(cell);
            else       _floorCells.Remove(cell);
            OnFloorChanged.Invoke(cell, value);
        }

        public void FillFloorRect(Vector2Int origin, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                SetFloor(origin + new Vector2Int(x, y), true);
        }
    }

    /// <summary>Catalogue entry describing one placeable furniture type.</summary>
    [System.Serializable]
    public class PlacedObjectData
    {
        public string     Id;
        public string     DisplayName;
        public string     Type;          // "shelf" | "pen" | "counter"
        public Vector2Int Size = Vector2Int.one;
        public float      Cost;
        public Color      Tint = Color.white;
    }

    public class GridEntry
    {
        public Vector2Int       Root;
        public PlacedObjectData Data;
        public Vector2Int       Size;
        public GameObject       Instance;   // set by whoever spawned the visual
        public string           Variant;    // shelf category / pen species

        public GridEntry(Vector2Int root, PlacedObjectData data, Vector2Int size)
        { Root = root; Data = data; Size = size; }
    }
}
