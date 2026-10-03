using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Progression.Quests;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="QuestDirector"/>: a completed quest pays its reward exactly
    /// once however often the book is evaluated, the reward is not counted as sales, and a save
    /// → load keeps the completed quests without paying them again. Time is frozen (timeScale 0)
    /// so customers and the periodic check cannot move the balance under the test.
    /// </summary>
    public class QuestDirectorPlayTests
    {
        private const int   Seed           = 4242;
        private const float FrozenTime     = 0f;
        private const float DayLength      = 600f;
        private const int   RepeatedChecks = 3;
        private const float Tolerance      = 1e-3f;
        private const string FirstQuestId  = "tut_open_catalogue";

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        [UnityTest]
        public IEnumerator CompletedQuest_PaysRewardOnce_AcrossRepeatedEvaluations()
        {
            yield return PlaytestHarness.Boot(false, Seed, FrozenTime, DayLength);
            var game = GameManager.Instance;
            Assert.IsNotNull(game.Quests, "GameBootstrapper did not create the QuestDirector.");
            game.Quests.Book.Restore(null);

            float revenueBefore = game.Shop.LifetimeRevenue;
            float paid = CompleteFirstQuest(game, out var done);

            for (int i = 0; i < RepeatedChecks; i++)
            {
                Assert.AreEqual(0, game.Quests.EvaluateNow().Count, $"check {i} completed a quest again");
                yield return null;
            }
            game.Quests.OnDayClosed();   // the day-close check must not pay again either

            Assert.AreEqual(paid, game.Shop.Balance, Tolerance, "a reward was paid more than once");
            Assert.AreEqual(revenueBefore, game.Shop.LifetimeRevenue, Tolerance, "rewards count as sales");
            Assert.GreaterOrEqual(game.Quests.LatestQuestHeadlines.Count, done.Count);
        }

        [UnityTest]
        public IEnumerator SaveThenLoad_KeepsCompletedQuests_AndDoesNotPayAgain()
        {
            yield return PlaytestHarness.Boot(false, Seed, FrozenTime, DayLength);
            var game = GameManager.Instance;
            game.Quests.Book.Restore(null);
            CompleteFirstQuest(game, out _);
            var completed = new List<string>(game.Quests.Book.Completed);

            Assert.IsTrue(game.SaveGame(), "the save was not written");
            var data = SaveSystem.Load();
            Assert.IsNotNull(data);
            CollectionAssert.Contains(data.Quests.CompletedIds, FirstQuestId);

            game.Quests.Book.Restore(null);
            float balanceBeforeLoad = game.Shop.Balance;
            SaveQuests.Apply(data, game);
            yield return null;

            CollectionAssert.AreEquivalent(completed, game.Quests.Book.Completed);
            Assert.AreEqual(0, game.Quests.EvaluateNow().Count, "a loaded quest completed again");
            Assert.AreEqual(balanceBeforeLoad, game.Shop.Balance, Tolerance, "loading paid a reward");
        }

        /// <summary>
        /// Raises the catalogue flag and evaluates once; checks the first quest completed and the
        /// balance rose by exactly the rewards of what completed. Returns the balance after.
        /// </summary>
        private static float CompleteFirstQuest(GameManager game, out List<QuestDefinition> done)
        {
            float before = game.Shop.Balance;
            game.Quests.RaiseFlag(QuestFlags.CatalogueOpened);
            done = game.Quests.EvaluateNow();

            float rewards = 0f;
            foreach (var quest in done) rewards += quest.Reward;
            Assert.IsTrue(done.Exists(q => q.Id == FirstQuestId), "the first tutorial quest did not complete");
            Assert.AreEqual(before + rewards, game.Shop.Balance, Tolerance, "reward not paid");
            return game.Shop.Balance;
        }
    }
}
