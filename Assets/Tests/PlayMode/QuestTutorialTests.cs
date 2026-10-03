using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Progression.Quests;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode test that plays the whole tutorial on an empty shop with real API calls (order,
    /// collect, place, stock order, sale, close the day) and checks every step completes exactly
    /// when its action is done, in catalogue order, and that the Early chapter then opens.
    /// Time is frozen so customers cannot move the numbers under the test.
    /// </summary>
    public class QuestTutorialTests
    {
        private const int   Seed         = 4243;
        private const float FrozenTime   = 0f;
        private const float DayLength    = 600f;
        private const int   StockUnits   = 12;
        private const float SalePrice    = 5f;
        private const float NoRotation   = 0f;
        private const string SaleItemId  = "tutorial_test_item";
        private const string SaleLabel   = "Tutorial test item";

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        /// <summary>Each tutorial step completes on its own action, never before it.</summary>
        [UnityTest]
        public IEnumerator Tutorial_CompletesStepByStep_ThenOpensEarlyChapter()
        {
            yield return PlaytestHarness.Boot(false, Seed, FrozenTime, DayLength, furnish: false);
            var game = GameManager.Instance;
            Assert.IsNotNull(game.Quests, "GameBootstrapper did not create the QuestDirector.");
            var steps   = QuestCatalog.InChapter(QuestChapter.Tutorial);
            var actions = Actions();
            Assert.AreEqual(steps.Count, actions.Count, "the test must drive every tutorial step");
            Assert.AreEqual(steps[0], game.Quests.Book.CurrentTutorialStep, "book must start at the first step");

            for (int i = 0; i < steps.Count; i++)
            {
                AssertStepCompletesOnItsAction(game, steps, i, actions[i]);
                yield return null;
            }

            Assert.IsNull(game.Quests.Book.CurrentTutorialStep, "the tutorial should be over");
            Assert.AreEqual(QuestChapter.Early, game.Quests.Book.CurrentChapter);
        }

        private static void AssertStepCompletesOnItsAction(GameManager game, List<QuestDefinition> steps,
                                                            int index, Action<GameManager> action)
        {
            var step = steps[index];
            Assert.AreEqual(step, game.Quests.Book.CurrentTutorialStep, $"wrong current step before {step.Id}");
            Assert.AreEqual(0, game.Quests.EvaluateNow().Count, $"{step.Id} (or a later step) completed early");

            action(game);
            game.Quests.EvaluateNow();   // the day-close action already evaluates, so do not require a result

            Assert.IsTrue(game.Quests.Book.IsComplete(step.Id), $"{step.Id} did not complete");
            for (int later = index + 1; later < steps.Count; later++)
                Assert.IsFalse(game.Quests.Book.IsComplete(steps[later].Id), $"{steps[later].Id} completed early");
        }

        /// <summary>One action per tutorial step, in catalogue order.</summary>
        private static List<Action<GameManager>> Actions() => new()
        {
            g => g.Quests.RaiseFlag(QuestFlags.CatalogueOpened),
            g => OrderFurniture(g, BuildCatalog.Counter),
            g => CollectCrates(g),
            g => PlaceOwned(g, BuildCatalog.Counter, null),
            g => OrderFurniture(g, BuildCatalog.ShelfSmall),
            g => { CollectCrates(g); PlaceOwned(g, BuildCatalog.ShelfSmall, ProductCategory.Food.ToString()); },
            g => Assert.IsTrue(g.OrderStock(ProductCategory.Food, StockUnits), "stock order refused"),
            g => g.Shop.RecordSale(SaleItemId, SaleLabel, 1, SalePrice),
            g => g.Quests.OnDayClosed(),
        };

        private static void OrderFurniture(GameManager game, string id) =>
            Assert.IsTrue(game.OrderFurniture(id), $"ordering {id} was refused");

        /// <summary>Lands every crate on order and unpacks it, as pressing the interact key at it does.</summary>
        private static void CollectCrates(GameManager game)
        {
            game.Furniture.ArriveAll();
            foreach (var order in new List<FurnitureOrder>(game.Furniture.Pending))
                Assert.IsTrue(game.Furniture.Collect(order), $"crate {order.CatalogId} was not collectable");
            game.Quests.RaiseFlag(QuestFlags.CrateCollected);
        }

        /// <summary>Takes one <paramref name="id"/> from the inventory and places it on the first free starter-lot cell.</summary>
        private static void PlaceOwned(GameManager game, string id, string variant)
        {
            var def = BuildCatalog.Get(id);
            Assert.IsTrue(game.Furniture.TakeOwned(id), $"no {id} in the inventory to place");
            Assert.IsTrue(TryFindFreeCell(game, def.Size, out Vector2Int cell), $"no free cell for {id}");
            Assert.IsNotNull(game.Build.Place(cell, def, variant, NoRotation, charge: false), $"placing {id} failed");
        }

        private static bool TryFindFreeCell(GameManager game, Vector2Int size, out Vector2Int found)
        {
            RectInt lot = game.Layout.LotStageCells(ShopLayout.StarterLotStage);
            foreach (Vector2Int cell in lot.allPositionsWithin)
            {
                if (!game.Grid.CanPlace(cell, size)) continue;
                found = cell;
                return true;
            }
            found = default;
            return false;
        }
    }
}
