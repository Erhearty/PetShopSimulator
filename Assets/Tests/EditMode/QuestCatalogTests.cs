using System;
using System.Collections.Generic;
using NUnit.Framework;
using PetShop.Progression.Quests;

namespace PetShop.Tests
{
    /// <summary>Data checks over every quest in <see cref="QuestCatalog"/>.</summary>
    public class QuestCatalogTests
    {
        [Test]
        public void EveryQuest_HasUniqueNonEmptyId()
        {
            var seen = new HashSet<string>();
            foreach (var q in QuestCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.Id));
                Assert.IsTrue(seen.Add(q.Id), $"duplicate quest id {q.Id}");
            }
        }

        [Test]
        public void EveryQuest_HasTitleInstructionRewardAndCondition()
        {
            foreach (var q in QuestCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.Title), q.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(q.InstructionTemplate), q.Id);
                Assert.Greater(q.Reward, 0f, q.Id);
                Assert.IsNotNull(q.Condition, q.Id);
            }
        }

        [Test]
        public void EveryQuest_HasValidChapter_AndEveryChapterHasQuests()
        {
            foreach (var q in QuestCatalog.All)
                Assert.IsTrue(Enum.IsDefined(typeof(QuestChapter), q.Chapter), q.Id);
            foreach (QuestChapter chapter in Enum.GetValues(typeof(QuestChapter)))
                Assert.IsNotEmpty(QuestCatalog.InChapter(chapter), chapter.ToString());
        }

        [Test]
        public void Catalogue_IsInChapterOrder()
        {
            for (int i = 1; i < QuestCatalog.All.Count; i++)
                Assert.LessOrEqual((int)QuestCatalog.All[i - 1].Chapter, (int)QuestCatalog.All[i].Chapter, QuestCatalog.All[i].Id);
        }

        [Test]
        public void EveryInstruction_ResolvesAllKeyTokens()
        {
            foreach (var q in QuestCatalog.All)
            {
                string text = q.InstructionText(QuestDefinition.DefaultKeyLabel);
                Assert.IsFalse(text.Contains("{") || text.Contains("}"), $"{q.Id}: {text}");
            }
        }

        [Test]
        public void TutorialInstructions_NameTheRealKeys()
        {
            StringAssert.Contains("Press Tab", QuestCatalog.Get("tut_open_catalogue").InstructionText(QuestDefinition.DefaultKeyLabel));
            StringAssert.Contains("press E", QuestCatalog.Get("tut_collect_crate").InstructionText(QuestDefinition.DefaultKeyLabel));
            StringAssert.Contains("Press Tab", QuestCatalog.Get("tut_order_stock").InstructionText(QuestDefinition.DefaultKeyLabel));
            StringAssert.Contains("Press Enter", QuestCatalog.Get("tut_close_day").InstructionText(QuestDefinition.DefaultKeyLabel));
        }

        [Test]
        public void NoQuest_IsMetByAnEmptyShop()
        {
            var empty = new QuestContext();
            foreach (var q in QuestCatalog.All)
                Assert.IsFalse(q.IsMet(empty), q.Id);
        }

        [Test]
        public void Get_UnknownId_IsNull()
        {
            Assert.IsNull(QuestCatalog.Get("no_such_quest"));
        }
    }
}
