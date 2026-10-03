using UnityEngine;
using PetShop.Commerce;
using PetShop.Progression.Quests;

namespace PetShop.Core
{
    /// <summary>The quest book as seen from the facade, and its day-close hook.</summary>
    public partial class GameManager
    {
        /// <summary>The quest runner. Set by GameBootstrapper; null in scenes without one.</summary>
        [HideInInspector] public QuestDirector Quests;

        /// <summary>Closes the day for the quest book and adds today's quest headlines to the summary.</summary>
        private void CollectQuestHeadlines(DaySummary summary)
        {
            if (Quests == null) return;
            Quests.OnDayClosed();
            summary.Headlines.AddRange(Quests.LatestQuestHeadlines);
        }
    }
}
