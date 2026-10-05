using NUnit.Framework;
using UnityEngine;
using PetShop.Customer;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the walkout-reason and nav-timeout counters <see cref="DayTelemetry"/> writes.</summary>
    public class DayTelemetryWalkoutTests
    {
        private const int   Day     = 15;
        private const int   Seed    = 2;
        private const float Elapsed = 0f;

        /// <summary>The band fields, in order, that ./build.sh soak and older readers rely on.</summary>
        private const string BandFieldsPrefix =
            "{\"day\":0,\"seed\":0,\"sales\":0,\"revenue\":0.0,\"checkouts\":0,\"walkoutsEmpty\":0," +
            "\"gaveUp\":0,\"navTimeouts\":0,\"strandedCheckouts\":0,\"reputation\":0.0,\"balance\":0.0";

        [Test]
        public void DayJson_KeepsBandFieldsFirstAndInOrder()
        {
            StringAssert.StartsWith(BandFieldsPrefix, JsonUtility.ToJson(new DayRecord()));
        }

        [TestCase(WalkoutReason.NoStockForWant,     "\"walkoutNoStockForWant\":1")]
        [TestCase(WalkoutReason.TooExpensive,       "\"walkoutTooExpensive\":1")]
        [TestCase(WalkoutReason.CouldNotReachShelf, "\"walkoutCouldNotReachShelf\":1")]
        [TestCase(WalkoutReason.NotTempted,         "\"walkoutNotTempted\":1")]
        public void CountWalkout_CountsTotalAndReason(WalkoutReason reason, string expected)
        {
            var r = new DayRecord();
            DayTelemetry.CountWalkout(r, reason);
            string json = JsonUtility.ToJson(r);
            StringAssert.Contains("\"walkoutsEmpty\":1", json);
            StringAssert.Contains(expected, json);
        }

        [TestCase(NavLeg.ForecourtIn,  "\"navTimeoutForecourtIn\":1")]
        [TestCase(NavLeg.Browse,       "\"navTimeoutBrowse\":1")]
        [TestCase(NavLeg.Register,     "\"navTimeoutRegister\":1")]
        [TestCase(NavLeg.Queue,        "\"navTimeoutQueue\":1")]
        [TestCase(NavLeg.StepAside,    "\"navTimeoutStepAside\":1")]
        [TestCase(NavLeg.ForecourtOut, "\"navTimeoutForecourtOut\":1")]
        [TestCase(NavLeg.Exit,         "\"navTimeoutExit\":1")]
        public void CountNavTimeout_CountsTotalAndLeg(NavLeg leg, string expected)
        {
            var r = new DayRecord();
            DayTelemetry.CountNavTimeout(r, leg);
            string json = JsonUtility.ToJson(r);
            StringAssert.Contains("\"navTimeouts\":1", json);
            StringAssert.Contains(expected, json);
        }

        [Test]
        public void ReasonCounters_LeaveTheWalkoutRatioInputsUnchanged()
        {
            var r = new DayRecord { day = Day, checkouts = 1 };
            DayTelemetry.CountWalkout(r, WalkoutReason.NoStockForWant);
            DayTelemetry.CountWalkout(r, WalkoutReason.TooExpensive);
            Assert.AreEqual(2, r.walkoutsEmpty);
            Assert.AreEqual(1, r.checkouts);
            Assert.AreEqual(0, r.gaveUp);
        }

        [Test]
        public void OutcomeLine_JsonCarriesReasonArchetypeTargetElapsed()
        {
            var line = DayTelemetry.FillOutcome(new DayTelemetry.OutcomeLine(), null,
                                                DayTelemetry.NavTimeoutReason, NavLeg.ForecourtIn, Day, Seed);
            string json = JsonUtility.ToJson(line);
            StringAssert.StartsWith("{\"walkout\":true", json);
            StringAssert.Contains($"\"day\":{Day}", json);
            StringAssert.Contains($"\"seed\":{Seed}", json);
            StringAssert.Contains("\"reason\":\"navTimeout\"", json);
            StringAssert.Contains("\"archetype\":\"\"", json);
            StringAssert.Contains("\"target\":\"ForecourtIn\"", json);
            StringAssert.Contains("\"elapsed\":0.0", json);
        }

        [TestCase(true,  true,  true,  WalkoutReason.CouldNotReachShelf)]
        [TestCase(false, true,  true,  WalkoutReason.NoStockForWant)]
        [TestCase(false, false, true,  WalkoutReason.TooExpensive)]
        [TestCase(false, false, false, WalkoutReason.NotTempted)]
        public void ResolveWalkoutReason_Priority(bool unreachable, bool noStock, bool tooExpensive,
                                                  WalkoutReason expected)
        {
            Assert.AreEqual(expected, CustomerAI.ResolveWalkoutReason(unreachable, noStock, tooExpensive));
        }
    }
}
