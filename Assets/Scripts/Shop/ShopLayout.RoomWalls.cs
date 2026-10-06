using UnityEngine;
using PetShop.Core;

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
        /// Name of each static corner filler older builds made. They are no longer built; any found on load is
        /// destroyed by <see cref="RemoveLegacyRoomWallCorners"/>.
        /// </summary>
        public const string RoomWallCornerName = "RoomWallCorner";
        /// <summary>Name of the parent older builds gave the corner fillers.</summary>
        private const string LegacyRoomWallCornersName = "RoomWallCorners";

        /// <summary>Height of every wall, window-wall and doorway piece (the prefabs' shared convention).</summary>
        public const float WallPieceHeight = 3.2f;
        /// <summary>Thickness of every wall, window-wall and doorway piece (the prefabs' shared convention).</summary>
        public const float WallPieceThickness = 0.24f;
        /// <summary>Name of the return-arm children a wall piece on a ring corner gets from <see cref="ShapeRoomWallPiece"/>.</summary>
        public const string CornerReturnName = "CornerReturn";
        /// <summary>Yaw that turns a piece's child from running along the piece to running across it.</summary>
        private const float CornerReturnYaw = 90f;
        /// <summary>Slack when telling a child spanning the whole piece from a smaller one.</summary>
        private const float FullSpanTolerance = 0.01f;

        /// <summary>
        /// Metres either side of the till spot's X that customers fill standing at the till and in its queue (the
        /// queue runs straight out from the till along +Z). The back door keeps out of the columns this lane covers.
        /// </summary>
        private const float TillLaneHalfWidth = 0.5f;

        /// <summary>
        /// Lays the room's walls on every free cell of the wall ring (<see cref="RoomWallCells"/>) through
        /// <paramref name="build"/>, without charging the player: the shopfront row (street side, corners
        /// excluded) gets doorway pieces in the doorway's columns (<see cref="DoorwayCells"/>) and window walls
        /// elsewhere — doorway pieces have no jambs, so adjacent ones form one opening centred on the door; the back
        /// row gets doorway pieces in the back door's columns (<see cref="BackDoorwayCells"/>, into the yard); every
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
            RectInt backDoor = BackDoorwayCells();
            int placed = 0;

            for (int x = ring.xMin; x < ring.xMax; x++)
            for (int y = ring.yMin; y < ring.yMax; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!IsRoomWallCell(cell)) continue;

                PlacedObjectData def = BuildCatalog.Get(RoomWallPieceFor(cell, ring, doorway, backDoor));
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

        /// <summary>
        /// True when <paramref name="cell"/> is one of the doorway cells: the shopfront's (<see cref="DoorwayCells"/>)
        /// or the back door's (<see cref="BackDoorwayCells"/>).
        /// </summary>
        public bool IsDoorwayCell(Vector2Int cell) =>
            DoorwayCells(DoorCell).Contains(cell) || BackDoorwayCells().Contains(cell);

        /// <summary>True when <paramref name="footprint"/> overlaps the front or back doorway.</summary>
        public bool OverlapsDoorway(RectInt footprint) =>
            DoorwayCells(DoorCell).Overlaps(footprint) || BackDoorwayCells().Overlaps(footprint);

        /// <summary>
        /// The back door's clear cells: the shopfront doorway's shape mirrored across the wall ring, so it crosses
        /// the back ring row and reaches into the room and out into the yard behind. It is as wide as the front
        /// doorway but offset towards the room's low-X side, ending just short of the till lane
        /// (<see cref="TillLaneHalfWidth"/> around <see cref="TillPosition"/>) so neither the till spot nor its
        /// queue stands in the back-door lane; it never starts left of the room's first interior column.
        /// </summary>
        public RectInt BackDoorwayCells()
        {
            RectInt ring  = RoomWallCells();
            RectInt front = DoorwayCells(DoorCell);
            int laneMin   = Mathf.FloorToInt((TillPosition.x - TillLaneHalfWidth) / GridManager.CellSize);
            int xMin      = Mathf.Max(RoomCells().xMin, laneMin - front.width);
            return new RectInt(xMin, ring.yMin + ring.yMax - front.yMax, front.width, front.height);
        }

        /// <summary>The back ring row's cells the back door's pieces stand on.</summary>
        public RectInt BackDoorRingCells()
        {
            RectInt back = BackDoorwayCells();
            return new RectInt(back.xMin, RoomWallCells().yMin, back.width, 1);
        }

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

        /// <summary>
        /// Destroys the static corner fillers (and their parent) that older builds laid at the wall ring's corners.
        /// Corners are now closed by the corner cell's own grid piece (<see cref="ShapeRoomWallPiece"/>).
        /// </summary>
        private void RemoveLegacyRoomWallCorners()
        {
            Transform root = ShopRoot != null ? ShopRoot : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root) continue;
                if (t.name == RoomWallCornerName || t.name == LegacyRoomWallCornersName)
                    PrefabPreview.DestroySafe(t.gameObject);
            }
        }

        /// <summary>True when <paramref name="cell"/> is one of the wall ring's four corner cells.</summary>
        public bool IsRoomWallCorner(Vector2Int cell)
        {
            RectInt ring = RoomWallCells();
            return (cell.x == ring.xMin || cell.x == ring.xMax - 1) && (cell.y == ring.yMin || cell.y == ring.yMax - 1);
        }

        /// <summary>
        /// Turns a plain wall piece just spawned on a wall-ring corner cell into an L that closes the corner: the arm
        /// along the piece runs from the room-side cell edge to the adjacent row's outer face, and a return arm runs
        /// across from the piece to the other room-side cell edge, where the adjacent row's first piece starts. Both
        /// arms are children of the piece, so the closure is removed with its cell. Other pieces and cells are left alone.
        /// </summary>
        public void ShapeRoomWallPiece(GameObject piece, Vector2Int cell, PlacedObjectData def)
        {
            if (piece == null || def == null || def.Id != BuildCatalog.Wall || !IsRoomWallCorner(cell)) return;
            Transform t = piece.transform;
            if (t.Find(CornerReturnName) != null) return;

            RectInt ring = RoomWallCells();
            Vector3 inX = cell.x == ring.xMin ? Vector3.right   : Vector3.left;
            Vector3 inZ = cell.y == ring.yMin ? Vector3.forward : Vector3.back;
            bool runsAlongZ = Mathf.Abs(t.right.z) > Mathf.Abs(t.right.x);
            Vector3 runIn    = runsAlongZ ? inZ : inX;
            Vector3 acrossIn = runsAlongZ ? inX : inZ;
            float s = Mathf.Sign(Vector3.Dot(t.right, runIn));
            float r = Mathf.Sign(Vector3.Dot(t.forward, acrossIn));

            float half       = GridManager.CellSize * 0.5f;
            float halfThick  = WallPieceThickness * 0.5f;
            float runLength  = half + halfThick;           // adjacent row's outer face to the room-side edge
            float runCentre  = s * (half - halfThick) * 0.5f;
            float backLength = half - halfThick;           // piece's room-side face to the room-side edge
            float backCentre = r * (half + halfThick) * 0.5f;

            var spans = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in t)
                if (Mathf.Abs(child.localScale.x - GridManager.CellSize) <= FullSpanTolerance) spans.Add(child);

            foreach (var child in spans)
            {
                var back = Object.Instantiate(child.gameObject, t, false).transform;
                back.name          = CornerReturnName;
                back.localRotation = Quaternion.Euler(0f, CornerReturnYaw, 0f) * child.localRotation;
                back.localPosition = new Vector3(0f, child.localPosition.y, backCentre);
                back.localScale    = new Vector3(backLength, child.localScale.y, child.localScale.z);

                child.localPosition = new Vector3(runCentre, child.localPosition.y, child.localPosition.z);
                child.localScale    = new Vector3(runLength, child.localScale.y, child.localScale.z);
            }

            var box = piece.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size   = new Vector3(runLength, box.size.y, box.size.z);
                box.center = new Vector3(runCentre, box.center.y, box.center.z);
            }
        }

        /// <summary>
        /// Catalogue id of the wall piece for a cell on the outer ring of <paramref name="ring"/>, given the front
        /// <paramref name="doorway"/> and the <paramref name="backDoor"/>'s cells.
        /// </summary>
        private static string RoomWallPieceFor(Vector2Int cell, RectInt ring, RectInt doorway, RectInt backDoor)
        {
            if (IsSideWallCell(cell, ring)) return BuildCatalog.Wall;
            if (cell.y == ring.yMax - 1)
                return InColumns(cell, doorway) ? BuildCatalog.WallDoor : BuildCatalog.WallWindow;
            bool backDoorCell = cell.y == ring.yMin && InColumns(cell, backDoor);
            return backDoorCell ? BuildCatalog.WallDoor : BuildCatalog.Wall;
        }

        /// <summary>True when <paramref name="cell"/>'s column lies within <paramref name="cells"/>' columns.</summary>
        private static bool InColumns(Vector2Int cell, RectInt cells) => cell.x >= cells.xMin && cell.x < cells.xMax;

        /// <summary>True for ring cells on the low-X or high-X side, corners included.</summary>
        private static bool IsSideWallCell(Vector2Int cell, RectInt ring) =>
            cell.x == ring.xMin || cell.x == ring.xMax - 1;
    }
}
