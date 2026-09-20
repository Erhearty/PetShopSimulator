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
        private System.Random _rng;

        // ── Model choices, best pack first ──────────────────────────────────────

        private static string Road      => ModelLibrary.FirstAvailable("Packs/Street/Roads/Streets/Road_Streight",
                                                                        ModelLibrary.Roads + "road-straight");
        private static string Crossing  => ModelLibrary.FirstAvailable("Packs/Street/Roads/Streets/Road_Crosswalk",
                                                                        ModelLibrary.Roads + "road-crossing");
        // "TraficLights/LampPost_*" really are traffic lights, whatever the file is called —
        // one every 19 m along a high street looked absurd. ParkLamp is the actual lamp post.
        private static string LampPost => ModelLibrary.FirstAvailable("Packs/Street/StreetProps/ParkLamp/ParkLamp",
                                                                      ModelLibrary.Roads + "light-square");

        private static string TrafficLight => ModelLibrary.FirstAvailable(
            "Packs/Street/StreetProps/TraficLights/LampPost_A", ModelLibrary.Roads + "traffic-light");

        private static readonly string[] ParadeShops =
        {
            "Packs/City/Buildings/Building_Bakery",     "Packs/City/Buildings/Building_Books Shop",
            "Packs/City/Buildings/Building_Coffee Shop","Packs/City/Buildings/Building_Gift Shop",
            "Packs/City/Buildings/Building_Clothing",   "Packs/City/Buildings/Building_Music Store",
            "Packs/City/Buildings/Building_Drug Store", "Packs/City/Buildings/Building_Bar",
        };

        private static readonly string[] OppositeShops =
        {
            "Packs/City/Buildings/Building_Pizza",       "Packs/City/Buildings/Building_Fast Food",
            "Packs/City/Buildings/Building_Fruits  Shop","Packs/City/Buildings/Building_Chicken Shop",
            "Packs/City/Buildings/Building_Residential_color01",
            "Packs/City/Buildings/Building_Residential_color02",
            "Packs/City/Buildings/Building_House_01_color01",
            "Packs/City/Buildings/Building_House_02_color02",
        };

        [Header("City behind the street")]
        public float CityHalfWidth = 190f;
        public float BlockWidth    = 60f;
        [Tooltip("Rows of blocks. The furthest must still fall inside the fog fade.")]
        public int   CityRows      = 4;

        private static readonly string[] CityFill =
        {
            "Packs/City/Buildings/Building_Residential_color01",
            "Packs/City/Buildings/Building_Residential_color02",
            "Packs/City/Buildings/Building_Residential_color03",
            "Packs/City/Buildings/Building_House_01_color01",
            "Packs/City/Buildings/Building_House_02_color02",
            "Packs/City/Buildings/Building_House_03_color03",
            "Packs/City/Buildings/Building_Clothing",
            "Packs/City/Buildings/Building_Factory",
            ModelLibrary.Commercial + "building-l", ModelLibrary.Commercial + "building-m",
            ModelLibrary.Commercial + "building-n", ModelLibrary.Commercial + "building-skyscraper-a",
            ModelLibrary.Commercial + "building-skyscraper-c",
        };

        private static readonly string[] Towers =
        {
            "Packs/City/Buildings/Building_Residential_color03",
            ModelLibrary.Commercial + "building-skyscraper-a", ModelLibrary.Commercial + "building-skyscraper-b",
            ModelLibrary.Commercial + "building-skyscraper-c", ModelLibrary.Commercial + "building-skyscraper-d",
        };

        private static readonly string[] Cars =
        {
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01",
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color02",
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color03",
            ModelLibrary.Cars + "sedan", ModelLibrary.Cars + "suv", ModelLibrary.Cars + "van",
            ModelLibrary.Cars + "taxi", ModelLibrary.Cars + "delivery",
        };

        private static readonly string[] Trees =
        {
            "Packs/Street/Foliage/Trees/Tree_A_V01", "Packs/Street/Foliage/Trees/Tree_A_V02_Leaves03",
            "Packs/Street/Foliage/Trees/Tree_B_V01_Leaves02",
            "Packs/Nature/Tree_01", "Packs/Nature/Tree_02", "Packs/Nature/Tree_03",
            ModelLibrary.Nature + "tree_default", ModelLibrary.Nature + "tree_oak",
        };

        // ── Entry point ─────────────────────────────────────────────────────────

        public void Generate(Transform parent, int seed = 20240919)
        {
            _rng = new System.Random(seed);

            StreetRoot = new GameObject("Street").transform;
            StreetRoot.SetParent(parent, false);

            BuildGroundPlanes();
            BuildRoadSurface();
            BuildParade();
            BuildOppositeBlock();
            BuildCityBlocks();
            BuildBackdrop();
            BuildStreetFurniture();
            BuildParkedCars();
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
            Place(pavement, new Vector3(0f, -0.06f, PavementBackZ + depth * 0.5f), collider: false);

            var walkable = new GameObject("PavementWalkable");
            walkable.transform.SetParent(StreetRoot, false);
            walkable.transform.position = new Vector3(0f, -0.06f, PavementBackZ + depth * 0.5f);
            walkable.AddComponent<BoxCollider>().size = new Vector3(WalkableHalf * 2f, 0.12f, depth);
            _props.Add(walkable);

            var kerb = MeshBuilder.CreateBox(HalfLength * 2f, 0.16f, 0.35f,
                MaterialFactory.Get("kerb", new Color(0.66f, 0.65f, 0.62f), 0f, 0.2f), "Kerb");
            Place(kerb, new Vector3(0f, -0.02f, KerbZ - 0.15f), collider: false);

            float farDepth = FarPavementZ - (KerbZ + RoadWidth);
            var farPave = MeshBuilder.CreateBox(HalfLength * 2f, 0.12f, farDepth, paveMat, "FarPavement");
            Place(farPave, new Vector3(0f, -0.06f, KerbZ + RoadWidth + farDepth * 0.5f), collider: false);

            var ground = MeshBuilder.CreateBox(1400f, 0.1f, 1400f,
                MaterialFactory.Get("ground", new Color(0.33f, 0.37f, 0.29f), 0f, 0.12f), "Ground");
            Place(ground, new Vector3(0f, -0.22f, 140f), collider: false);
        }

        /// <summary>
        /// A row of road tiles. The street-pack tile is longer than it is wide, so it is
        /// turned 90° to run along X and sized by its across-the-street dimension.
        /// </summary>
        private void BuildRoadSurface()
        {
            string tile = Road;
            if (tile == null)
            {
                var asphalt = MeshBuilder.CreateBox(HalfLength * 2f, 0.1f, RoadWidth,
                    MaterialFactory.Get("asphalt", new Color(0.24f, 0.24f, 0.26f), 0f, 0.3f), "Asphalt");
                Place(asphalt, new Vector3(0f, -0.1f, RoadCentreZ), collider: false);
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
                string model = Mathf.Abs(x) < step * 0.5f && Crossing != null ? Crossing : tile;

                var go = ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, -0.02f, RoadCentreZ),
                                            yaw, fit, RoadWidth);
                Track(go);
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
                        ParadeShops[i % ParadeShops.Length],
                        ModelLibrary.Commercial + "building-a");
                    if (model == null) return;

                    float height = Random(8f, 11.5f);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) return;

                    float x = side * (edge + size.x * 0.5f);
                    float z = ShopFrontZ - size.z * 0.5f;

                    Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, 0f, z),
                                             BuildingFacing, ModelLibrary.Fit.Height, height));
                    AddBlockingCollider(new Vector3(x, height * 0.5f, z), new Vector3(size.x, height, size.z));

                    edge += size.x + Random(0.1f, 0.6f);
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
                    OppositeShops[i % OppositeShops.Length],
                    ModelLibrary.Commercial + "building-f");
                if (model == null) return;

                float height = Random(8f, 14f);
                Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                if (size.x < 1f) return;

                float z = FarPavementZ + size.z * 0.5f;
                Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x + size.x * 0.5f, 0f, z),
                                         BuildingFacing + 180f, ModelLibrary.Fit.Height, height));

                x += size.x + Random(0.5f, 3f);
                i++;
            }
        }

        /// <summary>
        /// The city behind the opposite parade: proper blocks separated by cross streets,
        /// with every building square to the grid.
        ///
        /// The earlier version scattered towers at random rotations across open ground,
        /// which from a plan view read as debris on a field rather than a city. Buildings
        /// line block edges and face the street they front.
        /// </summary>
        private void BuildCityBlocks()
        {
            const float blockDepth  = 52f;   // building band + back-to-back depth
            const float crossRoad   = 12f;   // gap between blocks running in Z
            const float avenue      = 14f;   // gap between blocks running in X

            float firstBlockZ = FarPavementZ + 16f;

            for (int row = 0; row < CityRows; row++)
            {
                float z0 = firstBlockZ + row * (blockDepth + crossRoad);
                BuildCrossStreet(z0 - crossRoad * 0.5f);

                for (float x0 = -CityHalfWidth; x0 < CityHalfWidth; x0 += BlockWidth + avenue)
                {
                    BuildBlock(x0, z0, Mathf.Min(BlockWidth, CityHalfWidth - x0), blockDepth, row);
                    BuildAvenue(x0 + BlockWidth, z0, blockDepth);
                }
            }
        }

        /// <summary>One city block: buildings round the edge, facing outward.</summary>
        private void BuildBlock(float x0, float z0, float width, float depth, int row)
        {
            if (width < 8f) return;

            // Taller the further back, so the skyline builds up rather than staying flat.
            float minH = 10f + row * 6f;
            float maxH = 18f + row * 12f;

            // Front row faces the shop (-Z); back row faces away (+Z).
            foreach (var (z, facing) in new[] { (z0, BuildingFacing + 180f), (z0 + depth, BuildingFacing) })
            {
                float x = x0;
                int   i = row * 3;
                while (x < x0 + width)
                {
                    string model = ModelLibrary.AnyAvailable(_rng, CityFill);
                    if (model == null) return;

                    float height = Random(minH, maxH);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) return;
                    if (x + size.x > x0 + width + 4f) break;

                    var go = ModelLibrary.Spawn(model, StreetRoot,
                        new Vector3(x + size.x * 0.5f, 0f, z + (facing > 90f ? -size.z : size.z) * 0.5f),
                        facing, ModelLibrary.Fit.Height, height);
                    FadeWithDistance(go, row);
                    Track(go);

                    x += size.x + Random(0.2f, 1.6f);
                    i++;
                }
            }

            // Side rows facing the avenues, so a block is ringed rather than two lonely
            // terraces with a hole in the middle.
            foreach (var (x, facing) in new[] { (x0, BuildingFacing + 90f), (x0 + width, BuildingFacing + 270f) })
            {
                float z = z0 + 10f;
                while (z < z0 + depth - 10f)
                {
                    string model = ModelLibrary.AnyAvailable(_rng, CityFill);
                    if (model == null) break;

                    float height = Random(minH, maxH);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) break;
                    if (z + size.x > z0 + depth - 6f) break;

                    var go = ModelLibrary.Spawn(model, StreetRoot,
                        new Vector3(x + (facing < 180f ? -size.z : size.z) * 0.5f, 0f, z + size.x * 0.5f),
                        facing, ModelLibrary.Fit.Height, height);
                    FadeWithDistance(go, row);
                    Track(go);

                    z += size.x + Random(0.2f, 1.6f);
                }
            }

            // Block interior: paved, so it does not read as open field from above.
            var slab = MeshBuilder.CreateBox(width, 0.08f, depth,
                MaterialFactory.Get("block_interior", new Color(0.40f, 0.42f, 0.38f), 0f, 0.1f), "BlockInterior");
            Place(slab, new Vector3(x0 + width * 0.5f, -0.05f, z0 + depth * 0.5f), collider: false);
        }

        /// <summary>
        /// A row of buildings behind and beside the yard. Without it the shop backs onto an
        /// empty field, which is obvious the moment you look at the plan view.
        /// </summary>
        private void BuildBackdrop()
        {
            float behindZ = -ShopHalfWidth * 0.55f - 26f;

            for (float x = -CityHalfWidth * 0.6f; x < CityHalfWidth * 0.6f; x += Random(2f, 8f))
            {
                string model = ModelLibrary.AnyAvailable(_rng, CityFill);
                if (model == null) return;

                float height = Random(9f, 16f);
                Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                if (size.x < 1f) return;

                Track(ModelLibrary.Spawn(model, StreetRoot,
                    new Vector3(x + size.x * 0.5f, 0f, behindZ - size.z * 0.5f),
                    BuildingFacing, ModelLibrary.Fit.Height, height));

                x += size.x;
            }
        }

        /// <summary>
        /// Washes distant blocks towards the fog colour and drops their shadows.
        ///
        /// Linear fog alone still leaves the far row reading as full-contrast buildings that
        /// then vanish; tinting them as well makes the city recede instead of ending.
        /// </summary>
        private void FadeWithDistance(GameObject go, int row)
        {
            if (go == null || row <= 0) return;

            float t = Mathf.Clamp01(row / (float)Mathf.Max(1, CityRows - 1)) * 0.72f;
            Color haze = RenderSettings.fog ? RenderSettings.fogColor : new Color(0.74f, 0.81f, 0.89f);

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", Color.Lerp(Color.white, haze, t));
                renderer.SetPropertyBlock(block);

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows    = false;
            }
        }

        private void BuildCrossStreet(float z)
        {
            var asphalt = MeshBuilder.CreateBox(CityHalfWidth * 2f + 40f, 0.08f, 11f,
                MaterialFactory.Get("asphalt", new Color(0.22f, 0.22f, 0.24f), 0f, 0.12f), "CrossStreet");
            Place(asphalt, new Vector3(0f, -0.04f, z), collider: false);
        }

        /// <summary>A road running away from the main street, between two blocks.</summary>
        private void BuildAvenue(float x, float z0, float depth)
        {
            var asphalt = MeshBuilder.CreateBox(12f, 0.08f, depth + 12f,
                MaterialFactory.Get("asphalt", new Color(0.22f, 0.22f, 0.24f), 0f, 0.12f), "Avenue");
            Place(asphalt, new Vector3(x + 6f, -0.04f, z0 + depth * 0.5f), collider: false);
        }

        // ── Street furniture ────────────────────────────────────────────────────

        private void BuildStreetFurniture()
        {
            float lampZ = KerbZ - 1.4f;
            float treeZ = KerbZ - 3.0f;

            for (float x = -HalfLength + 8f; x < HalfLength; x += 22f)
            {
                Track(ModelLibrary.Spawn(LampPost, StreetRoot, new Vector3(x, 0f, lampZ),
                                         0f, ModelLibrary.Fit.Height, 5.5f));
                Track(ModelLibrary.Spawn(LampPost, StreetRoot,
                                         new Vector3(x + 11f, 0f, KerbZ + RoadWidth + 1.4f), 180f,
                                         ModelLibrary.Fit.Height, 5.5f));
            }

            // Traffic lights belong at the crossing and the ends of the street, nowhere else.
            foreach (float x in new[] { -9f, 9f })
                Track(ModelLibrary.Spawn(TrafficLight, StreetRoot, new Vector3(x, 0f, KerbZ - 1f),
                                         x < 0f ? 0f : 180f, ModelLibrary.Fit.Height, 5.5f));

            // Trees, skipping the stretch directly in front of the shop window.
            for (float x = -HalfLength + 14f; x < HalfLength; x += Random(11f, 17f))
            {
                if (Mathf.Abs(x) < 8f) continue;

                string model = ModelLibrary.AnyAvailable(_rng, Trees);
                if (model == null) break;

                Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, 0f, treeZ),
                                         Random(0f, 360f), ModelLibrary.Fit.Height, Random(5f, 7.5f)));
                AddBlockingCollider(new Vector3(x, 1f, treeZ), new Vector3(1f, 2f, 1f));
            }

            // The bench model runs along its Z axis, so turn it to sit along the pavement.
            Prop("Packs/Street/StreetProps/Bench/Bench_A",   ModelLibrary.Furniture + "benchCushion", 1.0f,
                 new[] { -21f, 19f }, (PavementBackZ + KerbZ) * 0.5f - 1.5f, 90f);
            Prop("Packs/Street/StreetProps/MailBox/MailBox", null, 1.3f,
                 new[] { -13f }, KerbZ - 1.2f, 0f);
            Prop("Packs/Street/StreetProps/Hidrant/Hidrant", null, 0.9f,
                 new[] { 12f }, KerbZ - 1.2f, 0f);
            Prop("Packs/Street/StreetProps/NewsBoard/NewsBoard", null, 1.7f,
                 new[] { -7.5f }, KerbZ - 1.6f, 0f);
            Prop("Packs/City/Props/Props_Dustbin", ModelLibrary.Furniture + "trashcan", 1.0f,
                 new[] { -6.5f, 7f }, KerbZ - 1.6f, 0f);
        }

        private void Prop(string preferred, string fallback, float size, float[] xs, float z, float yRot)
        {
            string model = ModelLibrary.FirstAvailable(preferred, fallback);
            if (model == null) return;
            foreach (float x in xs)
                Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, 0f, z),
                                         yRot, ModelLibrary.Fit.Height, size));
        }

        private void BuildParkedCars()
        {
            foreach (var (z, heading) in new[] { (KerbZ + 2.2f, 90f), (KerbZ + RoadWidth - 2.2f, 270f) })
            {
                for (float x = -HalfLength + 10f; x < HalfLength - 10f; x += Random(11f, 26f))
                {
                    if (Mathf.Abs(x) < 7f) continue;   // keep the crossing clear

                    string model = ModelLibrary.AnyAvailable(_rng, Cars);
                    if (model == null) return;

                    Track(ModelLibrary.Spawn(model, StreetRoot, new Vector3(x, 0f, z),
                                             heading, ModelLibrary.Fit.Depth, Random(4.3f, 5.4f)));
                }
            }
        }

        // ── Collision ───────────────────────────────────────────────────────────

        private void BuildBoundaries()
        {
            AddBlockingCollider(new Vector3(0f, 1.25f, KerbZ + 0.15f),
                                new Vector3(WalkableHalf * 2f, 2.5f, 0.3f), "KerbBarrier");

            // Behind the pavement, but only outside the shop's own frontage — the doorway
            // must stay open or customers can never get in.
            float wingWidth = WalkableHalf - ShopHalfWidth;
            foreach (int side in new[] { -1, 1 })
            {
                AddBlockingCollider(new Vector3(side * (ShopHalfWidth + wingWidth * 0.5f), 1.25f, PavementBackZ - 0.15f),
                                    new Vector3(wingWidth, 2.5f, 0.3f), "BackBarrier");
                AddBlockingCollider(new Vector3(side * WalkableHalf, 1.25f, (PavementBackZ + KerbZ) * 0.5f),
                                    new Vector3(0.3f, 2.5f, KerbZ - PavementBackZ), "EndBarrier");
            }
        }

        private void AddBlockingCollider(Vector3 centre, Vector3 size, string name = "Blocker")
        {
            var go = new GameObject(name);
            go.transform.SetParent(StreetRoot, false);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            _barriers.Add(go);
        }

        // ── Utilities ───────────────────────────────────────────────────────────

        private void Place(GameObject go, Vector3 position, bool collider = true)
        {
            go.transform.SetParent(StreetRoot, false);
            go.transform.position = position;
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            _props.Add(go);
        }

        private void Track(GameObject go)
        {
            if (go != null) _props.Add(go);
        }

        private float Random(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);
    }
}
