namespace PetShop.Core
{
    /// <summary>
    /// General player preferences. Persisted through an <see cref="IBindingStore"/> (PlayerPrefs
    /// in the shipping game) so tests can swap in an in-memory store.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Persisted key for <see cref="AutosaveEachMorning"/>.</summary>
        public const string AutosaveMorningKey = "settings.autosaveMorning";

        /// <summary>Persisted key for <see cref="ShowQuestTracker"/>.</summary>
        public const string ShowQuestTrackerKey = "settings.showQuestTracker";

        private const bool   ShowQuestTrackerDefault = true;
        private const bool   AutosaveMorningDefault = true;
        private const string TrueValue  = "1";
        private const string FalseValue = "0";

        private static IBindingStore _store = new PlayerPrefsBindingStore();

        /// <summary>Persistence backend; null restores the PlayerPrefs store.</summary>
        public static IBindingStore Store
        {
            get => _store;
            set => _store = value ?? new PlayerPrefsBindingStore();
        }

        /// <summary>When true the game saves quietly at the start of every new day. Defaults to true.</summary>
        public static bool AutosaveEachMorning
        {
            get => ReadBool(AutosaveMorningKey, AutosaveMorningDefault);
            set => _store.Write(AutosaveMorningKey, value ? TrueValue : FalseValue);
        }

        /// <summary>When true the HUD shows the quest tracker. Defaults to true.</summary>
        public static bool ShowQuestTracker
        {
            get => ReadBool(ShowQuestTrackerKey, ShowQuestTrackerDefault);
            set => _store.Write(ShowQuestTrackerKey, value ? TrueValue : FalseValue);
        }

        /// <summary>The stored flag under <paramref name="key"/>, or <paramref name="fallback"/> when unset.</summary>
        private static bool ReadBool(string key, bool fallback)
        {
            string raw = _store.Read(key);
            return raw == null ? fallback : raw == TrueValue;
        }
    }
}
