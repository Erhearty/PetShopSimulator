using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for SaveSystem's slots, summaries and legacy migration. Every test runs
    /// against a throwaway temp directory via <see cref="SaveSystem.RootOverride"/>.
    /// </summary>
    public class SaveSlotTests
    {
        private const float  Tolerance      = 1e-5f;
        private const int    FirstSlot      = 1;
        private const int    SecondSlot     = 2;
        private const int    ThirdSlot      = 3;
        private const string LegacyFileName = "petshop_save.json";
        private const string BakSuffix      = ".bak";

        private string _dir;

        /// <summary>Points SaveSystem at a fresh temp directory and resets the active slot.</summary>
        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_dir);
            SaveSystem.RootOverride = _dir;
            SaveSystem.ActiveSlot   = FirstSlot;
        }

        /// <summary>Restores the real save folder and slot 1, then removes the temp directory.</summary>
        [TearDown]
        public void TearDown()
        {
            SaveSystem.RootOverride = null;
            SaveSystem.ActiveSlot   = FirstSlot;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        /// <summary>A minimal save with a distinguishing day and balance.</summary>
        private static SaveData Sample(int day, float balance) =>
            new() { Day = day, Balance = balance, Reputation = 50f };

        private string LegacyPath => Path.Combine(_dir, LegacyFileName);

        [Test]
        public void SlotPath_EachSlot_IsDistinctAndInsideRoot()
        {
            var seen = new HashSet<string>();
            for (int slot = FirstSlot; slot <= SaveSystem.SlotCount; slot++)
            {
                string path = SaveSystem.SlotPath(slot);
                Assert.IsTrue(seen.Add(path), $"Slot {slot} path is not unique");
                Assert.AreEqual(_dir, Path.GetDirectoryName(path));
            }
        }

        [Test]
        public void ActiveSlot_OutOfRange_IsClamped()
        {
            SaveSystem.ActiveSlot = SaveSystem.SlotCount + 1;
            Assert.AreEqual(SaveSystem.SlotCount, SaveSystem.ActiveSlot);
            SaveSystem.ActiveSlot = 0;
            Assert.AreEqual(FirstSlot, SaveSystem.ActiveSlot);
        }

        [Test]
        public void SaveLoad_PerSlot_DoesNotTouchOtherSlots()
        {
            SaveSystem.ActiveSlot = SecondSlot;
            Assert.IsTrue(SaveSystem.Save(Sample(4, 200f)));

            Assert.IsTrue(SaveSystem.HasSave(SecondSlot));
            Assert.IsFalse(SaveSystem.HasSave(FirstSlot));
            Assert.IsFalse(SaveSystem.HasSave(ThirdSlot));
            Assert.AreEqual(4, SaveSystem.Load().Day);

            SaveSystem.ActiveSlot = FirstSlot;
            Assert.IsNull(SaveSystem.Load());
            Assert.IsTrue(SaveSystem.HasSave());
        }

        [Test]
        public void Delete_ActiveSlot_RemovesOnlyThatSlot()
        {
            SaveSystem.ActiveSlot = FirstSlot;
            SaveSystem.Save(Sample(1, 10f));
            SaveSystem.Save(Sample(2, 20f));
            SaveSystem.ActiveSlot = ThirdSlot;
            SaveSystem.Save(Sample(3, 30f));

            SaveSystem.ActiveSlot = FirstSlot;
            SaveSystem.Delete();

            Assert.IsFalse(SaveSystem.HasSave(FirstSlot));
            Assert.IsFalse(File.Exists(SaveSystem.SlotPath(FirstSlot) + BakSuffix));
            Assert.IsTrue(SaveSystem.HasSave(ThirdSlot));
        }

        [Test]
        public void Peek_OccupiedSlot_ReturnsDayAndBalance()
        {
            SaveSystem.ActiveSlot = ThirdSlot;
            SaveSystem.Save(Sample(7, 345.5f));

            var summary = SaveSystem.Peek(ThirdSlot);

            Assert.IsNotNull(summary);
            Assert.AreEqual(7, summary.Day);
            Assert.AreEqual(345.5f, summary.Balance, Tolerance);
            Assert.IsFalse(string.IsNullOrEmpty(summary.SavedAt));
        }

        [Test]
        public void Peek_EmptySlot_ReturnsNull()
        {
            Assert.IsNull(SaveSystem.Peek(SecondSlot));
        }

        [Test]
        public void Peek_CorruptSlot_ReturnsNull()
        {
            File.WriteAllText(SaveSystem.SlotPath(SecondSlot), "{ not json");
            LogAssert.ignoreFailingMessages = true;
            Assert.IsNull(SaveSystem.Peek(SecondSlot));
        }

        [Test]
        public void MigrateLegacy_LegacyOnly_MovesIntoSlotOne()
        {
            File.WriteAllText(LegacyPath, JsonUtility.ToJson(Sample(5, 99f)));
            File.WriteAllText(LegacyPath + BakSuffix, JsonUtility.ToJson(Sample(4, 88f)));
            LogAssert.Expect(LogType.Log, new Regex("legacy save"));

            Assert.IsTrue(SaveSystem.MigrateLegacy());

            Assert.IsFalse(File.Exists(LegacyPath));
            Assert.IsFalse(File.Exists(LegacyPath + BakSuffix));
            Assert.IsTrue(File.Exists(SaveSystem.SlotPath(FirstSlot) + BakSuffix));
            Assert.AreEqual(5, SaveSystem.Peek(FirstSlot).Day);
            Assert.IsFalse(SaveSystem.MigrateLegacy());
        }

        [Test]
        public void MigrateLegacy_SlotOneOccupied_LeavesBothAlone()
        {
            SaveSystem.ActiveSlot = FirstSlot;
            SaveSystem.Save(Sample(9, 500f));
            File.WriteAllText(LegacyPath, JsonUtility.ToJson(Sample(5, 99f)));

            Assert.IsFalse(SaveSystem.MigrateLegacy());

            Assert.IsTrue(File.Exists(LegacyPath));
            Assert.AreEqual(9, SaveSystem.Peek(FirstSlot).Day);
        }

        [Test]
        public void MigrateLegacy_NoLegacy_DoesNothing()
        {
            Assert.IsFalse(SaveSystem.MigrateLegacy());
            Assert.IsFalse(SaveSystem.HasSave());
        }
    }
}
