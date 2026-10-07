using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Title, pause, settings, HUD and hotkey help text.</summary>
    public static class Menus
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Settings
            Add("settings.title", "Settings", "Налаштування");
            Add("settings.capture_prompt", "Press a key… ({0} to cancel)", "Натисніть клавішу… ({0} — скасувати)");
            Add("settings.reset", "Reset to defaults", "Скинути до типових");
            Add("settings.controls", "Controls", "Керування");
            Add("settings.general", "General", "Загальні");
            Add("settings.rebind", "Rebind", "Змінити");
            Add("settings.reduce_motion", "Reduce motion: {0}", "Менше анімації: {0}");
            Add("settings.reduce_motion.on", "Reduce motion on.", "Менше анімації: увімкнено.");
            Add("settings.reduce_motion.off", "Reduce motion off.", "Менше анімації: вимкнено.");
            Add("settings.tracker", "Show quest tracker: {0}", "Трекер завдань: {0}");
            Add("settings.tracker.on", "Quest tracker on.", "Трекер завдань увімкнено.");
            Add("settings.tracker.off", "Quest tracker off.", "Трекер завдань вимкнено.");
            Add("settings.autosave", "Autosave each morning: {0}", "Автозбереження щоранку: {0}");
            Add("settings.autosave.on", "Autosave each morning on.", "Автозбереження щоранку увімкнено.");
            Add("settings.autosave.off", "Autosave each morning off.", "Автозбереження щоранку вимкнено.");
            Add("settings.language", "Language: {0}", "Мова: {0}");
            Add("settings.language.changed", "Language: {0}.", "Мова: {0}.");
            Add("settings.rebind_cancelled", "Rebind cancelled.", "Зміну клавіші скасовано.");
            Add("settings.bound", "{0} is now {1}.", "{0} тепер на {1}.");
            Add("settings.bound_moved", "{0} moved to {1}.", "{0} перенесено на {1}.");
            Add("settings.controls_reset", "Controls reset to defaults.", "Керування скинуто до типового.");
        }
    }
}
