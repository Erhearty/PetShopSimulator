using NUnit.Framework;
using TMPro;
using UnityEngine;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for the generated Nunito SDF font asset (built by FontAssetBuilder) that
    /// <see cref="UIFactory.Font"/> loads from Resources.
    /// </summary>
    public class NunitoFontAssetTests
    {
        /// <summary>Resources path of the font asset under test.</summary>
        private const string FontPath = "Fonts/Nunito SDF";

        /// <summary>A plain ASCII letter.</summary>
        private const char AsciiLetter = 'A';

        /// <summary>The euro sign, one of the prepopulated typographic extras.</summary>
        private const char EuroSign = '\u20AC';

        /// <summary>The asset is in Resources at the path UIFactory uses.</summary>
        [Test]
        public void FontAsset_LoadsFromResources()
        {
            Assert.AreEqual(UIFactory.FontResourcePath, FontPath);
            Assert.IsNotNull(Resources.Load<TMP_FontAsset>(FontPath));
        }

        /// <summary>ASCII and the euro sign are prepopulated in the asset.</summary>
        [Test]
        public void FontAsset_HasAsciiAndEuro()
        {
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            Assert.IsNotNull(font);
            Assert.IsTrue(font.HasCharacter(AsciiLetter), "missing 'A'");
            Assert.IsTrue(font.HasCharacter(EuroSign), "missing '€'");
        }
    }
}
