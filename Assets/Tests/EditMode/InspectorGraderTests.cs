using NUnit.Framework;
using PetShop.Progression;

namespace PetShop.Tests
{
    /// <summary>Deterministic tests for <see cref="InspectorGrader"/>.</summary>
    public class InspectorGraderTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void IsInspectionDay_EverySeventhDay()
        {
            Assert.IsFalse(InspectorGrader.IsInspectionDay(0));
            Assert.IsFalse(InspectorGrader.IsInspectionDay(1));
            Assert.IsFalse(InspectorGrader.IsInspectionDay(6));
            Assert.IsTrue(InspectorGrader.IsInspectionDay(7));
            Assert.IsFalse(InspectorGrader.IsInspectionDay(8));
            Assert.IsTrue(InspectorGrader.IsInspectionDay(14));
        }

        [Test]
        public void PerfectShop_GradeA_WithPositiveRewards()
        {
            var result = InspectorGrader.Grade(1f, 1f, 0f);
            Assert.AreEqual('A', result.Grade);
            Assert.Greater(result.ReputationDelta, 0f);
            Assert.Greater(result.CashDelta, 0f);
            Assert.AreEqual(8f, result.ReputationDelta, Tolerance);
            Assert.AreEqual(150f, result.CashDelta, Tolerance);
        }

        [Test]
        public void NeglectedShop_GradeF_WithFine()
        {
            var result = InspectorGrader.Grade(0f, 0f, 1f);
            Assert.AreEqual('F', result.Grade);
            Assert.Less(result.CashDelta, 0f);
            Assert.AreEqual(-8f, result.ReputationDelta, Tolerance);
            Assert.AreEqual(-200f, result.CashDelta, Tolerance);
        }

        [Test]
        public void NoPens_FullMarksInputs_NoPenalty()
        {
            var result = InspectorGrader.Grade(1f, 1f, 0f);
            Assert.GreaterOrEqual(result.ReputationDelta, 0f);
            Assert.GreaterOrEqual(result.CashDelta, 0f);
        }

        [Test]
        public void EmptyShelves_ReduceScore()
        {
            Assert.Less(InspectorGrader.Score(1f, 1f, 1f), InspectorGrader.Score(1f, 1f, 0f));
        }

        [Test]
        public void MiddlingShops_GradeBAndC()
        {
            Assert.AreEqual('B', InspectorGrader.Grade(0.8f, 0.8f, 0f).Grade);
            Assert.AreEqual('C', InspectorGrader.Grade(0.5f, 0.5f, 0f).Grade);
        }

        [Test]
        public void Summary_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(InspectorGrader.Grade(1f, 1f, 0f).Summary));
        }
    }
}
