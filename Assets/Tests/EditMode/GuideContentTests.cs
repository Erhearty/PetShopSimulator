using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="GuideContent"/>: every section has a title and body, names
    /// the current key of each action it is about, and follows a rebind.
    /// </summary>
    public class GuideContentTests
    {
        /// <summary>In-memory binding store so the tests never touch PlayerPrefs.</summary>
        private sealed class MemoryStore : IBindingStore
        {
            private readonly Dictionary<string, string> _values = new();
            public string Read(string key) => _values.TryGetValue(key, out var v) ? v : null;
            public void Write(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        private IBindingStore _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = InputBindings.Store;
            InputBindings.Store = new MemoryStore();
        }

        [TearDown]
        public void TearDown() => InputBindings.Store = _saved;

        [Test]
        public void EverySection_HasTitleAndBody()
        {
            var sections = GuideContent.Sections();
            Assert.IsNotEmpty(sections);
            foreach (var section in sections)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Title), "title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Body), section.Title);
                StringAssert.DoesNotContain("...", section.Body, section.Title);
            }
        }

        [Test]
        public void EverySection_MentionsTheCurrentKeyOfItsActions()
        {
            foreach (var section in GuideContent.Sections())
                foreach (var action in section.Keys)
                    StringAssert.Contains(InputBindings.Label(action), section.Body,
                                          $"{section.Title} should name the {action} key");
        }

        [Test]
        public void Sections_FollowARebind()
        {
            InputBindings.Set(GameAction.Interact, KeyCode.K);

            bool found = false;
            foreach (var section in GuideContent.Sections())
                if (section.Title == "Serving at the counter")
                {
                    found = true;
                    StringAssert.Contains("press K", section.Body);
                }
            Assert.IsTrue(found);
        }

        /// <summary>The guide explains the street front, the back door, yard walls and the cashier's counter.</summary>
        [Test]
        public void Guide_CoversLayoutDoorsWallsAndCashierCounter()
        {
            var all = string.Join("\n", GuideContent.Sections().Select(s => s.Body));
            StringAssert.Contains("onto the street", all);
            StringAssert.Contains("back door", all);
            StringAssert.Contains("anywhere in the yard", all);
            StringAssert.Contains("cashier needs a counter", all);
            StringAssert.Contains("behind the counter", all);
        }

        [Test]
        public void Guide_IsABindableActionDefaultingToF1()
        {
            Assert.AreEqual(KeyCode.F1, InputBindings.Default(GameAction.Guide));
            Assert.AreEqual("Guide", InputBindings.ActionName(GameAction.Guide));
            Assert.AreEqual("Shop book", InputBindings.ActionName(GameAction.Ledger));
        }
    }
}
