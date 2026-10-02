using UnityEngine;

namespace PetShop.Shop
{
    /// <summary>
    /// Marker for one piece of starter furniture, authored by hand in the scene. A brand-new
    /// game places one catalog item at each marker's cell (derived from its world position)
    /// through <c>BuildMode.Place</c>; a loaded save ignores the markers entirely. The marker
    /// object is switched off at runtime, so a prefab instance used as a preview never doubles
    /// the real furniture.
    /// </summary>
    public class StarterPiece : MonoBehaviour
    {
        [Tooltip("BuildCatalog id, e.g. 'shelf_small', 'pet_pen', 'counter'.")]
        public string CatalogId;

        [Tooltip("Shelf category or pen species. Empty for none.")]
        public string Variant;

        [Tooltip("Yaw in degrees.")]
        public float Rotation;

        [Tooltip("Optional seed for anything random about this piece. 0 = unseeded.")]
        public int Seed;

        private void Awake()
        {
            // Still discoverable (ShopLayout searches inactive objects) but no longer visible.
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
