using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>
    /// Runs a real-length (540 s) trading day at <see cref="TimeScale"/>x in the furnished shop and
    /// counts <see cref="CheckoutQueue.Join"/> per game hour: every hour from 09:00 to 17:00 must see
    /// at least <see cref="ArrivalSchedule.MinQueueJoinsPerHour"/> joins. The day length is the real
    /// one so the walk in from the pavement keeps its true share of an hour; shelves are topped up
    /// as they empty so the floor is tested, not the stock. Nobody is served: joins are what count.
    /// </summary>
    public class ArrivalCurvePlayTests
    {
        private const int   Seed      = 4711;
        private const float DayLength = 540f;
        private const float TimeScale = 20f;
        /// <summary>Doors close at ~16:55; the 16-17 hour is the last one checked.</summary>
        private const float LastCheckedHourEnd = 17f;
        /// <summary>480 game seconds at 20x is ~24 real seconds; this leaves room for slow frames.</summary>
        private const float RealSecondsBudget = 120f;
        private const int   TimeoutMs = 240000;

        private GameManager _game;
        private float _savedMaxDeltaTime = -1f;
        private readonly int[]       _joins = new int[ArrivalSchedule.HourCount];
        private readonly List<float> _lags  = new();

        [TearDown]
        public void TearDown()
        {
            if (_game != null && _game.Queue != null) _game.Queue.OnShopperJoined.RemoveListener(OnJoined);
            _game = null;
            if (_savedMaxDeltaTime > 0f) Time.maximumDeltaTime = _savedMaxDeltaTime;
            _savedMaxDeltaTime = -1f;
            PlaytestHarness.Teardown();
        }

        [UnityTest, Timeout(TimeoutMs)]
        public IEnumerator RealLengthDay_EveryTradingHour_HasTwoQueueJoins()
        {
            // The same frame cap -timescale applies, so the 20x is delivered, not clipped.
            _savedMaxDeltaTime    = Time.maximumDeltaTime;
            Time.maximumDeltaTime = GameManager.MaxDeltaTimeFor(TimeScale);
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength);
            _game = PlaytestHarness.Game;
            Assert.IsNotNull(_game.Queue, "The scene has no checkout queue.");
            Assert.IsTrue(_game.Spawner != null && _game.Spawner.CanTradeNow, "The furnished shop cannot trade.");
            _game.Queue.OnShopperJoined.AddListener(OnJoined);

            float deadline = Time.realtimeSinceStartup + RealSecondsBudget;
            while (_game.IsDayRunning && _game.CurrentGameHour < LastCheckedHourEnd)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"The day only reached {_game.ClockText} in {RealSecondsBudget} real seconds. {Describe()}");
                TopUpShelves();
                yield return null;
            }

            Debug.Log($"[ArrivalCurve] {Describe()}");
            for (int h = 0; h < _joins.Length; h++)
                Assert.GreaterOrEqual(_joins[h], ArrivalSchedule.MinQueueJoinsPerHour,
                                      $"hour {ArrivalSchedule.FirstHour + h}:00. {Describe()}");
        }

        private void OnJoined(CheckoutQueue.IShopper shopper)
        {
            int h = Mathf.FloorToInt(_game.CurrentGameHour) - ArrivalSchedule.FirstHour;
            if (h >= 0 && h < _joins.Length) _joins[h]++;
            if (shopper is CustomerAI customer) _lags.Add(customer.SecondsSinceSpawn);
        }

        /// <summary>Refills any shelf that is down to its last line, from its own category.</summary>
        private void TopUpShelves()
        {
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null || shelf.TotalUnits >= shelf.MaxPerLine) continue;
                var products = _game.Catalog.GetByCategory(shelf.Category);
                if (products.Count > 0) shelf.AddStock(products[0], shelf.MaxPerLine);
            }
        }

        /// <summary>Joins per hour and the measured spawn-to-queue time, for the log and failures.</summary>
        private string Describe()
        {
            float min = float.MaxValue, max = 0f, sum = 0f;
            foreach (float l in _lags) { min = Mathf.Min(min, l); max = Mathf.Max(max, l); sum += l; }
            string lags = _lags.Count == 0 ? "no joins"
                : $"spawn-to-queue {sum / _lags.Count:F1}s mean, {min:F1}-{max:F1}s over {_lags.Count}";
            return $"joins by hour 09..16: [{string.Join(",", _joins)}]; {lags} " +
                   $"(estimate {CustomerSpawner.SpawnToQueueSeconds}s); target {_game?.Spawner?.TargetToday}";
        }
    }
}
