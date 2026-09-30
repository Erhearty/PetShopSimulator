using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>Checks that <see cref="PlaytestHarness"/> can boot and tear down the world repeatedly.</summary>
    public class PlaytestHarnessSmokeTests
    {
        private const int   Seed      = 1234;
        private const int   Runs      = 2;
        private const float TimeScale = 1f;
        private const float DayLength = 40f;

        /// <summary>Always leaves the statics clean, even when an assertion fails mid-run.</summary>
        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        /// <summary>Two boot/teardown cycles in a row each start a day and leave nothing behind.</summary>
        [UnityTest]
        public IEnumerator BootAndTeardown_TwiceInARow_LeavesNoGameBehind()
        {
            for (int run = 0; run < Runs; run++)
            {
                yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength);
                Assert.IsNotNull(GameManager.Instance, $"Run {run}: no GameManager after Boot.");
                Assert.IsTrue(GameManager.Instance.IsDayRunning, $"Run {run}: the day is not running.");
                Assert.AreEqual(DayLength, GameManager.Instance.DayLengthSeconds, $"Run {run}: day length.");

                PlaytestHarness.Teardown();
                Assert.IsTrue(GameManager.Instance == null, $"Run {run}: GameManager survived Teardown.");
                Assert.IsFalse(ModelLibrary.ForceProcedural, $"Run {run}: ForceProcedural not reset.");
                Assert.IsNull(SaveSystem.PathOverride, $"Run {run}: PathOverride not reset.");
                Assert.IsFalse(System.IO.File.Exists(PlaytestHarness.TempSavePath), $"Run {run}: temp save not deleted.");
                yield return null;
            }
        }
    }
}
