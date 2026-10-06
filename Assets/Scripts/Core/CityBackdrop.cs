using UnityEngine;
using UnityEngine.Rendering;

namespace PetShop.Core
{
    /// <summary>
    /// A static low-poly city skyline around the world, plus a gradient sky in place of the old
    /// near-black clear colour. Everything is built once, in code: an open cylinder band well
    /// outside every street and scenery bound, wrapped in a generated texture of two-to-three
    /// layered skyline silhouettes (flat blocks, stepped roofs, a few lit windows) in
    /// <see cref="MaterialFactory.Palette"/> colours. The band is unlit, casts and receives no
    /// shadows, and has no collider, so the collider-based NavMesh bake never sees it.
    /// No per-frame work.
    /// </summary>
    public static class CityBackdrop
    {
        /// <summary>Name of the backdrop object, so a second <see cref="Apply"/> reuses it.</summary>
        public const string ObjectName = "CityBackdrop";

        // ── Band geometry ───────────────────────────────────────────────────────
        /// <summary>Facets around the band; few enough to read as low-poly.</summary>
        private const int   Segments          = 48;
        /// <summary>The band never sits closer than this to the world centre (m).</summary>
        private const float MinRadius         = 220f;
        /// <summary>Kept well inside the cameras' 900 m far clip plane (m).</summary>
        private const float MaxRadius         = 600f;
        /// <summary>Gap between the furthest scene geometry and the band (m).</summary>
        private const float BoundsMargin      = 80f;
        /// <summary>Band height as a fraction of its radius, so it subtends the same angle at any size.</summary>
        private const float HeightToRadius    = 0.24f;
        /// <summary>How far the band's lower edge sinks below the ground, hiding the seam (m).</summary>
        private const float SinkBelowGround   = 4f;
        /// <summary>The texture repeats this many times around the band.</summary>
        private const int   TextureRepeats    = 3;

        // ── Texture ─────────────────────────────────────────────────────────────
        private const int   TexWidth          = 2048;
        private const int   TexHeight         = 512;
        /// <summary>Fixed seed: System.Random, so the game's seeded UnityEngine.Random is untouched.</summary>
        private const int   SkylineSeed       = 7319;
        /// <summary>Alpha below which the cutout shader discards a texel (the sky gaps).</summary>
        private const float AlphaCutoff       = 0.5f;

        /// <summary>One silhouette layer, drawn far to near.</summary>
        private readonly struct Layer
        {
            public readonly Color Colour;
            public readonly float Haze;        // 0..1 blend towards the horizon sky
            public readonly float MinHeight;   // fraction of texture height
            public readonly float MaxHeight;
            public readonly int   MinWidth;    // pixels
            public readonly int   MaxWidth;
            public readonly float LitChance;   // fraction of windows lit

            public Layer(Color colour, float haze, float minH, float maxH, int minW, int maxW, float lit)
            {
                Colour = colour; Haze = haze; MinHeight = minH; MaxHeight = maxH;
                MinWidth = minW; MaxWidth = maxW; LitChance = lit;
            }
        }

        private static readonly Layer[] Layers =
        {
            new(MaterialFactory.Palette.SkylineFar,  0.45f, 0.45f, 0.85f, 60, 140, 0f),
            new(MaterialFactory.Palette.SkylineMid,  0.20f, 0.30f, 0.65f, 50, 120, 0.04f),
            new(MaterialFactory.Palette.SkylineNear, 0f,    0.15f, 0.45f, 40, 100, 0.08f),
        };

        /// <summary>Most roof steps on one building.</summary>
        private const int   MaxRoofSteps      = 2;
        /// <summary>Each roof step is this much narrower on each side (fraction of width).</summary>
        private const float RoofStepInset     = 0.18f;
        /// <summary>Each roof step adds this fraction of the building's height.</summary>
        private const float RoofStepRise      = 0.12f;
        /// <summary>Largest gap between neighbouring buildings (pixels).</summary>
        private const int   MaxStreetGap      = 14;
        private const int   WindowSize        = 6;
        private const int   WindowPitch       = 14;
        /// <summary>Building pixels kept clear of windows at the edges and top.</summary>
        private const int   WindowMargin      = 8;

        // ── Sky ─────────────────────────────────────────────────────────────────
        /// <summary>Exposure of the procedural gradient sky.</summary>
        private const float SkyExposure       = 1.1f;

