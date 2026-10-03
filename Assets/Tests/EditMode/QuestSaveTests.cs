using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Progression.Quests;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for the quest book in the save: the v4 → v5 step completes the tutorial for
    /// a shop that already has a counter and a shelf, and quest progress plus the lifetime
    /// counters round-trip through the SaveData JSON.
    /// </summary>
    public class QuestSaveTests
    {
        private const float Tolerance = 1e-4f;

        /// <summary>A version 4 save with a counter and a small shelf placed.</summary>
        private const string V4FurnishedJson =
            "{\"Version\":4,\"Balance\":10.0,\"Day\":6,\"Staff\":1,\"PriceMultiplier\":1.0," +
            "\"PlacedObjects\":[{\"catalogId\":\"counter\",\"cellX\":1,\"cellY\":2}," +
            "{\"catalogId\":\"shelf_small\",\"cellX\":3,\"cellY\":2}]}";

        /// <summary>A version 4 save with a counter but no shelf.</summary>
        private const string V4CounterOnlyJson =
            "{\"Version\":4,\"Balance\":10.0,\"Day\":2,\"Staff\":1,\"PriceMultiplier\":1.0," +
            "\"PlacedObjects\":[{\"catalogId\":\"counter\",\"cellX\":1,\"cellY\":2}]}";

        /// <summary>A version 5 save written without any quest fields.</summary>
        private const string V5NoQuestsJson =
            "{\"Version\":5,\"Balance\":10.0,\"Day\":2,\"Staff\":1,\"PriceMultiplier\":1.0}";

        private const float LifetimeRevenue   = 1234.5f;
        private const float BestDayProfit     = 321.25f;
        private const int   PetsBred          = 4;
        private const int   InspectionsPassed = 2;
        private const string BestGrade        = "A";
        private const string SpeciesA         = "Cat";
        private const string SpeciesB         = "Dog";

        [Test]
        public void Migrate_V4_WithCounterAndShelf_CompletesEveryTutorialQuest()
        {
            var data = SaveMigrator.Migrate(V4FurnishedJson);

            Assert.IsNotNull(data);
            Assert.AreEqual(SaveMigrator.CurrentVersion, data.Version);
            Assert.IsNotNull(data.Quests);
            foreach (var quest in QuestCatalog.InChapter(QuestChapter.Tutorial))
                CollectionAssert.Contains(data.Quests.CompletedIds, quest.Id);

            var book = new QuestBook();
            book.Restore(data.Quests);
            Assert.IsTrue(book.IsChapterComplete(QuestChapter.Tutorial));
            Assert.AreNotEqual(QuestChapter.Tutorial, book.CurrentChapter);
        }

        [Test]
        public void Migrate_V4_WithoutShelf_LeavesTutorialToDo()
        {
            var data = SaveMigrator.Migrate(V4CounterOnlyJson);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.Quests);
            Assert.AreEqual(0, data.Quests.CompletedIds.Count);

            var book = new QuestBook();
            book.Restore(data.Quests);
            Assert.AreEqual(QuestChapter.Tutorial, book.CurrentChapter);
        }

        [Test]
        public void Migrate_V5_WithoutQuests_RestoresAFreshBook()
        {
            var data = SaveMigrator.Migrate(V5NoQuestsJson);
            Assert.IsNotNull(data);

            var book = new QuestBook();
            book.SkipTutorial();
            book.Restore(data.Quests);

            Assert.AreEqual(0, book.Completed.Count);
            Assert.AreEqual(0, book.Flags.Count);
            Assert.AreEqual(QuestChapter.Tutorial, book.CurrentChapter);
        }

        [Test]
        public void SaveData_QuestProgressAndCounters_RoundTripThroughJson()
        {
            var book = new QuestBook();
            book.SkipTutorial();
            book.RaiseFlag(QuestFlags.StockOrdered);
            var data = new SaveData
            {
                Quests              = book.ToProgress(),
                LifetimeRevenue     = LifetimeRevenue,
                BestDayProfit       = BestDayProfit,
                SpeciesSold         = new List<string> { SpeciesA, SpeciesB },
                PetsBred            = PetsBred,
                InspectionsPassed   = InspectionsPassed,
                BestInspectionGrade = BestGrade,
            };

            var loaded = SaveMigrator.Migrate(JsonUtility.ToJson(data));

            Assert.IsNotNull(loaded);
            CollectionAssert.AreEqual(data.Quests.CompletedIds, loaded.Quests.CompletedIds);
            CollectionAssert.AreEqual(data.Quests.Flags, loaded.Quests.Flags);
            Assert.AreEqual(data.Quests.Chapter, loaded.Quests.Chapter);
            Assert.AreEqual(LifetimeRevenue, loaded.LifetimeRevenue, Tolerance);
            Assert.AreEqual(BestDayProfit, loaded.BestDayProfit, Tolerance);
            CollectionAssert.AreEqual(new[] { SpeciesA, SpeciesB }, loaded.SpeciesSold);
            Assert.AreEqual(PetsBred, loaded.PetsBred);
            Assert.AreEqual(InspectionsPassed, loaded.InspectionsPassed);
            Assert.AreEqual(BestGrade, loaded.BestInspectionGrade);

            var restored = new QuestBook();
            restored.Restore(loaded.Quests);
            CollectionAssert.AreEquivalent(book.Completed, restored.Completed);
            Assert.IsTrue(restored.HasFlag(QuestFlags.StockOrdered));
        }
    }
}
