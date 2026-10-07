using System.Linq;
using NUnit.Framework;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>Checks the day's arrival curve: peak at 14:00, symmetric, floored, and summing to the total.</summary>
    public class ArrivalScheduleTests
    {
        /// <summary>Bucket 13-14, just before the 14:00 peak.</summary>
        private const int BeforePeak = 4;
        /// <summary>Bucket 14-15, just after the 14:00 peak.</summary>
        private const int AfterPeak  = 5;
        /// <summary>Largest-remainder rounding may hand a tied pair's odd unit to one side only.</summary>
        private const int RoundingSlack = 1;
        private const float Tolerance = 0.0001f;

        private static readonly int[] Targets = { 0, 3, 10, 16, 17, 18, 25, 40, 100 };

        [TestCaseSource(nameof(Targets))]
        public void CountsPerHour_SumToTotal(int target)
        {
            int[] counts = ArrivalSchedule.CountsPerHour(target);
            Assert.AreEqual(ArrivalSchedule.HourCount, counts.Length);
            Assert.AreEqual(ArrivalSchedule.TotalFor(target), counts.Sum());
            Assert.AreEqual(System.Math.Max(target, 2 * 8), ArrivalSchedule.TotalFor(target));
        }

        [TestCaseSource(nameof(Targets))]
        public void CountsPerHour_EveryHourHasTheFloor(int target)
        {
            int[] counts = ArrivalSchedule.CountsPerHour(target);
            for (int h = 0; h < counts.Length; h++)
                Assert.GreaterOrEqual(counts[h], ArrivalSchedule.MinQueueJoinsPerHour, $"hour {9 + h}");
        }

        [TestCaseSource(nameof(Targets))]
        public void CountsPerHour_PeakIsAroundTwoPm(int target)
        {
            int[] counts = ArrivalSchedule.CountsPerHour(target);
            int peak = System.Math.Max(counts[BeforePeak], counts[AfterPeak]);
            Assert.AreEqual(counts.Max(), peak, string.Join(",", counts));
        }

        [TestCaseSource(nameof(Targets))]
        public void CountsPerHour_AreSymmetricAroundTwoPm(int target)
        {
            int[] counts = ArrivalSchedule.CountsPerHour(target);
            // 13-14 / 14-15, 12-13 / 15-16, 11-12 / 16-17 mirror each other about 14:00.
            for (int d = 0; d < 3; d++)
                Assert.LessOrEqual(System.Math.Abs(counts[BeforePeak - d] - counts[AfterPeak + d]), RoundingSlack,
                                   string.Join(",", counts));
        }

        [Test]
        public void CountsPerHour_LargeTarget_RisesTowardsThePeak()
        {
            int[] counts = ArrivalSchedule.CountsPerHour(100);
            for (int h = 0; h < BeforePeak; h++)
                Assert.LessOrEqual(counts[h], counts[h + 1], string.Join(",", counts));
            Assert.Greater(counts[BeforePeak], counts[0], string.Join(",", counts));
        }

        [TestCaseSource(nameof(Targets))]
        public void Slots_MarkTheFloorAsGuaranteedInEveryHour(int target)
        {
            int[] counts = ArrivalSchedule.CountsPerHour(target);
            var slots = ArrivalSchedule.Slots(counts, 0f);
            Assert.AreEqual(counts.Sum(), slots.Count);
            for (int h = 0; h < counts.Length; h++)
            {
                int start = ArrivalSchedule.FirstHour + h;
                var inHour = slots.Where(s => s.Hour >= start && s.Hour < start + 1).ToList();
                Assert.AreEqual(counts[h], inHour.Count, $"hour {start}");
                Assert.AreEqual(ArrivalSchedule.MinQueueJoinsPerHour, inHour.Count(s => s.Guaranteed), $"hour {start}");
            }
        }

        [Test]
        public void Slots_AreSortedAndShiftedByTheLeadButNeverBeforeOpening()
        {
            const float lead = 0.75f;
            var plain   = ArrivalSchedule.Slots(ArrivalSchedule.CountsPerHour(40), 0f);
            var shifted = ArrivalSchedule.Slots(ArrivalSchedule.CountsPerHour(40), lead);
            for (int i = 0; i < shifted.Count; i++)
            {
                Assert.GreaterOrEqual(shifted[i].Hour, ArrivalSchedule.FirstHour);
                Assert.AreEqual(System.Math.Max(9f, plain[i].Hour - lead), shifted[i].Hour, Tolerance);
                if (i > 0) Assert.GreaterOrEqual(shifted[i].Hour, shifted[i - 1].Hour);
            }
        }

        /// <summary>
        /// With the lead from <see cref="CustomerSpawner.SpawnToQueueSeconds"/> on a 540 s day, every
        /// hour still gets its guaranteed joins when the real spawn-to-queue time is anywhere in the
        /// measured 14-43 s spread (spawns clamped to 09:00 included).
        /// </summary>
        [TestCase(14f)]
        [TestCase(29f)]
        [TestCase(43f)]
        public void Slots_GuaranteedJoinsLandInTheirHour_AcrossTheMeasuredSpread(float actualSeconds)
        {
            const float dayLength = 540f;
            float lead   = ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, dayLength);
            float travel = ArrivalSchedule.LeadGameHours(actualSeconds, dayLength);
            // The floor-only day: every slot is guaranteed, two per hour.
            var slots = ArrivalSchedule.Slots(ArrivalSchedule.CountsPerHour(0), lead);
            var joins = new int[ArrivalSchedule.HourCount];
            foreach (var slot in slots)
            {
                Assert.IsTrue(slot.Guaranteed);
                int h = (int)System.Math.Floor(slot.Hour + travel) - ArrivalSchedule.FirstHour;
                if (h >= 0 && h < joins.Length) joins[h]++;
            }
            for (int h = 0; h < joins.Length; h++)
                Assert.AreEqual(ArrivalSchedule.MinQueueJoinsPerHour, joins[h], $"hour {9 + h}: {string.Join(",", joins)}");
        }

        [TestCase(0f, true)]
        [TestCase(0.5f, true)]
        [TestCase(0.51f, false)]
        [TestCase(2f, false)]
        public void GuaranteeHolds_UpToHalfAnHourOfLead(float leadHours, bool expected) =>
            Assert.AreEqual(expected, ArrivalSchedule.GuaranteeHolds(leadHours));

        [Test]
        public void GuaranteeHolds_DefaultDayYes_ShortTestDayNo()
        {
            Assert.IsTrue(ArrivalSchedule.GuaranteeHolds(
                ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, 540f)));
            Assert.IsFalse(ArrivalSchedule.GuaranteeHolds(
                ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, 40f)));
        }

        [Test]
        public void Slots_GuaranteedBuyersSitInTheMiddleOfTheirHour()
        {
            foreach (var slot in ArrivalSchedule.Slots(ArrivalSchedule.CountsPerHour(3), 0f))
            {
                float within = slot.Hour - (float)System.Math.Floor(slot.Hour);
                Assert.IsTrue(slot.Guaranteed);
                Assert.GreaterOrEqual(within, 0.25f);
                Assert.LessOrEqual(within, 0.75f);
            }
        }
    }
}
