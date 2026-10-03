using UnityEngine;
using UnityEngine.Rendering;

namespace PetShop.Shop
{
    /// <summary>
    /// Behaviour-free visual copies of furniture prefabs, shared by the build ghost and the
    /// held-item viewmodel. Copies keep only transforms, meshes and renderers.
    /// </summary>
    public static class PrefabPreview
    {
        /// <summary>
        /// Instantiates <paramref name="prefab"/> under an inactive holder (so no Awake runs),
        /// strips every MonoBehaviour and Collider, then parents it to <paramref name="parent"/>.
        /// </summary>
        public static GameObject CreateStripped(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            var holder = new GameObject("PreviewHolder");
            holder.SetActive(false);

            var copy = Object.Instantiate(prefab, holder.transform, false);
            StripComponents(copy);
            copy.transform.SetParent(parent, false);
            copy.SetActive(true);
            Object.DestroyImmediate(holder);
            return copy;
        }

        /// <summary>Gives every renderer in <paramref name="root"/> only <paramref name="mat"/>, on every slot.</summary>
        public static void ApplyMaterial(GameObject root, Material mat)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Turns shadow casting and receiving off for every renderer in <paramref name="root"/>.</summary>
        public static void DisableShadows(GameObject root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows    = false;
            }
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode (EditMode tests).</summary>
        public static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else                       Object.DestroyImmediate(obj);
        }

        /// <summary>Removes behaviours (last first, so dependants go before what they require), then colliders.</summary>
        private static void StripComponents(GameObject copy)
        {
            var behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
                if (behaviours[i] != null) Object.DestroyImmediate(behaviours[i]);

            foreach (var col in copy.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
        }
    }
}
