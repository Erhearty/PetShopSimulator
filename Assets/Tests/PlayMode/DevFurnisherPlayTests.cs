using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Customer;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>
    /// Boots an unfurnished new game and checks that <see cref="DevFurnisher.Furnish"/> gives it
    /// a counter, stocked shelves and populated pens, so the shop can trade.
    /// </summary>
    public class DevFurnisherPlayTests
    {
        private const int   Seed      = 1234;
        private const float TimeScale = 1f;
        private const float DayLength = 40f;
        /// <summary>Counter, two shelves and two pens.</summary>
        private const int   ExpectedPieces = 5;
        private const int   ExpectedPetsPerPen = 2;
        /// <summary>How far from the staff station the NavMesh may be and still count as reachable.</summary>
        private const float StationNavTolerance = 0.5f;

        /// <summary>Always leaves the statics clean, even when an assertion fails mid-run.</summary>
        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        /// <summary>An empty new shop is furnished with a counter, shelves and pens, and can trade.</summary>
        [UnityTest]
        public IEnumerator Furnish_EmptyNewGame_PlacesATradingShop()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);
            GameManager game = GameManager.Instance;
            Assert.IsFalse(DevFurnisher.IsFurnished(game), "A new game should start with an empty shop.");

            Assert.AreEqual(ExpectedPieces, PlaytestHarness.Furnish(Seed), "pieces placed");

            Assert.IsTrue(game.Spawner.HasCounter, "no counter registered");
            Assert.GreaterOrEqual(game.Shelves.Count, 1, "no shelf registered");
            Assert.GreaterOrEqual(game.Pens.Count, 1, "no pen registered");
            foreach (var shelf in game.Shelves) Assert.Greater(shelf.TotalUnits, 0, $"{shelf.Category} shelf not stocked");
            foreach (var pen in game.Pens) Assert.AreEqual(ExpectedPetsPerPen, pen.Count, $"{pen.PenSpecies} pen");
            Assert.IsTrue(CustomerSpawner.CanTrade(game.Spawner.HasCounter, game.Shelves.Count, game.Pens.Count));
            Assert.IsTrue(game.Spawner.CanTradeNow);

            // The assistant stands at the staff station: furniture must not cut it out of the NavMesh.
            Vector3 station = game.StaffStation.position;
            Assert.IsTrue(NavMesh.SamplePosition(station, out _, StationNavTolerance, NavMesh.AllAreas),
                          $"staff station {station} is off the NavMesh after furnishing");
        }

        /// <summary>A second call on a furnished shop places nothing.</summary>
        [UnityTest]
        public IEnumerator Furnish_AlreadyFurnished_PlacesNothing()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength);
            Assert.IsTrue(DevFurnisher.IsFurnished(GameManager.Instance), "Boot should furnish by default.");
            Assert.AreEqual(0, PlaytestHarness.Furnish(Seed));
        }
    }
}
