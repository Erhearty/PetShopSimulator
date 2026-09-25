using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Core
{
    /// <summary>
    /// Runtime loader for every art pack under Resources — the CC0 Kenney kits and the
    /// Asset Store packs alike.
    ///
    /// The packs are authored at wildly different scales: a Kenney furniture bookcase
    /// measures ~8.8 units tall while a whole city building measures ~1.3. Nothing here
    /// uses per-pack magic numbers. Callers say how big a thing should be in metres and
    /// the library measures the model and scales it to fit.
    ///
    /// Every lookup is allowed to miss. Callers must cope with a null so the game still
    /// runs with any (or all) of the art folders deleted.
    /// </summary>
    public static class ModelLibrary
    {
        // CC0 Kenney kits
        public const string Roads      = "Kenney/Roads/";
        public const string Commercial = "Kenney/Commercial/";
        public const string Suburban   = "Kenney/Suburban/";
        public const string Cars       = "Kenney/Cars/";
        public const string Nature     = "Kenney/Nature/";
        public const string Furniture  = "Kenney/Furniture/";
        public const string Food       = "Kenney/Food/";

        // Asset Store packs (Extension Asset licence — see THIRD-PARTY.md)
        public const string City       = "Packs/City/";        // SimplePoly City
        public const string CityBuild  = "Packs/City/Buildings/";
        public const string CityProps  = "Packs/City/Props/";
        public const string CityCars   = "Packs/City/Vehicles/";
        public const string CityNature = "Packs/City/Natures/";
        public const string CityRoads  = "Packs/City/Roads/";
        public const string Street     = "Packs/Street/";       // Lowpoly Street Pack
        public const string StreetRoad = "Packs/Street/Roads/";
        public const string StreetProp = "Packs/Street/StreetProps/";
        public const string StreetTree = "Packs/Street/Foliage/";
        public const string Interior   = "Packs/Furniture/";    // Stylized Wooden Set
        public const string People     = "Packs/People/";       // 100 animated characters
        public const string Plants     = "Packs/Nature/";       // Simple Nature Pack
        public const string Animals    = "Packs/Animals/";      // Cute Magic cube animals

        private static readonly Dictionary<string, GameObject> _prefabs = new();
        private static readonly Dictionary<string, Bounds>     _bounds  = new();
        private static readonly HashSet<string>                _missing = new();

        /// <summary>Prefix shared by every Asset Store pack path.</summary>
        private const string PacksPrefix = "Packs/";

        /// <summary>
        /// When set (by <c>-nopacks</c>), every model under Resources/Packs/ is treated as absent
        /// — silently — so the procedural and CC0 Kenney fallbacks are exercised.
        /// </summary>
        public static bool ForceProcedural;

        /// <summary>Every resource path that was looked up and found missing since the last cache reset.</summary>
        public static IReadOnlyCollection<string> MissingPaths => _missing;

        /// <summary>How a model should be sized when it is spawned.</summary>
        public enum Fit { None, Width, Height, Depth, Largest }

        // ── Loading ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The prefab at Resources/<paramref name="resourcePath"/>, cached; null when it is not
        /// installed or when <see cref="ForceProcedural"/> hides the Asset Store packs.
        /// </summary>
        public static GameObject Prefab(string resourcePath)
        {
            if (ForceProcedural && resourcePath != null && resourcePath.StartsWith(PacksPrefix, System.StringComparison.Ordinal)) return null;
            if (_prefabs.TryGetValue(resourcePath, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null && _missing.Add(resourcePath))
                Debug.LogWarning($"[Kenney] model not found: Resources/{resourcePath}");

            _prefabs[resourcePath] = prefab;
            return prefab;
        }

        public static bool Has(string resourcePath) => Prefab(resourcePath) != null;

        /// <summary>
        /// The first of these models that is actually installed, or null.
        /// Lets callers prefer a richer pack and fall back to the CC0 kit — or to nothing —
        /// without the art folders becoming a hard dependency.
        /// </summary>
        public static string FirstAvailable(params string[] resourcePaths)
        {
            foreach (string path in resourcePaths)
                if (!string.IsNullOrEmpty(path) && Has(path)) return path;
            return null;
        }

        /// <summary>Picks one at random from those that are installed, or null.</summary>
        public static string AnyAvailable(System.Random rng, params string[] resourcePaths)
        {
            var found = new List<string>();
            foreach (string path in resourcePaths)
                if (!string.IsNullOrEmpty(path) && Has(path)) found.Add(path);
            return found.Count == 0 ? null : found[rng.Next(found.Count)];
        }

        /// <summary>
        /// Instantiate a model, sized so the chosen axis measures <paramref name="targetSize"/>
        /// metres, standing on Y = 0 and centred on X/Z.
        ///
        /// The instance is parented under a holder that carries the requested position and
        /// yaw, and the model keeps its own baked root transform underneath. That matters:
        /// several packs are authored Z-up with a -90° X rotation baked into the prefab root,
        /// and writing eulerAngles straight onto the instance would lay them on their side.
        ///
        /// Returns null when the model is missing — every caller must cope with that so the
        /// game still runs with the art folders absent.
        /// </summary>
        public static GameObject Spawn(string resourcePath, Transform parent, Vector3 position,
                                       float yRotation = 0f, Fit fit = Fit.None, float targetSize = 1f,
                                       bool centreHorizontally = true)
        {
            var prefab = Prefab(resourcePath);
            if (prefab == null) return null;

            var holder = new GameObject(prefab.name);
            if (parent != null) holder.transform.SetParent(parent, false);

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;

            // Measure at the origin with the prefab's own rotation/scale intact.
            Vector3 bakedScale = instance.transform.localScale;
            instance.transform.position = Vector3.zero;

            Bounds bounds = WorldBounds(instance);
            float  scale  = 1f;

            if (fit != Fit.None)
            {
                float source = fit switch
                {
                    Fit.Width   => bounds.size.x,
                    Fit.Height  => bounds.size.y,
                    Fit.Depth   => bounds.size.z,
                    Fit.Largest => Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)),
                    _           => 1f,
                };
                scale = source > 0.0001f ? targetSize / source : 1f;
                instance.transform.localScale = bakedScale * scale;
                bounds = WorldBounds(instance);      // exact, rather than assuming it scaled cleanly
            }
            else if (!Mathf.Approximately(targetSize, 1f))
            {
                instance.transform.localScale = bakedScale * targetSize;
                bounds = WorldBounds(instance);
            }

            // Keep the world transform (so the baked rotation survives), then sit it on the
            // ground inside the holder.
            instance.transform.SetParent(holder.transform, worldPositionStays: true);
            instance.transform.localPosition = new Vector3(
                centreHorizontally ? -bounds.center.x : 0f,
                -bounds.min.y,
                centreHorizontally ? -bounds.center.z : 0f);

            holder.transform.position    = position;
            holder.transform.eulerAngles = new Vector3(0f, yRotation, 0f);
            return holder;
        }

        /// <summary>Combined world-space renderer bounds of a live instance.</summary>
        public static Bounds WorldBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Spawn without re-centring — for models whose pivot is already meaningful.</summary>
        public static GameObject SpawnRaw(string resourcePath, Transform parent, Vector3 position,
                                          float yRotation = 0f, float scale = 1f)
        {
            var prefab = Prefab(resourcePath);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale  = Vector3.one * scale;
            go.transform.position    = position;
            go.transform.eulerAngles = new Vector3(0f, yRotation, 0f);
            return go;
        }

        /// <summary>The model's size in metres once sized by <paramref name="fit"/>.</summary>
        public static Vector3 SizeAfterFit(string resourcePath, Fit fit, float targetSize)
            => LocalBounds(resourcePath).size * ScaleFor(resourcePath, fit, targetSize);

        public static float ScaleFor(string resourcePath, Fit fit, float targetSize)
        {
            if (fit == Fit.None) return targetSize;

            Vector3 size = LocalBounds(resourcePath).size;
            float source = fit switch
            {
                Fit.Width   => size.x,
                Fit.Height  => size.y,
                Fit.Depth   => size.z,
                Fit.Largest => Mathf.Max(size.x, Mathf.Max(size.y, size.z)),
                _           => 1f,
            };
            return source > 0.0001f ? targetSize / source : 1f;
        }

        // ── Measurement ─────────────────────────────────────────────────────────

        /// <summary>
        /// Combined mesh bounds in the prefab's own local space. Computed straight off the
        /// asset — no instantiation — by pushing each mesh's eight corners through the
        /// child's transform chain.
        /// </summary>
        public static Bounds LocalBounds(string resourcePath)
        {
            if (_bounds.TryGetValue(resourcePath, out var cached)) return cached;

            var prefab = Prefab(resourcePath);
            var result = new Bounds(Vector3.zero, Vector3.one);

            if (prefab != null)
            {
                bool started = false;
                // Include the prefab root's own TRS: packs authored Z-up bake a -90° X
                // rotation there, and ignoring it reports a road tile as 30 m tall.
                Matrix4x4 rootTrs = Matrix4x4.TRS(Vector3.zero,
                                                  prefab.transform.localRotation,
                                                  prefab.transform.localScale);

                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;

                    Matrix4x4 toRoot = rootTrs * RelativeMatrix(prefab.transform, filter.transform);
                    Bounds mb = filter.sharedMesh.bounds;

                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 p = new(
                            (corner & 1) == 0 ? mb.min.x : mb.max.x,
                            (corner & 2) == 0 ? mb.min.y : mb.max.y,
                            (corner & 4) == 0 ? mb.min.z : mb.max.z);
                        p = toRoot.MultiplyPoint3x4(p);

                        if (!started) { result = new Bounds(p, Vector3.zero); started = true; }
                        else          { result.Encapsulate(p); }
                    }
                }

                // Rigged characters render through SkinnedMeshRenderer and have no MeshFilter.
                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;

                    Matrix4x4 toRoot = rootTrs * RelativeMatrix(prefab.transform, skin.transform);
                    Bounds mb = skin.sharedMesh.bounds;

                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 p = new(
                            (corner & 1) == 0 ? mb.min.x : mb.max.x,
                            (corner & 2) == 0 ? mb.min.y : mb.max.y,
                            (corner & 4) == 0 ? mb.min.z : mb.max.z);
                        p = toRoot.MultiplyPoint3x4(p);

                        if (!started) { result = new Bounds(p, Vector3.zero); started = true; }
                        else          { result.Encapsulate(p); }
                    }
                }
            }

            _bounds[resourcePath] = result;
            return result;
        }

        private static Matrix4x4 RelativeMatrix(Transform root, Transform child)
        {
            Matrix4x4 m = Matrix4x4.identity;
            for (Transform t = child; t != null && t != root; t = t.parent)
                m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
            return m;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Tint every renderer on an instance — the kits share one atlas per kit.</summary>
        public static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        public static void SetLayer(GameObject go, int layer)
        {
            if (go != null) MeshBuilder.SetLayerRecursive(go, layer);
        }

        public static void SetShadows(GameObject go, bool cast, bool receive)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = cast
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = receive;
            }
        }

        public static void ClearCache()
        {
            _prefabs.Clear();
            _bounds.Clear();
            _missing.Clear();
        }

        /// <summary>Forgets every cached prefab, measured bounds and missing path (same as <see cref="ClearCache"/>).</summary>
        public static void ResetCache() => ClearCache();
    }
}
