using System;
using System.Collections.Generic;

namespace PetShop.Commerce
{
    /// <summary>Auto-reorder settings for one product category.</summary>
    [Serializable]
    public class ReorderRule
    {
        public ProductCategory Category;
        public bool            Enabled;
        public int             Threshold;
        public int             Units;
    }

    public static class ShopOrderExtensions
    {
        /// <summary>Units ordered for this category that have not been delivered yet.</summary>
        public static int PendingUnits(this ShopManager shop, ProductCategory category)
        {
            int total = 0;
            foreach (var o in shop.Orders)
                if (!o.Delivered && o.Category == category) total += o.Units;
            return total;
        }
    }

    /// <summary>
    /// Pure decision logic for automatic supplier reordering: one rule per category.
    /// </summary>
    public class AutoReorder
    {
        public const bool DefaultEnabled   = false;
        public const int  DefaultThreshold = 10;
        public const int  DefaultUnits     = 12;

        private readonly List<ReorderRule> _rules = new();

        /// <summary>Categories skipped for lack of money in the last <see cref="Decide"/> pass.</summary>
        public List<ProductCategory> LastSkipped { get; } = new();

        public IReadOnlyList<ReorderRule> Rules => _rules;

        public AutoReorder()
        {
            foreach (ProductCategory c in Enum.GetValues(typeof(ProductCategory)))
                _rules.Add(new ReorderRule
                {
                    Category  = c,
                    Enabled   = DefaultEnabled,
                    Threshold = DefaultThreshold,
                    Units     = DefaultUnits,
                });
        }

        public ReorderRule Rule(ProductCategory category)
        {
            foreach (var r in _rules) if (r.Category == category) return r;
            return null;
        }

        /// <summary>
        /// Returns the (category, units) orders to place. <paramref name="onHand"/> should include
        /// pending deliveries. An order is skipped (and listed in <see cref="LastSkipped"/>) when
        /// it would leave the running balance below <paramref name="reserve"/>.
        /// </summary>
        public IEnumerable<(ProductCategory, int)> Decide(Func<ProductCategory, int> onHand,
            Func<ProductCategory, float> orderCost, float balance, float reserve)
        {
            LastSkipped.Clear();
            var result = new List<(ProductCategory, int)>();
            foreach (var r in _rules)
            {
                if (!r.Enabled || r.Units <= 0 || onHand(r.Category) >= r.Threshold) continue;
                float cost = orderCost(r.Category);
                if (balance - cost < reserve) { LastSkipped.Add(r.Category); continue; }
                balance -= cost;
                result.Add((r.Category, r.Units));
            }
            return result;
        }
    }
}
