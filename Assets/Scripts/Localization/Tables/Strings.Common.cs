using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Shared words: on/off, language names, generic buttons.</summary>
    public static class Common
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            Add("common.on",  "on",  "увімк.");
            Add("common.off", "off", "вимк.");
            Add("common.close", "Close", "Закрити");
            Add("common.close_esc", "Close  ({0})", "Закрити  ({0})");
            Add("common.back", "Back", "Назад");
            Add("common.cancel", "Cancel", "Скасувати");
            Add("common.yes", "Yes", "Так");
            Add("common.no", "No", "Ні");
            Add("common.ok", "OK", "Гаразд");
            Add("common.none", "none", "немає");
            Add("common.language.en", "English", "English");
            Add("common.language.uk", "Українська", "Українська");
            Add("common.day", "Day {0}", "День {0}");
            Add("common.days", "{0} days", "{0} дн.");
            Add("common.days.one", "{0} day", "{0} день");
            Add("common.days.other", "{0} days", "{0} днів");
            uk["common.days.few"]  = "{0} дні";
            uk["common.days.many"] = "{0} днів";
        }
    }
}
