using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PauseMenu"/>: token styling of the primary and destructive
    /// rows, and the two-press confirm on "Abandon shop" that Esc cancels.
    /// </summary>
    public class PauseMenuTests
    {
        private GameObject _root;
        private PauseMenu  _menu;
        private LocTestScope _loc;

        [SetUp]
        public void SetUp()
        {
            _loc  = LocTestScope.Begin();
            _root = new GameObject("Canvas", typeof(RectTransform));
            _menu = _root.AddComponent<PauseMenu>();
            _menu.Build(_root.transform, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            _loc.End();
        }

        private Button Find(string name)
        {
            foreach (var b in _root.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
            return null;
        }

        private string StatusText()
        {
            foreach (var t in _root.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Status") return t.text;
            return null;
        }

        [Test]
        public void Resume_IsAccentWithOnAccentText()
        {
            var resume = Find("Resume");
            Assert.AreEqual(UIFactory.Accent, resume.GetComponent<Image>().color);
            Assert.AreEqual(UIFactory.OnAccent, resume.GetComponentInChildren<TMP_Text>().color);
            Assert.AreEqual("Resume", resume.GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void Abandon_IsDestructive_AndNeedsASecondPress()
        {
            var abandon = Find("Restart");
            var label   = abandon.GetComponentInChildren<TMP_Text>();
            Assert.AreEqual(UIFactory.DestructiveFill, abandon.GetComponent<Image>().color);
            Assert.AreEqual(UIFactory.Ink, label.color);
            Assert.AreEqual(PauseMenu.AbandonLabel, label.text);

            _menu.PressAbandon();

            Assert.IsTrue(_menu.IsConfirmingAbandon);
            Assert.AreEqual(PauseMenu.AbandonConfirmLabel, label.text);
        }

        [Test]
        public void CancelAbandon_DisarmsAndRestoresLabel()
        {
            _menu.PressAbandon();

            Assert.IsTrue(_menu.CancelAbandon());

            Assert.IsFalse(_menu.IsConfirmingAbandon);
            Assert.AreEqual(PauseMenu.AbandonLabel, Find("Restart").GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(_menu.CancelAbandon(), "nothing left to cancel");
            Assert.AreNotEqual(PauseMenu.AbandonWarning, StatusText(), "the warning gives way to the status line");
        }
    }
}
