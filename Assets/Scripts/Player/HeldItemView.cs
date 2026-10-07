using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Player
{
    /// <summary>
    /// First-person "in hand" viewmodel for the furniture item being placed: a shrunken,
    /// behaviour-free copy of its prefab held front-right-below the main camera. It lives on the
    /// HeldItem layer and is drawn by the main camera itself, so it goes through the same post
    /// stack (FXAA, bloom, ACES, vignette) as the world. It never clips into walls: its materials
    /// are instances moved to a late render queue, and just before them an invisible depth-clear
    /// quad (ZTest Always, ZWrite on, colour blended away) resets the depth buffer in front of the
    /// camera, so the item depth-tests only against itself and still sorts its own faces correctly.
    /// Owned and driven by BuildMode.
    /// </summary>
    public class HeldItemView : MonoBehaviour
    {
        /// <summary>Largest bounds dimension of the viewmodel, in metres.</summary>
        public const float MaxItemSize = 0.45f;
        /// <summary>Hand position relative to the main camera (right, down, forward), in metres.</summary>
        public static readonly Vector3 HandOffset = new(0.35f, -0.3f, 0.6f);
        /// <summary>Tilt of the held item so it reads as carried rather than floating.</summary>
        public static readonly Vector3 HandEuler = new(10f, -30f, 0f);

        /// <summary>Render queue of the held item's opaque parts: after the whole world, before post.</summary>
        public const int HeldQueue = (int)RenderQueue.Overlay;
        /// <summary>Render queue of the depth-clear quad: after the world, just before the item.</summary>
        public const int DepthClearQueue = HeldQueue - 1;
        /// <summary>
        /// Distance of the depth-clear quad in front of the camera, in metres: beyond the whole item
        /// (hand offset plus its size), so the item passes the depth test against it.
        /// </summary>
        private const float DepthClearDistance = 2f;
        /// <summary>Overscan of the depth-clear quad past the camera frustum.</summary>
        private const float DepthClearOverscan = 1.2f;
        private const string DepthClearShader = "Hidden/Internal-Colored";

        private Camera     _main;
        private bool       _mainHadLayer;
        private Transform  _anchor;
        private GameObject _depthClear;
        private Material   _depthClearMaterial;
        private GameObject _model;
        private readonly List<Material> _instances = new();
        private static Mesh _quadMesh;

        /// <summary>The viewmodel copy currently shown, or null.</summary>
        public GameObject Model => _model;

        /// <summary>Replaces the viewmodel with a copy of <paramref name="prefab"/>; null just hides.</summary>
        public void Show(GameObject prefab)
        {
            Hide();
            if (prefab == null || GameLayers.HeldItem < 0) return;
            EnsureRig();
            if (_anchor == null) return;

            _model = PrefabPreview.CreateStripped(prefab, _anchor);
            _model.name = "HeldItem";
            PrefabPreview.DisableShadows(_model);
            MeshBuilder.SetLayerRecursive(_model, GameLayers.HeldItem);
            MoveToHeldQueue(_model);
            FitToHand(_model);
            UpdateVisibility();
        }

        /// <summary>Removes the viewmodel.</summary>
        public void Hide()
        {
            PrefabPreview.DestroySafe(_model);
            _model = null;
            foreach (var mat in _instances) PrefabPreview.DestroySafe(mat);
            _instances.Clear();
            if (_depthClear != null) _depthClear.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_model == null) return;
            if (_main != Camera.main || _anchor == null) EnsureRig();
            FitDepthClear();
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            Hide();
            TearDownRig();
        }

        /// <summary>Hidden while a modal panel or the pause menu is open, or the build view is showing.</summary>
        private void UpdateVisibility()
        {
            var  game    = GameManager.Instance;
            bool modal   = game != null && (game.IsModalOpen || game.IsBuildViewActive);
            bool visible = _model != null && !modal;
            if (_model != null && _model.activeSelf != visible) _model.SetActive(visible);
            if (_depthClear != null && _depthClear.activeSelf != visible) _depthClear.SetActive(visible);
        }

        /// <summary>Builds the hand anchor and depth-clear quad under the current main camera.</summary>
        private void EnsureRig()
        {
            if (_main == Camera.main && _anchor != null) return;
            // Keep the model alive while the old anchor is torn down; it is re-parented below.
            if (_model != null) _model.transform.SetParent(transform, false);
            TearDownRig();
            _main = Camera.main;
            if (_main == null) return;

            // The main camera draws the item itself, so it goes through the camera's post stack.
            int bit = 1 << GameLayers.HeldItem;
            _mainHadLayer = (_main.cullingMask & bit) != 0;
            _main.cullingMask |= bit;

            _anchor = new GameObject("HeldItemAnchor").transform;
            _anchor.SetParent(_main.transform, false);
            _anchor.localPosition = HandOffset;
            _anchor.localEulerAngles = HandEuler;
            if (_model != null) _model.transform.SetParent(_anchor, false);
            _depthClear = CreateDepthClear(_main.transform, out _depthClearMaterial);
            FitDepthClear();
        }

        /// <summary>
        /// The invisible quad that resets depth in front of the camera just before the item draws:
        /// ZTest Always and ZWrite write its depth everywhere it covers, Blend Zero One keeps the colour
        /// already there. Null (with a warning) when the shader is missing; the item then depth-tests
        /// against the world as an ordinary object would.
        /// </summary>
        private static GameObject CreateDepthClear(Transform parent, out Material material)
        {
            material = null;
            var shader = Shader.Find(DepthClearShader);
            if (shader == null)
            {
                Debug.LogWarning($"[HeldItem] action=depth-clear-skip reason=missing-shader shader='{DepthClearShader}'");
                return null;
            }
            material = new Material(shader) { name = "HeldItemDepthClear", renderQueue = DepthClearQueue };
            material.SetInt("_SrcBlend", (int)BlendMode.Zero);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 1);
            material.SetInt("_ZTest", (int)CompareFunction.Always);

            var go = new GameObject("HeldItemDepthClear") { layer = GameLayers.HeldItem };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.forward * DepthClearDistance;
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial       = material;
            renderer.shadowCastingMode    = ShadowCastingMode.Off;
            renderer.receiveShadows       = false;
            renderer.lightProbeUsage      = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return go;
        }

        /// <summary>Sizes the depth-clear quad to cover the main camera's view at its distance.</summary>
        private void FitDepthClear()
        {
            if (_depthClear == null || _main == null) return;
            float height = _main.orthographic
                ? 2f * _main.orthographicSize
                : 2f * DepthClearDistance * Mathf.Tan(0.5f * _main.fieldOfView * Mathf.Deg2Rad);
            height *= DepthClearOverscan;
            _depthClear.transform.localScale = new Vector3(height * Mathf.Max(1f, _main.aspect), height, 1f);
        }

        /// <summary>A unit quad in the XY plane, shared by every depth-clear quad.</summary>
        private static Mesh QuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;
            _quadMesh = new Mesh { name = "HeldItemDepthClearQuad" };
            _quadMesh.vertices  = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
            };
            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateBounds();
            return _quadMesh;
        }

        /// <summary>
        /// Gives every renderer of <paramref name="model"/> its own material instances in the late
        /// held queue, opaque parts first and transparent ones one after, so the item draws after
        /// the world and the depth-clear quad. Instances are destroyed with the model.
        /// </summary>
        private void MoveToHeldQueue(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                var mats = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    var mat = new Material(shared[i]) { name = shared[i].name + " (Held)" };
                    bool transparent = shared[i].renderQueue > (int)RenderQueue.GeometryLast;
                    mat.renderQueue = HeldQueue + (transparent ? 1 : 0);
                    mats[i] = mat;
                    _instances.Add(mat);
                }
                r.sharedMaterials = mats;
            }
        }

        private void TearDownRig()
        {
            if (_anchor     != null) PrefabPreview.DestroySafe(_anchor.gameObject);
            if (_depthClear != null) PrefabPreview.DestroySafe(_depthClear);
            if (_depthClearMaterial != null) PrefabPreview.DestroySafe(_depthClearMaterial);
            // Put the main camera's HeldItem layer back as it was before it hosted the rig.
            if (_main != null && GameLayers.HeldItem >= 0 && !_mainHadLayer)
                _main.cullingMask &= ~(1 << GameLayers.HeldItem);
            _anchor = null; _depthClear = null; _depthClearMaterial = null; _main = null; _mainHadLayer = false;
        }

        /// <summary>Scales the copy so its largest dimension is <see cref="MaxItemSize"/>, centred on the hand.</summary>
        private void FitToHand(GameObject model)
        {
            var t = model.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;
            if (!TryGetBounds(model, out var bounds)) return;

            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest <= 0f) return;
            float scale = MaxItemSize / largest;
            Vector3 centre = _anchor.InverseTransformPoint(bounds.center);
            t.localScale    = Vector3.one * scale;
            t.localPosition = -centre * scale;
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
