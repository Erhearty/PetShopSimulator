using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for InputBindings, run against an in-memory store so the real
    /// PlayerPrefs are never touched.
    /// </summary>
    public class InputBindingsTests
    {
        /// <summary>A dictionary-backed binding store.</summary>
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
            _previous = InputBindings.Store;
            _store    = new MemoryStore();
            InputBindings.Store = _store;
        }

        /// <summary>Restores whichever store was in place before the test.</summary>
        [TearDown]
        public void TearDown() => InputBindings.Store = _previous;

        [Test]
        public void Get_NothingStored_MatchesDefaults()
        {
            Assert.AreEqual(KeyCode.W, InputBindings.Get(GameAction.MoveForward));
            Assert.AreEqual(KeyCode.Space, InputBindings.Get(GameAction.Jump));
            Assert.AreEqual(KeyCode.E, InputBindings.Get(GameAction.Interact));
            Assert.AreEqual(KeyCode.Return, InputBindings.Get(GameAction.EndDay));
            Assert.AreEqual(KeyCode.Delete, InputBindings.Get(GameAction.BuildRemove));
            foreach (var action in InputBindings.AllActions)
                Assert.AreEqual(InputBindings.Default(action), InputBindings.Get(action));
        }

        [Test]
        public void Set_KeyHeldByOtherAction_SwapsAndReturnsDisplaced()
        {
            GameAction? displaced = InputBindings.Set(GameAction.Jump, KeyCode.E);

            Assert.AreEqual(GameAction.Interact, displaced);
            Assert.AreEqual(KeyCode.E, InputBindings.Get(GameAction.Jump));
            Assert.AreEqual(KeyCode.Space, InputBindings.Get(GameAction.Interact));
            Assert.AreEqual("E", _store.Read(InputBindings.KeyPrefix + "Jump"));
        }

        [Test]
        public void Set_FreeKey_ReturnsNull()
        {
            Assert.IsNull(InputBindings.Set(GameAction.Jump, KeyCode.J));
            Assert.AreEqual(KeyCode.J, InputBindings.Get(GameAction.Jump));
        }

        [Test]
        public void ResetAll_AfterChanges_RestoresDefaults()
        {
            InputBindings.Set(GameAction.Jump, KeyCode.E);
            InputBindings.Set(GameAction.Help, KeyCode.J);

            InputBindings.ResetAll();

            foreach (var action in InputBindings.AllActions)
                Assert.AreEqual(InputBindings.Default(action), InputBindings.Get(action));
            Assert.AreEqual(0, _store.Values.Count);
        }

        [Test]
        public void Load_GarbageStoredValue_FallsBackToDefault()
        {
            _store.Values[InputBindings.KeyPrefix + "Jump"] = "NotAKey";
            _store.Values[InputBindings.KeyPrefix + "Interact"] = "99999";
            _store.Values[InputBindings.KeyPrefix + "Help"] = "J";
            InputBindings.Store = _store;   // drop the cache so values reload

            Assert.AreEqual(KeyCode.Space, InputBindings.Get(GameAction.Jump));
            Assert.AreEqual(KeyCode.E, InputBindings.Get(GameAction.Interact));
            Assert.AreEqual(KeyCode.J, InputBindings.Get(GameAction.Help));
        }

        [Test]
        public void IsBindable_RejectsEscapeMouseAndNone()
        {
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.Escape));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.Mouse0));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.Mouse6));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.None));
            Assert.IsTrue(InputBindings.IsBindable(KeyCode.J));
        }

        [Test]
        public void IsBindable_RejectsFixedAlternatesAndBuildHotkeys()
        {
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.KeypadEnter));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.UpArrow));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.Alpha1));
            Assert.IsFalse(InputBindings.IsBindable(KeyCode.Alpha8));
            Assert.IsTrue(InputBindings.IsBindable(KeyCode.Space));
            Assert.IsTrue(InputBindings.IsBindable(KeyCode.Return));
        }

        [Test]
        public void Label_UsesShortNames()
        {
            Assert.AreEqual("Enter", InputBindings.Label(GameAction.EndDay));
            Assert.AreEqual("Space", InputBindings.Label(GameAction.Jump));
            Assert.AreEqual("Del", InputBindings.Label(GameAction.BuildRemove));
            Assert.AreEqual("F5", InputBindings.Label(GameAction.QuickSave));
        }
    }
}
