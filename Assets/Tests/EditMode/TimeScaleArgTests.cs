using NUnit.Framework;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>Checks -timescale parsing: clamped to 1..20, ignored when absent or malformed.</summary>
    public class TimeScaleArgTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase("8", 8f)]
        [TestCase("2.5", 2.5f)]
        [TestCase("50", 20f)]
        [TestCase("0.25", 1f)]
        [TestCase("-3", 1f)]
        public void ParseTimeScale_ClampsToRange(string value, float expected)
        {
            float? scale = GameManager.ParseTimeScale(new[] { "Player", "-daylength", "540", "-timescale", value });
            Assert.IsTrue(scale.HasValue);
            Assert.AreEqual(expected, scale.Value, Tolerance);
        }

        [Test]
        public void ParseTimeScale_Absent_IsNull() =>
            Assert.IsNull(GameManager.ParseTimeScale(new[] { "Player", "-daylength", "540" }));

        [Test]
        public void ParseTimeScale_NotANumber_IsNull() =>
            Assert.IsNull(GameManager.ParseTimeScale(new[] { "Player", "-timescale", "fast" }));

        [Test]
        public void ParseTimeScale_MissingValue_IsNull() =>
            Assert.IsNull(GameManager.ParseTimeScale(new[] { "Player", "-timescale" }));

        [Test]
        public void ParseTimeScale_NullArgs_IsNull() =>
            Assert.IsNull(GameManager.ParseTimeScale(null));

        /// <summary>A 30 fps frame advances the full timescale: the cap no longer limits the speed-up.</summary>
        [TestCase(10f)]
        [TestCase(20f)]
        public void MaxDeltaTimeFor_ThirtyFpsFrame_DeliversTheFullTimeScale(float timeScale) =>
            Assert.GreaterOrEqual(GameManager.MaxDeltaTimeFor(timeScale), timeScale / 30f - Tolerance);

        /// <summary>At 20x a 60 fps frame is 1/3 game second; it must not be clipped (the old 0.1 s cap did).</summary>
        [Test]
        public void MaxDeltaTimeFor_TwentyX_AllowsSixtyFpsFrames() =>
            Assert.Greater(GameManager.MaxDeltaTimeFor(20f), 20f / 60f);

        /// <summary>Low timescales keep Unity's default cap; a slow hitch at 20x is still clipped.</summary>
        [Test]
        public void MaxDeltaTimeFor_FloorsAtDefaultAndStaysBounded()
        {
            Assert.AreEqual(GameManager.DefaultMaxDeltaTime, GameManager.MaxDeltaTimeFor(1f), Tolerance);
            Assert.AreEqual(GameManager.DefaultMaxDeltaTime, GameManager.MaxDeltaTimeFor(8f), Tolerance);
            Assert.LessOrEqual(GameManager.MaxDeltaTimeFor(GameManager.MaxTimeScale), 1f);
        }
    }
}
