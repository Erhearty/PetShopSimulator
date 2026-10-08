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

        /// <summary>Freely placed pieces (any position and yaw): they hold no cells, so they never block the grid.</summary>
        private readonly List<GridEntry> _free = new();

        /// <summary>The freely placed pieces, in placement order.</summary>
        public IReadOnlyList<GridEntry> FreePieces => _free;

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
            result.AddRange(_free);
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

        /// <summary>
        /// Registers a piece standing at an exact world position and yaw. It occupies no grid cells; its
        /// <see cref="GridEntry.Root"/> is the cell its centre lies in.
        /// </summary>
        public GridEntry PlaceFree(PlacedObjectData data, Vector3 position, float yaw)
        {
            position.y = 0f;
            var entry = new GridEntry(WorldToGrid(position), data, data.Size)
                { IsFree = true, Position = position, Yaw = yaw };
            _free.Add(entry);
            OnObjectPlaced.Invoke(entry.Root, data);
            return entry;
        }

        public bool RemoveFree(GridEntry entry)
        {
            if (entry == null || !_free.Remove(entry)) return false;
            OnObjectRemoved.Invoke(entry.Root);
            return true;
        }

        /// <summary>
        /// True when a freely placed piece's footprint covers <paramref name="cell"/>. Thin building pieces
        /// (walls, fences) do not count: they are kept apart from other pieces by the physics overlap check.
        /// </summary>
        public bool CoveredByFree(Vector2Int cell)
        {
            foreach (var piece in _free)
            {
                if (piece.Data != null && BuildCatalog.IsBuildingPiece(piece.Data.Id)) continue;
                if (BuildMode.FreeCells(piece.Position, piece.Yaw, piece.Size).Contains(cell)) return true;
            }
            return false;
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
            _free.Clear();
        }

        public void SetFloor(Vector2Int cell, bool value)
        {
            if (value) _floorCells.Add(cell);
            else       _floorCells.Remove(cell);
            OnFloorChanged.Invoke(cell, value);
        }

        /// <summary>
        /// Makes every cell of the footprint at <paramref name="cell"/> buildable floor.
        /// Used on load so pieces saved before lot stages existed keep their ground.
        /// </summary>
        public void EnsureFloor(Vector2Int cell, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (!_floorCells.Contains(check)) SetFloor(check, true);
            }
        }

        public void FillFloorRect(Vector2Int origin, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                SetFloor(origin + new Vector2Int(x, y), true);
        }
    }

    /// <summary>Which catalogue section an entry belongs to.</summary>
    public enum BuildCategory
    {
        /// <summary>Working furniture: shelves, pens, the counter.</summary>
        Furniture,
        /// <summary>Building fabric: walls, doorways, fences.</summary>
        Structure,
        /// <summary>Purely visual decor; never interactable.</summary>
        Decoration,
    }

    /// <summary>Catalogue entry describing one placeable furniture type.</summary>
    [System.Serializable]
    public class PlacedObjectData
    {
        public string        Id;
        public string        DisplayName;
        public string        Type;          // "shelf" | "pen" | "counter" | "wall" | ... | "decoration"
        public BuildCategory Category = BuildCategory.Furniture;
        public string        Description = "";
        public Vector2Int    Size = Vector2Int.one;
        public float         Cost;
        public Color         Tint = Color.white;
        /// <summary>Kept loadable for old saves but left out of catalogue listings.</summary>
        public bool          Hidden;
        /// <summary>Species of a per-species pen, named in its localized title; null for everything else.</summary>
        [System.NonSerialized] public PetShop.Pets.Pet.Species? PenSpecies;

        /// <summary>
        /// <see cref="DisplayName"/> in the current language, for display only — ids, saves and
        /// ledger comparisons keep the English <see cref="DisplayName"/>.
        /// </summary>
        public string LocalizedName =>
            PenSpecies.HasValue
                ? PetShop.Localization.Loc.F("furniture.species_pen.name", PetShop.Localization.Loc.T(SpeciesKey))
                : LocalizedOr("furniture." + Id + ".name", DisplayName);

        /// <summary><see cref="Description"/> in the current language.</summary>
        public string LocalizedDescription =>
            PenSpecies.HasValue
                ? PetShop.Localization.Loc.F("furniture.species_pen.desc", PetShop.Localization.Loc.T(SpeciesKey))
                : LocalizedOr("furniture." + Id + ".desc", Description);

        private string SpeciesKey => "species." + PenSpecies.Value.ToString().ToLowerInvariant();

        private static string LocalizedOr(string key, string fallback) =>
            PetShop.Localization.Loc.Has(key) ? PetShop.Localization.Loc.T(key) : fallback;
    }

    public class GridEntry
    {
        public Vector2Int       Root;
        public PlacedObjectData Data;
        public Vector2Int       Size;
        public GameObject       Instance;   // set by whoever spawned the visual
        public string           Variant;    // shelf category / pen species

        /// <summary>True for a piece placed at a free position and yaw rather than on grid cells.</summary>
        public bool             IsFree;
        /// <summary>World position of a free piece's centre (floor level).</summary>
        public Vector3          Position;
        /// <summary>Yaw of a free piece in degrees.</summary>
        public float            Yaw;

        public GridEntry(Vector2Int root, PlacedObjectData data, Vector2Int size)
        { Root = root; Data = data; Size = size; }
    }
}
