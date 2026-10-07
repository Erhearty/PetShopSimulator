using NUnit.Framework;
using UnityEngine;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the hourly queue-join record and its soak violation check.</summary>
    public class DayTelemetryQueueJoinTests
    {
        /// <summary>The clock when a full day closes.</summary>
        private const float ClosingHour = 18f;
        /// <summary>A 29 s spawn-to-queue lead on a 540 s day, in game hours.</summary>
        private const float Lead = 29f / 60f;

        private static float[] Unblocked() => DayTelemetry.NewLastBlocked();

        private static bool[] All(bool value)
        {
            var a = new bool[DayTelemetry.HoursRecorded];
            for (int i = 0; i < a.Length; i++) a[i] = value;
            return a;
        }

        private static int[] Joins(params int[] perHour)
        {
            var joins = new int[DayTelemetry.HoursRecorded];
            for (int i = 0; i < perHour.Length; i++) joins[i] = perHour[i];
            return joins;
        }

        [Test]
        public void DayLine_Json_KeepsBandFieldsFirstAndAddsQueueJoinsByHour()
        {
            var line = new DayTelemetry.DayLine();
            line.queueJoinsByHour[0] = 3;
            string json = JsonUtility.ToJson(line);
            StringAssert.StartsWith("{\"day\":0,\"seed\":0,\"sales\":0", json);
            StringAssert.Contains("\"queueJoinsByHour\":[3,0,0,0,0,0,0,0,0]", json);
        }

        [TestCase(9f, 0)]
        [TestCase(13.99f, 4)]
        [TestCase(14f, 5)]
        [TestCase(17.5f, 8)]
        [TestCase(18f, 8)]
        [TestCase(8f, 0)]
        public void HourIndex_BucketsAndClamps(float gameHour, int expected) =>
            Assert.AreEqual(expected, DayTelemetry.HourIndex(gameHour));

        [Test]
        public void Violations_EveryHourAtTheFloor_None()
        {
            var violations = DayTelemetry.QueueJoinViolations(Joins(2, 2, 2, 2, 2, 2, 2, 2, 0), All(true));
            CollectionAssert.IsEmpty(violations);
        }

        [Test]
        public void Violations_ShortHours_ExactFormat()
        {
            var violations = DayTelemetry.QueueJoinViolations(Joins(2, 1, 2, 2, 2, 2, 2, 0, 0), All(true));
            CollectionAssert.AreEqual(new[] { "hour=10 queueJoins=1", "hour=16 queueJoins=0" }, violations);
        }

        [Test]
        public void Violations_IneligibleHour_IsNotFlagged()
        {
            bool[] eligible = All(true);
            eligible[1] = false;   // shop could not trade or had no stock at some point in 10-11
            var violations = DayTelemetry.QueueJoinViolations(Joins(2, 0, 2, 2, 2, 2, 2, 2, 0), eligible);
            CollectionAssert.IsEmpty(violations);
        }

        [Test]
        public void Violations_AfterDoorsClose_HourSeventeenIsNeverChecked()
        {
            var violations = DayTelemetry.QueueJoinViolations(Joins(2, 2, 2, 2, 2, 2, 2, 2, 0), All(true));
            CollectionAssert.IsEmpty(violations);
        }

        [Test]
        public void EligibleHours_FullDaySeenAndUnblocked_AllEligible() =>
            CollectionAssert.AreEqual(All(true), DayTelemetry.EligibleHours(All(true), Unblocked(), ClosingHour, Lead));

        [Test]
        public void EligibleHours_BlockedOrUnseen_AreNotEligible()
        {
            bool[] seen = All(true);
            float[] lastBlocked = Unblocked();
            seen[0]        = false;
            lastBlocked[3] = 12.1f;
            bool[] eligible = DayTelemetry.EligibleHours(seen, lastBlocked, ClosingHour, Lead);
            Assert.IsFalse(eligible[0]);
            Assert.IsFalse(eligible[3]);
            Assert.IsTrue(eligible[1]);
        }

        [Test]
        public void EligibleHours_DayEndedEarly_CutOffHourIsNotEligible()
        {
            bool[] eligible = DayTelemetry.EligibleHours(All(true), Unblocked(), 12.5f, Lead);
            Assert.IsTrue(eligible[2]);    // 11-12 finished
            Assert.IsFalse(eligible[3]);   // 12-13 cut off at 12:30
        }

        /// <summary>
        /// The shop opens for trading mid-day: blocked until 12:45, inside the 13-14 hour's lead
        /// window [~12:31, 14:00), when its buyers are let in. 13-14 is not eligible, so its short
        /// count is no VIOLATION; 14-15, whose lead window starts at ~13:31, is eligible.
        /// </summary>
        [Test]
        public void EligibleHours_ShopOpensMidDay_HourAfterOpeningIsNotEligible()
        {
            float[] lastBlocked = Unblocked();
            for (int i = 0; i <= 3; i++) lastBlocked[i] = 9f + i + 0.99f;
            lastBlocked[3] = 12.75f;   // trading from 12:45
            bool[] eligible = DayTelemetry.EligibleHours(All(true), lastBlocked, ClosingHour, Lead);
            for (int i = 0; i <= 3; i++) Assert.IsFalse(eligible[i], $"hour {9 + i}");
            Assert.IsFalse(eligible[4]);   // 13-14: lead window [12:31, 14:00) overlaps the block
            Assert.IsTrue(eligible[5]);    // 14-15: lead window starts after the block
            Assert.IsTrue(eligible[7]);
            var violations = DayTelemetry.QueueJoinViolations(Joins(0, 0, 0, 0, 1, 2, 2, 2, 0), eligible);
            CollectionAssert.IsEmpty(violations);
        }

        /// <summary>Blocked only before the lead window: the next hour still counts.</summary>
        [Test]
        public void EligibleHours_BlockEndsBeforeTheLeadWindow_HourIsEligible()
        {
            float[] lastBlocked = Unblocked();
            lastBlocked[3] = 12.25f;   // trading from 12:15, before 13:00 - lead (~12:31)
            bool[] eligible = DayTelemetry.EligibleHours(All(true), lastBlocked, ClosingHour, Lead);
            Assert.IsFalse(eligible[3]);
            Assert.IsTrue(eligible[4]);
        }

        /// <summary>Without a lead only the hour itself matters, as before.</summary>
        [Test]
        public void EligibleHours_NoLead_OnlyTheHourItselfMatters()
        {
            float[] lastBlocked = Unblocked();
            lastBlocked[3] = 12.99f;
            bool[] eligible = DayTelemetry.EligibleHours(All(true), lastBlocked, ClosingHour, 0f);
            Assert.IsTrue(eligible[4]);
        }
    }
}
