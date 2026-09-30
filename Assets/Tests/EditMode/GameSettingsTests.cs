using System.Collections.Generic;
using NUnit.Framework;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for GameSettings, run against an in-memory store (never PlayerPrefs).</summary>
    public class GameSettingsTests
    {
        /// <summary>A dictionary-backed store.</summary>
        private sealed class MemoryStore : IBindingStore
        {
            /// <summary>Raw stored values, keyed by store key.</summary>
            public readonly Dictionary<string, string> Values = new();

            /// <inheritdoc/>
            public string Read(string key) => Values.TryGetValue(key, out var v) ? v : null;

            /// <inheritdoc/>
            public void Write(string key, string value) => Values[key] = value;

            /// <inheritdoc/>
            public void Delete(string key) => Values.Remove(key);
        }

        private IBindingStore _previous;
        private MemoryStore   _store;

        /// <summary>Swaps in a fresh in-memory store for each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _previous = GameSettings.Store;
            _store    = new MemoryStore();
            GameSettings.Store = _store;
        }

        /// <summary>Restores whichever store was in place before the test.</summary>
        [TearDown]
        public void TearDown() => GameSettings.Store = _previous;

        [Test]
        public void AutosaveEachMorning_NothingStored_DefaultsTrue()
        {
            Assert.IsTrue(GameSettings.AutosaveEachMorning);
        }

        [Test]
        public void AutosaveEachMorning_SetFalse_PersistsAcrossFreshRead()
        {
            GameSettings.AutosaveEachMorning = false;

            Assert.IsTrue(_store.Values.ContainsKey(GameSettings.AutosaveMorningKey));
            GameSettings.Store = _store;   // fresh read through the store
            Assert.IsFalse(GameSettings.AutosaveEachMorning);
        }

        [Test]
        public void AutosaveEachMorning_SetTrueAfterFalse_ReadsTrue()
        {
            GameSettings.AutosaveEachMorning = false;
            GameSettings.AutosaveEachMorning = true;
            Assert.IsTrue(GameSettings.AutosaveEachMorning);
        }
    }
}
