using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using PetShop.Localization;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// Every character in the Ukrainian tables must be drawable by the UI font or one of its
    /// fallbacks. Glyphs are looked up in the source font files through the FontEngine, so the
    /// dynamic atlases are never touched (no asset churn from running the test).
    /// </summary>
    public class CyrillicFontTests
    {
        private const int SamplingPointSize = 90;

        [Test]
        public void UiFont_IsAvailable()
        {
            Assert.IsNotNull(UIFactory.Font(), "Nunito SDF must load from Resources.");
        }

        [Test]
        public void UiFontChain_CoversUkrainianAlphabet()
        {
            AssertCovered("АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя’«»—…№");
        }

        [Test]
        public void UiFontChain_CoversEveryUkrainianTableCharacter()
        {
            var chars = new HashSet<char>();
            foreach (var key in Loc.Keys(Language.Uk))
            {
                Assert.IsTrue(Loc.TryGetText(Language.Uk, key, out string text));
                foreach (char c in text) if (!char.IsWhiteSpace(c) && !char.IsControl(c)) chars.Add(c);
            }
            var sb = new StringBuilder();
            foreach (char c in chars) sb.Append(c);
            AssertCovered(sb.ToString());
        }

        private static void AssertCovered(string text)
        {
            var chain = FontChain(UIFactory.Font());
            Assert.IsNotEmpty(chain, "No font with a source file in the UI font chain.");

            var missing = new StringBuilder();
            foreach (char c in text)
            {
                if (c < 0x20 || c == '<' || c == '>') continue;
                if (!Covered(chain, c)) missing.Append(c);
            }
            Assert.IsEmpty(missing.ToString(), "Characters with no glyph in the UI font or its fallbacks.");
        }

        private static bool Covered(List<TMP_FontAsset> chain, char c)
        {
            foreach (var font in chain)
            {
                if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(c)) return true;
                if (font.sourceFontFile == null) continue;
                if (FontEngine.LoadFontFace(font.sourceFontFile, SamplingPointSize) != FontEngineError.Success) continue;
                if (FontEngine.TryGetGlyphIndex(c, out uint index) && index != 0) return true;
            }
            return false;
        }

        private static List<TMP_FontAsset> FontChain(TMP_FontAsset root)
        {
            var chain = new List<TMP_FontAsset>();
            Collect(root, chain);
            if (TMP_Settings.instance != null && TMP_Settings.fallbackFontAssets != null)
                foreach (var f in TMP_Settings.fallbackFontAssets) Collect(f, chain);
            return chain;
        }

        private static void Collect(TMP_FontAsset font, List<TMP_FontAsset> chain)
        {
            if (font == null || chain.Contains(font)) return;
            chain.Add(font);
            if (font.fallbackFontAssetTable == null) return;
            foreach (var f in font.fallbackFontAssetTable) Collect(f, chain);
        }
    }
}
