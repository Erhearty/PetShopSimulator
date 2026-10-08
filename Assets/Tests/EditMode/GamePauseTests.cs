using NUnit.Framework;
using UnityEngine;
using PetShop.UI;

namespace PetShop.Tests
{
    public class GamePauseTests
    {
        private float _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = Time.timeScale;
            GamePause.ResetForTests();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            GamePause.ResetForTests();
            Time.timeScale = _saved;
        }

        [Test]
        public void Acquire_FreezesTime_ReleaseRestoresIt()
        {
            Time.timeScale = 4f;
            GamePause.Acquire();
            Assert.AreEqual(0f, Time.timeScale);
            GamePause.Release();
            Assert.AreEqual(4f, Time.timeScale);
        }

        [Test]
        public void NestedHolds_StayPausedUntilTheLastRelease()
        {
            GamePause.Acquire();   // Shop book
            GamePause.Acquire();   // pause menu opened over it
            GamePause.Release();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(GamePause.IsPaused);
            GamePause.Release();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(GamePause.IsPaused);
        }

        [Test]
        public void ExtraRelease_IsIgnored()
        {
            GamePause.Release();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, GamePause.Holds);
        }
    }
}
