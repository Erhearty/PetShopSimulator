using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Customer
{
    /// <summary>
    /// Pure arrival plan for one trading day. Customers are spread over the eight trading hours
    /// 09-10 ... 16-17 in proportion to a normal curve peaking at <see cref="PeakHour"/>, with every
    /// hour holding at least <see cref="MinQueueJoinsPerHour"/> guaranteed buyers so the till sees
    /// steady trade from opening until the doors close.
    /// </summary>
    public static class ArrivalSchedule
    {
        /// <summary>Opening hour of the first arrival bucket (09:00).</summary>
        public const int FirstHour = 9;

        /// <summary>One-hour arrival buckets: 09-10 through 16-17 (the doors close just before 17:00).</summary>
        public const int HourCount = 8;

        /// <summary>Game hours in a whole trading day, 09:00 to 18:00.</summary>
        public const float TradingDayHours = 9f;

        /// <summary>Guaranteed buyers (and so queue joins) in every trading hour.</summary>
        public const int MinQueueJoinsPerHour = 2;

        /// <summary>Busiest moment of the day: arrivals peak at 14:00.</summary>
        public const float PeakHour = 14f;

        /// <summary>Spread of the arrival curve, in game hours.</summary>
        public const float SigmaHours = 2.5f;

        /// <summary>
        /// Fraction of an hour, centred on its middle, the guaranteed buyers are spaced across
        /// (0.1 puts two of them at :28 and :31). Keeping them at the hour's middle means a
        /// customer who reaches the till up to ±20 min (game time) sooner or later than estimated
        /// still joins the queue inside the hour they were meant for; the measured spawn-to-queue
        /// spread is ±0.33 h on a 540 s day. Everyone else is spread over the whole hour.
        /// </summary>
        public const float GuaranteedSpread = 0.1f;

        /// <summary>
        /// Longest arrival lead, in game hours, at which the hourly guarantee is promised. Beyond it
        /// the walk in from the pavement is too long a share of an hour for the spread in walk times
        /// to keep guaranteed buyers inside their hour (and opening-hour buyers pile up at 09:00).
        /// </summary>
        public const float MaxGuaranteedLeadHours = 0.6f;

        /// <summary>The fewest customers a day can have: the floor in every hour.</summary>
        public const int MinTotal = MinQueueJoinsPerHour * HourCount;

        /// <summary>One planned arrival: when to spawn (game hour) and whether it must buy.</summary>
        public readonly struct Slot
        {
            /// <summary>Game hour (09:00 = 9) at which the customer is let in.</summary>
            public readonly float Hour;
            /// <summary>True for an hour's guaranteed buyers (CustomerAI.MustBuy).</summary>
            public readonly bool  Guaranteed;

            public Slot(float hour, bool guaranteed)
            {
                Hour       = hour;
                Guaranteed = guaranteed;
            }
        }

        /// <summary>Customers in a day for <paramref name="target"/>: the target, raised to fit the floor.</summary>
        public static int TotalFor(int target) => Mathf.Max(target, MinTotal);

        /// <summary>Relative arrival weight of bucket <paramref name="hourIndex"/> (0 = 09-10): the normal pdf at its midpoint.</summary>
        public static double Weight(int hourIndex)
        {
            double z = (FirstHour + hourIndex + 0.5 - PeakHour) / SigmaHours;
            return System.Math.Exp(-0.5 * z * z);
        }

        /// <summary>Guaranteed buyers among <paramref name="count"/> customers in one hour.</summary>
        public static int GuaranteedIn(int count) => Mathf.Min(MinQueueJoinsPerHour, Mathf.Max(0, count));

        /// <summary>
        /// Customers per hour bucket (index 0 = 09-10) for <paramref name="target"/>, summing to
        /// <see cref="TotalFor"/>. Each share is proportional to <see cref="Weight"/>; an hour whose
        /// share falls below the floor is pinned to it and the rest is shared again among the others,
        /// then those shares are rounded by largest remainder (ties go to the earlier hour).
        /// </summary>
        public static int[] CountsPerHour(int target)
        {
            int total  = TotalFor(target);
            var counts = new int[HourCount];
            var pinned = new bool[HourCount];

            // Pin hours below the floor until every free hour's share is at least the floor.
            bool changed = true;
            while (changed)
            {
                changed = false;
                FreeShare(total, pinned, out int free, out double freeWeight);
                for (int h = 0; h < HourCount; h++)
                {
                    if (pinned[h]) continue;
                    if (freeWeight <= 0.0 || free * Weight(h) / freeWeight < MinQueueJoinsPerHour)
                    {
                        pinned[h] = true;
                        changed   = true;
                    }
                }
            }

            FreeShare(total, pinned, out int remaining, out double weight);
            var fractions = new double[HourCount];
            int assigned  = 0;
            for (int h = 0; h < HourCount; h++)
            {
                if (pinned[h]) { counts[h] = MinQueueJoinsPerHour; continue; }
                double quota = remaining * Weight(h) / weight;
                counts[h]    = (int)System.Math.Floor(quota);
                fractions[h] = quota - counts[h];
                assigned    += counts[h];
            }

            // Largest remainder: hand the leftover units to the biggest fractional parts.
            for (int left = remaining - assigned; left > 0; left--)
            {
                int best = -1;
                for (int h = 0; h < HourCount; h++)
                    if (!pinned[h] && (best < 0 || fractions[h] > fractions[best])) best = h;
                if (best < 0) break;
                counts[best]++;
                fractions[best] = -1.0;
            }
            return counts;
        }

        /// <summary>Customers and total weight left for the hours not pinned to the floor.</summary>
        private static void FreeShare(int total, bool[] pinned, out int free, out double freeWeight)
        {
            free       = total;
            freeWeight = 0.0;
            for (int h = 0; h < HourCount; h++)
            {
                if (pinned[h]) free -= MinQueueJoinsPerHour;
                else           freeWeight += Weight(h);
            }
        }

        /// <summary>
        /// Converts a spawn-to-queue time in game seconds into game hours for a day lasting
        /// <paramref name="dayLengthSeconds"/>, i.e. how far ahead of its intended queue join a
        /// customer must be let in.
        /// </summary>
        public static float LeadGameHours(float spawnToQueueSeconds, float dayLengthSeconds) =>
            dayLengthSeconds <= 0f ? 0f : spawnToQueueSeconds / dayLengthSeconds * TradingDayHours;

        /// <summary>
        /// Whether the hourly guarantee can hold with an arrival lead of <paramref name="leadHours"/>
        /// game hours: true up to <see cref="MaxGuaranteedLeadHours"/>.
        /// </summary>
        public static bool GuaranteeHolds(float leadHours) => leadHours <= MaxGuaranteedLeadHours;

        /// <summary>
        /// Spawn times for <paramref name="counts"/>, sorted earliest first. Within its hour each
        /// customer gets an evenly spaced queue-join time (guaranteed buyers across the middle
        /// <see cref="GuaranteedSpread"/> of the hour, the rest across the whole hour); the spawn is
        /// that time minus <paramref name="leadHours"/>, never earlier than opening.
        /// </summary>
        public static List<Slot> Slots(int[] counts, float leadHours)
        {
            var slots = new List<Slot>();
            if (counts == null) return slots;

            for (int h = 0; h < counts.Length && h < HourCount; h++)
            {
                int guaranteed = GuaranteedIn(counts[h]);
                int others     = Mathf.Max(0, counts[h] - guaranteed);
                for (int k = 0; k < guaranteed; k++)
                {
                    float offset = 0.5f + ((k + 0.5f) / guaranteed - 0.5f) * GuaranteedSpread;
                    slots.Add(new Slot(SpawnHour(h, offset, leadHours), true));
                }
                for (int k = 0; k < others; k++)
                    slots.Add(new Slot(SpawnHour(h, (k + 0.5f) / others, leadHours), false));
            }

            slots.Sort((a, b) => a.Hour.CompareTo(b.Hour));
            return slots;
        }

        private static float SpawnHour(int hourIndex, float offset, float leadHours) =>
            Mathf.Max(FirstHour, FirstHour + hourIndex + offset - Mathf.Max(0f, leadHours));
    }
}
