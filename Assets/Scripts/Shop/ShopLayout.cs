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
    public class ShopLayout : MonoBehaviour
    {
        [Header("Yard — the open lot the shop stands in (metres)")]
        public float YardWidth = 64f;
        public float YardDepth = 34f;

        [Header("Shop building (metres)")]
        public float RoomWidth  = 16f;
        public float RoomDepth  = 12f;
        public float WallHeight = 4f;
        public float ShopSideMargin  = 2f;
        public float ShopFrontMargin = 7f;

        [Header("Anchors — optional, derived from the dimensions when empty")]
        public Transform ShopCentreAnchor;
        public Transform DoorAnchor;
        public Transform ForecourtAnchor;
        public Transform PlayerStartAnchor;
        public Transform PavementAnchor;

        [Header("Zones")]
        [Tooltip("x, z, width, depth in world metres — where the pens go.")]
        public Rect PaddockRect = new(2f, -9f, 24f, 17f);
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

        /// <summary>On the forecourt, a couple of metres outside the shop door.</summary>
        public Vector3 ForecourtPosition => ForecourtAnchor != null ? Flat(ForecourtAnchor.position)
            : ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f + 2.5f);

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
                return ForecourtPosition + Vector3.forward * 6f;
            }
        }

        /// <summary>Runtime-only pavement position for layouts with no pavement anchor.</summary>
        public void SetPavementCentre(Vector3 centre) => _pavementOverride = centre;

        public Rect PaddockArea => PaddockRect;

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
        /// cells from an earlier stage stay buildable; the doorway never is. Does nothing
        /// before <see cref="FillFloorGrid"/> has bound a grid.
        /// </summary>
        public void ApplyLotStage(int stage)
        {
            if (_grid == null) return;
            RectInt area = LotStageCells(stage);
            _grid.FillFloorRect(area.position, area.size);
            ClearDoorway();
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
            Rect paddock = PaddockArea;
            float xMin = Mathf.Min(shop.x - RoomWidth * 0.5f, paddock.xMin);
            float xMax = Mathf.Max(shop.x + RoomWidth * 0.5f, paddock.xMax);
            float zMin = stage >= BackStripLotStage ? -YardDepth * 0.5f
                       : Mathf.Min(shop.z - RoomDepth * 0.5f, paddock.yMin);

            int x0 = Mathf.Max(yard.xMin, Mathf.FloorToInt(xMin / cs));
            int x1 = Mathf.Min(yard.xMax, Mathf.CeilToInt(xMax / cs));
            int z0 = Mathf.Max(yard.yMin, Mathf.FloorToInt(zMin / cs));
            int z1 = Mathf.Min(yard.yMax, Mathf.CeilToInt(YardFrontZ / cs));
            return new RectInt(x0, z0, Mathf.Max(0, x1 - x0), Mathf.Max(0, z1 - z0));
        }

        private const int DoorwayBehind = -1;
        private const int DoorwayAhead  = 2;
        private const int DoorwayLeft   = -1;
        private const int DoorwayRight  = 0;

        /// <summary>Grid cells of the doorway kept clear of furniture, given the door cell.</summary>
        public static RectInt DoorwayCells(Vector2Int door) => new(
            door.x + DoorwayLeft, door.y + DoorwayBehind,
            DoorwayRight - DoorwayLeft + 1, DoorwayAhead - DoorwayBehind + 1);

        private void ClearDoorway()
        {
            Vector2Int door = _grid.WorldToGrid(DoorPosition);
            for (int dz = DoorwayBehind; dz <= DoorwayAhead; dz++)
                for (int dx = DoorwayLeft; dx <= DoorwayRight; dx++)
                    _grid.SetFloor(new Vector2Int(door.x + dx, door.y + dz), false);
        }

        // ── NavMesh ─────────────────────────────────────────────────────────────

        /// <summary>
        /// (Re)bakes the walkable surface. Call after furniture changes so customers
        /// path around new shelves.
        /// </summary>
        public void BakeNavMesh()
        {
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

            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Rect p = PaddockArea;
            Gizmos.DrawWireCube(new Vector3(p.center.x, 0.05f, p.center.y), new Vector3(p.width, 0.1f, p.height));

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
