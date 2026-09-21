using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Commerce
{
    /// <summary>
    /// Single source of truth for every sellable product.
    /// <see cref="Load"/> prefers an authored asset at Resources/ItemDatabase, and falls
    /// back to a procedurally built catalog so the game boots with zero assets on disk.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "PetShop/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ProductItem> items = new();

        private Dictionary<string, ProductItem>                    _map;
        private Dictionary<ProductCategory, List<ProductItem>>     _byCategory;

        private static ItemDatabase _runtime;

        // ── Lookup ──────────────────────────────────────────────────────────────

        public IReadOnlyList<ProductItem> All => items;

        public ProductItem Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map == null) Rebuild();
            return _map.TryGetValue(id, out var item) ? item : null;
        }

        public IReadOnlyList<ProductItem> GetByCategory(ProductCategory category)
        {
            if (_byCategory == null) Rebuild();
            return _byCategory.TryGetValue(category, out var list)
                ? list
                : (IReadOnlyList<ProductItem>)System.Array.Empty<ProductItem>();
        }

        /// <summary>Typical wholesale cost of a unit in this aisle — what an order is priced off.</summary>
        public float AverageUnitCost(ProductCategory category)
        {
            var list = GetByCategory(category);
            if (list.Count == 0) return 3.2f;

            float total = 0f;
            foreach (var item in list) total += item.unitCost;
            return total / list.Count;
        }

        public ProductItem RandomOfCategory(ProductCategory category)
        {
            var list = GetByCategory(category);
            return list.Count == 0 ? null : list[Random.Range(0, list.Count)];
        }

        public ProductItem RandomAny() =>
            items.Count == 0 ? null : items[Random.Range(0, items.Count)];

        private void Rebuild()
        {
            _map        = new Dictionary<string, ProductItem>(items.Count);
            _byCategory = new Dictionary<ProductCategory, List<ProductItem>>();

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                _map[item.id] = item;

                if (!_byCategory.TryGetValue(item.category, out var list))
                    _byCategory[item.category] = list = new List<ProductItem>();
                list.Add(item);
            }
        }

        private void OnValidate() { _map = null; _byCategory = null; }

        // ── Loading ─────────────────────────────────────────────────────────────

        /// <summary>Cached catalog — authored asset if present, procedural fallback otherwise.</summary>
        public static ItemDatabase Load()
        {
            if (_runtime != null) return _runtime;

            _runtime = Resources.Load<ItemDatabase>("ItemDatabase");
            if (_runtime == null || _runtime.items.Count == 0)
                _runtime = CreateDefault();

            return _runtime;
        }

        /// <summary>The built-in catalog, created entirely in memory.</summary>
        public static ItemDatabase CreateDefault()
        {
            var db = CreateInstance<ItemDatabase>();
            db.name = "ItemDatabase (runtime)";
            db.items = new List<ProductItem>
            {
                ProductItem.Create("cat_food",      "Cat Food",        ProductCategory.Food,      2.20f,  4.50f, new Color(0.90f, 0.72f, 0.30f), "bag",            0.26f),
                ProductItem.Create("dog_food",      "Dog Food",        ProductCategory.Food,      2.80f,  5.60f, new Color(0.78f, 0.55f, 0.24f), "bag",            0.30f),
                ProductItem.Create("rabbit_pellets","Rabbit Pellets",  ProductCategory.Food,      1.60f,  3.40f, new Color(0.72f, 0.80f, 0.40f), "carton",         0.24f),
                ProductItem.Create("fish_flakes",   "Fish Flakes",     ProductCategory.Food,      1.10f,  2.60f, new Color(0.95f, 0.85f, 0.55f), "carton-small",   0.18f),

                ProductItem.Create("dog_toy",       "Squeaky Bone",    ProductCategory.Toy,       3.00f,  7.50f, new Color(0.88f, 0.30f, 0.28f), "can",            0.17f),
                ProductItem.Create("cat_wand",      "Feather Wand",    ProductCategory.Toy,       2.40f,  6.20f, new Color(0.92f, 0.45f, 0.62f), "bottle-oil",     0.26f),
                ProductItem.Create("chew_ball",     "Chew Ball",       ProductCategory.Toy,       1.90f,  4.80f, new Color(0.35f, 0.65f, 0.90f), "soda-can",       0.18f),

                ProductItem.Create("collar",        "Leather Collar",  ProductCategory.Accessory, 4.50f, 11.00f, new Color(0.55f, 0.35f, 0.22f), "bag-flat",       0.14f),
                ProductItem.Create("leash",         "Nylon Leash",     ProductCategory.Accessory, 5.20f, 12.50f, new Color(0.30f, 0.50f, 0.80f), "bag-flat",       0.14f),
                ProductItem.Create("water_bowl",    "Water Bowl",      ProductCategory.Accessory, 3.10f,  7.90f, new Color(0.65f, 0.72f, 0.78f), "bowl",           0.14f),
                ProductItem.Create("bird_perch",    "Bird Perch",      ProductCategory.Accessory, 4.00f,  9.80f, new Color(0.72f, 0.62f, 0.42f), "barrel",         0.24f),

                ProductItem.Create("flea_drops",    "Flea Drops",      ProductCategory.Medicine,  6.00f, 14.00f, new Color(0.35f, 0.80f, 0.55f), "bottle-oil",     0.24f),
                ProductItem.Create("vitamins",      "Pet Vitamins",    ProductCategory.Medicine,  5.40f, 12.80f, new Color(0.45f, 0.85f, 0.70f), "can-small",      0.14f),
                ProductItem.Create("wound_gel",     "Wound Gel",       ProductCategory.Medicine,  7.20f, 16.50f, new Color(0.30f, 0.70f, 0.62f), "bottle-ketchup", 0.26f),
            };
            return db;
        }
    }
}