        /// <summary>
        /// Builds the backdrop (once) and gives the main camera the gradient sky. Safe to call
        /// more than once. CameraTour's camera clears to the skybox too, so it sees the same view.
        /// </summary>
        public static GameObject Apply()
        {
            ApplySky();

            var existing = GameObject.Find(ObjectName);
            if (existing != null) return existing;

            Bounds world = SceneBounds();
            float radius = Mathf.Clamp(HorizontalExtent(world) + BoundsMargin, MinRadius, MaxRadius);
            float height = radius * HeightToRadius;

            var go = new GameObject(ObjectName);
            go.transform.position = new Vector3(world.center.x, world.min.y - SinkBelowGround, world.center.z);
            go.layer = GameLayers.Scenery;
            go.isStatic = false;   // never baked into the NavMesh or lightmaps

            go.AddComponent<MeshFilter>().sharedMesh = BuildBand(radius, height);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial       = BuildMaterial();
            renderer.shadowCastingMode    = ShadowCastingMode.Off;
            renderer.receiveShadows       = false;
            renderer.lightProbeUsage      = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>The main camera clears to a gradient sky; a flat hazy colour if the sky shader was stripped.</summary>
        private static void ApplySky()
        {
            var sky = MaterialFactory.CreateSkybox(MaterialFactory.Palette.SkyZenith,
                                                   MaterialFactory.Palette.SkyGround, SkyExposure);
            if (sky != null) RenderSettings.skybox = sky;

            var cam = Camera.main;
            if (cam == null) return;
            cam.backgroundColor = MaterialFactory.Palette.SkyHorizon;
            cam.clearFlags      = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        }

        /// <summary>Union of every renderer already in the scene: shop, street, parking, scenery.</summary>
        private static Bounds SceneBounds()
        {
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>Distance from the bounds centre to its furthest horizontal corner.</summary>
        private static float HorizontalExtent(Bounds b) => new Vector2(b.extents.x, b.extents.z).magnitude;

        /// <summary>An open, inward-facing cylinder band.</summary>
        private static Mesh BuildBand(float radius, float height)
        {
            int columns = Segments + 1;   // the seam repeats a column so UVs wrap cleanly
            var vertices = new Vector3[columns * 2];
            var uvs      = new Vector2[columns * 2];
            var colours  = new Color[columns * 2];
            var tris     = new int[Segments * 6];

            for (int i = 0; i < columns; i++)
            {
                float t     = (float)i / Segments;
                float angle = t * Mathf.PI * 2f;
                var   dir   = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                vertices[i * 2]     = dir;
                vertices[i * 2 + 1] = dir + Vector3.up * height;
                uvs[i * 2]          = new Vector2(t * TextureRepeats, 0f);
                uvs[i * 2 + 1]      = new Vector2(t * TextureRepeats, 1f);
                colours[i * 2]      = Color.white;
                colours[i * 2 + 1]  = Color.white;
            }

            for (int s = 0; s < Segments; s++)
            {
                int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
                int k = s * 6;
                // Wound to face the centre.
                tris[k]     = a; tris[k + 1] = c; tris[k + 2] = b;
                tris[k + 3] = c; tris[k + 4] = d; tris[k + 5] = b;
            }

            var mesh = new Mesh { name = "mesh_city_backdrop" };
            mesh.vertices  = vertices;
            mesh.uv        = uvs;
            mesh.colors    = colours;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unlit, alpha-cut so the sky shows between towers. Sprites/Default is always included.</summary>
        private static Material BuildMaterial()
        {
            var shader = Shader.Find("Unlit/Transparent Cutout") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[CityBackdrop] No unlit shader available; the skyline is skipped.");
                return null;
            }
            var mat = new Material(shader) { name = "mat_city_backdrop", mainTexture = BuildTexture() };
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", AlphaCutoff);
            return mat;
        }

        /// <summary>Two-to-three layered skyline silhouettes, far layers lighter and hazier.</summary>
        private static Texture2D BuildTexture()
        {
            var pixels = new Color32[TexWidth * TexHeight];   // starts fully transparent
            var rng    = new System.Random(SkylineSeed);
            Color haze = MaterialFactory.Palette.SkyHorizon;
            Color32 lit = MaterialFactory.Palette.LitWindow;

            foreach (var layer in Layers)
            {
                Color32 wall = Color.Lerp(layer.Colour, haze, layer.Haze);
                int x = rng.Next(0, TexWidth);
                int start = x;
                while (x < start + TexWidth)
                {
                    int width  = rng.Next(layer.MinWidth, layer.MaxWidth + 1);
                    int height = Mathf.RoundToInt(Lerp(rng, layer.MinHeight, layer.MaxHeight) * TexHeight);
                    DrawBuilding(pixels, x, width, height, rng.Next(0, MaxRoofSteps + 1), wall);
                    if (layer.LitChance > 0f) DrawWindows(pixels, x, width, height, layer.LitChance, lit, rng);
                    x += width + rng.Next(0, MaxStreetGap + 1);
                }
            }

            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
            {
                name       = "tex_city_skyline",
                wrapModeU  = TextureWrapMode.Repeat,
                wrapModeV  = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static float Lerp(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>A flat block with up to <paramref name="steps"/> narrower stepped tiers on top.</summary>
        private static void DrawBuilding(Color32[] px, int x, int width, int height, int steps, Color32 colour)
        {
            FillRect(px, x, 0, width, height, colour);
            int tierX = x, tierW = width, top = height;
            int rise = Mathf.RoundToInt(height * RoofStepRise);
            for (int s = 0; s < steps; s++)
            {
                int inset = Mathf.RoundToInt(tierW * RoofStepInset);
                tierX += inset;
                tierW -= inset * 2;
                if (tierW <= 0 || rise <= 0) break;
                FillRect(px, tierX, top, tierW, rise, colour);
                top += rise;
            }
        }

        /// <summary>A sparse grid of lit windows on the main block.</summary>
        private static void DrawWindows(Color32[] px, int x, int width, int height, float chance, Color32 lit, System.Random rng)
        {
            for (int wy = WindowMargin; wy + WindowSize < height - WindowMargin; wy += WindowPitch)
            for (int wx = WindowMargin; wx + WindowSize < width - WindowMargin; wx += WindowPitch)
                if (rng.NextDouble() < chance) FillRect(px, x + wx, wy, WindowSize, WindowSize, lit);
        }

        /// <summary>Fills a rectangle, wrapping horizontally so the band has no seam.</summary>
        private static void FillRect(Color32[] px, int x, int y, int w, int h, Color32 colour)
        {
            int yEnd = Mathf.Min(y + h, TexHeight);
            for (int row = Mathf.Max(y, 0); row < yEnd; row++)
            for (int col = x; col < x + w; col++)
            {
                int wrapped = ((col % TexWidth) + TexWidth) % TexWidth;
                px[row * TexWidth + wrapped] = colour;
            }
        }
    }
}
