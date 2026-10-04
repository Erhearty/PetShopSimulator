using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="UIFactory"/>: the design tokens, legacy aliases, type scale,
    /// rounded sprites and button states.
    /// </summary>
    public class UIFactoryTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("Root", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Tokens_MatchTheDesignHexValues()
        {
            Assert.AreEqual((Color)new Color32(0x14, 0x30, 0x3A, 255), UIFactory.Surface);
            Assert.AreEqual((Color)new Color32(0xF5, 0xB9, 0x35, 255), UIFactory.Accent);
            Assert.AreEqual((Color)new Color32(0xF2, 0xF6, 0xF5, 255), UIFactory.Ink);
        }

        [Test]
        public void LegacyNames_AliasTheTokenRoles()
        {
            Assert.AreEqual(UIFactory.Positive, UIFactory.Good);
            Assert.AreEqual(UIFactory.Destructive, UIFactory.Bad);
            Assert.AreEqual(UIFactory.Warning, UIFactory.Warn);
            Assert.AreEqual(UIFactory.Raised, UIFactory.ButtonBg);
            Assert.AreEqual(UIFactory.Accent, UIFactory.ButtonOn);
        }

        [Test]
        public void TypeScale_Is14_18_24_32()
        {
            Assert.AreEqual(14f, UIFactory.TextSmall);
            Assert.AreEqual(18f, UIFactory.TextBody);
            Assert.AreEqual(24f, UIFactory.TextHeading);
            Assert.AreEqual(32f, UIFactory.TextTitle);
        }

        [Test]
        public void Sprites_UsePanelAndControlRadius()
        {
            Assert.AreEqual(UIFactory.PanelRadius, UIFactory.RoundedSprite().border.x);
            Assert.AreEqual(UIFactory.ControlRadius, UIFactory.ControlSprite().border.x);
        }

        [Test]
        public void Button_HasStatesFocusOutlineAndReadableLabel()
        {
            var btn = UIFactory.Button("B", _root.transform, "Go", Vector2.zero, Vector2.one, 17f, UIFactory.Accent);

            Assert.IsNotNull(btn.GetComponent<FocusOutline>());
            Assert.AreNotEqual(btn.colors.normalColor, btn.colors.disabledColor);
            Assert.AreEqual(UIFactory.OnAccent, btn.GetComponentInChildren<TMP_Text>().color);

            UIFactory.SetSelected(btn, false);
            Assert.AreEqual(UIFactory.Raised, btn.GetComponent<Image>().color);
            Assert.AreEqual(UIFactory.Ink, btn.GetComponentInChildren<TMP_Text>().color);
        }
    }
}
