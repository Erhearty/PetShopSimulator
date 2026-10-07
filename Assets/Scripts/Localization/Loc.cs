using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Localization
{
    /// <summary>The languages the game ships in.</summary>
    public enum Language { En, Uk }

    /// <summary>
    /// Player-facing text lookup. Every string the player reads goes through <see cref="T"/>,
    /// <see cref="F"/> or <see cref="Plural"/> with a dotted lower-case key; the English and
    /// Ukrainian tables live in Localization/Tables. Lookups fall back current → English → key.
    /// Ids, enum names and save data never go through here.
    /// </summary>
    public static class Loc
    {
        /// <summary>Persisted code of <see cref="Language.En"/>.</summary>
        public const string EnCode = "en";

        /// <summary>Persisted code of <see cref="Language.Uk"/>.</summary>
        public const string UkCode = "uk";

        private const string OneSuffix   = ".one";
        private const string FewSuffix   = ".few";
        private const string ManySuffix  = ".many";
        private const string OtherSuffix = ".other";

        /// <summary>Every table, registered in one explicit list.</summary>
        private static readonly Action<Dictionary<string, string>, Dictionary<string, string>>[] Tables =
        {
            Strings.Common.Register,
            Strings.Menus.Register,
            Strings.Panels.Register,
            Strings.Guide.Register,
            Strings.Commerce.Register,
            Strings.World.Register,
            Strings.Pets.Register,
            Strings.Quests.Register,
        };

        private static Dictionary<string, string> _en;
        private static Dictionary<string, string> _uk;
        private static Language? _current;
        private static readonly HashSet<string> Warned = new();

        /// <summary>Raised after the language changes, so built UI can re-read its text.</summary>
        public static event Action LanguageChanged;

        /// <summary>The active language, read lazily from <see cref="GameSettings.Language"/>.</summary>
        public static Language Current
        {
            get
            {
                if (!_current.HasValue) _current = FromCode(GameSettings.Language);
                return _current.Value;
            }
        }

        /// <summary>Switches language, persists it and raises <see cref="LanguageChanged"/>.</summary>
        public static void SetLanguage(Language language)
        {
            GameSettings.Language = Code(language);
            Apply(language);
        }

        /// <summary>Switches language for this session only (the <c>-lang</c> playtest flag), without persisting.</summary>
        public static void SetLanguageWithoutSaving(Language language) => Apply(language);

        /// <summary>Forgets the cached language and missing-key warnings (tests).</summary>
        public static void ResetForTests()
        {
            _current = null;
            Warned.Clear();
        }

        private static void Apply(Language language)
        {
            bool changed = !_current.HasValue || _current.Value != language;
            _current = language;
            if (!changed || LanguageChanged == null) return;
            foreach (Action handler in LanguageChanged.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>The persisted code of <paramref name="language"/>: "en" or "uk".</summary>
        public static string Code(Language language) => language == Language.Uk ? UkCode : EnCode;

        /// <summary>The language for a persisted code; anything unknown is English.</summary>
        public static Language FromCode(string code) => TryParse(code, out var language) ? language : Language.En;

        /// <summary>True when <paramref name="code"/> names a shipped language ("en"/"uk", case-insensitive).</summary>
        public static bool TryParse(string code, out Language language)
        {
            language = Language.En;
            if (string.IsNullOrEmpty(code)) return false;
            string c = code.Trim().ToLowerInvariant();
            if (c == UkCode) { language = Language.Uk; return true; }
            return c == EnCode;
        }

        /// <summary>The text for <paramref name="key"/> in the current language, else English, else the key (warned once).</summary>
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (TryGet(Current, key, out string text)) return text;
            if (Current != Language.En && TryGet(Language.En, key, out text)) return text;
            if (Warned.Add(key)) Debug.LogWarning($"[Loc] Missing text for key '{key}'.");
            return key;
        }

        /// <summary>True when <paramref name="key"/> has text in <paramref name="language"/>.</summary>
        public static bool Has(string key, Language language) => TryGet(language, key, out _);

        /// <summary>True when <paramref name="key"/> has text in English (the reference table).</summary>
        public static bool Has(string key) => Has(key, Language.En);

        /// <summary><see cref="T"/> formatted with <paramref name="args"/> in the invariant culture.</summary>
        public static string F(string key, params object[] args) =>
            Format(T(key), args);

        /// <summary>
        /// The plural form of <paramref name="key"/> for <paramref name="count"/>: English uses
        /// <c>.one</c>/<c>.other</c>, Ukrainian <c>.one</c>/<c>.few</c>/<c>.many</c>. The count is
        /// <c>{0}</c>; <paramref name="args"/> follow as <c>{1}</c>, <c>{2}</c>…
        /// </summary>
        public static string Plural(string key, int count, params object[] args)
        {
            var all = new object[(args?.Length ?? 0) + 1];
            all[0] = count;
            if (args != null) Array.Copy(args, 0, all, 1, args.Length);

            string full = key + PluralSuffix(Current, count);
            if (TryGet(Current, full, out string text)) return Format(text, all);
            full = key + PluralSuffix(Language.En, count);
            if (TryGet(Language.En, full, out text)) return Format(text, all);
            return Format(T(full), all);
        }

        /// <summary>The plural-form suffix for <paramref name="count"/> in <paramref name="language"/>.</summary>
        public static string PluralSuffix(Language language, int count)
        {
            long n = Math.Abs((long)count);
            if (language == Language.En) return n == 1 ? OneSuffix : OtherSuffix;

            long mod10 = n % 10, mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return OneSuffix;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return FewSuffix;
            return ManySuffix;
        }

        /// <summary>Every key in the <paramref name="language"/> table.</summary>
        public static IReadOnlyCollection<string> Keys(Language language) => Table(language).Keys;

        private static string Format(string pattern, object[] args)
        {
            if (args == null || args.Length == 0) return pattern;
            try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] Bad format string '{pattern}'.");
                return pattern;
            }
        }

        /// <summary>The raw (unformatted) text of <paramref name="key"/> in <paramref name="language"/>, with no fallback.</summary>
        public static bool TryGetText(Language language, string key, out string text) => TryGet(language, key, out text);

        private static bool TryGet(Language language, string key, out string text) =>
            Table(language).TryGetValue(key, out text) && text != null;

        private static Dictionary<string, string> Table(Language language)
        {
            EnsureLoaded();
            return language == Language.Uk ? _uk : _en;
        }

        private static void EnsureLoaded()
        {
            if (_en != null) return;
            var en = new Dictionary<string, string>();
            var uk = new Dictionary<string, string>();
            foreach (var register in Tables) register(en, uk);
            _en = en;
            _uk = uk;
        }
    }
}
