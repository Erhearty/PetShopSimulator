using System;
using System.Collections.Generic;
using UnityEngine;
using PetShop.Localization;

namespace PetShop.Core
{
    /// <summary>Every player action that can be bound to a key.</summary>
    public enum GameAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight,
        Jump, Interact, EndDay, QuickSave, Ledger,
        BuildMode, BuildRotate, BuildRemove, Guide, DebugConsole
    }

    /// <summary>Where key bindings are persisted. Swappable so tests never touch PlayerPrefs.</summary>
    public interface IBindingStore
    {
        /// <summary>Returns the stored value for <paramref name="key"/>, or null if none.</summary>
        string Read(string key);

        /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>.</summary>
        void Write(string key, string value);

        /// <summary>Removes any value stored under <paramref name="key"/>.</summary>
        void Delete(string key);
    }

    /// <summary>The shipping store: bindings live in PlayerPrefs.</summary>
    public sealed class PlayerPrefsBindingStore : IBindingStore
    {
        /// <inheritdoc/>
        public string Read(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        /// <inheritdoc/>
        public void Write(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The single source of truth for which key does what. All gameplay key reads go through
    /// here so a rebind in the settings panel takes effect everywhere at once.
    /// </summary>
    public static class InputBindings
    {
        /// <summary>Prefix of the persisted key for each action, e.g. "bind.Jump".</summary>
        public const string KeyPrefix = "bind.";

        private const float MaxMoveMagnitude = 1f;

        private static IBindingStore _store = new PlayerPrefsBindingStore();
        private static Dictionary<GameAction, KeyCode> _bindings;

        /// <summary>All bindable actions, in display order.</summary>
        public static readonly GameAction[] AllActions = (GameAction[])Enum.GetValues(typeof(GameAction));

        /// <summary>Persistence backend. Setting it drops the cache so bindings reload from it.</summary>
        public static IBindingStore Store
        {
            get => _store;
            set { _store = value ?? new PlayerPrefsBindingStore(); _bindings = null; }
        }

        /// <summary>The factory key for <paramref name="action"/>.</summary>
        public static KeyCode Default(GameAction action) => action switch
        {
            GameAction.MoveForward => KeyCode.W,
            GameAction.MoveBack    => KeyCode.S,
            GameAction.MoveLeft    => KeyCode.A,
            GameAction.MoveRight   => KeyCode.D,
            GameAction.Jump        => KeyCode.Space,
            GameAction.Interact    => KeyCode.E,
            GameAction.EndDay      => KeyCode.Return,
            GameAction.QuickSave   => KeyCode.F5,
            GameAction.Ledger      => KeyCode.Tab,
            GameAction.BuildMode   => KeyCode.B,
            GameAction.BuildRotate => KeyCode.R,
            GameAction.BuildRemove => KeyCode.Delete,
            GameAction.Guide       => KeyCode.F1,
            GameAction.DebugConsole => KeyCode.F2,
            _                      => KeyCode.None,
        };

        /// <summary>The key currently bound to <paramref name="action"/>.</summary>
        public static KeyCode Get(GameAction action) => Bindings[action];

        /// <summary>
        /// Binds <paramref name="key"/> to <paramref name="action"/>. If another action already
        /// held that key, the two swap and the displaced action is returned; otherwise null.
        /// Unbindable keys are ignored (nothing changes, returns null).
        /// </summary>
        public static GameAction? Set(GameAction action, KeyCode key)
        {
            if (!IsBindable(key)) return null;
            KeyCode previous = Get(action);
            if (previous == key) return null;

            GameAction? displaced = FindHolder(key, action);
            if (displaced.HasValue) Assign(displaced.Value, previous);
            Assign(action, key);
            return displaced;
        }

        /// <summary>Restores every action to its default and clears the persisted overrides.</summary>
        public static void ResetAll()
        {
            var fresh = new Dictionary<GameAction, KeyCode>();
            foreach (var action in AllActions)
            {
                fresh[action] = Default(action);
                _store.Delete(StoreKey(action));
            }
            _bindings = fresh;
        }

        /// <summary>True on the frame the key bound to <paramref name="action"/> went down.</summary>
        public static bool GetKeyDown(GameAction action) => Input.GetKeyDown(Get(action));

        /// <summary>True while the key bound to <paramref name="action"/> is held.</summary>
        public static bool GetKey(GameAction action) => Input.GetKey(Get(action));

        /// <summary>
        /// Movement intent: x = right, y = forward, from the four bound move keys plus the
        /// fixed arrow-key alternates. Clamped to magnitude 1 so diagonals are not faster.
        /// </summary>
        public static Vector2 MoveVector()
        {
            float x = Axis(GameAction.MoveRight, KeyCode.RightArrow, GameAction.MoveLeft, KeyCode.LeftArrow);
            float y = Axis(GameAction.MoveForward, KeyCode.UpArrow, GameAction.MoveBack, KeyCode.DownArrow);
            return Vector2.ClampMagnitude(new Vector2(x, y), MaxMoveMagnitude);
        }

        /// <summary>Short, player-facing name of the key bound to <paramref name="action"/>.</summary>
        public static string Label(GameAction action) => KeyLabel(Get(action));

        /// <summary>Short, player-facing name for <paramref name="key"/> ("Enter", "Del", "1"...).</summary>
        public static string KeyLabel(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)(key - KeyCode.Alpha0)).ToString();
            return key switch
            {
                KeyCode.Return       => "Enter",
                KeyCode.KeypadEnter  => "Num Enter",
                KeyCode.Delete       => "Del",
                KeyCode.Backspace    => "Bksp",
                KeyCode.Escape       => "Esc",
                KeyCode.LeftShift    => "LShift",
                KeyCode.RightShift   => "RShift",
                KeyCode.LeftControl  => "LCtrl",
                KeyCode.RightControl => "RCtrl",
                KeyCode.LeftAlt      => "LAlt",
                KeyCode.RightAlt     => "RAlt",
                KeyCode.PageUp       => "PgUp",
                KeyCode.PageDown     => "PgDn",
                KeyCode.UpArrow      => "Up",
                KeyCode.DownArrow    => "Down",
                KeyCode.LeftArrow    => "Left",
                KeyCode.RightArrow   => "Right",
                _                    => key.ToString(),
            };
        }

        /// <summary>
        /// The four move keys in one hint: "WASD" when every key is a single character,
        /// otherwise the labels joined with slashes.
        /// </summary>
        public static string MoveLabel()
        {
            string[] parts =
            {
                Label(GameAction.MoveForward), Label(GameAction.MoveLeft),
                Label(GameAction.MoveBack), Label(GameAction.MoveRight),
            };
            foreach (var part in parts)
                if (part.Length > 1) return string.Join("/", parts);
            return string.Concat(parts);
        }

        /// <summary>Human-readable name of an action for the settings panel.</summary>
        public static string ActionName(GameAction action) => action switch
        {
            GameAction.MoveForward => Loc.T("action.move_forward"),
            GameAction.MoveBack    => Loc.T("action.move_back"),
            GameAction.MoveLeft    => Loc.T("action.move_left"),
            GameAction.MoveRight   => Loc.T("action.move_right"),
            GameAction.Jump        => Loc.T("action.jump"),
            GameAction.Interact    => Loc.T("action.interact"),
            GameAction.EndDay      => Loc.T("action.end_day"),
            GameAction.QuickSave   => Loc.T("action.quick_save"),
            GameAction.Ledger      => Loc.T("action.ledger"),
            GameAction.BuildMode   => Loc.T("action.build_mode"),
            GameAction.BuildRotate => Loc.T("action.build_rotate"),
            GameAction.BuildRemove => Loc.T("action.build_remove"),
            GameAction.Guide       => Loc.T("action.guide"),
            GameAction.DebugConsole => Loc.T("action.debug_console"),
            _                      => action.ToString(),
        };

        /// <summary>Fixed alternates that always work and so cannot be rebound: Num Enter and the arrow keys.</summary>
        private static readonly KeyCode[] FixedAlternateKeys =
        {
            KeyCode.KeypadEnter,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        };

        /// <summary>First and last of the fixed build hotkeys (1-9).</summary>
        private const KeyCode FirstBuildHotkey = KeyCode.Alpha1;
        private const KeyCode LastBuildHotkey  = KeyCode.Alpha9;

        /// <summary>Fixed build-toolbar tool hotkeys (Wall, Window wall, Doorway, Fence, Remove).</summary>
        private static readonly KeyCode[] BuildToolKeys =
        {
            KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.G,
        };

        /// <summary>
        /// Whether a key may be bound. None, Escape (reserved for menus), mouse buttons
        /// (reserved for camera and placement), the fixed alternates (Num Enter, arrows) and
        /// the build hotkeys 1-9 and the build-toolbar keys Z/X/C/V/G are rejected.
        /// </summary>
        public static bool IsBindable(KeyCode key)
        {
            if (key == KeyCode.None || key == KeyCode.Escape) return false;
            if (Array.IndexOf(FixedAlternateKeys, key) >= 0) return false;
            if (key >= FirstBuildHotkey && key <= LastBuildHotkey) return false;
            if (Array.IndexOf(BuildToolKeys, key) >= 0) return false;
            return key < KeyCode.Mouse0 || key > KeyCode.Mouse6;
        }

        // ── Internals ───────────────────────────────────────────────────────────

        private static Dictionary<GameAction, KeyCode> Bindings => _bindings ??= Load();

        private static string StoreKey(GameAction action) => KeyPrefix + action;

        private static Dictionary<GameAction, KeyCode> Load()
        {
            var map = new Dictionary<GameAction, KeyCode>();
            foreach (var action in AllActions)
                map[action] = Parse(_store.Read(StoreKey(action)), Default(action));
            return map;
        }

        /// <summary>Stored KeyCode name, or the fallback if it is missing, unknown or unbindable.</summary>
        private static KeyCode Parse(string stored, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(stored)) return fallback;
            if (!Enum.TryParse(stored, out KeyCode key)) return fallback;
            if (!Enum.IsDefined(typeof(KeyCode), key) || !IsBindable(key)) return fallback;
            return key;
        }

        private static GameAction? FindHolder(KeyCode key, GameAction except)
        {
            foreach (var pair in Bindings)
                if (pair.Key != except && pair.Value == key) return pair.Key;
            return null;
        }

        private static void Assign(GameAction action, KeyCode key)
        {
            Bindings[action] = key;
            _store.Write(StoreKey(action), key.ToString());
        }

        private static float Axis(GameAction positive, KeyCode positiveAlt, GameAction negative, KeyCode negativeAlt)
        {
            float value = 0f;
            if (GetKey(positive) || Input.GetKey(positiveAlt)) value += 1f;
            if (GetKey(negative) || Input.GetKey(negativeAlt)) value -= 1f;
            return value;
        }
    }
}
