using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PetShop.Core;
using PetShop.Commerce;

namespace PetShop.Shop
{
    /// <summary>
    /// Scene-authored description of the shop world: yard and room dimensions, the anchors
    /// customers and the player use, the lot stages and the NavMesh. A new game starts
    /// with an empty shop; the player orders and places every piece of furniture. Everything here is edited by hand in the scene; anchors left empty fall back to the
    /// positions derived from the dimensions, which reproduces the generated layout exactly.
    /// </summary>
    public partial class ShopLayout : MonoBehaviour
    {
        [Header("Yard — the open lot the shop stands in (metres)")]
        public float YardWidth = 64f;
        public float YardDepth = 34f;

        [Header("Shop building (metres)")]
        public float RoomWidth  = 16f;
        public float RoomDepth  = 12f;
        /// <summary>Height of the room's walls: always the wall pieces' height, so the roof sits on their tops.</summary>
        public float WallHeight => WallPieceHeight;
        public float ShopSideMargin  = 2f;
        /// <summary>
        /// Room edge to street edge. 3 m puts the room's front wall-ring row on the yard's front row, so the
        /// shopfront opens straight onto the street (layout version 1; version 0 stood 7 m back).
        /// </summary>
        public float ShopFrontMargin = 3f;

        /// <summary>Save layout version written by this build; see <see cref="LegacyLayoutCellShift"/>.</summary>
        public const int CurrentLayoutVersion = 1;
        /// <summary>Grid cells the room moved towards the street between layout version 0 and 1.</summary>
        public static readonly Vector2Int LegacyLayoutCellShift = new(0, 2);

        /// <summary>Metres from the wall ring's street edge to the forecourt spot customers and deliveries use.</summary>
        private const float ForecourtStandoff = 2.5f;
        /// <summary>Metres from the forecourt spot to the derived pavement centre, along the street direction.</summary>
        private const float PavementAhead = 2.5f;
        /// <summary>
        /// Metres of yard behind the room inside the starter lot: the strip the room vacated when it moved to the
        /// street, so the starter lot keeps its old size.
        /// </summary>
        private const float StarterLotBackStrip = 4f;

        [Header("Anchors — optional, derived from the dimensions when empty")]
        public Transform ShopCentreAnchor;
        public Transform DoorAnchor;
        public Transform ForecourtAnchor;
        public Transform PlayerStartAnchor;
        public Transform PavementAnchor;

        [Header("Zones")]
        public float PavementSpread = 24f;

        [Header("Roots")]
        public Transform ShopRoot;
        public Transform FurnitureRoot;
        public Transform Player;

        [Header("Furniture")]
        [Tooltip("Baked prefab per BuildCatalog id; FurnitureFactory spawns from it.")]
        public FurniturePrefabs FurniturePrefabs;

        public const int StarterLotStage   = PetShop.Progression.ProgressionRules.StarterLotStage;
        public const int BackStripLotStage = PetShop.Progression.ProgressionRules.BackStripLotStage;
        public const int FullYardLotStage  = PetShop.Progression.ProgressionRules.FullYardLotStage;

        private GridManager    _grid;
        private NavMeshSurface _surface;
        private Vector3?       _pavementOverride;

        // ── Anchors ─────────────────────────────────────────────────────────────

        /// <summary>Centre of the shop building in world space.</summary>
        public Vector3 ShopCentre => ShopCentreAnchor != null ? Flat(ShopCentreAnchor.position) : new Vector3(
            -YardWidth * 0.5f + ShopSideMargin  + RoomWidth * 0.5f,
            0f,
             YardDepth * 0.5f - ShopFrontMargin - RoomDepth * 0.5f);

        /// <summary>World Z of the yard's street-facing edge.</summary>
        public float YardFrontZ => YardDepth * 0.5f;

        /// <summary>Just inside the shop's front door.</summary>
        public Vector3 DoorPosition => DoorAnchor != null ? Flat(DoorAnchor.position)
            : ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f - 1.6f);

