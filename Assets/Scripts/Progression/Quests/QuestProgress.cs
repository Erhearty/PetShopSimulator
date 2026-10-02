using System;
using System.Collections.Generic;

namespace PetShop.Progression.Quests
{
    /// <summary>
    /// Serializable quest-book state for the save file: completed quest ids, the current
    /// chapter and the raised one-shot flags. Public fields so JsonUtility can write it.
    /// </summary>
    [Serializable]
    public class QuestProgress
    {
        /// <summary>Ids of every completed quest.</summary>
        public List<string> CompletedIds = new();

        /// <summary>The chapter being worked on.</summary>
        public QuestChapter Chapter = QuestChapter.Tutorial;

        /// <summary>Raised one-shot flags (see <see cref="QuestFlags"/>).</summary>
        public List<string> Flags = new();
    }

    /// <summary>Names of the one-shot flags a <see cref="QuestBook"/> remembers between sessions.</summary>
    public static class QuestFlags
    {
        /// <summary>The furniture catalogue has been opened.</summary>
        public const string CatalogueOpened = "catalogue_opened";

        /// <summary>A delivery crate has been collected.</summary>
        public const string CrateCollected = "crate_collected";

        /// <summary>A stock order has been placed.</summary>
        public const string StockOrdered = "stock_ordered";

        /// <summary>A day has been closed.</summary>
        public const string DayClosed = "day_closed";
    }
}
