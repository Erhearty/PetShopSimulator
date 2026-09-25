using System;
using System.Collections.Generic;

namespace PetShop.Dev
{
    /// <summary>One day of a soak run, written as a single JSON line by <see cref="DayTelemetry"/>.</summary>
    [Serializable]
    public class DayRecord
    {
        /// <summary>Shop day number (from the day summary).</summary>
        public int day;
        /// <summary>Gameplay seed from -seed, or <see cref="SoakBands.NoSeed"/> when none was given.</summary>
        public int seed;
        /// <summary>Number of sale records rung up that day.</summary>
        public int sales;
        /// <summary>Total takings that day.</summary>
        public float revenue;
        /// <summary>Customers who paid at the till.</summary>
        public int checkouts;
        /// <summary>Customers who left having picked up nothing.</summary>
        public int walkoutsEmpty;
        /// <summary>Customers who gave up waiting in the queue.</summary>
        public int gaveUp;
        /// <summary>Walks that ended by timeout rather than arrival.</summary>
        public int navTimeouts;
        /// <summary>Checkouts rung up further than <see cref="SoakBands.StrandedDistanceMetres"/> from the till.</summary>
        public int strandedCheckouts;
        /// <summary>Shop reputation at close.</summary>
        public float reputation;
        /// <summary>Shop balance at close.</summary>
        public float balance;
    }

    /// <summary>
    /// Pure pass/fail bands for a soak run. Every band value below is a PLACEHOLDER: they
    /// are to be re-set from the data of the first real 15-day run.
    /// </summary>
    public static class SoakBands
    {
        /// <summary>Seed recorded when the run was not seeded.</summary>
        public const int NoSeed = -1;

        /// <summary>Placeholder. Mirrors GameManager: a closing balance below 0 is game over.</summary>
        public const float BankruptcyFloor = 0f;

        /// <summary>Lower end of ShopManager's reputation clamp (Mathf.Clamp(..., 0f, 100f)).</summary>
        public const float MinReputation = 0f;

        /// <summary>Upper end of ShopManager's reputation clamp (Mathf.Clamp(..., 0f, 100f)).</summary>
        public const float MaxReputation = 100f;

        /// <summary>Placeholder. Highest acceptable walkoutsEmpty / (checkouts + walkoutsEmpty).</summary>
        public const float MaxWalkoutRatio = 0.6f;

        /// <summary>Placeholder. The walkout band only applies on days after this one.</summary>
        public const int WalkoutGraceDays = 2;

        /// <summary>Placeholder. Highest acceptable navTimeouts / customers.</summary>
        public const float MaxNavTimeoutRatio = 0.1f;

        /// <summary>Placeholder. Every run of this many consecutive days must contain a sale.</summary>
        public const int SalesWindowDays = 3;

        /// <summary>Placeholder. A checkout further than this from the till (XZ, metres) counts as stranded.</summary>
        public const float StrandedDistanceMetres = 3f;

        /// <summary>Customers with a known outcome that day: checkouts + empty walkouts + give-ups.</summary>
        public static int CustomerCount(DayRecord r) => r.checkouts + r.walkoutsEmpty + r.gaveUp;

        /// <summary>Single-day band violations for <paramref name="r"/>; empty when in band.</summary>
        public static IReadOnlyList<string> Check(DayRecord r)
        {
            var violations = new List<string>();
            if (r == null)
            {
                violations.Add("no day record");
                return violations;
            }

            CheckBalance(r, violations);
            CheckReputation(r, violations);
            CheckWalkouts(r, violations);
            CheckNavTimeouts(r, violations);
            return violations;
        }

        /// <summary>
        /// Multi-day band: one violation per run of <see cref="SalesWindowDays"/> consecutive
        /// records in <paramref name="days"/> with no sales. Fewer records than that never violate.
        /// </summary>
        public static IReadOnlyList<string> CheckWindow(IReadOnlyList<DayRecord> days)
        {
            var violations = new List<string>();
            if (days == null) return violations;

            for (int start = 0; start + SalesWindowDays <= days.Count; start++)
            {
                if (AnySales(days, start)) continue;
                int first = days[start]?.day ?? start;
                int last  = days[start + SalesWindowDays - 1]?.day ?? start + SalesWindowDays - 1;
                violations.Add($"no sales on days {first}-{last}");
            }
            return violations;
        }

        private static bool AnySales(IReadOnlyList<DayRecord> days, int start)
        {
            for (int i = start; i < start + SalesWindowDays; i++)
                if (days[i] != null && days[i].sales > 0) return true;
            return false;
        }

        private static void CheckBalance(DayRecord r, List<string> violations)
        {
            if (float.IsNaN(r.balance) || float.IsInfinity(r.balance))
                violations.Add($"day {r.day}: balance is not a finite number ({r.balance})");
            else if (r.balance < BankruptcyFloor)
                violations.Add($"day {r.day}: balance {r.balance:F2} is below the bankruptcy floor {BankruptcyFloor:F2}");
        }

        private static void CheckReputation(DayRecord r, List<string> violations)
        {
            bool inRange = r.reputation >= MinReputation && r.reputation <= MaxReputation;
            if (!inRange)
                violations.Add($"day {r.day}: reputation {r.reputation:F2} is outside {MinReputation}-{MaxReputation}");
        }

        private static void CheckWalkouts(DayRecord r, List<string> violations)
        {
            int leavers = r.checkouts + r.walkoutsEmpty;
            if (r.day <= WalkoutGraceDays || leavers == 0) return;

            float ratio = (float)r.walkoutsEmpty / leavers;
            if (ratio > MaxWalkoutRatio)
                violations.Add($"day {r.day}: walkout ratio {ratio:F2} exceeds {MaxWalkoutRatio:F2}");
        }

        private static void CheckNavTimeouts(DayRecord r, List<string> violations)
        {
            if (r.navTimeouts <= 0) return;

            int customers = CustomerCount(r);
            float ratio = customers == 0 ? float.PositiveInfinity : (float)r.navTimeouts / customers;
            if (ratio > MaxNavTimeoutRatio)
                violations.Add($"day {r.day}: {r.navTimeouts} navigation timeouts for {customers} customers exceeds ratio {MaxNavTimeoutRatio:F2}");
        }
    }
}
