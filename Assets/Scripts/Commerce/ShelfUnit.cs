using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Commerce
{
    /// <summary>
    /// A physical shelf. Holds up to <see cref="MaxLines"/> different products,
    /// <see cref="MaxPerLine"/> units of each, and renders one small crate per unit
    /// on the shelf boards. Restocking is paid for out of the shop balance.
    /// </summary>
    public class ShelfUnit : MonoBehaviour
    {
        [System.Serializable]
        public class StockLine
        {
            public ProductItem Product;
            public int         Units;
        }

        [Header("Config")]
        public ProductCategory Category   = ProductCategory.Food;
        public int             MaxLines   = 3;
        public int             MaxPerLine = 5;

        [Header("Geometry — set by whoever builds the shelf mesh")]
        public float ShelfWidth  = 1.8f;
        public float ShelfHeight = 1.5f;
        public float ShelfDepth  = 0.5f;
        public bool  TwoShelves  = true;

        public UnityEvent<ShelfUnit> OnStockChanged = new();

        private readonly List<StockLine> _lines = new();
        private Transform  _visualRoot;
        private WorldLabel _priceTag;

        // ── Queries ─────────────────────────────────────────────────────────────

        public IReadOnlyList<StockLine> Lines => _lines;

        public int TotalUnits
        {
            get { int t = 0; foreach (var l in _lines) t += l.Units; return t; }
        }

        public bool IsEmpty  => TotalUnits <= 0;
        public bool HasSpace => _lines.Count < MaxLines || TotalUnits < _lines.Count * MaxPerLine;

        /// <summary>Id of the first stocked product — used by the save system and UI.</summary>
        public string ProductId
        {
            get
            {
                foreach (var l in _lines)
                    if (l.Units > 0 && l.Product != null) return l.Product.id;
                return null;
            }
        }

        /// <summary>What it would cost to fill every line to capacity.</summary>
        public float RestockCost()
        {
            float cost = 0f;
            foreach (var l in _lines)
                if (l.Product != null) cost += (MaxPerLine - l.Units) * l.Product.unitCost;
            return cost;
        }

        // ── Stock management ────────────────────────────────────────────────────

        private void Start()
        {
            // Price tag above the unit. Genre standard: the player should never have to open
            // a panel to see what a shelf is selling or how empty it is.
            _priceTag = WorldLabel.Create(transform, new Vector3(0f, ShelfHeight + 0.26f, ShelfDepth * 0.5f),
                                          "", 0.085f, UIFactory.Ink, width: 1.7f);
            RefreshLabel();

            var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
            if (shop != null) shop.OnPriceChanged.AddListener(_ => RefreshLabel());
        }

        /// <summary>
        /// Category, marked-up price and stock, colour-coded so a nearly-empty shelf reads
        /// from across the room.
        /// </summary>
        public void RefreshLabel()
        {
            if (_priceTag == null) return;

            if (_lines.Count == 0)
            {
                _priceTag.SetText($"{Category}\n<size=70%>empty — press E</size>");
                _priceTag.SetColour(UIFactory.Bad);
                return;
            }

            var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
            float lowest = float.MaxValue, highest = 0f;
            foreach (var line in _lines)
            {
                if (line.Product == null) continue;
                float price = shop != null ? shop.PriceOf(line.Product.basePrice) : line.Product.basePrice;
                lowest  = Mathf.Min(lowest, price);
                highest = Mathf.Max(highest, price);
            }

            int capacity = Mathf.Max(1, _lines.Count * MaxPerLine);
            float fill   = TotalUnits / (float)capacity;

            string priceText = Mathf.Approximately(lowest, highest)
                ? $"€ {lowest:0.00}"
                : $"€ {lowest:0.00} – {highest:0.00}";

            _priceTag.SetText($"{Category}   {priceText}\n<size=75%>{TotalUnits} / {capacity} in stock</size>");
            _priceTag.SetColour(fill <= 0.01f ? UIFactory.Bad
                              : fill < 0.3f   ? new Color(0.95f, 0.72f, 0.35f)
                                              : UIFactory.Ink);
        }

        /// <summary>Add units of a product. Returns how many were actually added.</summary>
        public int AddStock(ProductItem product, int units)
        {
            if (product == null || units <= 0) return 0;

            var line = _lines.Find(l => l.Product == product);
            if (line == null)
            {
                if (_lines.Count >= MaxLines) return 0;
                line = new StockLine { Product = product, Units = 0 };
                _lines.Add(line);
            }

            int added = Mathf.Min(units, MaxPerLine - line.Units);
            if (added <= 0) return 0;

            line.Units += added;
            RefreshVisuals();
            RefreshLabel();
            OnStockChanged.Invoke(this);
            return added;
        }

        /// <summary>Take one unit for a customer. Returns the product taken, or null if empty.</summary>
        public ProductItem TakeOne()
        {
            var stocked = _lines.FindAll(l => l.Units > 0 && l.Product != null);
            if (stocked.Count == 0) return null;

            var line = stocked[Random.Range(0, stocked.Count)];
            line.Units--;
            RefreshVisuals();
            RefreshLabel();
            OnStockChanged.Invoke(this);
            return line.Product;
        }

        /// <summary>What one restock did: how much went on the shelf, and what it cost.</summary>
        public struct RestockResult
        {
            public int   Units;
            public int   FromWarehouse;
            public float Spent;
        }

        /// <summary>
        /// Fill every line to capacity. Units already delivered to the stockroom go on for
        /// free (they were paid for when ordered); anything beyond that is bought on the spot
        /// at cash-and-carry prices, which is deliberately dearer than ordering ahead.
        /// An empty shelf first picks products from the catalog for its category.
        /// </summary>
        public RestockResult Restock(ShopManager shop, ItemDatabase catalog)
        {
            var result = new RestockResult();

            if (_lines.Count == 0 && catalog != null)
            {
                var available = catalog.GetByCategory(Category);
                for (int i = 0; i < available.Count && _lines.Count < MaxLines; i++)
                    _lines.Add(new StockLine { Product = available[i], Units = 0 });
            }

            foreach (var line in _lines)
            {
                if (line.Product == null) continue;
                while (line.Units < MaxPerLine)
                {
                    // Stockroom first — it is already paid for.
                    if (shop != null && shop.TakeFromWarehouse(Category, 1) == 1)
                    {
                        line.Units++;
                        result.Units++;
                        result.FromWarehouse++;
                        continue;
                    }

                    float price = line.Product.unitCost * ShopManager.EmergencyMarkup;
                    if (shop != null && !shop.ChangeBalance(-price, $"Cash-and-carry {line.Product.displayName}"))
                        goto done;

                    line.Units++;
                    result.Units++;
                    result.Spent += price;
                }
            }
        done:
            RefreshVisuals();
            RefreshLabel();
            OnStockChanged.Invoke(this);
            return result;
        }

        public void Clear()
        {
            _lines.Clear();
            RefreshVisuals();
            OnStockChanged.Invoke(this);
        }

        /// <summary>Human-readable contents, for the interaction panel.</summary>
        public string Describe()
        {
            if (_lines.Count == 0) return $"{Category} shelf — empty";
            var sb = new System.Text.StringBuilder($"{Category} shelf\n");
            foreach (var l in _lines)
                sb.AppendLine($"  {l.Product.displayName}  {l.Units}/{MaxPerLine}   €{l.Product.basePrice:0.00}");
            return sb.ToString().TrimEnd();
        }

        // ── Visuals ─────────────────────────────────────────────────────────────

        private void RefreshVisuals()
        {
            if (_visualRoot == null)
            {
                _visualRoot = new GameObject("Products").transform;
                _visualRoot.SetParent(transform, false);
            }
            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
                Destroy(_visualRoot.GetChild(i).gameObject);

            float[] boards = MeshBuilder.ShelfBoardHeights(ShelfHeight, TwoShelves);

            for (int row = 0; row < _lines.Count && row < boards.Length; row++)
            {
                var line = _lines[row];
                if (line.Product == null) continue;

                float slot    = (ShelfWidth - 0.18f) / MaxPerLine;
                float startX  = -(line.Units - 1) * slot * 0.5f;
                string model  = string.IsNullOrEmpty(line.Product.shelfModel)
                    ? null
                    : ModelLibrary.Food + line.Product.shelfModel;

                for (int i = 0; i < line.Units; i++)
                {
                    Vector3 local = new(startX + i * slot, boards[row] + 0.02f, 0f);
                    GameObject unit = null;

                    if (model != null)
                    {
                        unit = ModelLibrary.Spawn(model, _visualRoot, Vector3.zero, 0f,
                                                   ModelLibrary.Fit.Height, line.Product.shelfSize);
                        if (unit != null)
                        {
                            unit.transform.localPosition    = local;
                            unit.transform.localEulerAngles = new Vector3(0f, Random.Range(-18f, 18f), 0f);
                        }
                    }

                    if (unit == null)
                    {
                        // No model for this product (or the asset pack is absent) — coloured crate.
                        float w   = Mathf.Min(0.2f, slot * 0.8f);
                        var mat   = MaterialFactory.Get($"prod_{line.Product.id}", line.Product.fallbackColor, 0f, 0.55f);
                        unit = MeshBuilder.CreateBox(w, w * 1.1f, Mathf.Min(0.2f, ShelfDepth * 0.6f),
                                                     mat, $"{line.Product.id}_{i}");
                        unit.transform.SetParent(_visualRoot, false);
                        unit.transform.localPosition = local + new Vector3(0f, w * 0.55f, 0f);
                    }

                    foreach (var col in unit.GetComponentsInChildren<Collider>(true)) Destroy(col);
                    MeshBuilder.SetLayerRecursive(unit, gameObject.layer);
                }
            }
        }
    }
}
