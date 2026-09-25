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

        /// <summary>A day comfortably inside every band.</summary>
        private static DayRecord InBand() => new()
        {
            day = LateDay, seed = 7, sales = 10, revenue = 120f,
            checkouts = 10, walkoutsEmpty = 2, gaveUp = 0, navTimeouts = 1,
            strandedCheckouts = 0, reputation = HealthyRep, balance = HealthyBalance,
        };

        private static DayRecord SalesDay(int day, int sales) => new() { day = day, sales = sales };

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
        [TestCase(-0.01f)]
        public void Check_BadBalance_IsOneViolation(float balance)
        {
            var r = InBand();
            r.balance = balance;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_BalanceAtFloor_IsInBand()
        {
            var r = InBand();
            r.balance = SoakBands.BankruptcyFloor;
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
        public void Check_HighNavTimeoutRatio_IsOneViolation()
        {
            var r = InBand();
            r.walkoutsEmpty = 0;
            r.navTimeouts = 2;   // 2 / 10 customers = 0.2
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void Check_NavTimeoutsWithNoCustomers_IsOneViolation()
        {
            var r = InBand();
            r.checkouts = 0;
            r.walkoutsEmpty = 0;
            r.navTimeouts = 1;
            Assert.AreEqual(1, SoakBands.Check(r).Count);
        }

        [Test]
        public void CheckWindow_ThreeDaysWithoutSales_IsOneViolation()
        {
            var days = new[] { SalesDay(1, 0), SalesDay(2, 0), SalesDay(3, 0) };
            Assert.AreEqual(1, SoakBands.CheckWindow(days).Count);
        }

        [Test]
        public void CheckWindow_OneSaleInEveryWindow_IsInBand()
        {
            var days = new[] { SalesDay(1, 0), SalesDay(2, 0), SalesDay(3, 1), SalesDay(4, 0), SalesDay(5, 0) };
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(days));
        }

        [Test]
        public void CheckWindow_FewerDaysThanWindow_IsInBand()
        {
            var days = new[] { SalesDay(1, 0), SalesDay(2, 0) };
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(days));
            CollectionAssert.IsEmpty(SoakBands.CheckWindow(null));
        }
    }
}
