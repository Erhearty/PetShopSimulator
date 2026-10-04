using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Player
{
    /// <summary>
    /// First-person "in hand" viewmodel for the furniture item being placed: a shrunken,
    /// behaviour-free copy of its prefab held front-right-below the main camera. It lives on the
    /// HeldItem layer, which the main camera stops drawing; a child overlay camera draws only
    /// that layer on top, so the item never clips into walls. Owned and driven by BuildMode.
    /// </summary>
    public class HeldItemView : MonoBehaviour
    {
        /// <summary>Largest bounds dimension of the viewmodel, in metres.</summary>
        public const float MaxItemSize = 0.45f;
        /// <summary>Hand position relative to the main camera (right, down, forward), in metres.</summary>
        public static readonly Vector3 HandOffset = new(0.35f, -0.3f, 0.6f);
        /// <summary>Tilt of the held item so it reads as carried rather than floating.</summary>
        public static readonly Vector3 HandEuler = new(10f, -30f, 0f);
        private const float OverlayNearClip    = 0.01f;
        private const float OverlayDepthOffset = 1f;

        private Camera     _main;
        private Camera     _overlay;
        private Transform  _anchor;
        private GameObject _model;

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
            FitToHand(_model);
            UpdateVisibility();
        }

        /// <summary>Removes the viewmodel.</summary>
        public void Hide()
        {
            PrefabPreview.DestroySafe(_model);
            _model = null;
            if (_overlay != null) _overlay.enabled = false;
        }

        private void LateUpdate()
        {
            if (_model == null) return;
            if (_main != Camera.main || _anchor == null) EnsureRig();
            if (_overlay != null && _main != null) _overlay.fieldOfView = _main.fieldOfView;
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
            if (_overlay != null) _overlay.enabled = visible;
        }

        /// <summary>Builds the hand anchor and overlay camera under the current main camera.</summary>
        private void EnsureRig()
        {
            if (_main == Camera.main && _anchor != null) return;
            // Keep the model alive while the old anchor is torn down; it is re-parented below.
            if (_model != null) _model.transform.SetParent(transform, false);
            TearDownRig();
            _main = Camera.main;
            if (_main == null) return;

            int layer = GameLayers.HeldItem;
            _main.cullingMask &= ~(1 << layer);

            _anchor = new GameObject("HeldItemAnchor").transform;
            _anchor.SetParent(_main.transform, false);
            _anchor.localPosition = HandOffset;
            _anchor.localEulerAngles = HandEuler;
            if (_model != null) _model.transform.SetParent(_anchor, false);
            _overlay = CreateOverlay(_main, layer);
        }

        private static Camera CreateOverlay(Camera main, int layer)
        {
            var go = new GameObject("HeldItemCamera");
            go.transform.SetParent(main.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags    = CameraClearFlags.Depth;
            cam.cullingMask   = 1 << layer;
            cam.depth         = main.depth + OverlayDepthOffset;
            cam.nearClipPlane = OverlayNearClip;
            cam.fieldOfView   = main.fieldOfView;
            return cam;
        }

        private void TearDownRig()
        {
            if (_anchor  != null) PrefabPreview.DestroySafe(_anchor.gameObject);
            if (_overlay != null) PrefabPreview.DestroySafe(_overlay.gameObject);
            // Give the main camera its HeldItem layer back once it no longer hosts the rig.
            if (_main != null && GameLayers.HeldItem >= 0) _main.cullingMask |= 1 << GameLayers.HeldItem;
            _anchor = null; _overlay = null; _main = null;
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
