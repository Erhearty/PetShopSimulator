using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.UI;

namespace PetShop.Tests
{
    public class LocalizationTests
    {
        private LocTestScope _scope;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp() => _scope = LocTestScope.Begin(Language.En);

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            _scope.End();
        }

        [Test]
        public void Language_DefaultsToEnglish_WhenUnset()
        {
            Loc.ResetForTests();
            Assert.AreEqual("en", GameSettings.Language);
            Assert.AreEqual(Language.En, Loc.Current);
        }

        [Test]
        public void Language_UnknownValue_ReadsAsEnglish()
        {
            _scope.Store.Write(GameSettings.LanguageKey, "fr");
            Loc.ResetForTests();
            Assert.AreEqual("en", GameSettings.Language);
            Assert.AreEqual(Language.En, Loc.Current);
        }

        [Test]
        public void SetLanguage_PersistsUnderLanguageKey_AndSwitchesText()
        {
            Loc.SetLanguage(Language.Uk);
            Assert.AreEqual("uk", _scope.Store.Read("language"));
            Assert.AreEqual("uk", GameSettings.Language);
            Assert.AreEqual("Налаштування", Loc.T("settings.title"));

            Loc.ResetForTests();
            Assert.AreEqual(Language.Uk, Loc.Current, "The choice survives a restart.");
        }

        [Test]
        public void SetLanguageWithoutSaving_DoesNotPersist()
        {
            Loc.SetLanguageWithoutSaving(Language.Uk);
            Assert.AreEqual(Language.Uk, Loc.Current);
            Assert.IsNull(_scope.Store.Read(GameSettings.LanguageKey));
        }

