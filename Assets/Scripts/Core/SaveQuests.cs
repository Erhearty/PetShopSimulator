using System.Collections.Generic;
using UnityEngine;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>
    /// Copies the quest book and the lifetime counters its quests read (sales, best day, species
    /// sold, pets bred, inspections) to and from <see cref="SaveData"/>.
    /// </summary>
    internal static class SaveQuests
    {
        /// <summary>Stands for "no inspection yet" in memory.</summary>
        private const char NoGrade = '\0';

        /// <summary>Copies the quest state and counters into <paramref name="data"/>.</summary>
        public static void Capture(SaveData data, GameManager game)
        {
            var shop = game.Shop;
            data.LifetimeRevenue     = shop.LifetimeRevenue;
            data.BestDayProfit       = shop.BestDayProfit;
            data.SpeciesSold         = SpeciesNames(shop.SpeciesSold);
            data.PetsBred            = BreedingSystem.PetsBred;
            data.InspectionsPassed   = game.Progression != null ? game.Progression.InspectionsPassed : 0;
            data.BestInspectionGrade = game.Progression != null ? GradeText(game.Progression.BestInspectionGrade) : "";
            data.Quests              = game.Quests != null ? game.Quests.Book.ToProgress() : null;
        }

        /// <summary>Restores the counters and the quest book from <paramref name="data"/>; nothing is paid.</summary>
        public static void Apply(SaveData data, GameManager game)
        {
            game.Shop.RestoreStats(data.LifetimeRevenue, data.BestDayProfit, data.SpeciesSold);
            BreedingSystem.RestorePetsBred(data.PetsBred);
            if (game.Progression != null)
                game.Progression.RestoreInspections(data.InspectionsPassed, ParseGrade(data.BestInspectionGrade));
            if (game.Quests == null) return;
            game.Quests.Book.Restore(data.Quests);
            Debug.Log($"[Quests] Restored {game.Quests.Book.Completed.Count} completed quests, " +
                      $"chapter {game.Quests.Book.CurrentChapter}.");
        }

        /// <summary>Zeroes the counters a new game does not get fresh, and optionally skips the tutorial.</summary>
        public static void ResetForNewGame(GameManager game, bool skipTutorial)
        {
            BreedingSystem.RestorePetsBred(0);
            if (game.Quests == null) return;
            game.Quests.Book.Restore(null);
            if (skipTutorial) game.Quests.Book.SkipTutorial();
        }

        private static List<string> SpeciesNames(IEnumerable<Pet.Species> species)
        {
            var names = new List<string>();
            foreach (var s in species) names.Add(s.ToString());
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        private static string GradeText(char grade) => grade == NoGrade ? "" : grade.ToString();

        private static char ParseGrade(string text) => string.IsNullOrEmpty(text) ? NoGrade : text[0];
    }
}
