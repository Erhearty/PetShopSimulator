using System;
using TMPro;
using UnityEngine;

namespace PetShop.Localization
{
    /// <summary>
    /// Keeps a TMP label's text in the current language: re-reads its key (or text producer)
    /// whenever <see cref="Loc.LanguageChanged"/> fires, so built UI flips without a rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        private TMP_Text     _label;
        private string       _key;
        private object[]     _args;
        private Func<string> _producer;
        private bool         _subscribed;

        /// <summary>The key this label shows, or null when it uses a producer.</summary>
        public string Key => _key;

        /// <summary>Binds <paramref name="label"/> to <paramref name="key"/> (formatted with <paramref name="args"/>) and sets its text now.</summary>
        public static LocalizedText Bind(TMP_Text label, string key, params object[] args)
        {
            if (label == null) return null;
            var lt = Get(label);
            lt._key      = key;
            lt._args     = args;
            lt._producer = null;
            lt.Refresh();
            return lt;
        }

        /// <summary>Binds <paramref name="label"/> to a text producer re-run on every language change, and sets its text now.</summary>
        public static LocalizedText Bind(TMP_Text label, Func<string> producer)
        {
            if (label == null) return null;
            var lt = Get(label);
            lt._key      = null;
            lt._args     = null;
            lt._producer = producer;
            lt.Refresh();
            return lt;
        }

        private static LocalizedText Get(TMP_Text label)
        {
            var lt = label.GetComponent<LocalizedText>();
            if (lt == null) lt = label.gameObject.AddComponent<LocalizedText>();
            lt._label = label;
            lt.Subscribe();
            return lt;
        }

        /// <summary>Re-reads the text in the current language.</summary>
        public void Refresh()
        {
            // Destroyed without OnDestroy (edit mode / DestroyImmediate): drop the stale subscription.
            if (this == null)
            {
                Loc.LanguageChanged -= Refresh;
                return;
            }
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (_label == null) return;
            if (_producer != null) _label.text = _producer();
            else if (_key != null) _label.text = _args != null && _args.Length > 0 ? Loc.F(_key, _args) : Loc.T(_key);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            Loc.LanguageChanged += Refresh;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            Loc.LanguageChanged -= Refresh;
            _subscribed = false;
        }
    }
}
