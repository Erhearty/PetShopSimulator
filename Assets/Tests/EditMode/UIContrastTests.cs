using NUnit.Framework;
using UnityEngine;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode WCAG 2.x contrast checks for the text/fill pairs <see cref="UIFactory"/> uses on
    /// buttons, computed from relative luminance rather than trusted from the token comments.
    /// </summary>
    public class UIContrastTests
    {
        /// <summary>WCAG AA minimum for normal-size text.</summary>
        private const float MinimumTextContrast = 4.5f;
        /// <summary>Offset WCAG adds to both luminances before taking their ratio.</summary>
        private const float FlareOffset = 0.05f;
        private const float LinearThreshold = 0.04045f;
        private const float LinearScale = 12.92f;
        private const float GammaOffset = 0.055f;
        private const float GammaScale  = 1.055f;
        private const float Gamma       = 2.4f;
        private const float RedWeight   = 0.2126f;
        private const float GreenWeight = 0.7152f;
        private const float BlueWeight  = 0.0722f;

        /// <summary>WCAG relative luminance of an sRGB colour.</summary>
        private static float Luminance(Color c) =>
            RedWeight * Linear(c.r) + GreenWeight * Linear(c.g) + BlueWeight * Linear(c.b);

        private static float Linear(float channel) => channel <= LinearThreshold
            ? channel / LinearScale
            : Mathf.Pow((channel + GammaOffset) / GammaScale, Gamma);

        /// <summary>WCAG contrast ratio between two colours (order-independent).</summary>
        private static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + FlareOffset) / (Mathf.Min(la, lb) + FlareOffset);
        }

        [Test]
        public void Ink_On_DestructiveFill_MeetsAA()
        {
            Assert.GreaterOrEqual(Contrast(UIFactory.Ink, UIFactory.DestructiveFill), MinimumTextContrast);
            Assert.AreEqual(UIFactory.Ink, UIFactory.LabelColourOn(UIFactory.DestructiveFill));
        }

        [Test]
        public void OnAccent_On_Accent_MeetsAA()
        {
            Assert.GreaterOrEqual(Contrast(UIFactory.OnAccent, UIFactory.Accent), MinimumTextContrast);
            Assert.AreEqual(UIFactory.OnAccent, UIFactory.LabelColourOn(UIFactory.Accent));
        }

        [Test]
        public void Ink_On_ButtonBg_MeetsAA()
        {
            Assert.GreaterOrEqual(Contrast(UIFactory.Ink, UIFactory.ButtonBg), MinimumTextContrast);
            Assert.AreEqual(UIFactory.Ink, UIFactory.LabelColourOn(UIFactory.ButtonBg));
        }
    }
}