        /// <summary>On the street side, a couple of metres outside the shopfront's wall ring.</summary>
        public Vector3 ForecourtPosition => ForecourtAnchor != null ? Flat(ForecourtAnchor.position)
            : ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f + WallRingOffset * GridManager.CellSize + ForecourtStandoff);

        /// <summary>Where the player starts (full position, height included).</summary>
        public Vector3 PlayerStartPosition => PlayerStartAnchor != null ? PlayerStartAnchor.position
            : ForecourtPosition + new Vector3(2f, 0f, 2f);

        /// <summary>Centre of the pavement customers spawn on and leave by.</summary>
        public Vector3 PavementCentre
        {
            get
            {
                if (PavementAnchor != null) return Flat(PavementAnchor.position);
                if (_pavementOverride.HasValue) return _pavementOverride.Value;
                return ForecourtPosition + Vector3.forward * PavementAhead;
            }
        }

        /// <summary>Runtime-only pavement position for layouts with no pavement anchor.</summary>
        public void SetPavementCentre(Vector3 centre) => _pavementOverride = centre;

        /// <summary>The till spot customers queue at, inside the shop at the back wall.</summary>
        public Vector3 TillPosition => ShopCentre + new Vector3(0f, 0f, -RoomDepth * 0.5f + 2.6f);

        private static Vector3 Flat(Vector3 p) => new(p.x, 0f, p.z);

        // ── Lot stages ──────────────────────────────────────────────────────────

        /// <summary>
        /// Makes the starting lot buildable and binds the layout to <paramref name="grid"/>
        /// so later <see cref="ApplyLotStage"/> calls reach it. Safe to call more than once.
        /// </summary>
        public void FillFloorGrid(GridManager grid)
        {
            _grid = grid;
            ApplyLotStage(StarterLotStage);
        }

        /// <summary>
        /// Makes every cell of lot <paramref name="stage"/> buildable. Stages only grow, so
        /// cells from an earlier stage stay buildable; the doorway never is, except where it crosses
        /// the wall ring. The room's wall ring (<see cref="RoomWallCells"/>) always keeps its floor, so a
        /// removed wall can be put back after a load or tier-up. Does nothing
        /// before <see cref="FillFloorGrid"/> has bound a grid.
        /// </summary>
        public void ApplyLotStage(int stage)
        {
            if (_grid == null) return;
            _lotStage = Mathf.Max(_lotStage, stage);
            RectInt area = LotStageCells(stage);
            _grid.FillFloorRect(area.position, area.size);
            ClearDoorway();
            EnsureRoomWallFloor();
            RemoveLegacyRoomWallCorners();
        }

        /// <summary>Grid cells covered by a lot stage, clamped to the yard.</summary>
        public RectInt LotStageCells(int stage)
        {
            float cs = GridManager.CellSize;
            int halfX = Mathf.FloorToInt(YardWidth * 0.5f / cs);
            int halfZ = Mathf.FloorToInt(YardDepth * 0.5f / cs);
            var yard = new RectInt(-halfX, -halfZ, halfX * 2, halfZ * 2);
            if (stage >= FullYardLotStage) return yard;

            Vector3 shop = ShopCentre;
            float xMin = shop.x - RoomWidth * 0.5f;
            float xMax = shop.x + RoomWidth * 0.5f;
            float zMin = stage >= BackStripLotStage ? -YardDepth * 0.5f
                                                    : shop.z - RoomDepth * 0.5f - StarterLotBackStrip;

            int x0 = Mathf.Max(yard.xMin, Mathf.FloorToInt(xMin / cs));
            int x1 = Mathf.Min(yard.xMax, Mathf.CeilToInt(xMax / cs));
            int z0 = Mathf.Max(yard.yMin, Mathf.FloorToInt(zMin / cs));
            int z1 = Mathf.Min(yard.yMax, Mathf.CeilToInt(YardFrontZ / cs));
            return new RectInt(x0, z0, Mathf.Max(0, x1 - x0), Mathf.Max(0, z1 - z0));
        }

        /// <summary>
        /// Grid cells of the shop room's interior: every cell the room's floor plan
        /// (<see cref="ShopCentre"/> ± <see cref="RoomWidth"/>/2, <see cref="RoomDepth"/>/2) covers.
        /// </summary>
        public RectInt RoomCells()
        {
            float cs = GridManager.CellSize;
            Vector3 shop = ShopCentre;
            int x0 = Mathf.FloorToInt((shop.x - RoomWidth * 0.5f) / cs);
            int x1 = Mathf.CeilToInt ((shop.x + RoomWidth * 0.5f) / cs);
            int z0 = Mathf.FloorToInt((shop.z - RoomDepth * 0.5f) / cs);
            int z1 = Mathf.CeilToInt ((shop.z + RoomDepth * 0.5f) / cs);
            return new RectInt(x0, z0, x1 - x0, z1 - z0);
        }

        /// <summary>True when a footprint of <paramref name="size"/> rooted at <paramref name="cell"/> overlaps the shop room.</summary>
        public bool IsInsideShop(Vector2Int cell, Vector2Int size) =>
            RoomCells().Overlaps(new RectInt(cell, size));

        private const int DoorwayBehind = -1;
        private const int DoorwayAhead  = 2;
        private const int DoorwayLeft   = -1;
        private const int DoorwayRight  = 0;

        /// <summary>Grid cells of the doorway kept clear of furniture, given the door cell.</summary>
        public static RectInt DoorwayCells(Vector2Int door) => new(
            door.x + DoorwayLeft, door.y + DoorwayBehind,
            DoorwayRight - DoorwayLeft + 1, DoorwayAhead - DoorwayBehind + 1);

        /// <summary>
        /// True when a pen of <paramref name="size"/> rooted at <paramref name="cell"/> may stand in the
        /// yard: every footprint cell lies inside the full yard, outside the shop room and the doorway,
        /// and is free on <paramref name="grid"/>. Ignores the unlocked lot stage — pens go anywhere in the yard.
        /// </summary>
        public bool CanPlacePen(GridManager grid, Vector2Int cell, Vector2Int size)
        {
            if (grid == null) return false;
            var footprint = new RectInt(cell, size);
            RectInt yard  = LotStageCells(FullYardLotStage);
            if (IsInsideShop(cell, size) || OverlapsDoorway(footprint))
                return false;
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (!yard.Contains(check) || grid.TryGetObject(check, out _) || grid.CoveredByFree(check)) return false;
            }
            return true;
        }

        /// <summary>
        /// True when a building piece (wall, window wall, doorway, fence) of <paramref name="size"/> rooted at
        /// <paramref name="cell"/> may stand in the yard: every footprint cell lies inside the full yard (never the
        /// street), is free on <paramref name="grid"/>, and is not a doorway clearance cell off the wall ring.
        /// Ignores the unlocked lot stage and needs no floor — walls go anywhere in the yard. The wall ring's own
        /// rule (<see cref="AllowsOnRoomWallRing"/>) is checked separately.
        /// </summary>
        public bool CanPlaceStructure(GridManager grid, Vector2Int cell, Vector2Int size)
        {
            if (grid == null) return false;
            RectInt yard = LotStageCells(FullYardLotStage);
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (!yard.Contains(check) || grid.TryGetObject(check, out _) || grid.CoveredByFree(check)) return false;
                if (IsDoorwayCell(check) && !IsRoomWallCell(check)) return false;
            }
            return true;
        }

        /// <summary>Highest lot stage applied so far; its cells are buildable floor.</summary>
        private int _lotStage = StarterLotStage;

        /// <summary>
        /// Takes back the floor a removed pen laid outside the unlocked lot, so the cells it stood on
        /// do not become buildable for other furniture. Cells inside the unlocked lot keep their floor.
        /// </summary>
        public void ReleasePenFloor(GridManager grid, Vector2Int cell, Vector2Int size)
        {
            if (grid == null) return;
            RectInt unlocked = LotStageCells(_lotStage);
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                var check = cell + new Vector2Int(x, y);
                if (unlocked.Contains(check) || IsRoomWallCell(check) || grid.TryGetObject(check, out _)) continue;
                grid.SetFloor(check, false);
            }
        }

        /// <summary>Takes the floor off the front and back doorways so no furniture blocks either way through.</summary>
        private void ClearDoorway()
        {
            foreach (var cell in DoorwayCells(_grid.WorldToGrid(DoorPosition)).allPositionsWithin)
                _grid.SetFloor(cell, false);
            foreach (var cell in BackDoorwayCells().allPositionsWithin)
                _grid.SetFloor(cell, false);
        }

        // ── NavMesh ─────────────────────────────────────────────────────────────

        /// <summary>Open <see cref="SuspendNavMeshBakes"/> calls; bakes are deferred while above zero.</summary>
        private int  _bakeSuspensions;
        /// <summary>True when a bake was asked for while suspended and is still owed.</summary>
        private bool _bakePending;

        /// <summary>
        /// Defers <see cref="BakeNavMesh"/> until the matching <see cref="ResumeNavMeshBakes"/>, so laying
        /// many pieces at once (seeding, loading) bakes once instead of once per piece. Nests.
        /// </summary>
        public void SuspendNavMeshBakes() => _bakeSuspensions++;

        /// <summary>Ends one <see cref="SuspendNavMeshBakes"/>; the last one runs a single owed bake.</summary>
        public void ResumeNavMeshBakes()
        {
            if (_bakeSuspensions > 0) _bakeSuspensions--;
            if (_bakeSuspensions > 0 || !_bakePending) return;
            _bakePending = false;
            BakeNavMesh();
        }

        /// <summary>
        /// (Re)bakes the walkable surface. Call after furniture changes so customers
        /// path around new shelves.
        /// </summary>
        public void BakeNavMesh()
        {
            if (_bakeSuspensions > 0) { _bakePending = true; return; }
            Transform root = ShopRoot != null ? ShopRoot : transform;

            if (_surface == null)
            {
                _surface = root.GetComponent<NavMeshSurface>();
                if (_surface == null) _surface = root.gameObject.AddComponent<NavMeshSurface>();
                _surface.collectObjects    = CollectObjects.Children;
                _surface.useGeometry       = NavMeshCollectGeometry.PhysicsColliders;
                _surface.layerMask         = ~(1 << GameLayers.Character);
                _surface.overrideVoxelSize = true;
                _surface.voxelSize         = 0.16f;
            }
            _surface.BuildNavMesh();
            Debug.Log("[ShopLayout] NavMesh baked.");
        }

