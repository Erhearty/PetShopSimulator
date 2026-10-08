using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// The single source of truth for colours and materials. Everything is cached by key,
    /// so the same key always yields the same material instance.
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<string, Material> _cache = new();
        private static Shader   _litShader;
        private static Material _glass;

        // ── Palette ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Named world colour roles for the warm shop interior: oak floors, cream-sage walls and
        /// sunflower accents that match the UI accent (#F5B935). World code picks a role from
        /// here rather than inventing a colour, so the shop, pens, pets and people stay coherent.
        /// </summary>
        public static class Palette
        {
            /// <summary>Warm oak floorboards.</summary>
            public static readonly Color OakFloor     = new(0.60f, 0.44f, 0.29f);
            /// <summary>Darker oak for trims, shelf backs and fences.</summary>
            public static readonly Color OakDark      = new(0.42f, 0.29f, 0.18f);
            /// <summary>Mid oak for shelving and furniture.</summary>
            public static readonly Color OakMid       = new(0.70f, 0.51f, 0.32f);
            /// <summary>Light oak for pen fencing.</summary>
            public static readonly Color OakLight     = new(0.82f, 0.64f, 0.41f);
            /// <summary>Cream wall plaster.</summary>
            public static readonly Color CreamWall    = new(0.94f, 0.91f, 0.83f);
            /// <summary>Sage green for wall trims and skirting.</summary>
            public static readonly Color Sage         = new(0.56f, 0.66f, 0.54f);
            /// <summary>Counter tops: warm cream.</summary>
            public static readonly Color CounterCream = new(0.95f, 0.92f, 0.84f);
            /// <summary>Sunflower accent, identical to the UI accent #F5B935.</summary>
            public static readonly Color Sunflower    = new(0.961f, 0.725f, 0.208f);
            /// <summary>Pen floor: sand.</summary>
            public static readonly Color PenSand      = new(0.70f, 0.60f, 0.43f);
            /// <summary>Clean straw bedding.</summary>
            public static readonly Color Straw        = new(0.89f, 0.77f, 0.45f);
            /// <summary>Feed heap.</summary>
            public static readonly Color Feed         = new(0.80f, 0.62f, 0.30f);
            /// <summary>Muck in a dirty pen.</summary>
            public static readonly Color Muck         = new(0.36f, 0.29f, 0.18f);
            /// <summary>Dark track behind the pen upkeep bars.</summary>
            public static readonly Color BarTrack     = new(0.10f, 0.11f, 0.14f, 0.9f);
            /// <summary>The shopkeeper's apron colour — sage, so they read as staff.</summary>
            public static readonly Color Staff        = new(0.36f, 0.55f, 0.45f);
            /// <summary>Generic customer clothing.</summary>
            public static readonly Color Customer     = new(0.85f, 0.55f, 0.25f);

            /// <summary>Skin tones for procedural people.</summary>
            public static readonly Color[] Skin =
            {
                new(1f, 0.87f, 0.73f), new(0.94f, 0.76f, 0.59f), new(0.80f, 0.60f, 0.40f),
                new(0.60f, 0.40f, 0.24f), new(0.40f, 0.24f, 0.13f),
            };

            /// <summary>Muted, warm-leaning shirt colours, so crowds stay varied but in key.</summary>
            public static readonly Color[] Shirts =
            {
                new(0.80f, 0.36f, 0.32f), new(0.33f, 0.50f, 0.72f), new(0.40f, 0.62f, 0.44f),
                new(0.96f, 0.73f, 0.21f), new(0.62f, 0.42f, 0.66f), new(0.32f, 0.64f, 0.64f),
                new(0.93f, 0.90f, 0.84f), new(0.90f, 0.55f, 0.30f), new(0.56f, 0.66f, 0.54f),
            };

            /// <summary>Trouser colours: denim, khaki, charcoal and stone.</summary>
            public static readonly Color[] Trousers =
            {
                new(0.24f, 0.30f, 0.42f), new(0.52f, 0.46f, 0.34f),
                new(0.20f, 0.21f, 0.24f), new(0.46f, 0.43f, 0.39f),
            };

            /// <summary>Warm key light (late-afternoon sun through the shopfront).</summary>
            public static readonly Color KeyLight      = new(1f, 0.91f, 0.78f);
            /// <summary>Cool, soft fill light that lifts the shadows.</summary>
            public static readonly Color FillLight     = new(0.72f, 0.82f, 0.95f);
            /// <summary>Ambient sky colour (trilight).</summary>
            public static readonly Color AmbientSky    = new(0.62f, 0.66f, 0.70f);
            /// <summary>Ambient horizon colour (trilight).</summary>
            public static readonly Color AmbientEquator = new(0.58f, 0.52f, 0.44f);
            /// <summary>Ambient ground bounce: warm oak.</summary>
            public static readonly Color AmbientGround = new(0.34f, 0.27f, 0.20f);

            /// <summary>Sky tint overhead (the gradient sky's zenith).</summary>
            public static readonly Color SkyZenith     = new(0.47f, 0.64f, 0.84f);
            /// <summary>Hazy sky at the horizon; also the flat clear colour when the sky shader is missing.</summary>
            public static readonly Color SkyHorizon    = new(0.80f, 0.85f, 0.89f);
            /// <summary>Below-horizon colour of the gradient sky.</summary>
            public static readonly Color SkyGround     = new(0.52f, 0.50f, 0.47f);
            /// <summary>Farthest skyline layer: pale and hazy.</summary>
            public static readonly Color SkylineFar    = new(0.68f, 0.74f, 0.82f);
            /// <summary>Middle skyline layer.</summary>
            public static readonly Color SkylineMid    = new(0.50f, 0.57f, 0.67f);
            /// <summary>Nearest skyline layer: the darkest blocks.</summary>
            public static readonly Color SkylineNear   = new(0.34f, 0.38f, 0.48f);
            /// <summary>A lit window in the skyline: the sunflower accent.</summary>
            public static readonly Color LitWindow     = Sunflower;
        }

        public static readonly Color FloorColor       = Palette.OakFloor;
        public static readonly Color WallColor        = Palette.CreamWall;
        public static readonly Color WallTrimColor    = Palette.Sage;
        public static readonly Color ShelfColor       = Palette.OakMid;
        public static readonly Color ShelfBackColor   = Palette.OakDark;
        public static readonly Color CounterColor     = Palette.CounterCream;
        public static readonly Color CounterTrimColor = Palette.Sunflower;
        public static readonly Color PenColor         = Palette.PenSand;   // sand and straw
        public static readonly Color PenFenceColor    = Palette.OakLight;
        public static readonly Color PlayerColor      = Palette.Staff;
        public static readonly Color CustomerColor    = Palette.Customer;

        public static Material Floor       => Get("floor",        FloorColor,       0f,    0.12f);
        public static Material Wall        => Get("wall",         WallColor,        0f,    0.15f);
        public static Material WallTrim    => Get("wall_trim",    WallTrimColor,    0f,    0.5f);
        public static Material Shelf       => Get("shelf",        ShelfColor,       0f,    0.2f);
        public static Material ShelfBack   => Get("shelf_back",   ShelfBackColor,   0f,    0.15f);
        public static Material Counter     => Get("counter",      CounterColor,     0.1f,  0.6f);
        public static Material CounterTrim => Get("counter_trim", CounterTrimColor, 0f,    0.4f);
        public static Material Pen         => Get("pen",          PenColor,         0f,    0.08f);
        public static Material PenFence    => Get("pen_fence",    PenFenceColor,    0f,    0.4f);
        public static Material Player      => Get("player",       PlayerColor,      0f,    0.5f);
        public static Material Customer    => Get("customer",     CustomerColor,    0f,    0.5f);

        /// <summary>Shopfront glazing — you look through it at the street.</summary>
        public static Material Glass
        {
            get
            {
                if (_glass != null) return _glass;
                _glass = CreateTransparent("glass", new Color(0.62f, 0.78f, 0.82f, 0.18f));
                if (_glass != null)
                {
                    if (_glass.HasProperty("_Metallic"))   _glass.SetFloat("_Metallic", 0.5f);
                    if (_glass.HasProperty("_Glossiness")) _glass.SetFloat("_Glossiness", 0.92f);
                    if (_glass.HasProperty("_Smoothness")) _glass.SetFloat("_Smoothness", 0.92f);
                }
                return _glass;
            }
        }

        public static Material ForProduct(Commerce.ProductCategory cat) => cat switch
        {
            Commerce.ProductCategory.Food      => Get("prod_food", Palette.Sunflower,             0f, 0.6f),
            Commerce.ProductCategory.Toy       => Get("prod_toy",  new Color(0.84f, 0.36f, 0.32f), 0f, 0.6f),
            Commerce.ProductCategory.Accessory => Get("prod_acc",  new Color(0.36f, 0.54f, 0.76f), 0f, 0.6f),
            Commerce.ProductCategory.Medicine  => Get("prod_med",  new Color(0.40f, 0.72f, 0.54f), 0f, 0.6f),
            _                                  => Get("prod_default", Color.gray, 0f, 0.6f),
        };

        public static Material ForPet(Pets.Pet.Species species) => species switch
        {
            Pets.Pet.Species.Cat     => Get("pet_cat",     new Color(0.85f, 0.65f, 0.45f), 0f,    0.3f),
            Pets.Pet.Species.Dog     => Get("pet_dog",     new Color(0.70f, 0.55f, 0.40f), 0f,    0.3f),
            Pets.Pet.Species.Rabbit  => Get("pet_rabbit",  new Color(0.92f, 0.88f, 0.85f), 0f,    0.2f),
            Pets.Pet.Species.Hamster => Get("pet_hamster", new Color(0.88f, 0.70f, 0.45f), 0f,    0.3f),
            Pets.Pet.Species.Parrot  => Get("pet_parrot",  new Color(0.30f, 0.75f, 0.35f), 0f,    0.4f),
            Pets.Pet.Species.Fish    => Get("pet_fish",    new Color(0.25f, 0.55f, 0.90f), 0.05f, 0.7f),
            Pets.Pet.Species.Chicken => Get("pet_chicken", new Color(0.95f, 0.92f, 0.86f), 0f,    0.3f),
            Pets.Pet.Species.Penguin => Get("pet_penguin", new Color(0.18f, 0.20f, 0.24f), 0f,    0.3f),
            Pets.Pet.Species.Deer    => Get("pet_deer",    new Color(0.72f, 0.55f, 0.36f), 0f,    0.3f),
            Pets.Pet.Species.Horse   => Get("pet_horse",   new Color(0.50f, 0.36f, 0.24f), 0f,    0.3f),
            Pets.Pet.Species.Tiger   => Get("pet_tiger",   new Color(0.92f, 0.58f, 0.18f), 0f,    0.3f),
            _                        => Get("pet_default", Color.gray, 0f, 0.3f),
        };

        // ── Shader resolution ───────────────────────────────────────────────────

        /// <summary>
        /// The opaque lit shader to build materials from.
        /// Shader.Find only sees shaders that survived build-time stripping, so if the
        /// lookups miss we borrow the shader off a throwaway primitive's default material,
        /// which is always shipped.
        /// </summary>
        public static Shader LitShader
        {
            get
            {
                if (_litShader != null) return _litShader;

                _litShader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard")
                          ?? Shader.Find("Legacy Shaders/Diffuse");

                if (_litShader == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var rend  = probe.GetComponent<Renderer>();
                    if (rend != null && rend.sharedMaterial != null)
                        _litShader = rend.sharedMaterial.shader;
                    Object.Destroy(probe);
                }

                if (_litShader == null)
                    Debug.LogError("[MaterialFactory] No usable shader found — everything will render pink.");

                return _litShader;
            }
        }

        // ── Cache ───────────────────────────────────────────────────────────────

        public static Material Get(string key, Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var shader = LitShader;
            if (shader == null) return null;

            var mat = new Material(shader) { name = "mat_" + key };
            SetColor(mat, color);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            _cache[key] = mat;
            return mat;
        }

        /// <summary>An alpha-blended material — used for the build-mode ghost.</summary>
        public static Material CreateTransparent(string name, Color color)
        {
            var shader = LitShader;
            if (shader == null) return null;

            var mat = new Material(shader) { name = "mat_" + name };
            mat.SetFloat("_Mode", 3f);
            mat.SetFloat("_Surface", 1f);   // URP: transparent
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetColor(mat, color);
            return mat;
        }

        public static void SetColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
        }

        /// <summary>The procedural sky. Falls back to a flat colour when the shader is stripped.</summary>
        public static Material CreateSkybox(Color sky, Color ground, float exposure = 1.25f)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[MaterialFactory] Skybox/Procedural was stripped from the build.");
                return null;
            }

            var mat = new Material(shader) { name = "mat_sky" };
            mat.SetColor("_SkyTint",             sky);
            mat.SetColor("_GroundColor",         ground);
            mat.SetFloat("_AtmosphereThickness", 0.85f);
            mat.SetFloat("_Exposure",            exposure);
            mat.SetFloat("_SunSize",             0.045f);
            mat.SetFloat("_SunSizeConvergence",  4f);
            return mat;
        }

        public static void ClearCache()
        {
            foreach (var m in _cache.Values)
                if (m != null) Object.Destroy(m);
            _cache.Clear();
            if (_glass != null) { Object.Destroy(_glass); _glass = null; }
        }
    }
}
