using System.Collections.Generic;
using PetShop.Core;
using PetShop.Localization;

namespace PetShop.Tests
{
    /// <summary>
    /// Forces a language for a test without touching PlayerPrefs: swaps GameSettings onto an
    /// in-memory store, then restores the previous store and language cache in <see cref="End"/>.
    /// </summary>
    public sealed class LocTestScope
    {
        private readonly IBindingStore _previous;

        /// <summary>The in-memory store GameSettings reads while the scope is open.</summary>
        public MemoryStore Store { get; } = new();

        private LocTestScope(Language language)
        {
            _previous = GameSettings.Store;
            GameSettings.Store = Store;
            Loc.ResetForTests();
            Loc.SetLanguageWithoutSaving(language);
        }

        /// <summary>Opens a scope with <paramref name="language"/> active (call in SetUp).</summary>
        public static LocTestScope Begin(Language language = Language.En) => new(language);

        /// <summary>Restores the previous settings store and forgets the forced language (call in TearDown).</summary>
        public void End()
        {
            GameSettings.Store = _previous;
            Loc.ResetForTests();
        }

        /// <summary>A dictionary-backed <see cref="IBindingStore"/>.</summary>
        public sealed class MemoryStore : IBindingStore
        {
            public readonly Dictionary<string, string> Values = new();
            public string Read(string key) => Values.TryGetValue(key, out var v) ? v : null;
            public void Write(string key, string value) => Values[key] = value;
            public void Delete(string key) => Values.Remove(key);
        }
    }
}
