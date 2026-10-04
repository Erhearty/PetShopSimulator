using UnityEngine;

namespace PetShop.Shop
{
    /// <summary>
    /// The shop room's walls as ordinary placed building pieces: one piece on every cell of the
    /// ring just outside the room, with window walls along the shopfront and a doorway where customers enter.
    /// </summary>
    public partial class ShopLayout
    {
        /// <summary>Yaw of a wall piece running along the X axis (the shopfront and back wall).</summary>
        public const float AlongXWallYaw = 0f;
        /// <summary>Yaw of a wall piece running along the Z axis (the side walls).</summary>
        public const float AlongZWallYaw = 90f;

        /// <summary>
        /// Cells between the room's outermost interior cells and the wall ring. Wall pieces stand in
        /// the middle of their cell, so the ring lies just outside <see cref="RoomCells"/> — where the old
        /// scene walls stood — and the whole room interior stays usable floor.
        /// </summary>
        private const int WallRingOffset = 1;

        /// <summary>
        /// Lays the room's walls on every free cell of the wall ring (<see cref="RoomWallCells"/>) through
        /// <paramref name="build"/>, without charging the player: the shopfront row (street side, corners
        /// excluded) gets doorway pieces in the doorway's columns (<see cref="DoorwayCells"/>) and window walls
        /// elsewhere — doorway pieces have no jambs, so adjacent ones form one opening centred on the door; every
        /// other ring cell, corners included, gets a plain wall. Cells already occupied are left alone.
        /// </summary>
        /// <returns>How many wall pieces were placed.</returns>
        /// <remarks>NavMesh bakes are suspended while seeding, so the whole ring costs one bake.</remarks>
        public int SeedRoomWalls(BuildMode build)
        {
            if (build == null || build.GridManager == null) return 0;
            SuspendNavMeshBakes();
            try     { return SeedRoomWallPieces(build); }
            finally { ResumeNavMeshBakes(); }
        }

        /// <summary>Body of <see cref="SeedRoomWalls"/>: places the ring's pieces one by one.</summary>
        private int SeedRoomWallPieces(BuildMode build)
        {
            GridManager grid = build.GridManager;
            RectInt ring     = RoomWallCells();
            RectInt doorway  = DoorwayCells(grid.WorldToGrid(DoorPosition));
            int placed = 0;

            for (int x = ring.xMin; x < ring.xMax; x++)
            for (int y = ring.yMin; y < ring.yMax; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!IsRoomWallCell(cell)) continue;

                PlacedObjectData def = BuildCatalog.Get(RoomWallPieceFor(cell, ring, doorway));
                if (def == null) continue;
                grid.EnsureFloor(cell, def.Size);
                if (grid.TryGetObject(cell, out _)) continue;

                float yaw = IsSideWallCell(cell, ring) ? AlongZWallYaw : AlongXWallYaw;
                if (build.Place(cell, def, null, yaw, charge: false) != null) placed++;
            }
            return placed;
        }

        /// <summary>
        /// The rectangle whose outer ring holds the room's walls: <see cref="RoomCells"/> grown by
        /// <see cref="WallRingOffset"/> on every side.
        /// </summary>
        public RectInt RoomWallCells()
        {
            RectInt room = RoomCells();
            return new RectInt(room.xMin - WallRingOffset, room.yMin - WallRingOffset,
                               room.width + 2 * WallRingOffset, room.height + 2 * WallRingOffset);
        }

        /// <summary>True when <paramref name="cell"/> lies on the outer ring of <see cref="RoomWallCells"/>.</summary>
        public bool IsRoomWallCell(Vector2Int cell)
        {
            RectInt ring = RoomWallCells();
            if (!ring.Contains(cell)) return false;
            return cell.x == ring.xMin || cell.x == ring.xMax - 1 ||
                   cell.y == ring.yMin || cell.y == ring.yMax - 1;
        }

        /// <summary>True when <paramref name="cell"/> is one of the doorway cells (<see cref="DoorwayCells"/>).</summary>
        public bool IsDoorwayCell(Vector2Int cell) => DoorwayCells(DoorCell).Contains(cell);

        /// <summary>Grid cell of <see cref="DoorPosition"/>; needs no bound grid.</summary>
        private Vector2Int DoorCell => new(Mathf.FloorToInt(DoorPosition.x / GridManager.CellSize),
                                           Mathf.FloorToInt(DoorPosition.z / GridManager.CellSize));

        /// <summary>
        /// True when <paramref name="def"/> may cover the wall-ring cells of a <paramref name="size"/> footprint at
        /// <paramref name="cell"/>: the ring is reserved structure, so only building pieces
        /// (<see cref="BuildCatalog.IsBuildingPiece"/>) stand on it, and only doorway pieces where it crosses the
        /// doorway, keeping the shopfront passable. Footprints off the ring are always allowed here.
        /// </summary>
        public bool AllowsOnRoomWallRing(PlacedObjectData def, Vector2Int cell, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (IsRoomWallCell(check) && !AllowsOnRingCell(def, check)) return false;
            }
            return true;
        }

        /// <summary>True when <paramref name="def"/> may stand on the ring cell <paramref name="cell"/>.</summary>
        private bool AllowsOnRingCell(PlacedObjectData def, Vector2Int cell)
        {
            if (def == null) return false;
            return IsDoorwayCell(cell) ? def.Id == BuildCatalog.WallDoor : BuildCatalog.IsBuildingPiece(def.Id);
        }

        /// <summary>
        /// Gives every wall-ring cell floor, whether or not a piece stands there, so a removed wall can
        /// always be put back — even outside the unlocked lot or across the doorway.
        /// </summary>
        private void EnsureRoomWallFloor()
        {
            RectInt ring = RoomWallCells();
            for (int x = ring.xMin; x < ring.xMax; x++)
            for (int y = ring.yMin; y < ring.yMax; y++)
            {
                var cell = new Vector2Int(x, y);
                if (IsRoomWallCell(cell) && !_grid.HasFloor(cell)) _grid.SetFloor(cell, true);
            }
        }

        /// <summary>Catalogue id of the wall piece for a cell on the outer ring of <paramref name="ring"/>.</summary>
        private static string RoomWallPieceFor(Vector2Int cell, RectInt ring, RectInt doorway)
        {
            bool shopfront = cell.y == ring.yMax - 1 && !IsSideWallCell(cell, ring);
            if (!shopfront) return BuildCatalog.Wall;
            bool doorColumn = cell.x >= doorway.xMin && cell.x < doorway.xMax;
            return doorColumn ? BuildCatalog.WallDoor : BuildCatalog.WallWindow;
        }

        /// <summary>True for ring cells on the low-X or high-X side, corners included.</summary>
        private static bool IsSideWallCell(Vector2Int cell, RectInt ring) =>
            cell.x == ring.xMin || cell.x == ring.xMax - 1;
    }
}