#if UNITY_EDITOR
        // ── Editor gizmos ───────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            float cs = GridManager.CellSize;
            DrawCells(LotStageCells(FullYardLotStage),  new Color(0.3f, 0.3f, 0.3f, 0.6f), cs);
            DrawCells(LotStageCells(BackStripLotStage), new Color(0.9f, 0.7f, 0.2f, 0.8f), cs);
            DrawCells(LotStageCells(StarterLotStage),   new Color(0.2f, 0.9f, 0.3f, 0.9f), cs);

            Vector2Int door = new(Mathf.FloorToInt(DoorPosition.x / cs), Mathf.FloorToInt(DoorPosition.z / cs));
            DrawCells(DoorwayCells(door), new Color(1f, 0.3f, 0.3f, 0.9f), cs);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(DoorPosition, 0.3f);
            Gizmos.DrawWireSphere(ForecourtPosition, 0.3f);
            Gizmos.DrawWireSphere(PavementCentre, 0.3f);
            Gizmos.DrawWireCube(ShopCentre + Vector3.up * WallHeight * 0.5f,
                                new Vector3(RoomWidth, WallHeight, RoomDepth));
        }

        private static void DrawCells(RectInt r, Color colour, float cs)
        {
            Gizmos.color = colour;
            var size   = new Vector3(r.width * cs, 0.1f, r.height * cs);
            var centre = new Vector3((r.x + r.width * 0.5f) * cs, 0.05f, (r.y + r.height * 0.5f) * cs);
            Gizmos.DrawWireCube(centre, size);
        }
#endif
    }
}
