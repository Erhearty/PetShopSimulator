using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace PetShop.Tests
{
    /// <summary>
    /// Guard against player-facing text written as hardcoded string literals instead of going
    /// through localisation (Loc.T / UIFactory.LabelKey / ButtonKey / Localize).
    ///
    /// The scan is per line and regex-driven, so it only sees what is on the line of the call:
    /// a text argument (or assigned literal) that sits on a continuation line is NOT caught.
    /// </summary>
    public class NoHardcodedPlayerStringsTests
    {
        // `Notify(` followed by its first argument.
        private static readonly Regex NotifyCall = new Regex(@"\bNotify\s*\(\s*", RegexOptions.Compiled);

        // `.text = ` (assignment, not `==`) followed by the assigned expression.
        private static readonly Regex TextAssign = new Regex(@"\.text\s*=(?!=)\s*", RegexOptions.Compiled);

        // UIFactory.Label(name, parent, text, ...) / UIFactory.Button(name, parent, text, ...).
        // LabelKey/ButtonKey do not match (no '(' right after Label/Button).
        private static readonly Regex FactoryCall = new Regex(@"\bUIFactory\.(?:Label|Button)\(", RegexOptions.Compiled);

        [Test]
        public void Scripts_ContainNoHardcodedPlayerFacingStrings()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string root = dataPath + "/Scripts";
            Assert.IsTrue(Directory.Exists(root), $"Scripts folder not found at {root}");

            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            System.Array.Sort(files, System.StringComparer.Ordinal);

            var hits = new List<string>();
            foreach (var file in files)
            {
                string normalized = file.Replace('\\', '/');
                string rel = "Assets" + normalized.Substring(dataPath.Length);
                if (rel.Contains("/Dev/") || rel.Contains("/Localization/Tables/")) continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsHardcodedPlayerString(lines[i]))
                        hits.Add($"{rel}:{i + 1}: {lines[i].Trim()}");
                }
            }

            Assert.That(hits, Is.Empty,
                "Hardcoded player-facing strings found (use Loc.T / LabelKey / ButtonKey / Localize):\n"
                + string.Join("\n", hits));
        }

        [TestCase("_game.Notify(\"Hello\");", true)]
        [TestCase("Notify(Loc.T(\"x\"))", false)]
        [TestCase("label.text = \"\";", false)]
        [TestCase("label.text = $\"{a} {b}\";", false)]
        [TestCase("label.text = $\"Day {n}\";", true)]
        [TestCase("UIFactory.Label(\"Name\", parent, \"Hello\", a, b)", true)]
        [TestCase("UIFactory.Label(\"Name\", parent, \"\", a, b)", false)]
        [TestCase("UIFactory.LabelKey(\"Name\", parent, \"hud.x\", a, b)", false)]
        [TestCase("Debug.Log(\"Hello\")", false)]
        [TestCase("UIFactory.Button(\"Btn\", parent, \"OK\", a, b)", true)]
        [TestCase("UIFactory.Label($\"Row{i}\", parent, $\"{n} \u20ac\", a, b)", false)]
        [TestCase("// label.text = \"Hello\";", false)]
        public void Detector_ClassifiesSampleLines(string line, bool expected)
        {
            Assert.AreEqual(expected, IsHardcodedPlayerString(line), line);
        }

        /// <summary>
        /// True if <paramref name="line"/> passes a string literal containing a letter (outside
        /// interpolation holes) to Notify, assigns one to `.text`, or passes one as the text
        /// (3rd) argument of UIFactory.Label / UIFactory.Button.
        /// </summary>
        internal static bool IsHardcodedPlayerString(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (line.Contains("Debug.Log")) return false; // also LogWarning / LogError
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) return false;

            foreach (Match m in NotifyCall.Matches(line))
                if (LiteralHasLetter(line, m.Index + m.Length)) return true;

            foreach (Match m in TextAssign.Matches(line))
                if (LiteralHasLetter(line, m.Index + m.Length)) return true;

            foreach (Match m in FactoryCall.Matches(line))
            {
                int arg = ThirdArgumentStart(line, m.Index + m.Length);
                if (arg >= 0 && LiteralHasLetter(line, arg)) return true;
            }
            return false;
        }

        /// <summary>
        /// If a string literal (plain, $-interpolated or @-verbatim) starts at <paramref name="pos"/>,
        /// returns whether its visible text - escapes dropped, interpolation holes stripped,
        /// `{{`/`}}` kept as braces - contains a letter. Not a literal: false.
        /// </summary>
        private static bool LiteralHasLetter(string s, int pos)
        {
            bool interpolated = false, verbatim = false;
            int i = pos;
            while (i < s.Length && (s[i] == '$' || s[i] == '@'))
            {
                if (s[i] == '$') interpolated = true; else verbatim = true;
                i++;
            }
            if (i >= s.Length || s[i] != '"') return false;
            i++;

            int depth = 0;
            while (i < s.Length)
            {
                char c = s[i];
                char next = i + 1 < s.Length ? s[i + 1] : '\0';
                if (depth == 0)
                {
                    if (c == '"')
                    {
                        if (verbatim && next == '"') { i += 2; continue; }
                        return false; // end of literal
                    }
                    if (c == '\\' && !verbatim) { i = SkipEscape(s, i); continue; }
                    if (interpolated && c == '{')
                    {
                        if (next == '{') { i += 2; continue; }
                        depth = 1; i++; continue;
                    }
                    if (interpolated && c == '}' && next == '}') { i += 2; continue; }
                    if (char.IsLetter(c)) return true;
                    i++;
                }
                else
                {
                    // Inside a hole: skip nested literals, track brace nesting.
                    if (c == '"') { i = SkipString(s, i); continue; }
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                    i++;
                }
            }
            return false;
        }

        /// <summary>Index just past a backslash escape starting at <paramref name="i"/>.</summary>
        private static int SkipEscape(string s, int i)
        {
            char kind = i + 1 < s.Length ? s[i + 1] : '\0';
            int j = i + 2;
            if (kind == 'u') return j + 4;
            if (kind == 'U') return j + 8;
            if (kind == 'x')
            {
                int n = 0;
                while (j < s.Length && n < 4 && System.Uri.IsHexDigit(s[j])) { j++; n++; }
            }
            return j;
        }

        /// <summary>Index just past the string literal whose opening quote is at <paramref name="q"/>.</summary>
        private static int SkipString(string s, int q)
        {
            bool verbatim = (q > 0 && s[q - 1] == '@') || (q > 1 && s[q - 1] == '$' && s[q - 2] == '@');
            int i = q + 1;
            while (i < s.Length)
            {
                char c = s[i];
                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '"') { i += 2; continue; }
                        return i + 1;
                    }
                }
                else
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == '"') return i + 1;
                }
                i++;
            }
            return s.Length;
        }

        /// <summary>
        /// Given the index just after a call's '(', returns where the 3rd argument starts (first
        /// non-space char), or -1 if the call closes or the line ends first.
        /// </summary>
        private static int ThirdArgumentStart(string s, int pos)
        {
            int depth = 0, commas = 0;
            int i = pos;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i = SkipString(s, i); continue; }
                if (c == '\'')
                {
                    i++;
                    while (i < s.Length && s[i] != '\'') i += s[i] == '\\' ? 2 : 1;
                    i++;
                    continue;
                }
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) return -1;
                    depth--;
                }
                else if (c == ',' && depth == 0 && ++commas == 2)
                {
                    i++;
                    while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                    return i < s.Length ? i : -1;
                }
                i++;
            }
            return -1;
        }
    }
}
