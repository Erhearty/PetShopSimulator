using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Pets
{
    /// <summary>
    /// A named coat palette and a classifier that maps any colour to its nearest named entry.
    /// Distance is measured in HSV (as a cone, so hue wraps around and low-saturation colours
    /// collapse toward the White / Grey / Black / Cream family).
    /// </summary>
    public static class CoatColours
    {
        private readonly struct Swatch
        {
            public readonly string Name;
            public readonly float H, S, V;
            public Swatch(string name, float h, float s, float v) { Name = name; H = h; S = s; V = v; }
        }

        private const float FullTurn = 2f * Mathf.PI;

        private static readonly Swatch[] Palette =
        {
            new Swatch("White",     0.00f, 0.00f, 0.95f),
            new Swatch("Cream",     0.12f, 0.15f, 0.92f),
            new Swatch("Golden",    0.11f, 0.55f, 0.85f),
            new Swatch("Ginger",    0.065f, 0.80f, 0.85f),
            new Swatch("Brown",     0.08f, 0.50f, 0.55f),
            new Swatch("Chocolate", 0.07f, 0.60f, 0.30f),
            new Swatch("Grey",      0.00f, 0.00f, 0.55f),
            new Swatch("Black",     0.00f, 0.00f, 0.15f),
            new Swatch("Red",       0.00f, 0.80f, 0.80f),
            new Swatch("Blue",      0.60f, 0.75f, 0.85f),
            new Swatch("Green",     0.35f, 0.70f, 0.65f),
            new Swatch("Yellow",    0.15f, 0.85f, 0.95f),
        };

        private static readonly string[] NameList = BuildNames();

        /// <summary>Every coat name the classifier can return.</summary>
        public static IReadOnlyList<string> Names => NameList;

        /// <summary>
        /// The player-facing name of the coat <paramref name="coatName"/> (one of <see cref="Names"/>) in
        /// the current language, or the name itself when the table has no entry. Display only:
        /// compare and save the English name.
        /// </summary>
        public static string DisplayName(string coatName)
        {
            if (string.IsNullOrEmpty(coatName)) return string.Empty;
            string key = "coat." + coatName.ToLowerInvariant();
            return PetShop.Localization.Loc.Has(key) ? PetShop.Localization.Loc.T(key) : coatName;
        }

        /// <summary>Name of the palette entry nearest to <paramref name="c"/>.</summary>
        public static string Classify(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            string best = Palette[0].Name;
            float bestDist = float.MaxValue;
            foreach (var sw in Palette)
            {
                float d = Distance(h, s, v, sw);
                if (d < bestDist) { bestDist = d; best = sw.Name; }
            }
            return best;
        }

        /// <summary>Squared distance between an HSV colour and a swatch in cone space.</summary>
        private static float Distance(float h, float s, float v, Swatch sw)
        {
            float r1 = s * v, r2 = sw.S * sw.V;
            float dx = r1 * Mathf.Cos(h * FullTurn) - r2 * Mathf.Cos(sw.H * FullTurn);
            float dy = r1 * Mathf.Sin(h * FullTurn) - r2 * Mathf.Sin(sw.H * FullTurn);
            float dz = v - sw.V;
            return dx * dx + dy * dy + dz * dz;
        }

        private static string[] BuildNames()
        {
            var names = new string[Palette.Length];
            for (int i = 0; i < Palette.Length; i++) names[i] = Palette[i].Name;
            return names;
        }
    }
}
