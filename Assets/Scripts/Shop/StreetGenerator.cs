using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the world outside the shop window: the pavement customers arrive on, a road,
    /// the parade of shops the pet shop belongs to, a block opposite, and a skyline.
    ///
    /// Models come from whichever art packs are installed — the Asset Store street/city
    /// packs when present, the CC0 Kenney kits otherwise, and plain procedural geometry if
    /// neither is. Ground planes and every collider are procedural regardless, so what is
    /// walkable never depends on imported art.
    ///
    /// Cross-section running outward from the shop front (+Z):
    ///
    ///   z 8.1        shop front wall
    ///   z 7 – 15     near pavement   walkable; customers spawn and queue here
    ///   z 15 – 25    road            no collider, therefore no NavMesh, therefore no pedestrians
    ///   z 25 – 31    far pavement    decoration only
    ///   z 31 +       block opposite, then the skyline
    /// </summary>
    public class StreetGenerator : MonoBehaviour
    {
        [Header("Street geometry (metres)")]
        public float ShopFrontZ    = 8f;
        public float PavementBackZ = 7f;     // starts under the shop wall so the NavMesh joins
        public float KerbZ         = 15f;
        public float RoadWidth     = 10f;
        public float FarPavementZ  = 31f;
        public float HalfLength    = 70f;    // street is built this far each way along X
        public float WalkableHalf  = 26f;    // ...but only this much of it is walkable

        [Header("Shop footprint (must match ShopGenerator)")]
        public float ShopHalfWidth = 10f;

        /// <summary>
        /// Yaw that turns a city building to face +Z, i.e. out towards the road from the
        /// shop's side of the street.
        ///
        /// Derived, not guessed: the city pack's own demo scene was parsed and each
        /// building's nearest road measured in the building's local frame. +Z won 28 votes
        /// to -Z's 15 across 56 buildings, so the models are authored facing +Z and need no
        /// correction. If the parade ever shows its back, flip this by 180.
        /// </summary>
        public const float BuildingFacing = 0f;

        public Transform StreetRoot { get; private set; }

        public Vector3 PavementCentre => new(0f, 0f, (PavementBackZ + KerbZ) * 0.5f + 1f);
        public float   PavementSpread => WalkableHalf - 3f;

        private float RoadCentreZ => KerbZ + RoadWidth * 0.5f;

        private readonly List<GameObject> _props    = new();
        private readonly List<GameObject> _barriers = new();
        private StreetBuildContext _ctx;

        [Header("City behind the street")]
        public float CityHalfWidth = 190f;
        public float BlockWidth    = 60f;
        [Tooltip("Rows of blocks. The furthest must still fall inside the fog fade.")]
        public int   CityRows      = 4;

        // ── Entry point ─────────────────────────────────────────────────────────

        public void Generate(Transform parent, int seed = 20240919)
        {
            var rng = new System.Random(seed);

            StreetRoot = new GameObject("Street").transform;
            StreetRoot.SetParent(parent, false);

            _ctx = new StreetBuildContext(this, StreetRoot, rng, _props, _barriers);
            var city      = new CityBlockBuilder(_ctx);
            var furniture = new StreetFurnitureBuilder(_ctx);

            BuildGroundPlanes();
            BuildRoadSurface();
            BuildParade();
            BuildOppositeBlock();
            city.BuildCityBlocks();
            city.BuildBackdrop();
            furniture.BuildStreetFurniture();
            furniture.BuildParkedCars();
            BuildBoundaries();

            ModelLibrary.SetLayer(StreetRoot.gameObject, GameLayers.Scenery);

            // Barriers still collide and still bake into the NavMesh, but the camera must be
            // able to see straight through them.
            foreach (var barrier in _barriers)
                if (barrier != null) barrier.layer = GameLayers.Ghost;

            Debug.Log($"[Street] Built {_props.Count} street objects.");
        }

        // ── Ground ──────────────────────────────────────────────────────────────

        private void BuildGroundPlanes()
        {
            float depth   = KerbZ - PavementBackZ;
            var   paveMat = MaterialFactory.Get("pavement", new Color(0.56f, 0.55f, 0.53f), 0f, 0.18f);

            // Drawn the full length of the street, but only collides near the shop: the
            // NavMesh bake volume comes from colliders, and this is what keeps it small.
            var pavement = MeshBuilder.CreateBox(HalfLength * 2f, 0.12f, depth, paveMat, "Pavement");
            _ctx.Place(pavement, new Vector3(0f, -0.06f, PavementBackZ + depth * 0.5f), collider: false);

            var walkable = new GameObject("PavementWalkable");
            walkable.transform.SetParent(StreetRoot, false);
            walkable.transform.position = new Vector3(0f, -0.06f, PavementBackZ + depth * 0.5f);
            walkable.AddComponent<BoxCollider>().size = new Vector3(WalkableHalf * 2f, 0.12f, depth);
            _props.Add(walkable);

            var kerb = MeshBuilder.CreateBox(HalfLength * 2f, 0.16f, 0.35f,
                MaterialFactory.Get("kerb", new Color(0.66f, 0.65f, 0.62f), 0f, 0.2f), "Kerb");
            _ctx.Place(kerb, new Vector3(0f, -0.02f, KerbZ - 0.15f), collider: false);

            float farDepth = FarPavementZ - (KerbZ + RoadWidth);
            var farPave = MeshBuilder.CreateBox(HalfLength * 2f, 0.12f, farDepth, paveMat, "FarPavement");
            _ctx.Place(farPave, new Vector3(0f, -0.06f, KerbZ + RoadWidth + farDepth * 0.5f), collider: false);

            var ground = MeshBuilder.CreateBox(1400f, 0.1f, 1400f,
                MaterialFactory.Get("ground", new Color(0.33f, 0.37f, 0.29f), 0f, 0.12f), "Ground");
            _ctx.Place(ground, new Vector3(0f, -0.22f, 140f), collider: false);
        }

        /// <summary>
        /// A row of road tiles. The street-pack tile is longer than it is wide, so it is
        /// turned 90° to run along X and sized by its across-the-street dimension.
        /// </summary>
        private void BuildRoadSurface()
        {
            string tile = StreetModels.Road;
            if (tile == null)
            {
                var asphalt = MeshBuilder.CreateBox(HalfLength * 2f, 0.1f, RoadWidth,
                    MaterialFactory.Get("asphalt", new Color(0.24f, 0.24f, 0.26f), 0f, 0.3f), "Asphalt");
                _ctx.Place(asphalt, new Vector3(0f, -0.1f, RoadCentreZ), collider: false);
                return;
            }

            bool streetPack = tile.StartsWith("Packs/");
            float yaw   = streetPack ? 90f : 0f;
            var   fit   = streetPack ? ModelLibrary.Fit.Width : ModelLibrary.Fit.Width;
            Vector3 size = ModelLibrary.SizeAfterFit(tile, fit, RoadWidth);
            float step  = streetPack ? size.z : size.x;   // length once turned along X
            if (step < 1f) step = RoadWidth;

            int tiles = Mathf.CeilToInt(HalfLength * 2f / step) + 1;
            for (int i = 0; i <= tiles; i++)
            {
                float x = -tiles * step * 0.5f + i * step;
                string model = Mathf.Abs(x) < step * 0.5f && StreetModels.Crossing != null ? StreetModels.Crossing : tile;

                var go = ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, -0.02f, RoadCentreZ),
                                            yaw, fit, RoadWidth);
                _ctx.Track(go);
                ModelLibrary.SetShadows(go, cast: false, receive: true);
            }
        }

        // ── Buildings ───────────────────────────────────────────────────────────

        /// <summary>The parade of shops the pet shop sits in, fronts flush with its own.</summary>
        private void BuildParade()
        {
            foreach (int side in new[] { -1, 1 })
            {
                float edge = ShopHalfWidth;
                int   i    = side > 0 ? 3 : 0;

                while (edge < HalfLength - 6f)
                {
                    string model = ModelLibrary.FirstAvailable(
                        StreetModels.ParadeShops[i % StreetModels.ParadeShops.Length],
                        ModelLibrary.Commercial + "building-a");
                    if (model == null) return;

                    float height = _ctx.Random(8f, 11.5f);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) return;

                    float x = side * (edge + size.x * 0.5f);
                    float z = ShopFrontZ - size.z * 0.5f;

                    _ctx.Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, 0f, z),
                                             BuildingFacing, ModelLibrary.Fit.Height, height));
                    _ctx.AddBlockingCollider(new Vector3(x, height * 0.5f, z), new Vector3(size.x, height, size.z));

                    edge += size.x + _ctx.Random(0.1f, 0.6f);
                    i++;
                }
            }
        }

        /// <summary>The block across the road, facing back towards the shop.</summary>
        private void BuildOppositeBlock()
        {
            float x = -HalfLength;
            int   i = 0;

            while (x < HalfLength)
            {
                string model = ModelLibrary.FirstAvailable(
                    StreetModels.OppositeShops[i % StreetModels.OppositeShops.Length],
                    ModelLibrary.Commercial + "building-f");
                if (model == null) return;

                float height = _ctx.Random(8f, 14f);
                Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                if (size.x < 1f) return;

                float z = FarPavementZ + size.z * 0.5f;
                _ctx.Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x + size.x * 0.5f, 0f, z),
                                         BuildingFacing + 180f, ModelLibrary.Fit.Height, height));

                x += size.x + _ctx.Random(0.5f, 3f);
                i++;
            }
        }

        // ── Collision ───────────────────────────────────────────────────────────

        private void BuildBoundaries()
        {
            _ctx.AddBlockingCollider(new Vector3(0f, 1.25f, KerbZ + 0.15f),
                                new Vector3(WalkableHalf * 2f, 2.5f, 0.3f), "KerbBarrier");

            // Behind the pavement, but only outside the shop's own frontage — the doorway
            // must stay open or customers can never get in.
            float wingWidth = WalkableHalf - ShopHalfWidth;
            foreach (int side in new[] { -1, 1 })
            {
                _ctx.AddBlockingCollider(new Vector3(side * (ShopHalfWidth + wingWidth * 0.5f), 1.25f, PavementBackZ - 0.15f),
                                    new Vector3(wingWidth, 2.5f, 0.3f), "BackBarrier");
                _ctx.AddBlockingCollider(new Vector3(side * WalkableHalf, 1.25f, (PavementBackZ + KerbZ) * 0.5f),
                                    new Vector3(0.3f, 2.5f, KerbZ - PavementBackZ), "EndBarrier");
            }
        }
    }
}
