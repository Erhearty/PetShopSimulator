using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the pure command-line parse in <see cref="PlaytestOptions"/>.</summary>
    public class PlaytestOptionsTests
    {
        private const string Exe = "PetShopSimulator.x86_64";

        [Test]
        public void Parse_NoFlags_ReturnsDefaults()
        {
            var s = PlaytestOptions.Parse(new[] { Exe, "-batchmode", "-daylength", "12" });

            Assert.IsNull(s.Seed);
            Assert.IsFalse(s.NoPacks);
            Assert.IsNull(s.SavePath);
            Assert.IsNull(s.TelemetryPath);
            Assert.IsNull(s.QuitAfterDays);
        }

        [Test]
        public void Parse_NullOrEmpty_ReturnsDefaults()
        {
            Assert.IsNull(PlaytestOptions.Parse(null).Seed);
            Assert.IsFalse(PlaytestOptions.Parse(new string[0]).NoPacks);
        }

        [Test]
        public void Parse_AllFlagsPresent_ReadsEveryValue()
        {
            var s = PlaytestOptions.Parse(new[]
            {
                Exe, "-seed", "7", "-nopacks", "-savepath", "/tmp/pt.json",
                "-telemetry", "/tmp/pt.jsonl", "-quitafterdays", "3", "-daylength", "12",
            });

            Assert.AreEqual(7, s.Seed);
            Assert.IsTrue(s.NoPacks);
            Assert.AreEqual("/tmp/pt.json", s.SavePath);
            Assert.AreEqual("/tmp/pt.jsonl", s.TelemetryPath);
            Assert.AreEqual(3, s.QuitAfterDays);
        }

        [Test]
        public void Parse_NegativeSeed_IsAccepted()
        {
            Assert.AreEqual(-5, PlaytestOptions.Parse(new[] { "-seed", "-5" }).Seed);
        }

        [Test]
        public void Parse_MalformedSeed_IsIgnoredWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Game\] Ignoring -seed"));
            var s = PlaytestOptions.Parse(new[] { "-seed", "abc", "-nopacks" });

            Assert.IsNull(s.Seed);
            Assert.IsTrue(s.NoPacks);
        }

        [TestCase("0")]
        [TestCase("-2")]
        [TestCase("three")]
        public void Parse_InvalidQuitAfterDays_IsIgnoredWithWarning(string days)
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Game\] Ignoring -quitafterdays"));
            Assert.IsNull(PlaytestOptions.Parse(new[] { "-quitafterdays", days }).QuitAfterDays);
        }

        [TestCase("-seed")]
        [TestCase("-savepath")]
        [TestCase("-telemetry")]
        [TestCase("-quitafterdays")]
        public void Parse_TrailingFlagWithoutValue_IsIgnoredWithWarning(string flag)
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Game\] Ignoring " + flag + ": it needs a value"));
            var s = PlaytestOptions.Parse(new[] { Exe, flag });

            Assert.IsNull(s.Seed);
            Assert.IsNull(s.SavePath);
            Assert.IsNull(s.TelemetryPath);
            Assert.IsNull(s.QuitAfterDays);
        }

        [Test]
        public void Parse_FlagFollowedByAnotherFlag_DoesNotSwallowIt()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Game\] Ignoring -savepath: it needs a value"));
            var s = PlaytestOptions.Parse(new[] { "-savepath", "-nopacks" });

            Assert.IsNull(s.SavePath);
            Assert.IsTrue(s.NoPacks);
        }
    }
}
