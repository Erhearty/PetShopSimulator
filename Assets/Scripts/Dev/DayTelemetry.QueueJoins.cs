using System.Collections.Generic;
using PetShop.Commerce;
using PetShop.Customer;
using UnityEngine;

namespace PetShop.Dev
{
    /// <summary>
    /// Checkout-queue joins per game hour. Each join bumps its hour on the day line
    /// (<see cref="DayLine.queueJoinsByHour"/>, hours 9..17), and at day end every trading hour
    /// (09-10 ... 16-17) that ran in full with the shop able to trade and holding stock - from the
    /// arrival lead before the hour, when its buyers are let in, to its end - yet saw fewer than <see cref="ArrivalSchedule.MinQueueJoinsPerHour"/> joins, is logged as
    /// "[Soak] VIOLATION hour=H queueJoins=N".
    /// </summary>
    public partial class DayTelemetry
    {
        /// <summary>First hour recorded in <see cref="DayLine.queueJoinsByHour"/> (09:00).</summary>
        public const int FirstRecordedHour = ArrivalSchedule.FirstHour;

        /// <summary>Hours recorded: 9 through 17 (the 17-18 hour, after the doors close, included).</summary>
        public const int HoursRecorded = 9;

        /// <summary>The day line actually written: the band record plus the hourly queue joins.</summary>
        [System.Serializable]
        public class DayLine : DayRecord
        {
            /// <summary>Queue joins in each game hour, index 0 = 09-10 through index 8 = 17-18.</summary>
            public int[] queueJoinsByHour = new int[HoursRecorded];
        }

        /// <summary>Hours the clock was seen in today.</summary>
        private readonly bool[] _hourSeen    = new bool[HoursRecorded];
        /// <summary><see cref="LastBlockedHour"/> entry for an hour the shop could always sell in.</summary>
        public const float NotBlocked = float.NegativeInfinity;

        /// <summary>
        /// Per hour, the latest game hour at which the shop could not trade or had nothing in stock,
        /// or <see cref="NotBlocked"/>.
        /// </summary>
        private readonly float[] _lastBlocked = NewLastBlocked();

        /// <summary>A fresh per-hour "last blocked" record with every hour <see cref="NotBlocked"/>.</summary>
        public static float[] NewLastBlocked()
        {
            var a = new float[HoursRecorded];
            for (int i = 0; i < a.Length; i++) a[i] = NotBlocked;
            return a;
        }
        private CheckoutQueue _queue;

        /// <summary>Bucket for <paramref name="gameHour"/> (9..18), clamped into 0..<see cref="HoursRecorded"/>-1.</summary>
        public static int HourIndex(float gameHour) =>
            Mathf.Clamp(Mathf.FloorToInt(gameHour) - FirstRecordedHour, 0, HoursRecorded - 1);

        /// <summary>
        /// Hours that count towards the floor: seen, finished by <paramref name="endHour"/> (the
        /// clock when the day closed, so a day ended early does not flag the hour it was cut off in),
        /// and with the shop able to sell over the whole of [H - <paramref name="leadHours"/>, H+1):
        /// an hour's buyers are let in the arrival lead before it, so a shop that opens for trade
        /// just before H (or mid-hour in H-1) never released them. <paramref name="lastBlocked"/>
        /// holds, per hour, the latest game hour the shop could not sell, or <see cref="NotBlocked"/>.
        /// </summary>
        public static bool[] EligibleHours(bool[] seen, float[] lastBlocked, float endHour, float leadHours)
        {
            var eligible = new bool[HoursRecorded];
            float lead = Mathf.Max(0f, leadHours);
            for (int i = 0; i < HoursRecorded; i++)
            {
                int   hour     = FirstRecordedHour + i;
                bool  finished = hour + 1 <= endHour;
                if (!finished || seen == null || i >= seen.Length || !seen[i]) continue;

                float windowStart = hour - lead;
                bool  clear       = BlockedAt(lastBlocked, i) == NotBlocked;
                // Earlier hours overlapping the lead window: their last block must precede it.
                for (int j = i - 1; clear && j >= 0 && FirstRecordedHour + j + 1 > windowStart; j--)
                    clear = BlockedAt(lastBlocked, j) < windowStart;
                eligible[i] = clear;
            }
            return eligible;
        }

        private static float BlockedAt(float[] lastBlocked, int i) =>
            lastBlocked == null || i >= lastBlocked.Length ? NotBlocked : lastBlocked[i];

        /// <summary>
        /// "hour=H queueJoins=N" for each trading hour 9..16 that is <paramref name="eligible"/> but
        /// had fewer than <see cref="ArrivalSchedule.MinQueueJoinsPerHour"/> joins.
        /// </summary>
        public static IReadOnlyList<string> QueueJoinViolations(int[] joinsByHour, bool[] eligible)
        {
            var violations = new List<string>();
            if (joinsByHour == null || eligible == null) return violations;

            for (int i = 0; i < ArrivalSchedule.HourCount && i < joinsByHour.Length && i < eligible.Length; i++)
            {
                if (!eligible[i] || joinsByHour[i] >= ArrivalSchedule.MinQueueJoinsPerHour) continue;
                violations.Add($"hour={FirstRecordedHour + i} queueJoins={joinsByHour[i]}");
            }
            return violations;
        }

        private void HookQueue()
        {
            _queue = _game != null ? _game.Queue : null;
            if (_queue != null) _queue.OnShopperJoined.AddListener(OnShopperJoined);
        }

        private void UnhookQueue()
        {
            if (_queue != null) _queue.OnShopperJoined.RemoveListener(OnShopperJoined);
            _queue = null;
        }

        private void OnShopperJoined(CheckoutQueue.IShopper _)
        {
            if (_game == null || !_game.IsDayRunning || !(_current is DayLine line)) return;
            line.queueJoinsByHour[HourIndex(_game.CurrentGameHour)]++;
        }

        /// <summary>Samples, every frame of the trading day, whether the shop could sell this hour.</summary>
        private void Update()
        {
            if (_finished || _game == null || !_game.IsDayRunning) return;
            int i = HourIndex(_game.CurrentGameHour);
            _hourSeen[i] = true;
            if (!ShopCanSell()) _lastBlocked[i] = _game.CurrentGameHour;
        }

        /// <summary>The shop can trade (counter plus shelf or pen) and has a unit or an adult pet to sell.</summary>
        private bool ShopCanSell()
        {
            if (_game.Spawner == null || !_game.Spawner.CanTradeNow) return false;
            foreach (var shelf in _game.Shelves)
                if (shelf != null && !shelf.IsEmpty) return true;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.HasAdults) return true;
            return false;
        }

        /// <summary>Today's eligible hours, then clears the hourly samples for the next day.</summary>
        private bool[] TakeEligibleHours()
        {
            float lead = _game != null
                ? ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, _game.DayLengthSeconds) : 0f;
            bool[] eligible = EligibleHours(_hourSeen, _lastBlocked, _game != null ? _game.CurrentGameHour : 0f, lead);
            System.Array.Clear(_hourSeen, 0, _hourSeen.Length);
            for (int i = 0; i < _lastBlocked.Length; i++) _lastBlocked[i] = NotBlocked;
            return eligible;
        }
    }
}
