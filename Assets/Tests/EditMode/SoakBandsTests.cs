using System.Collections.Generic;
using NUnit.Framework;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the pure soak-run bands in <see cref="SoakBands"/>.</summary>
    public class SoakBandsTests
    {
        private const int   LateDay       = 5;
        private const int   GraceDay      = 2;
        private const float HealthyBalance = 500f;
        private const float HealthyRep     = 50f;
        private const int   ObservedSeed   = 1;
        /// <summary>
        /// Synthetic bankrupt-day closing balance, not from the log: below zero but well above
        /// <see cref="SoakBands.MinBalance"/>.
        /// </summary>
        private const float SyntheticBankruptBalance = -133.37f;
        /// <summary>How far past a band edge the out-of-band cases sit.</summary>
        private const float BalanceStep = 0.01f;

        /// <summary>
        /// The seed-1 14-day run of 2026-09-25, Kenney installed, copied from the 14 day lines of
        /// Logs/build/soak.jsonl.
        /// </summary>
        private static readonly DayRecord[] ObservedRun =
        {
            Observed(1,  0, 0f,                  0, 0, 0, 40f,                 911f),
            Observed(2,  0, 0f,                  0, 0, 0, 40f,                 816f),
            Observed(3,  2, 25.600000381469727f, 1, 0, 0, 41.400001525878906f, 740.5999755859375f),
            Observed(4,  2, 18.899999618530273f, 1, 0, 0, 42.80000305175781f,  652.5f),
            Observed(5,  2, 11f,                 1, 0, 1, 44.20000457763672f,  550.5f),
            Observed(6,  7, 489.52001953125f,    3, 0, 1, 48.90000915527344f,  921.02001953125f),
            Observed(7,  0, 0f,                  0, 0, 0, 48.90000915527344f,  796.02001953125f),
            Observed(8,  3, 15.700000762939453f, 1, 0, 0, 50.800010681152344f, 680.719970703125f),
            Observed(9,  5, 22.5f,               2, 0, 1, 54.100013732910156f, 566.219970703125f),
            Observed(10, 2, 11.199999809265137f, 1, 0, 0, 55.50001525878906f,  434.419921875f),
            Observed(11, 4, 29.5f,               2, 0, 0, 58.300018310546875f, 314.919921875f),
            Observed(12, 2, 58.40999984741211f,  1, 0, 3, 59.70001983642578f,  218.32992553710938f),
            Observed(13, 0, 0f,                  0, 0, 0, 59.70001983642578f,  57.329925537109375f),
            Observed(14, 0, 0f,                  0, 0, 2, 59.70001983642578f,  -109.67007446289063f),
        };

        /// <summary>A day comfortably inside every band.</summary>
        private static DayRecord InBand() => new()
        {
            day = LateDay, seed = 7, sales = 10, revenue = 120f,
            checkouts = 10, walkoutsEmpty = 2, gaveUp = 0, navTimeouts = 1,
            strandedCheckouts = 0, reputation = HealthyRep, balance = HealthyBalance,
        };

        private static DayRecord SalesDay(int day, int sales) => new() { day = day, sales = sales };

        private static DayRecord Observed(int day, int sales, float revenue, int checkouts, int walkouts,
                                          int navTimeouts, float reputation, float balance) => new()
        {
            day = day, seed = ObservedSeed, sales = sales, revenue = revenue, checkouts = checkouts,
            walkoutsEmpty = walkouts, gaveUp = 0, navTimeouts = navTimeouts, strandedCheckouts = 0,
            reputation = reputation, balance = balance,
        };

        [Test]
        public void Check_InBand_HasNoViolations()
        {
            CollectionAssert.IsEmpty(SoakBands.Check(InBand()));
        }

        [Test]
        public void Check_Null_IsAViolation()
        {
            Assert.AreEqual(1, SoakBands.Check(null).Count);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Check_NonFiniteBalance_IsOneViolation(float balance)
        {
            var r = InBand();
            r.balance = balance;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_BalanceBelowMinimum_IsOneViolation()
        {
            var r = InBand();
            r.balance = SoakBands.MinBalance - BalanceStep;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_BalanceAtMinimum_IsInBand()
        {
            var r = InBand();
            r.balance = SoakBands.MinBalance;
            CollectionAssert.IsEmpty(SoakBands.Check(r));
        }

        /// <summary>A synthetic bankrupt day (not from the log) above the minimum balance is in band.</summary>
        [Test]
        public void Check_SyntheticBankruptDay_IsInBand()
        {
            var r = InBand();
            r.balance = SyntheticBankruptBalance;
            CollectionAssert.IsEmpty(SoakBands.Check(r));
        }

        [TestCase(-1f)]
        [TestCase(101f)]
        [TestCase(float.NaN)]
        public void Check_ReputationOutsideClamp_IsOneViolation(float reputation)
        {
            var r = InBand();
            r.reputation = reputation;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_HighWalkoutRatioAfterGrace_IsOneViolation()
        {
            var r = InBand();
            r.checkouts = 3;
            r.walkoutsEmpty = 7;
            r.navTimeouts = 0;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_HighWalkoutRatioDuringGrace_IsInBand()
        {
            var r = InBand();
            r.day = GraceDay;
            r.checkouts = 3;
            r.walkoutsEmpty = 7;
            r.navTimeouts = 0;
            CollectionAssert.IsEmpty(SoakBands.Check(r));
        }

        [Test]
        public void Check_NavTimeoutsAtDailyMaximum_IsInBand()
        {
            var r = InBand();
            r.navTimeouts = SoakBands.MaxNavTimeoutsPerDay;
            CollectionAssert.IsEmpty(SoakBands.Check(r));
        }

        [Test]
        public void Check_NavTimeoutsWithNoCustomers_IsInBand()
        {
            var r = InBand();
            r.checkouts = 0;
            r.walkoutsEmpty = 0;
            r.navTimeouts = 1;
            CollectionAssert.IsEmpty(SoakBands.Check(r));
        }

        [Test]
        public void Check_NavTimeoutsAboveDailyMaximum_IsOneViolation()
        {
            var r = InBand();
            r.navTimeouts = SoakBands.MaxNavTimeoutsPerDay + 1;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void CheckWindow_NoSalesDuringGrace_IsInBand()
        {
            var days = new[] { SalesDay(1, 0), SalesDay(2, 0), SalesDay(3, 0), SalesDay(4, 0), SalesDay(5, 1) };
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(days));
        }

        [Test]
        public void CheckWindow_ThreeDaysWithoutSalesAfterGrace_IsOneViolation()
        {
            int first = SoakBands.SalesGraceDays + 1;
            var days = new[] { SalesDay(first, 0), SalesDay(first + 1, 0), SalesDay(first + 2, 0) };
            Assert.AreEqual(1, SoakBands.CheckWindow(days).Count);
        }

        [Test]
        public void CheckWindow_OneSaleInEveryWindow_IsInBand()
        {
            var days = new[] { SalesDay(4, 0), SalesDay(5, 0), SalesDay(6, 1), SalesDay(7, 0), SalesDay(8, 0) };
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(days));
        }

        [Test]
        public void CheckWindow_FewerDaysThanWindow_IsInBand()
        {
            var days = new[] { SalesDay(1, 0), SalesDay(2, 0) };
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(days));
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(null));
        }

        [Test]
        public void ObservedSeed1Run_HasNoViolations()
        {
            var violations = new List<string>(SoakBands.CheckWindow(ObservedRun));
            foreach (var day in ObservedRun) violations.AddRange(SoakBands.Check(day));
            CollectionAssert.IsEmpty(violations);
        }
    }
}
