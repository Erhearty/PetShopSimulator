using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using PetShop.Localization;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// Guards the localisation rules: every Loc key named in code exists, non-empty, in English
    /// and Ukrainian; UI scripts never assign raw word literals; UIFactory text wraps.
    /// </summary>
    public class LocStringPresenceTests
    {
        // A Loc call (or key-taking UIFactory helper) followed by the rest of its line.
        private static readonly Regex KeyCall = new(
            @"(?:Loc\.(?:T|F|Plural|Has)|LabelKey|ButtonKey|Localize|LocalizedText\.Bind)\(([^;]*)", RegexOptions.Compiled);
        private static readonly Regex KeyLiteral = new(
            "\"([a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+)\"", RegexOptions.Compiled);
        private static readonly Regex RawTextAssign = new(
            "\\.text\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex RawFactoryText = new(
            "(?:Label|Button)\\(\\s*[^,]+,[^,]+,\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex RawHeaderText = new(
            "Header\\(\\s*[^,]+,\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex HasWord = new("[A-Za-z]{2,}", RegexOptions.Compiled);

        private LocTestScope _loc;

        [SetUp]
        public void SetUp() => _loc = LocTestScope.Begin();

        [TearDown]
        public void TearDown() => _loc.End();

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts");

        private static IEnumerable<string> ScriptFiles(string sub = "") =>
            Directory.GetFiles(Path.Combine(ScriptsRoot, sub), "*.cs", SearchOption.AllDirectories);

        private static bool Present(string key, Language language)
        {
            if (NonEmpty(key, language)) return true;
            if (IsPrefixOfNonEmptyKeys(key, language)) return true;   // dynamic keys, e.g. LocNames.Of("catalog.tab", x)
            if (language == Language.En) return NonEmpty(key + ".one", language) && NonEmpty(key + ".other", language);
            return NonEmpty(key + ".one", language) && NonEmpty(key + ".few", language) && NonEmpty(key + ".many", language);
        }

        private static bool IsPrefixOfNonEmptyKeys(string prefix, Language language)
        {
            bool any = false;
            foreach (string k in Loc.Keys(language))
            {
                if (!k.StartsWith(prefix + ".")) continue;
                if (!NonEmpty(k, language)) return false;
                any = true;
            }
            return any;
        }

        private static bool NonEmpty(string key, Language language) =>
            Loc.TryGetText(language, key, out string text) && !string.IsNullOrWhiteSpace(text);

        [Test]
        public void EveryReferencedKey_ExistsNonEmptyInEnglishAndUkrainian()
        {
            var missing = new List<string>();
            foreach (string file in ScriptFiles())
            {
                if (file.Contains(Path.DirectorySeparatorChar + "Localization" + Path.DirectorySeparatorChar)) continue;
                foreach (string line in File.ReadAllLines(file))
                    foreach (Match call in KeyCall.Matches(line))
                        foreach (Match literal in KeyLiteral.Matches(call.Groups[1].Value))
                        {
                            string key = literal.Groups[1].Value;
                            if (!Present(key, Language.En)) missing.Add($"{key} (en) in {Path.GetFileName(file)}");
                            if (!Present(key, Language.Uk)) missing.Add($"{key} (uk) in {Path.GetFileName(file)}");
                        }
            }
            Assert.IsEmpty(missing, "Missing or empty Loc keys:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryTableKey_HasNonEmptyEnglishAndUkrainianText()
        {
            var bad = new List<string>();
            foreach (string key in Loc.Keys(Language.En))
            {
                if (!NonEmpty(key, Language.En)) bad.Add(key + " (en)");
                if (NonEmpty(key, Language.Uk)) continue;
                // Ukrainian has no .other form: it uses .few/.many instead.
                if (key.EndsWith(".other") && NonEmpty(key.Substring(0, key.Length - 6) + ".many", Language.Uk)) continue;
                bad.Add(key + " (uk)");
            }
            Assert.IsEmpty(bad, "Empty or untranslated keys:\n" + string.Join("\n", bad));
        }

        /// <summary>Building is gridless and the fox is gone: no English text may say otherwise.</summary>
        [Test]
        public void EnglishText_HasNoStaleGridOrFoxWording()
        {
            var stale = new Regex(@"\bcells?\b|\bsnap|\bgrid\b|\bfox\b", RegexOptions.IgnoreCase);
            var bad = new List<string>();
            foreach (string key in Loc.Keys(Language.En))
                if (Loc.TryGetText(Language.En, key, out string text) &&
                    stale.IsMatch(text.Replace("no grid", "")))
                    bad.Add(key);
            Assert.IsEmpty(bad, "Stale wording in:\n" + string.Join("\n", bad));
        }

        [Test]
        public void UiScripts_AssignNoRawWordLiterals()
        {
            var offenders = new List<string>();
            foreach (string file in ScriptFiles("UI"))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    foreach (var regex in new[] { RawTextAssign, RawFactoryText, RawHeaderText })
                        foreach (Match m in regex.Matches(lines[i]))
                            if (HasWord.IsMatch(m.Groups[1].Value))
                                offenders.Add($"{Path.GetFileName(file)}:{i + 1} \"{m.Groups[1].Value}\"");
            }
            Assert.IsEmpty(offenders, "Raw UI literals (use Loc keys):\n" + string.Join("\n", offenders));
        }

        [Test]
        public void UIFactoryLabel_WrapsAndHandlesOverflow()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                TMP_Text label = UIFactory.Label("L", root.transform, "text", Vector2.zero, Vector2.one);
                Assert.AreEqual(TextWrappingModes.Normal, label.textWrappingMode);
                Assert.AreNotEqual(TextOverflowModes.Overflow, label.overflowMode);

                var button = UIFactory.Button("B", root.transform, "text", Vector2.zero, Vector2.one);
                var buttonLabel = button.GetComponentInChildren<TMP_Text>();
                Assert.AreEqual(TextWrappingModes.Normal, buttonLabel.textWrappingMode);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LabelKey_ResolvesRealTextInBothLanguages()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                TMP_Text label = UIFactory.LabelKey("L", root.transform, "common.close", Vector2.zero, Vector2.one);
                Assert.AreEqual("Close", label.text);
                Loc.SetLanguageWithoutSaving(Language.Uk);
                Assert.AreEqual("Закрити", label.text);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
