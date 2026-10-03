using System.Collections.Generic;
using PetShop.Progression.Quests;

namespace PetShop.Core
{
    /// <summary>Quest-book state and the lifetime counters its quests read (save version 5+).</summary>
    public partial class SaveData
    {
        /// <summary>Completed quests, chapter and flags; null restores a fresh book.</summary>
        public QuestProgress Quests;
        /// <summary>All sales income since the shop opened.</summary>
        public float LifetimeRevenue;
        /// <summary>The best single day's profit so far.</summary>
        public float BestDayProfit;
        /// <summary>Names of every Pet.Species sold at least once.</summary>
        public List<string> SpeciesSold = new();
        /// <summary>Animals born in the shop's pens.</summary>
        public int PetsBred;
        /// <summary>Inspections graded A or B.</summary>
        public int InspectionsPassed;
        /// <summary>The best inspection grade so far as one letter; empty before the first.</summary>
        public string BestInspectionGrade = "";
    }
}
