using UnityEngine;

namespace PetShop.Commerce
{
    public enum ProductCategory { Food, Toy, Accessory, Medicine }

    /// <summary>
    /// A sellable shelf product. Instances are normally built procedurally by
    /// <see cref="ItemDatabase.CreateDefault"/>; the CreateAssetMenu entry exists so
    /// designers can author extra products as assets later.
    /// </summary>
    [CreateAssetMenu(fileName = "NewProduct", menuName = "PetShop/Product Item")]
    public class ProductItem : ScriptableObject
    {
        [Header("Identity")]
        public string          id;
        public string          displayName;
        public ProductCategory category;

        [Header("Economics")]
        public float unitCost  = 5f;    // what you pay to restock one unit
        public float basePrice = 10f;   // what a customer pays for one unit

        [Header("Display")]
        public Color fallbackColor = Color.white;

        [Tooltip("Kenney food-kit model shown on the shelf. Empty falls back to a coloured crate.")]
        public string shelfModel = "";

        [Tooltip("How tall one unit stands on the shelf, in metres.")]
        public float shelfSize = 0.22f;

        public float DefaultMargin => basePrice - unitCost;

        /// <summary>Build a runtime product without needing an asset on disk.</summary>
        public static ProductItem Create(string id, string displayName, ProductCategory category,
                                         float unitCost, float basePrice, Color color,
                                         string shelfModel = "", float shelfSize = 0.22f)
        {
            var p = CreateInstance<ProductItem>();
            p.name          = id;
            p.id            = id;
            p.displayName   = displayName;
            p.category      = category;
            p.unitCost      = unitCost;
            p.basePrice     = basePrice;
            p.fallbackColor = color;
            p.shelfModel    = shelfModel;
            p.shelfSize     = shelfSize;
            return p;
        }
    }
}
