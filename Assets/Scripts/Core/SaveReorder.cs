using System;
using PetShop.Commerce;

namespace PetShop.Core
{
    /// <summary>Saved auto-reorder settings for one product category.</summary>
    [Serializable]
    public class ReorderRuleSave
    {
        public string category;
        public bool   enabled;
        public int    threshold;
        public int    units;
    }

    /// <summary>Converts <see cref="AutoReorder"/> rules to and from save data.</summary>
    public static class SaveReorder
    {
        /// <summary>Copies every rule into <paramref name="data"/>.AutoReorder.</summary>
        public static void Capture(SaveData data, AutoReorder reorder)
        {
            data.AutoReorder.Clear();
            foreach (var r in reorder.Rules)
                data.AutoReorder.Add(new ReorderRuleSave
                {
                    category = r.Category.ToString(),
                    enabled  = r.Enabled,
                    threshold = r.Threshold,
                    units    = r.Units,
                });
        }

        /// <summary>
        /// Resets <paramref name="reorder"/> to defaults, then applies saved values by category.
        /// Old saves (no entries) leave the defaults.
        /// </summary>
        public static void Apply(SaveData data, AutoReorder reorder)
        {
            ResetToDefaults(reorder);
            if (data?.AutoReorder == null) return;
            foreach (var s in data.AutoReorder)
            {
                if (s == null || !Enum.TryParse(s.category, out ProductCategory c)) continue;
                var rule = reorder.Rule(c);
                if (rule == null) continue;
                rule.Enabled   = s.enabled;
                rule.Threshold = Math.Max(0, s.threshold);
                rule.Units     = Math.Max(0, s.units);
            }
        }

        /// <summary>Puts every rule back to the built-in defaults.</summary>
        public static void ResetToDefaults(AutoReorder reorder)
        {
            foreach (var r in reorder.Rules)
            {
                r.Enabled   = AutoReorder.DefaultEnabled;
                r.Threshold = AutoReorder.DefaultThreshold;
                r.Units     = AutoReorder.DefaultUnits;
            }
        }
    }
}
