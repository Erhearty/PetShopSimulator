using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the physical shop at runtime: room shell, lighting, the player, and the
    /// NavMesh customers walk on. Furniture itself is owned by <see cref="Core.GameManager"/>
    /// so that the starter layout and a loaded save go through exactly the same path.
    /// </summary>
    public class ShopGenerator : MonoBehaviour
    {
        [Header("Yard — the open lot the shop stands in (metres)")]
        public float YardWidth = 64f;
        public float YardDepth = 34f;

        [Header("Shop building (metres) — sits in the yard's near-left corner")]
        public float RoomWidth  = 16f;
        public float RoomDepth  = 12f;
        public float WallHeight = 4f;
        public float DoorWidth  = 4f;

        [Tooltip("Gap between the shop and the yard's left wall.")]
        public float ShopSideMargin = 2f;

        [Tooltip("Gap between the shop front and the street, i.e. the depth of the forecourt.")]
        public float ShopFrontMargin = 7f;

        /// <summary>Centre of the shop building in world space. The yard is centred on the origin.</summary>
        public Vector3 ShopCentre => new(
            -YardWidth * 0.5f + ShopSideMargin  + RoomWidth * 0.5f,
            0f,
             YardDepth * 0.5f - ShopFrontMargin - RoomDepth * 0.5f);

        /// <summary>World Z of the yard's street-facing edge.</summary>
        public float YardFrontZ => YardDepth * 0.5f;

        public StreetGenerator Street  { get; private set; }
        public Transform ShopRoot      { get; private set; }
        public Transform FurnitureRoot { get; private set; }
        public Transform Player        { get; private set; }

        /// <summary>Just inside the shop's front door.</summary>
        public Vector3 DoorPosition => ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f - 1.6f);

        /// <summary>On the forecourt, a couple of metres outside the shop door.</summary>
        public Vector3 ForecourtPosition => ShopCentre + new Vector3(0f, 0f, RoomDepth * 0.5f + 2.5f);

        private GridManager   _grid;
        private NavMeshSurface _surface;

        public void Generate(GridManager grid)
        {
            _grid    = grid;
            ShopRoot = new GameObject("Shop").transform;

            FurnitureRoot = new GameObject("Furniture").transform;
            FurnitureRoot.SetParent(ShopRoot, false);

            // The builders only hold state; constructing them spawns nothing.
            var ctx      = new ShopBuildContext(this, _grid);
            var building = new ShopBuildingBuilder(ctx);
            var front    = new ShopFrontBuilder(ctx, building);
            var interior = new ShopInteriorBuilder(ctx);

            building.BuildYard();
            building.BuildShopBuilding();
            front.BuildShopFront();
            front.BuildFacade();
            interior.FillFloorGrid();

            Street = gameObject.GetComponent<StreetGenerator>() ?? gameObject.AddComponent<StreetGenerator>();
            // The whole street cross-section is measured from the yard's front edge —
            // forgetting to shift it leaves the road sitting on top of the yard.
            Street.ShopFrontZ    = YardFrontZ;
            Street.PavementBackZ = YardFrontZ - 1f;
            Street.KerbZ         = YardFrontZ + 7f;
            Street.FarPavementZ  = Street.KerbZ + Street.RoadWidth + 6f;
            Street.ShopHalfWidth = YardWidth * 0.5f;
            // The street has to reach roughly as far as the city blocks behind it, or the
            // parade trails off into open ground while the skyline carries on.
            Street.HalfLength    = Mathf.Max(120f, YardWidth * 0.5f + 60f);
            Street.WalkableHalf  = YardWidth * 0.5f;
            Street.Generate(ShopRoot);

            interior.SetupLighting();
            interior.DressInterior();
            new YardDresser(ctx).DressYard();

            var rig = new PlayerRigSpawner(ctx);
            Player = rig.SpawnPlayer();
            rig.SpawnCamera(Player);
            Debug.Log($"[ShopGenerator] Yard {YardWidth}x{YardDepth} m with a {RoomWidth}x{RoomDepth} m shop at " +
                      $"({ShopCentre.x:F0}, {ShopCentre.z:F0}); street and props built.");
        }

        // ── The yard ────────────────────────────────────────────────────────────

        // Yard zones, in world coordinates. The paddock is where StarterLayout puts the pens;
        // sized to the six starter pens plus working margins, because a paddock much bigger
        // than its pens reads as an empty sand pit with a few hutches in one corner.
        internal Rect PaddockArea => new(2f, -9f, 24f, 17f);      // x, z, width, depth
        internal float PathZ      => ShopCentre.z - RoomDepth * 0.5f - 2.2f;

        // ── NavMesh ─────────────────────────────────────────────────────────────

        /// <summary>
        /// (Re)bakes the walkable surface. Call after furniture changes so customers
        /// path around new shelves.
        /// </summary>
        public void BakeNavMesh()
        {
            if (ShopRoot == null) return;

            if (_surface == null)
            {
                _surface = ShopRoot.gameObject.AddComponent<NavMeshSurface>();
                _surface.collectObjects   = CollectObjects.Children;
                _surface.useGeometry      = NavMeshCollectGeometry.PhysicsColliders;
                _surface.layerMask        = ~(1 << GameLayers.Character);
                _surface.overrideVoxelSize = true;
                _surface.voxelSize        = 0.16f;
            }
            _surface.BuildNavMesh();
            Debug.Log("[ShopGenerator] NavMesh baked.");
        }

        // ── Starter layout ──────────────────────────────────────────────────────

        /// <summary>
        /// The shop you start a brand-new game with.
        ///
        /// Shelves and the till go inside the building; the pens go out in the yard, which is
        /// the whole point of the layout — the animals live outdoors and the supplies are sold
        /// indoors. Cells are computed from the live geometry rather than hard-coded, so the
        /// layout survives a change to the yard or shop size.
        /// </summary>
        public List<FurniturePlacement> StarterLayout(GridManager grid)
        {
            var list = new List<FurniturePlacement>();
            Vector2Int shop = grid.WorldToGrid(ShopCentre);
            int halfX = Mathf.FloorToInt(RoomWidth  * 0.5f / GridManager.CellSize);
            int halfZ = Mathf.FloorToInt(RoomDepth * 0.5f / GridManager.CellSize);

            // ── Indoors: counter facing the door, shelves along the back and side walls ──
            list.Add(new FurniturePlacement(BuildCatalog.Counter,
                     new Vector2Int(shop.x - 1, shop.y - halfZ), null, 0f));

            string[] categories = { "Food", "Food", "Toy", "Accessory", "Medicine", "Toy" };
            int c = 0;
            for (int x = shop.x - halfX; x < shop.x + halfX; x++)
            {
                // back wall, skipping the counter's two cells
                if (x >= shop.x - 1 && x <= shop.x) continue;
                list.Add(new FurniturePlacement(BuildCatalog.ShelfSmall,
                         new Vector2Int(x, shop.y - halfZ), categories[c++ % categories.Length], 0f));
            }

            list.Add(new FurniturePlacement(BuildCatalog.ShelfLarge,
                     new Vector2Int(shop.x - halfX, shop.y - halfZ + 2), "Food", 90f));
            list.Add(new FurniturePlacement(BuildCatalog.ShelfLarge,
                     new Vector2Int(shop.x + halfX - 2, shop.y - halfZ + 2), "Accessory", 270f));

            // ── Outdoors: pens inside the fenced paddock ──
            Vector2Int yardStart = grid.WorldToGrid(new Vector3(PaddockArea.x + 3f, 0f,
                                                                PaddockArea.y + PaddockArea.height - 5f));
            // Only species with real models — placeholder blobs are worse than fewer pens.
            string[] species = { "Dog", "Cat", "Fox", "Chicken", "Penguin", "Deer" };
            for (int i = 0; i < species.Length; i++)
            {
                int row = i / 3, col = i % 3;
                list.Add(new FurniturePlacement(BuildCatalog.PetPen,
                         new Vector2Int(yardStart.x + col * 3, yardStart.y - row * 3),
                         species[i], 0f));
            }

            return list;
        }

        public readonly struct FurniturePlacement
        {
            public readonly string     CatalogId;
            public readonly Vector2Int Cell;
            public readonly string     Variant;
            public readonly float      Rotation;

            public FurniturePlacement(string catalogId, Vector2Int cell, string variant, float rotation)
            { CatalogId = catalogId; Cell = cell; Variant = variant; Rotation = rotation; }
        }
    }
}
