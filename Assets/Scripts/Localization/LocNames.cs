using System;

namespace PetShop.Localization
{
    /// <summary>
    /// Display names for enum values (product categories, species, rarities, staff roles,
    /// customer types, events). The enum value itself stays the identity used in saves and
    /// comparisons; only the returned text is translated.
    /// </summary>
    public static class LocNames
    {
        /// <summary>
        /// The display name of <paramref name="value"/> under <c>&lt;prefix&gt;.&lt;lower-case name&gt;</c>,
        /// or the enum name itself when the table has no entry.
        /// </summary>
        public static string Of(string prefix, Enum value)
        {
            if (value == null) return "";
            string name = value.ToString();
            string key  = $"{prefix}.{ToKey(name)}";
            return Loc.Has(key) ? Loc.T(key) : name;
        }

        /// <summary>A product category name, lower-case in running text (e.g. "food").</summary>
        public static string Category(Enum category) => Of("category", category);

        /// <summary>A product category name as a title (e.g. "Food").</summary>
        public static string CategoryTitle(Enum category) => Of("category.title", category);

        /// <summary>A species name as a title (e.g. "Rabbit").</summary>
        public static string Species(Enum species) => Of("species", species);

        /// <summary>A rarity name (e.g. "Rare").</summary>
        public static string Rarity(Enum rarity) => Of("rarity", rarity);

        /// <summary>A staff role name (e.g. "Cashier").</summary>
        public static string Role(Enum role) => Of("staff.role", role);

        /// <summary>snake_case lower-case of a PascalCase enum name: "BargainHunter" → "bargain_hunter".</summary>
        public static string ToKey(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
