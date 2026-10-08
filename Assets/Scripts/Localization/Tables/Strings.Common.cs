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
            Add("console.placeholder", "Type a command…", "Введіть команду…");
            Add("console.hint", "Commands: greedisgood <amount>", "Команди: greedisgood <сума>");
            Add("console.added", "Added €{0}.", "Додано €{0}.");
            Add("console.invalid", "Usage: greedisgood <amount> (a whole number above 0)", "Використання: greedisgood <сума> (ціле число більше 0)");
            Add("console.unknown", "Unknown command: {0}", "Невідома команда: {0}");
            Add("day.born.one", "A pet was born overnight!", "Минулої ночі народилася тваринка!");
            Add("day.born.other", "{0} pets were born overnight!", "Минулої ночі народилося тваринок: {0}!");
            uk["day.born.few"]  = "Минулої ночі народилося тваринок: {0}!";
            uk["day.born.many"] = "Минулої ночі народилося тваринок: {0}!";
            Add("day.game_over",
                "You could not cover day {0}'s bills — €{1:N0} rent and €{2:N0} in wages.\nThe shop closed with €{3:N2}.",
                "Ви не змогли покрити рахунки за день {0} — €{1:N0} оренди та €{2:N0} зарплат.\nМагазин закрився з балансом €{3:N2}.");
            Add("common.day", "Day {0}", "День {0}");
            Add("common.days", "{0} days", "{0} дн.");
            Add("common.days.one", "{0} day", "{0} день");
            Add("common.days.other", "{0} days", "{0} днів");
            uk["common.days.few"]  = "{0} дні";
            uk["common.days.many"] = "{0} днів";
        }
    }
}
