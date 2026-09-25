using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Builds the city around the street: the grid of blocks behind the opposite parade,
    /// with their cross streets and avenues, and the row of buildings behind the shop.
    /// </summary>
    internal class CityBlockBuilder
    {
        private readonly StreetBuildContext _ctx;

        public CityBlockBuilder(StreetBuildContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// The city behind the opposite parade: proper blocks separated by cross streets,
        /// with every building square to the grid.
        ///
        /// The earlier version scattered towers at random rotations across open ground,
        /// which from a plan view read as debris on a field rather than a city. Buildings
        /// line block edges and face the street they front.
        /// </summary>
        public void BuildCityBlocks()
        {
            const float blockDepth  = 52f;   // building band + back-to-back depth
            const float crossRoad   = 12f;   // gap between blocks running in Z
            const float avenue      = 14f;   // gap between blocks running in X

            float firstBlockZ = _ctx.FarPavementZ + 16f;

            for (int row = 0; row < _ctx.CityRows; row++)
            {
                float z0 = firstBlockZ + row * (blockDepth + crossRoad);
                BuildCrossStreet(z0 - crossRoad * 0.5f);

                for (float x0 = -_ctx.CityHalfWidth; x0 < _ctx.CityHalfWidth; x0 += _ctx.BlockWidth + avenue)
                {
                    BuildBlock(x0, z0, Mathf.Min(_ctx.BlockWidth, _ctx.CityHalfWidth - x0), blockDepth, row);
                    BuildAvenue(x0 + _ctx.BlockWidth, z0, blockDepth);
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
            foreach (var (z, facing) in new[] { (z0, StreetGenerator.BuildingFacing + 180f), (z0 + depth, StreetGenerator.BuildingFacing) })
            {
                float x = x0;
                int   i = row * 3;
                while (x < x0 + width)
                {
                    string model = ModelLibrary.AnyAvailable(_ctx.Rng, StreetModels.CityFill);
                    if (model == null) return;

                    float height = _ctx.Random(minH, maxH);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) return;
                    if (x + size.x > x0 + width + 4f) break;

                    var go = ModelLibrary.Spawn(model, _ctx.StreetRoot,
                        new Vector3(x + size.x * 0.5f, 0f, z + (facing > 90f ? -size.z : size.z) * 0.5f),
                        facing, ModelLibrary.Fit.Height, height);
                    FadeWithDistance(go, row);
                    _ctx.Track(go);

                    x += size.x + _ctx.Random(0.2f, 1.6f);
                    i++;
                }
            }

            // Side rows facing the avenues, so a block is ringed rather than two lonely
            // terraces with a hole in the middle.
            foreach (var (x, facing) in new[] { (x0, StreetGenerator.BuildingFacing + 90f), (x0 + width, StreetGenerator.BuildingFacing + 270f) })
            {
                float z = z0 + 10f;
                while (z < z0 + depth - 10f)
                {
                    string model = ModelLibrary.AnyAvailable(_ctx.Rng, StreetModels.CityFill);
                    if (model == null) break;

                    float height = _ctx.Random(minH, maxH);
                    Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                    if (size.x < 1f) break;
                    if (z + size.x > z0 + depth - 6f) break;

                    var go = ModelLibrary.Spawn(model, _ctx.StreetRoot,
                        new Vector3(x + (facing < 180f ? -size.z : size.z) * 0.5f, 0f, z + size.x * 0.5f),
                        facing, ModelLibrary.Fit.Height, height);
                    FadeWithDistance(go, row);
                    _ctx.Track(go);

                    z += size.x + _ctx.Random(0.2f, 1.6f);
                }
            }

            // Block interior: paved, so it does not read as open field from above.
            var slab = MeshBuilder.CreateBox(width, 0.08f, depth,
                MaterialFactory.Get("block_interior", new Color(0.40f, 0.42f, 0.38f), 0f, 0.1f), "BlockInterior");
            _ctx.Place(slab, new Vector3(x0 + width * 0.5f, -0.05f, z0 + depth * 0.5f), collider: false);
        }

        /// <summary>
        /// A row of buildings behind and beside the yard. Without it the shop backs onto an
        /// empty field, which is obvious the moment you look at the plan view.
        /// </summary>
        public void BuildBackdrop()
        {
            float behindZ = -_ctx.ShopHalfWidth * 0.55f - 26f;

            for (float x = -_ctx.CityHalfWidth * 0.6f; x < _ctx.CityHalfWidth * 0.6f; x += _ctx.Random(2f, 8f))
            {
                string model = ModelLibrary.AnyAvailable(_ctx.Rng, StreetModels.CityFill);
                if (model == null) return;

                float height = _ctx.Random(9f, 16f);
                Vector3 size = ModelLibrary.SizeAfterFit(model, ModelLibrary.Fit.Height, height);
                if (size.x < 1f) return;

                _ctx.Track(ModelLibrary.Spawn(model, _ctx.StreetRoot,
                    new Vector3(x + size.x * 0.5f, 0f, behindZ - size.z * 0.5f),
                    StreetGenerator.BuildingFacing, ModelLibrary.Fit.Height, height));

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

            float t = Mathf.Clamp01(row / (float)Mathf.Max(1, _ctx.CityRows - 1)) * 0.72f;
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
            var asphalt = MeshBuilder.CreateBox(_ctx.CityHalfWidth * 2f + 40f, 0.08f, 11f,
                MaterialFactory.Get("asphalt", new Color(0.22f, 0.22f, 0.24f), 0f, 0.12f), "CrossStreet");
            _ctx.Place(asphalt, new Vector3(0f, -0.04f, z), collider: false);
        }

        /// <summary>A road running away from the main street, between two blocks.</summary>
        private void BuildAvenue(float x, float z0, float depth)
        {
            var asphalt = MeshBuilder.CreateBox(12f, 0.08f, depth + 12f,
                MaterialFactory.Get("asphalt", new Color(0.22f, 0.22f, 0.24f), 0f, 0.12f), "Avenue");
            _ctx.Place(asphalt, new Vector3(x + 6f, -0.04f, z0 + depth * 0.5f), collider: false);
        }
    }
}