        [Test]
        public void SetLanguage_RaisesLanguageChanged()
        {
            int raised = 0;
            void Handler() => raised++;
            Loc.LanguageChanged += Handler;
            try { Loc.SetLanguage(Language.Uk); }
            finally { Loc.LanguageChanged -= Handler; }
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void T_MissingKey_ReturnsKey_AndWarnsOnce()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no.such.key"));
            Assert.AreEqual("no.such.key", Loc.T("no.such.key"));
            Assert.AreEqual("no.such.key", Loc.T("no.such.key"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void F_UsesCurrentCulture()
        {
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            try
            {
                Assert.AreEqual("Day 1,5", Loc.F("common.day", 1.5));
                Assert.AreEqual(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, "Day {0:N2}", 1234.5),
                    Loc.F("common.day", 1234.5.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));
                Assert.AreEqual($"Day {1.5}", Loc.F("common.day", 1.5), "Matches plain interpolation.");
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
        }

        [TestCase(1, ".one")]
        [TestCase(2, ".few")]
        [TestCase(4, ".few")]
        [TestCase(5, ".many")]
        [TestCase(11, ".many")]
        [TestCase(12, ".many")]
        [TestCase(14, ".many")]
        [TestCase(21, ".one")]
        [TestCase(22, ".few")]
        [TestCase(111, ".many")]
        [TestCase(0, ".many")]
        public void PluralSuffix_Ukrainian(int n, string expected) =>
            Assert.AreEqual(expected, Loc.PluralSuffix(Language.Uk, n));

        [TestCase(1, ".one")]
        [TestCase(0, ".other")]
        [TestCase(2, ".other")]
        public void PluralSuffix_English(int n, string expected) =>
            Assert.AreEqual(expected, Loc.PluralSuffix(Language.En, n));

        [Test]
        public void Plural_FormatsCountInBothLanguages()
        {
            Assert.AreEqual("1 day", Loc.Plural("common.days", 1));
            Assert.AreEqual("3 days", Loc.Plural("common.days", 3));
            Loc.SetLanguageWithoutSaving(Language.Uk);
            Assert.AreEqual("1 день", Loc.Plural("common.days", 1));
            Assert.AreEqual("3 дні", Loc.Plural("common.days", 3));
            Assert.AreEqual("5 днів", Loc.Plural("common.days", 5));
        }

        [Test]
        public void Tables_HaveTheSameKeys_ModuloPluralForms()
        {
            var en = new HashSet<string>(Loc.Keys(Language.En));
            var uk = new HashSet<string>(Loc.Keys(Language.Uk));
            var problems = new List<string>();
            foreach (var key in en)
            {
                if (key.EndsWith(".other")) continue;
                if (!uk.Contains(key)) problems.Add($"uk missing {key}");
            }
            foreach (var key in uk)
            {
                if (key.EndsWith(".few") || key.EndsWith(".many")) continue;
                if (!en.Contains(key)) problems.Add($"en missing {key}");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Tables_PluralKeys_HaveEveryForm()
        {
            var problems = new List<string>();
            foreach (var key in Loc.Keys(Language.En))
            {
                if (!key.EndsWith(".other")) continue;
                string stem = key.Substring(0, key.Length - ".other".Length);
                foreach (var s in new[] { ".one", ".few", ".many" })
                    if (!Loc.Has(stem + s, Language.Uk)) problems.Add($"uk missing {stem}{s}");
                if (!Loc.Has(stem + ".one", Language.En)) problems.Add($"en missing {stem}.one");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Tables_PlaceholdersMatch()
        {
            var placeholder = new Regex(@"\{(\d+)[^}]*\}");
            var problems = new List<string>();
            foreach (var key in Loc.Keys(Language.En))
            {
                if (!Loc.TryGetText(Language.Uk, key, out string ukText)) continue;
                Loc.TryGetText(Language.En, key, out string enText);
                var a = Indices(placeholder, enText);
                var b = Indices(placeholder, ukText);
                if (!a.SetEquals(b)) problems.Add(key);
            }
            Assert.IsEmpty(problems, "Placeholder mismatch: " + string.Join(", ", problems));
        }

        [Test]
        public void Tables_KeysAreDottedLowerCase()
        {
            var shape = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_\-]+)+$");
            var bad = new List<string>();
            foreach (var key in Loc.Keys(Language.En)) if (!shape.IsMatch(key)) bad.Add(key);
            foreach (var key in Loc.Keys(Language.Uk)) if (!shape.IsMatch(key)) bad.Add(key);
            Assert.IsEmpty(bad, string.Join(", ", bad));
        }

        [Test]
        public void Tables_FormatWithoutErrors()
        {
            var args = new object[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            foreach (var lang in new[] { Language.En, Language.Uk })
            foreach (var key in Loc.Keys(lang))
            {
                Loc.TryGetText(lang, key, out string text);
                Assert.DoesNotThrow(() => string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args), key);
            }
        }

        [Test]
        public void ProductItem_LocalizedName_FallsBackToDisplayName()
        {
            var p = ProductItem.Create("no_such_product_id", "Mystery Box", ProductCategory.Toy, 1f, 2f, Color.white);
            _created.Add(p);
            Assert.AreEqual("Mystery Box", p.LocalizedName);
        }

        [Test]
        public void ButtonKey_FollowsLanguage_AndAutoSizes()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            _created.Add(root);
            var button = UIFactory.ButtonKey("B", root.transform, "settings.title", Vector2.zero, Vector2.one, 20f);
            var label = button.GetComponentInChildren<TMP_Text>();

            Assert.AreEqual("Settings", label.text);
            Assert.IsTrue(label.enableAutoSizing);
            Assert.AreEqual(20f, label.fontSizeMax, 0.001f);
            Assert.AreEqual(16f, label.fontSizeMin, 0.001f);

            Loc.SetLanguage(Language.Uk);
            Assert.AreEqual("Налаштування", label.text);
        }

        [Test]
        public void PlaytestOptions_LangFlag_Parses()
        {
            Assert.AreEqual("uk", PlaytestOptions.Parse(new[] { "-lang", "uk" }).Language);
            Assert.AreEqual("en", PlaytestOptions.Parse(new[] { "-LANG", "EN" }).Language);
            Assert.IsNull(PlaytestOptions.Parse(new string[0]).Language);
            LogAssert.Expect(LogType.Warning, new Regex("-lang"));
            Assert.IsNull(PlaytestOptions.Parse(new[] { "-lang", "fr" }).Language);
        }

        private static HashSet<string> Indices(Regex placeholder, string text)
        {
            var set = new HashSet<string>();
            foreach (Match m in placeholder.Matches(text ?? "")) set.Add(m.Groups[1].Value);
            return set;
        }
    }
}
