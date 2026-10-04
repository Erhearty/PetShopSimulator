using System.Collections.Generic;
using NUnit.Framework;
using PetShop.Pets;
using PetShop.Progression;
using PetShop.Progression.Quests;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>Deterministic tests for <see cref="QuestBook"/> over the real <see cref="QuestCatalog"/>.</summary>
    public class QuestBookTests
    {
        private const int    ManyShelves   = 5;
        private const int    LotsOfMoney   = 100000;
        private const int    ManyStaff     = 4;
        private const string OpenCatalogue = "tut_open_catalogue";
        private const string OrderCounter  = "tut_order_counter";
        private const string CollectCrate  = "tut_collect_crate";
        private const string ThreeShelves  = "early_three_shelves";
        private const string Breed         = "mid_breed";

        private static Dictionary<string, int> Counts(params (string key, int n)[] entries)
        {
            var d = new Dictionary<string, int>();
            foreach (var (key, n) in entries) d[key] = n;
            return d;
        }

        /// <summary>A snapshot that satisfies every quest in the catalogue.</summary>
        private static QuestContext Everything() => new QuestContext
        {
            Balance = LotsOfMoney, LifetimeRevenue = LotsOfMoney, BestDayProfit = LotsOfMoney,
            PlacedById   = Counts((BuildCatalog.Counter, 1), (BuildCatalog.ShelfSmall, ManyShelves), (BuildCatalog.PetPen, 1)),
            PlacedByType = Counts((QuestCatalog.ShelfType, ManyShelves), (BuildCatalog.PenType, 1)),
            HighestPlacedLotStage = ProgressionRules.FullYardLotStage,
            StaffCount = ManyStaff, Tier = ProgressionRules.MaxTier,
            InspectionsPassed = 1, BestInspectionGrade = 'A', PetsBred = 1,
            SpeciesSold = new HashSet<Pet.Species> { Pet.Species.Dog, Pet.Species.Cat, Pet.Species.Horse, Pet.Species.Tiger },
            AutoReorderOn = true,
            CatalogueOpened = true, CrateCollected = true, StockOrdered = true, DayClosed = true,
        };

        private static List<string> Ids(List<QuestDefinition> quests) => quests.ConvertAll(q => q.Id);

        [Test]
        public void NewBook_StartsOnFirstTutorialStep()
        {
            var book = new QuestBook();
            Assert.AreEqual(QuestChapter.Tutorial, book.CurrentChapter);
            Assert.AreEqual(OpenCatalogue, book.CurrentTutorialStep.Id);
            Assert.AreEqual(1, book.ActiveQuests().Count);
        }

        [Test]
        public void Evaluate_EmptyShop_CompletesNothing()
        {
            var book = new QuestBook();
            Assert.IsEmpty(book.Evaluate(new QuestContext()));
            Assert.IsEmpty(book.Completed);
        }

        [Test]
        public void Evaluate_LaterStepSatisfied_WaitsForEarlierStep()
        {
            var book = new QuestBook();
            var ctx  = new QuestContext { OnOrderById = Counts((BuildCatalog.Counter, 1)), CrateCollected = true };

            Assert.IsEmpty(book.Evaluate(ctx));
            Assert.IsFalse(book.IsComplete(OrderCounter));
            Assert.AreEqual(OpenCatalogue, book.CurrentTutorialStep.Id);
        }

        [Test]
        public void Evaluate_TutorialAdvancesOneStepInOrder()
        {
            var book = new QuestBook();
            var done = book.Evaluate(new QuestContext { CatalogueOpened = true });

            CollectionAssert.AreEqual(new[] { OpenCatalogue }, Ids(done));
            Assert.AreEqual(OrderCounter, book.CurrentTutorialStep.Id);
        }

        [Test]
        public void Evaluate_NextStepAlreadySatisfiedOnUnlock_CompletesInSamePass()
        {
            var book = new QuestBook();
            var ctx  = new QuestContext
            {
                CatalogueOpened = true, OnOrderById = Counts((BuildCatalog.Counter, 1)), CrateCollected = true,
            };

            var done = book.Evaluate(ctx);

            CollectionAssert.AreEqual(new[] { OpenCatalogue, OrderCounter, CollectCrate }, Ids(done));
            Assert.AreEqual("tut_place_counter", book.CurrentTutorialStep.Id);
        }

        [Test]
        public void Evaluate_LaterChapterQuestSatisfied_StaysLockedUntilCurrentChapterDone()
        {
            var book = new QuestBook();
            book.SkipTutorial();

            Assert.IsEmpty(book.Evaluate(new QuestContext { PetsBred = 1 }));
            Assert.IsFalse(book.IsComplete(Breed));
            Assert.AreEqual(QuestChapter.Early, book.CurrentChapter);
            Assert.IsFalse(book.IsUnlocked(QuestChapter.Mid));
        }

        [Test]
        public void Evaluate_EverythingSatisfied_CompletesChaptersInOrder()
        {
            var book = new QuestBook();
            var done = book.Evaluate(Everything());

            Assert.AreEqual(QuestCatalog.All.Count, done.Count);
            for (int i = 1; i < done.Count; i++)
                Assert.LessOrEqual((int)done[i - 1].Chapter, (int)done[i].Chapter, $"{done[i].Id} completed out of chapter order");
            Assert.IsTrue(book.AllComplete);
            Assert.IsEmpty(book.Evaluate(Everything()), "a quest must complete only once");
        }

        [Test]
        public void Evaluate_FinishingChapter_UnlocksOnlyTheNext()
        {
            var book = new QuestBook();
            book.SkipTutorial();
            var early = Everything();
            early.Tier = ProgressionRules.LocalFavouriteTier;
            early.PetsBred = 0;

            book.Evaluate(early);

            Assert.IsTrue(book.IsChapterComplete(QuestChapter.Early));
            Assert.AreEqual(QuestChapter.Mid, book.CurrentChapter);
            Assert.IsFalse(book.IsComplete(Breed));
            Assert.IsFalse(book.IsUnlocked(QuestChapter.End));
        }

        [Test]
        public void SkipTutorial_CompletesEveryStepAndUnlocksEarly()
        {
            var book = new QuestBook();
            book.SkipTutorial();

            Assert.IsTrue(book.IsChapterComplete(QuestChapter.Tutorial));
            Assert.IsNull(book.CurrentTutorialStep);
            Assert.AreEqual(QuestChapter.Early, book.CurrentChapter);
            Assert.AreEqual(QuestCatalog.InChapter(QuestChapter.Early).Count, book.ActiveQuests().Count);
            Assert.IsEmpty(book.Evaluate(new QuestContext()));
        }

        [Test]
        public void ProgressText_ShowsClampedCounter()
        {
            var quest = QuestCatalog.Get(ThreeShelves);
            var one   = new QuestContext { PlacedByType = Counts((QuestCatalog.ShelfType, 1)) };
            var many  = new QuestContext { PlacedByType = Counts((QuestCatalog.ShelfType, ManyShelves)) };

            Assert.AreEqual("Own 3 shelves (1/3)", quest.ProgressText(one));
            Assert.AreEqual("Own 3 shelves (3/3)", quest.ProgressText(many));
            Assert.AreEqual("Own 3 shelves (0/3)", quest.ProgressText(new QuestContext()));
        }

        [Test]
        public void ProgressText_QuestWithoutCounter_IsTitle()
        {
            var quest = QuestCatalog.Get(OpenCatalogue);
            Assert.IsNull(quest.Progress);
            Assert.AreEqual(quest.Title, quest.ProgressText(new QuestContext()));
        }

        [Test]
        public void Progress_RoundTripsThroughRestore()
        {
            var book = new QuestBook();
            book.Evaluate(new QuestContext { CatalogueOpened = true });
            book.RaiseFlag(QuestFlags.CatalogueOpened);

            var restored = new QuestBook();
            restored.Restore(book.ToProgress());

            Assert.IsTrue(restored.IsComplete(OpenCatalogue));
            Assert.IsTrue(restored.HasFlag(QuestFlags.CatalogueOpened));
            Assert.AreEqual(OrderCounter, restored.CurrentTutorialStep.Id);
        }

        [Test]
        public void Restore_SkippedTutorial_ResumesInEarly()
        {
            var book = new QuestBook();
            book.SkipTutorial();

            var restored = new QuestBook();
            restored.Restore(book.ToProgress());

            Assert.AreEqual(QuestChapter.Early, restored.CurrentChapter);
            Assert.IsTrue(restored.IsChapterComplete(QuestChapter.Tutorial));
        }

        /// <summary>A saved chapter ahead of the completed set is not trusted: the chapter is re-derived.</summary>
        [Test]
        public void Restore_SavedChapterAheadOfCompletedSet_ResumesAtFirstIncompleteChapter()
        {
            var progress = new QuestProgress { Chapter = QuestChapter.End };

            var restored = new QuestBook();
            restored.Restore(progress);

            Assert.AreEqual(QuestChapter.Tutorial, restored.CurrentChapter);
            Assert.IsFalse(restored.AllComplete);
        }
    }
}
