using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Dresses the yard: surfacing, the paddock fence and shelter, planting, the display
    /// garden and the working props. Owns the yard's own seeded random stream.
    /// </summary>
    internal class YardDresser
    {
        private readonly ShopBuildContext _ctx;

        public YardDresser(ShopBuildContext ctx)
        {
            _ctx = ctx;
        }

        private System.Random _yardRng;
        private float YRand(float a, float b) => (float)(_yardRng.NextDouble() * (b - a) + a);

        public void DressYard()
        {
            var yard = new GameObject("YardDressing").transform;
            yard.SetParent(_ctx.ShopRoot, false);

            _yardRng = new System.Random(4242);

            BuildYardSurfaces(yard);
            BuildPaddockFence(yard);
            BuildPaddockShelter(yard);
            BuildYardPlanting(yard);
            BuildGarden(yard);
            BuildYardProps(yard);
        }

        /// <summary>
        /// Surfacing. An open lot of flat grass reads as an unfinished level, so the yard is
        /// broken into legible zones: paved circulation, a sanded paddock, and planting.
        /// </summary>
        private void BuildYardSurfaces(Transform parent)
        {
            var paving = MaterialFactory.Get("yard_paving", new Color(0.58f, 0.57f, 0.54f), 0f, 0.15f);
            var sand   = MaterialFactory.Get("paddock_sand", new Color(0.64f, 0.56f, 0.40f), 0f, 0.1f);

            var approach = MeshBuilder.CreateBox(5.5f, 0.06f, _ctx.ShopFrontMargin + 1f, paving, "PathApproach");
            Slab(approach, parent, new Vector3(_ctx.ShopCentre.x, 0.02f,
                                               _ctx.YardDepth * 0.5f - (_ctx.ShopFrontMargin + 1f) * 0.5f));

            float spineFrom = _ctx.ShopCentre.x;
            float spineTo   = _ctx.PaddockArea.x + _ctx.PaddockArea.width * 0.5f;
            var spine = MeshBuilder.CreateBox(spineTo - spineFrom + 6f, 0.06f, 4.2f, paving, "PathSpine");
            Slab(spine, parent, new Vector3((spineFrom + spineTo) * 0.5f, 0.02f, _ctx.PathZ));

            var paddock = MeshBuilder.CreateBox(_ctx.PaddockArea.width, 0.05f, _ctx.PaddockArea.height, sand, "PaddockGround");
            Slab(paddock, parent, new Vector3(_ctx.PaddockArea.x + _ctx.PaddockArea.width * 0.5f, 0.015f,
                                              _ctx.PaddockArea.y + _ctx.PaddockArea.height * 0.5f));

            var spur = MeshBuilder.CreateBox(3.2f, 0.06f, 5f, paving, "PathSpur");
            Slab(spur, parent, new Vector3(_ctx.PaddockArea.x + _ctx.PaddockArea.width * 0.5f, 0.025f, _ctx.PathZ - 4f));
        }

        private void Slab(GameObject go, Transform parent, Vector3 position)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Object.Destroy(go.GetComponent<Collider>());
        }

        /// <summary>A post-and-rail fence round the paddock, with a gap onto the path.</summary>
        private void BuildPaddockFence(Transform parent)
        {
            var wood = MaterialFactory.Get("paddock_rail", new Color(0.62f, 0.47f, 0.30f), 0f, 0.2f);
            float x0 = _ctx.PaddockArea.x, x1 = _ctx.PaddockArea.x + _ctx.PaddockArea.width;
            float z0 = _ctx.PaddockArea.y, z1 = _ctx.PaddockArea.y + _ctx.PaddockArea.height;
            float gateCentre = _ctx.PaddockArea.x + _ctx.PaddockArea.width * 0.5f;

            void Run(Vector3 from, Vector3 to, float gapAt = float.NaN, float gapWidth = 0f)
            {
                float length = Vector3.Distance(from, to);
                int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.2f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 at = Vector3.Lerp(from, to, i / (float)posts);
                    if (!float.IsNaN(gapAt) && Mathf.Abs(at.x - gapAt) < gapWidth * 0.5f) continue;

                    const float postHeight = 1.1f;
                    var post = MeshBuilder.CreateBox(0.12f, postHeight, 0.12f, wood, "PaddockPost");
                    post.transform.SetParent(parent, false);
                    // Setting position drops CreateBox's base lift; stand the post on the ground.
                    post.transform.position = at + Vector3.up * (postHeight * 0.5f);
                }
                foreach (float y in new[] { 0.55f, 0.95f })
                {
                    Vector3 mid = (from + to) * 0.5f; mid.y = y;
                    bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
                    var rail = MeshBuilder.CreateBox(alongX ? length : 0.08f, 0.08f,
                                                     alongX ? 0.08f : length, wood, "PaddockRail");
                    rail.transform.SetParent(parent, false);
                    rail.transform.position = mid;
                    Object.Destroy(rail.GetComponent<Collider>());
                }
            }

            Run(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            Run(new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), gateCentre, 4.5f);
            Run(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1));
            Run(new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
        }

        /// <summary>
        /// An open pergola over the pen rows: posts, beams and slatted rafters. Gives the
        /// animals shade and the paddock a bit of height, which a flat field of hutches
        /// badly needed.
        /// </summary>
        private void BuildPaddockShelter(Transform parent)
        {
            var post  = MaterialFactory.Get("pergola_post", new Color(0.55f, 0.40f, 0.26f), 0f, 0.2f);
            var beam  = MaterialFactory.Get("pergola_beam", new Color(0.62f, 0.47f, 0.31f), 0f, 0.2f);
            var slat  = MaterialFactory.Get("pergola_slat", new Color(0.68f, 0.54f, 0.36f), 0f, 0.2f);

            const float height = 3.1f;

            // Covers the pen rows, not the whole paddock — the gate end stays open.
            float x0 = _ctx.PaddockArea.x + 1.5f;
            float x1 = _ctx.PaddockArea.x + _ctx.PaddockArea.width - 5.5f;
            float z0 = _ctx.PaddockArea.y + 3.5f;
            float z1 = _ctx.PaddockArea.y + _ctx.PaddockArea.height - 1.5f;

            foreach (float x in new[] { x0, (x0 + x1) * 0.5f, x1 })
            foreach (float z in new[] { z0, z1 })
            {
                var leg = MeshBuilder.CreateBox(0.18f, height, 0.18f, post, "PergolaPost");
                leg.transform.SetParent(parent, false);
                // Setting position drops CreateBox's base lift; centre the leg so it runs ground to beam.
                leg.transform.position = new Vector3(x, height * 0.5f, z);
            }

            // Beams along X over each row of posts
            foreach (float z in new[] { z0, z1 })
            {
                var run = MeshBuilder.CreateBox(x1 - x0 + 0.6f, 0.18f, 0.16f, beam, "PergolaBeam");
                run.transform.SetParent(parent, false);
                run.transform.position = new Vector3((x0 + x1) * 0.5f, height, z);
                Object.Destroy(run.GetComponent<Collider>());
            }

            // Slatted rafters across, spaced so light still reaches the pens
            for (float x = x0; x <= x1 + 0.01f; x += 0.85f)
            {
                var rafter = MeshBuilder.CreateBox(0.1f, 0.12f, z1 - z0 + 0.7f, slat, "PergolaRafter");
                rafter.transform.SetParent(parent, false);
                rafter.transform.position = new Vector3(x, height + 0.15f, (z0 + z1) * 0.5f);
                Object.Destroy(rafter.GetComponent<Collider>());
            }

            // A short solid canopy at the far end, for real shade
            var canopy = MeshBuilder.CreateBox(x1 - x0 + 0.6f, 0.08f, 3.2f,
                MaterialFactory.Get("pergola_canopy", new Color(0.46f, 0.50f, 0.42f), 0f, 0.15f), "PergolaCanopy");
            canopy.transform.SetParent(parent, false);
            canopy.transform.position = new Vector3((x0 + x1) * 0.5f, height + 0.24f, z0 + 1.2f);
            Object.Destroy(canopy.GetComponent<Collider>());
        }

        /// <summary>Hedging and beds along the walls, so the boundary is not a bare line.</summary>
        private void BuildYardPlanting(Transform parent)
        {
            float hw = _ctx.YardWidth * 0.5f, hd = _ctx.YardDepth * 0.5f;

            string hedge = ModelLibrary.FirstAvailable("Packs/Nature/Bush_03", "Packs/Nature/Bush_01",
                                                        ModelLibrary.Nature + "plant_bush");
            string tree  = ModelLibrary.FirstAvailable("Packs/Street/Foliage/Trees/Tree_A_V01",
                                                        "Packs/Nature/Tree_02",
                                                        ModelLibrary.Nature + "tree_default");
            string grass = ModelLibrary.FirstAvailable("Packs/Nature/Grass_01", ModelLibrary.Nature + "grass_large");

            var bedMat = MaterialFactory.Get("yard_bed", new Color(0.32f, 0.25f, 0.18f), 0f, 0.1f);

            var backBed = MeshBuilder.CreateBox(_ctx.YardWidth - 4f, 0.08f, 3.4f, bedMat, "BackBed");
            Slab(backBed, parent, new Vector3(0f, 0.03f, -hd + 2.2f));

            for (float x = -hw + 4f; x < hw - 4f; x += YRand(2.2f, 3.6f))
                YardProp(hedge, parent, new Vector3(x, 0.06f, -hd + YRand(1.4f, 3f)),
                         YRand(0f, 360f), YRand(0.9f, 1.5f));

            for (float x = -hw + 7f; x < hw - 6f; x += YRand(9f, 14f))
                YardProp(tree, parent, new Vector3(x, 0f, -hd + 3.4f), YRand(0f, 360f), YRand(5f, 7f), block: true);

            var westBed = MeshBuilder.CreateBox(2.6f, 0.08f, _ctx.YardDepth - 8f, bedMat, "WestBed");
            Slab(westBed, parent, new Vector3(-hw + 1.6f, 0.03f, -2f));
            for (float z = -hd + 6f; z < hd - 8f; z += YRand(2.4f, 4f))
                YardProp(hedge, parent, new Vector3(-hw + YRand(0.9f, 2.4f), 0.06f, z),
                         YRand(0f, 360f), YRand(0.8f, 1.3f));

            // Scatter tufts over the open grass only. Without the shop test, tufts sprout
            // through the shop floor and show up as weeds growing indoors.
            var shopFootprint = new Rect(_ctx.ShopCentre.x - _ctx.RoomWidth * 0.5f - 1f,
                                         _ctx.ShopCentre.z - _ctx.RoomDepth * 0.5f - 1f,
                                         _ctx.RoomWidth + 2f, _ctx.RoomDepth + 2f);

            for (int i = 0; i < 40; i++)
            {
                float x = YRand(-hw + 3f, hw - 3f), z = YRand(-hd + 6f, hd - 3f);
                var at = new Vector2(x, z);

                if (_ctx.PaddockArea.Contains(at))    continue;
                if (shopFootprint.Contains(at))  continue;
                if (Mathf.Abs(z - _ctx.PathZ) < 3f)   continue;
                if (z > _ctx.YardDepth * 0.5f - _ctx.ShopFrontMargin - 1f) continue;   // keep the forecourt clear

                YardProp(grass, parent, new Vector3(x, 0f, z), YRand(0f, 360f), YRand(0.3f, 0.6f));
            }
        }

        /// <summary>
        /// A small display garden in the gap between the shop and the paddock. That stretch
        /// was the largest patch of featureless grass in the lot, which a plan view makes
        /// obvious and an eye-level shot does not.
        /// </summary>
        private void BuildGarden(Transform parent)
        {
            float cx = (_ctx.ShopCentre.x + _ctx.RoomWidth * 0.5f + _ctx.PaddockArea.x) * 0.5f;
            float cz = _ctx.PathZ - 7f;

            var bedMat = MaterialFactory.Get("garden_bed", new Color(0.34f, 0.27f, 0.19f), 0f, 0.1f);
            var edging = MaterialFactory.Get("garden_edge", new Color(0.60f, 0.58f, 0.54f), 0f, 0.15f);

            var bed = MeshBuilder.CreateBox(9f, 0.1f, 7f, bedMat, "GardenBed");
            Slab(bed, parent, new Vector3(cx, 0.04f, cz));

            foreach (var (size, offset) in new[]
            {
                (new Vector3(9.4f, 0.18f, 0.3f), new Vector3(0f, 0f,  3.5f)),
                (new Vector3(9.4f, 0.18f, 0.3f), new Vector3(0f, 0f, -3.5f)),
                (new Vector3(0.3f, 0.18f, 7f),   new Vector3( 4.55f, 0f, 0f)),
                (new Vector3(0.3f, 0.18f, 7f),   new Vector3(-4.55f, 0f, 0f)),
            })
            {
                var kerb = MeshBuilder.CreateBox(size.x, size.y, size.z, edging, "GardenKerb");
                kerb.transform.SetParent(parent, false);
                kerb.transform.position = new Vector3(cx, 0f, cz) + offset;
                Object.Destroy(kerb.GetComponent<Collider>());
            }

            string shrub  = ModelLibrary.FirstAvailable("Packs/Nature/Bush_02", "Packs/Nature/Bush_01");
            string flower = ModelLibrary.FirstAvailable("Packs/Nature/Flowers_01", "Packs/Nature/Flowers_02");
            string tree   = ModelLibrary.FirstAvailable("Packs/Nature/Tree_03", "Packs/Nature/Tree_01");

            YardProp(tree, parent, new Vector3(cx, 0f, cz), 0f, YRand(4.5f, 5.5f), block: true);
            for (int i = 0; i < 10; i++)
                YardProp(i % 3 == 0 ? shrub : flower, parent,
                         new Vector3(cx + YRand(-3.8f, 3.8f), 0.08f, cz + YRand(-2.8f, 2.8f)),
                         YRand(0f, 360f), YRand(0.4f, 0.9f));

            string bench = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/Bench/Bench_A",
                                                        ModelLibrary.Furniture + "benchCushion");
            YardProp(bench, parent, new Vector3(cx - 5.6f, 0f, cz), 0f, 1.0f);
            YardProp(bench, parent, new Vector3(cx + 5.6f, 0f, cz), 180f, 1.0f);
        }

        /// <summary>The things that make a yard look worked in.</summary>
        private void BuildYardProps(Transform parent)
        {
            float hw = _ctx.YardWidth * 0.5f;
            float paddockLeft = _ctx.PaddockArea.x;

            var straw = MaterialFactory.Get("straw_bale", new Color(0.82f, 0.71f, 0.38f), 0f, 0.12f);
            foreach (var offset in new[] { new Vector3(0f, 0f, 0f), new Vector3(1.05f, 0f, 0.1f),
                                           new Vector3(0.5f, 0.72f, 0.05f) })
            {
                var bale = MeshBuilder.CreateBox(1f, 0.7f, 0.7f, straw, "StrawBale");
                bale.transform.SetParent(parent, false);
                bale.transform.position = new Vector3(paddockLeft + 1.6f, 0f, _ctx.PaddockArea.y + 1.8f) + offset;
                bale.transform.localEulerAngles = new Vector3(0f, YRand(-8f, 8f), 0f);
            }

            var trough = MeshBuilder.CreateBox(2.2f, 0.45f, 0.8f,
                MaterialFactory.Get("trough", new Color(0.42f, 0.44f, 0.46f), 0.2f, 0.3f), "WaterTrough");
            trough.transform.SetParent(parent, false);
            trough.transform.position = new Vector3(paddockLeft + _ctx.PaddockArea.width - 3f, 0f,
                                                     _ctx.PaddockArea.y + 2f);

            var water = MeshBuilder.CreateBox(1.95f, 0.06f, 0.6f,
                MaterialFactory.Get("trough_water", new Color(0.35f, 0.55f, 0.65f), 0.1f, 0.85f), "Water");
            water.transform.SetParent(trough.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.86f, 0f);
            Object.Destroy(water.GetComponent<Collider>());

            string bench = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/Bench/Bench_A",
                                                        ModelLibrary.Furniture + "benchCushion");
            foreach (float dx in new[] { -5.5f, 5.5f })
                YardProp(bench, parent, _ctx.ShopCentre + new Vector3(dx, 0f, _ctx.RoomDepth * 0.5f + 3.4f), 90f, 1.0f);

            YardProp(ModelLibrary.FirstAvailable("Packs/City/Props/Props_Dustbin",
                                                  ModelLibrary.Furniture + "trashcan"),
                     parent, _ctx.ShopCentre + new Vector3(_ctx.RoomWidth * 0.5f + 1.2f, 0f, _ctx.RoomDepth * 0.5f + 1.2f),
                     0f, 1.0f);

            // Planters flanking the shop door. Fitted by WIDTH: PlantPot_A is a wide shallow
            // trough, so sizing it by height blows it up into a mound of earth taller than
            // the doorway.
            string pot = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/PlantPots/Elements/PlantPot_A",
                                                      ModelLibrary.Furniture + "pottedPlant");
            foreach (float dx in new[] { -3.4f, 3.4f })
            {
                var planter = ModelLibrary.Spawn(pot, parent,
                    _ctx.ShopCentre + new Vector3(dx, 0f, _ctx.RoomDepth * 0.5f + 1.1f),
                    0f, ModelLibrary.Fit.Width, 1.3f);
                if (planter != null) ModelLibrary.SetLayer(planter, GameLayers.Scenery);

                YardProp(ModelLibrary.FirstAvailable("Packs/Nature/Bush_02", "Packs/Nature/Flowers_01"),
                         parent, _ctx.ShopCentre + new Vector3(dx, 0.25f, _ctx.RoomDepth * 0.5f + 1.1f),
                         YRand(0f, 360f), 0.7f);
            }

            string lamp = ModelLibrary.FirstAvailable("Packs/Street/StreetProps/ParkLamp/ParkLamp",
                                                       ModelLibrary.Roads + "light-square");
            foreach (var pos in new[] { new Vector3(_ctx.ShopCentre.x + 9f, 0f, _ctx.PathZ + 2.6f),
                                        new Vector3(_ctx.PaddockArea.x - 2f, 0f, _ctx.PathZ + 2.6f),
                                        new Vector3(-hw + 3f, 0f, -_ctx.YardDepth * 0.5f + 8f),
                                        new Vector3( hw - 3f, 0f, -_ctx.YardDepth * 0.5f + 8f) })
                YardProp(lamp, parent, pos, 0f, 5f);
        }

        private void YardProp(string model, Transform parent, Vector3 pos, float yRot, float size, bool block = false)
        {
            if (model == null) return;
            var go = ModelLibrary.Spawn(model, parent, pos, yRot, ModelLibrary.Fit.Height, size);
            if (go == null) return;
            ModelLibrary.SetLayer(go, GameLayers.Scenery);

            if (!block) return;
            var blocker = new GameObject("PropBlocker");
            blocker.transform.SetParent(parent, false);
            blocker.transform.position = pos + Vector3.up;
            blocker.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            blocker.layer = GameLayers.Ghost;
        }
    }
}
