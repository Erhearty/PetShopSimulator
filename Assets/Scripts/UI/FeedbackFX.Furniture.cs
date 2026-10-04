using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// Furniture placement pop and removal shrink. Both animate a visual-only clone — mesh
    /// filters and mesh renderers only, no colliders, NavMeshObstacles or scripts — parented under
    /// the effect host, never the furniture itself. A placed object keeps its real scale (its
    /// renderers are hidden while the clone pops), so a NavMesh bake in the same frame sees the
    /// final collider geometry; a removed object's clone lives on after the original is destroyed.
    /// Clones are built on the place/remove event only, never per frame.
    /// </summary>
    public partial class FeedbackFX
    {
        /// <summary>Seconds a removal shrink takes (1 → 0, ease-in).</summary>
        public const float ShrinkDuration = 0.2f;
        /// <summary>Name of a clone's root object, for tests and debugging.</summary>
        public const string CloneName = "FX_FurnitureClone";

        private struct FurnitureTween
        {
            public GameObject Clone; public GameObject Source; public Renderer[] Hidden;
            public float Age; public bool Shrink;
        }

        private static readonly List<MeshRenderer> RendererBuffer = new();
        private FurnitureTween[] _furniture;
        private int _furnitureCount;

        /// <summary>Furniture pops and shrinks currently running.</summary>
        public int ActiveFurnitureTweenCount => _furnitureCount;

        /// <summary>Dust puff and (unless motion is reduced) a pop on a visual clone of a player-placed object.</summary>
        public void PlayPlaced(GameObject placed)
        {
            if (placed == null) return;
            EnsureBuilt();
            Emit(_dust, placed.transform.position, DustBurst);
            if (!GameSettings.ReduceMotion) StartFurnitureTween(placed, false);
        }

        /// <summary>Dust puff and (unless motion is reduced) a shrinking visual clone of an object being removed.</summary>
        public void PlayRemoved(GameObject removed)
        {
            if (removed == null) return;
            EnsureBuilt();
            ForgetFurniture(removed);
            Emit(_dust, removed.transform.position, DustBurst);
            if (!GameSettings.ReduceMotion) StartFurnitureTween(removed, true);
        }

        /// <summary>1 → 0 with an ease-in, for <paramref name="t"/> in [0, 1].</summary>
        public static float ShrinkScale(float t)
        {
            float u = Mathf.Clamp01(t);
            return 1f - u * u;
        }

        private void StartFurnitureTween(GameObject source, bool shrink)
        {
            var renderers = VisibleMeshRenderers(source);
            if (renderers.Length == 0) return;
            if (_furnitureCount == _furniture.Length) FinishFurniture(0);
            var clone = BuildVisualClone(source.transform, renderers);
            clone.transform.localScale = Vector3.one * (shrink ? ShrinkScale(0f) : PopScale(0f));
            if (!shrink) SetRenderersEnabled(renderers, false);
            _furniture[_furnitureCount++] = new FurnitureTween
            {
                Clone = clone, Source = source, Hidden = shrink ? null : renderers, Shrink = shrink,
            };
        }

        private void TickFurniture(float dt)
        {
            for (int i = _furnitureCount - 1; i >= 0; i--)
            {
                ref var tween = ref _furniture[i];
                if (tween.Clone == null || (!tween.Shrink && tween.Source == null)) { FinishFurniture(i); continue; }
                tween.Age += dt;
                float t = tween.Age / (tween.Shrink ? ShrinkDuration : PopDuration);
                if (t >= 1f) { FinishFurniture(i); continue; }
                tween.Clone.transform.localScale = Vector3.one * (tween.Shrink ? ShrinkScale(t) : PopScale(t));
            }
        }

        /// <summary>Ends tween <paramref name="i"/>: shows the original again (pops) and destroys the clone.</summary>
        private void FinishFurniture(int i)
        {
            ref var tween = ref _furniture[i];
            if (tween.Hidden != null && tween.Source != null) SetRenderersEnabled(tween.Hidden, true);
            if (tween.Clone != null) PrefabPreview.DestroySafe(tween.Clone);
            _furniture[i] = _furniture[--_furnitureCount];
            _furniture[_furnitureCount] = default;
        }

        /// <summary>Finishes any pop still running on <paramref name="source"/>, so its renderers are visible again.</summary>
        private void ForgetFurniture(GameObject source)
        {
            for (int i = _furnitureCount - 1; i >= 0; i--)
                if (_furniture[i].Source == source) FinishFurniture(i);
        }

        /// <summary>A destroyed effect host must not leave furniture invisible.</summary>
        private void OnDestroy()
        {
            for (int i = _furnitureCount - 1; i >= 0; i--) FinishFurniture(i);
        }

        /// <summary>
        /// Enabled mesh renderers under <paramref name="source"/> with a mesh, excluding text and anything
        /// under a <see cref="WorldLabel"/> (it re-enables its own renderers every pre-cull).
        /// </summary>
        private static MeshRenderer[] VisibleMeshRenderers(GameObject source)
        {
            source.GetComponentsInChildren(false, RendererBuffer);
            for (int i = RendererBuffer.Count - 1; i >= 0; i--)
                if (!IsCloneable(RendererBuffer[i])) RendererBuffer.RemoveAt(i);
            var result = RendererBuffer.ToArray();
            RendererBuffer.Clear();
            return result;
        }

        private static bool IsCloneable(MeshRenderer renderer)
        {
            if (!renderer.enabled || renderer.GetComponent<TMP_Text>() != null) return false;
            if (renderer.GetComponentInParent<WorldLabel>() != null) return false;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null;
        }

        /// <summary>A root at the source's pivot holding one mesh-only copy per renderer, at the same world pose.</summary>
        private GameObject BuildVisualClone(Transform source, MeshRenderer[] renderers)
        {
            var root = new GameObject(CloneName);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(source.position, source.rotation);
            foreach (var renderer in renderers) CopyVisual(renderer, root.transform);
            return root;
        }

        private static void CopyVisual(MeshRenderer renderer, Transform root)
        {
            var part = new GameObject(renderer.name) { layer = renderer.gameObject.layer };
            part.transform.SetParent(root, false);
            part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            part.transform.localScale = renderer.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var copy = part.AddComponent<MeshRenderer>();
            copy.sharedMaterials   = renderer.sharedMaterials;
            copy.shadowCastingMode = renderer.shadowCastingMode;
            copy.receiveShadows    = renderer.receiveShadows;
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            foreach (var renderer in renderers)
                if (renderer != null) renderer.enabled = enabled;
        }
    }
}
