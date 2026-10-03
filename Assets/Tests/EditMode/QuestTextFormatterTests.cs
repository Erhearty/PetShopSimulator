using System.Collections.Generic;
using NUnit.Framework;
using PetShop.Progression.Quests;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for <see cref="QuestTextFormatter"/>: tracker and journal wording.</summary>
    public class QuestTextFormatterTests
    {
        private const int OneShelf = 1;

        private static QuestBook EarlyBook()
        {
            var book = new QuestBook();
            book.SkipTutorial();
            return book;
        }

        [Test]
        public void Tracker_NullBook_IsEmpty()
        {
            Assert.AreEqual(string.Empty, QuestTextFormatter.TrackerText(null, null));
        }

        [Test]
        public void Tracker_FreshBook_ShowsFirstTutorialStep()
        {
            var book = new QuestBook();
            string text = QuestTextFormatter.TrackerText(book, null);

            StringAssert.Contains("Tutorial 1/", text);
            StringAssert.Contains(book.CurrentTutorialStep.Title, text);
            StringAssert.Contains(book.CurrentTutorialStep.Instruction, text);
        }

        [Test]
        public void Tracker_EarlyChapter_ListsThreeQuestsWithCounterAndMore()
        {
            var ctx = new QuestContext
            {
                PlacedByType = new Dictionary<string, int> { { QuestCatalog.ShelfType, OneShelf } },
            };
            string text = QuestTextFormatter.TrackerText(EarlyBook(), ctx);

            StringAssert.Contains($"(1/{QuestCatalog.EarlyShelfTarget})", text);
            StringAssert.Contains($"+4 more ({QuestJournalPanel.OpenKeyLabel})", text);
        }

        [Test]
        public void Tracker_EverythingDone_SaysSo()
        {
            var progress = new QuestProgress { Chapter = QuestChapter.End };
            foreach (var q in QuestCatalog.All) progress.CompletedIds.Add(q.Id);
            var book = new QuestBook();
            book.Restore(progress);

            Assert.AreEqual(QuestTextFormatter.AllDoneText, QuestTextFormatter.TrackerText(book, null));
        }

        [Test]
        public void ChapterBody_FreshBook_MarksStatesAndRewards()
        {
            var book = new QuestBook();
            string tutorial = QuestTextFormatter.ChapterBody(book, QuestChapter.Tutorial, null);
            string end = QuestTextFormatter.ChapterBody(book, QuestChapter.End, null);

            StringAssert.Contains("[active]", tutorial);
            StringAssert.Contains("[up next]", tutorial);
            StringAssert.Contains($"Reward: €{QuestCatalog.TutorialStepReward:N0}", tutorial);
            StringAssert.Contains("[locked]", end);
            StringAssert.Contains(QuestTextFormatter.LockedNotice, end);
        }

        [Test]
        public void ChapterBody_NullBook_IsUnavailable()
        {
            Assert.AreEqual(QuestTextFormatter.UnavailableText,
                            QuestTextFormatter.ChapterBody(null, QuestChapter.Early, null));
        }

        [Test]
        public void ChapterSummary_ShowsStateAndCount()
        {
            var book = EarlyBook();
            int tutorialCount = QuestCatalog.InChapter(QuestChapter.Tutorial).Count;

            StringAssert.Contains($"done  {tutorialCount}/{tutorialCount}",
                                  QuestTextFormatter.ChapterSummary(book, QuestChapter.Tutorial));
            StringAssert.Contains("active  0/", QuestTextFormatter.ChapterSummary(book, QuestChapter.Early));
            StringAssert.Contains("locked", QuestTextFormatter.ChapterSummary(book, QuestChapter.End));
        }

        [Test]
        public void CounterText_ClampsToTarget()
        {
            var ctx = new QuestContext
            {
                PlacedByType = new Dictionary<string, int> { { QuestCatalog.ShelfType, QuestCatalog.EarlyShelfTarget + 2 } },
            };
            var quest = QuestCatalog.Get("early_three_shelves");

            Assert.AreEqual($"{QuestCatalog.EarlyShelfTarget}/{QuestCatalog.EarlyShelfTarget}",
                            QuestTextFormatter.CounterText(quest, ctx));
            Assert.IsNull(QuestTextFormatter.CounterText(QuestCatalog.Get("early_first_pen"), ctx));
        }
    }
}
