using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PetShop.Core;
using PetShop.Localization;

namespace PetShop.UI
{
    /// <summary>
    /// The settings screen: a "Controls" list where every <see cref="GameAction"/> can be
    /// rebound, and an empty "General" section for later options. Opened from the pause menu
    /// or the title screen, and drawn over whichever of them opened it.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const string EscapeKeyLabel = "Esc";
        private const string CaptureMarker = "…";
        private const float  HeaderFont    = 24f;
        private const float  SectionFont   = 17f;
        private const float  RowFont       = 15f;
        private const float  StatusFont    = 15f;
        private const float  RowFill       = 0.88f;
        private const float  GeneralRowHeight = 0.09f;
        private const float  GeneralRowGap    = 0.01f;

        private static readonly Vector2 Centre        = new(0.5f, 0.5f);
        private static readonly Vector2 PanelHalfSize = new(450f, 320f);
        private static readonly Color   DimColor      = UIFactory.Dim;
        private static readonly KeyCode[] AllKeys     = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        private GameObject  _root;
        private GameManager _game;
        private GameUI      _ui;
        private TMP_Text    _status;
        private Button      _resetButton;
        private Button      _closeButton;
        private Button      _autosaveButton;
        private TMP_Text    _autosaveLabel;
        private Button      _trackerButton;
        private TMP_Text    _trackerLabel;
        private Button      _motionButton;
        private TMP_Text    _motionLabel;
        private Button      _languageButton;
        private TMP_Text    _languageLabel;
        private readonly Dictionary<GameAction, TMP_Text> _nameLabels = new();

        private readonly Dictionary<GameAction, TMP_Text> _keyLabels = new();
        private readonly List<Button> _rebindButtons = new();

        private GameAction? _capturing;
        private Selectable _pendingSelect;
        private int  _captureStartFrame = -1;
        private int  _captureEndFrame   = -1;
        private bool _previousModal;
        private bool _openedOverTitle;

        /// <summary>True while the panel is visible.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>
        /// True while waiting for a key, and for the rest of the frame a capture ended in, so
        /// the Escape that cancelled a capture is not also read as "close the panel".
        /// </summary>
        public bool IsCapturing => _capturing.HasValue || _captureEndFrame == Time.frameCount;

        /// <summary>Container for general (non-controls) options; empty for now, filled by later features.</summary>
        public Transform GeneralSection { get; private set; }

        /// <summary>Builds the (hidden) panel under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("SettingsDim", canvas, Vector2.zero, Vector2.one, DimColor);
            var panel = UIFactory.ModalPanel("Settings", _root.transform, Centre, Centre,
                                        UIFactory.PanelBg, -PanelHalfSize, PanelHalfSize).transform;

            BuildChrome(panel);
            BuildControls(panel);
            BuildGeneral(panel);
            WireNavigation();
            Loc.LanguageChanged += OnLanguageChanged;

            _root.SetActive(false);
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Re-reads every text built from code rather than a bound key.</summary>
        private void OnLanguageChanged()
        {
            foreach (var pair in _nameLabels) pair.Value.text = InputBindings.ActionName(pair.Key);
            RefreshAutosaveLabel();
            RefreshTrackerLabel();
            RefreshMotionLabel();
            RefreshLanguageLabel();
        }

        /// <summary>Opens the panel on top of everything and selects the first rebind button.</summary>
        public void Show()
        {
            if (_root == null || IsOpen) return;
            if (_ui == null) _ui = FindAnyObjectByType<GameUI>();

            // GameUI ignores keys while the title screen is up, so the panel handles its own Escape there.
            _openedOverTitle = _ui != null && _ui.Title != null && _ui.Title.IsOpen;
            _previousModal   = _game != null && _game.IsModalOpen;
            _game?.SetModalOpen(true);

            _capturing   = null;
            _status.text = "";
            RefreshKeys();
            RefreshAutosaveLabel();
            RefreshTrackerLabel();
            RefreshMotionLabel();
            RefreshLanguageLabel();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            SelectFirstRow();
        }

        /// <summary>Closes the panel and puts the modal state back as it was before Show.</summary>
        public void Hide()
        {
            if (_root == null || !IsOpen) return;
            _capturing     = null;
            _pendingSelect = null;
            SetRowsInteractable(true);
            _root.SetActive(false);
            _game?.SetModalOpen(_previousModal);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        // ── Construction ────────────────────────────────────────────────────────

        /// <summary>Accent strip, header, close button, status line and reset button.</summary>
        private void BuildChrome(Transform panel)
        {
            UIFactory.Localize(UIFactory.Header(panel, "", new Vector2(0.03f, 0.90f), new Vector2(0.5f, 0.97f)),
                               "settings.title");

            _closeButton = UIFactory.ButtonKey("Close", panel, "common.close_esc",
                new Vector2(0.79f, 0.905f), new Vector2(0.97f, 0.965f), RowFont, null, EscapeKeyLabel);
            _closeButton.onClick.AddListener(Hide);

            _status = UIFactory.Label("Status", panel, "",
                new Vector2(0.03f, 0.02f), new Vector2(0.70f, 0.09f), StatusFont, UIFactory.Accent);

            _resetButton = UIFactory.ButtonKey("Reset", panel, "settings.reset",
                new Vector2(0.72f, 0.025f), new Vector2(0.97f, 0.09f), RowFont);
            _resetButton.onClick.AddListener(ResetAll);
        }

        /// <summary>The "Controls" section: one row per action.</summary>
        private void BuildControls(Transform panel)
        {
            UIFactory.LabelKey("ControlsHeader", panel, "settings.controls",
                new Vector2(0.03f, 0.83f), new Vector2(0.60f, 0.89f), SectionFont, UIFactory.Ink);

            var rows = UIFactory.Node("Controls", panel,
                                      new Vector2(0.03f, 0.11f), new Vector2(0.60f, 0.83f)).transform;

            var actions = InputBindings.AllActions;
            float rowHeight = 1f / actions.Length;
            for (int i = 0; i < actions.Length; i++)
            {
                float top = 1f - i * rowHeight;
                BuildRow(rows, actions[i], top - rowHeight * RowFill, top);
            }
        }

        /// <summary>Action name, current key and a Rebind button, between <paramref name="bottom"/> and <paramref name="top"/>.</summary>
        private void BuildRow(Transform rows, GameAction action, float bottom, float top)
        {
            _nameLabels[action] = UIFactory.Label($"Name_{action}", rows, InputBindings.ActionName(action),
                new Vector2(0f, bottom), new Vector2(0.52f, top), RowFont, UIFactory.InkMuted);
            UIFactory.AutoFit(_nameLabels[action]);

            _keyLabels[action] = UIFactory.Label($"Key_{action}", rows, "",
                new Vector2(0.52f, bottom), new Vector2(0.74f, top), RowFont, UIFactory.Ink,
                TextAlignmentOptions.Center);

            var rebind = UIFactory.ButtonKey($"Rebind_{action}", rows, "settings.rebind",
                new Vector2(0.76f, bottom), new Vector2(1f, top), RowFont);
            rebind.onClick.AddListener(() => BeginCapture(action));
            _rebindButtons.Add(rebind);
        }

        /// <summary>The "General" section: the autosave toggle; further options parent to <see cref="GeneralSection"/>.</summary>
        private void BuildGeneral(Transform panel)
        {
            UIFactory.LabelKey("GeneralHeader", panel, "settings.general",
                new Vector2(0.64f, 0.83f), new Vector2(0.97f, 0.89f), SectionFont, UIFactory.Ink);

            GeneralSection = UIFactory.Node("General", panel,
                                            new Vector2(0.64f, 0.11f), new Vector2(0.97f, 0.83f)).transform;

            _autosaveButton = UIFactory.Button("AutosaveMorning", GeneralSection, "",
                new Vector2(0f, 1f - GeneralRowHeight), Vector2.one, RowFont);
            _autosaveLabel = _autosaveButton.GetComponentInChildren<TMP_Text>();
            _autosaveButton.onClick.AddListener(ToggleAutosave);
            RefreshAutosaveLabel();
            BuildTrackerToggle();
            BuildMotionToggle();
            BuildLanguageToggle();
        }

        /// <summary>The language switcher, three rows under the autosave toggle: cycles English ↔ Ukrainian.</summary>
        private void BuildLanguageToggle()
        {
            const int rowIndex = 3;
            float top = 1f - rowIndex * (GeneralRowHeight + GeneralRowGap);
            _languageButton = UIFactory.Button("Language", GeneralSection, "",
                new Vector2(0f, top - GeneralRowHeight), new Vector2(1f, top), RowFont);
            _languageLabel = _languageButton.GetComponentInChildren<TMP_Text>();
            _languageButton.onClick.AddListener(ToggleLanguage);
            RefreshLanguageLabel();
        }

        /// <summary>Switches to the next language; the whole open UI re-reads its text and the choice is saved.</summary>
        private void ToggleLanguage()
        {
            var next = Loc.Current == Language.En ? Language.Uk : Language.En;
            Loc.SetLanguage(next);
            RefreshLanguageLabel();
            _status.text = Loc.F("settings.language.changed", LanguageName(next));
        }

        /// <summary>Shows the current language, in its own name, on its button.</summary>
        private void RefreshLanguageLabel()
        {
            if (_languageLabel == null) return;
            _languageLabel.text = Loc.F("settings.language", LanguageName(Loc.Current));
        }

        private static string LanguageName(Language language) =>
            Loc.T(language == Language.Uk ? "common.language.uk" : "common.language.en");

        private static string OnOff(bool on) => Loc.T(on ? "common.on" : "common.off");

        /// <summary>The "Reduce motion" toggle, two rows under the autosave toggle.</summary>
        private void BuildMotionToggle()
        {
            const int rowIndex = 2;
            float top = 1f - rowIndex * (GeneralRowHeight + GeneralRowGap);
            _motionButton = UIFactory.Button("ReduceMotion", GeneralSection, "",
                new Vector2(0f, top - GeneralRowHeight), new Vector2(1f, top), RowFont);
            _motionLabel = _motionButton.GetComponentInChildren<TMP_Text>();
            _motionButton.onClick.AddListener(ToggleMotion);
            RefreshMotionLabel();
        }

        /// <summary>Flips the reduce-motion setting (pops, bubble pops, counter ticks) and reports it.</summary>
        private void ToggleMotion()
        {
            GameSettings.ReduceMotion = !GameSettings.ReduceMotion;
            RefreshMotionLabel();
            _status.text = Loc.T(GameSettings.ReduceMotion ? "settings.reduce_motion.on" : "settings.reduce_motion.off");
        }

        /// <summary>Shows the current reduce-motion setting on its button.</summary>
        private void RefreshMotionLabel()
        {
            if (_motionLabel == null) return;
            _motionLabel.text = Loc.F("settings.reduce_motion", OnOff(GameSettings.ReduceMotion));
        }

        /// <summary>The "Show quest tracker" toggle, one row under the autosave toggle.</summary>
        private void BuildTrackerToggle()
        {
            float top = 1f - GeneralRowHeight - GeneralRowGap;
            _trackerButton = UIFactory.Button("QuestTracker", GeneralSection, "",
                new Vector2(0f, top - GeneralRowHeight), new Vector2(1f, top), RowFont);
            _trackerLabel = _trackerButton.GetComponentInChildren<TMP_Text>();
            _trackerButton.onClick.AddListener(ToggleTracker);
            RefreshTrackerLabel();
        }

        /// <summary>Flips the quest tracker setting, applies it to the HUD and reports it.</summary>
        private void ToggleTracker()
        {
            GameSettings.ShowQuestTracker = !GameSettings.ShowQuestTracker;
            RefreshTrackerLabel();
            if (_ui == null) _ui = FindAnyObjectByType<GameUI>();
            if (_ui != null && _ui.HUD != null && _ui.HUD.Tracker != null) _ui.HUD.Tracker.Refresh();
            _status.text = Loc.T(GameSettings.ShowQuestTracker ? "settings.tracker.on" : "settings.tracker.off");
        }

        /// <summary>Shows the current quest tracker setting on its button.</summary>
        private void RefreshTrackerLabel()
        {
            if (_trackerLabel == null) return;
            _trackerLabel.text = Loc.F("settings.tracker", OnOff(GameSettings.ShowQuestTracker));
        }

        /// <summary>Flips the morning autosave setting and reports it.</summary>
        private void ToggleAutosave()
        {
            GameSettings.AutosaveEachMorning = !GameSettings.AutosaveEachMorning;
            RefreshAutosaveLabel();
            _status.text = Loc.T(GameSettings.AutosaveEachMorning ? "settings.autosave.on" : "settings.autosave.off");
        }

        /// <summary>Shows the current morning autosave setting on its button.</summary>
        private void RefreshAutosaveLabel()
        {
            if (_autosaveLabel == null) return;
            _autosaveLabel.text = Loc.F("settings.autosave", OnOff(GameSettings.AutosaveEachMorning));
        }

        /// <summary>Explicit up/down navigation: rebind rows top to bottom, then the autosave and quest tracker toggles, Reset, Close.</summary>
        private void WireNavigation()
        {
            var chain = new List<Selectable>(_rebindButtons) { _autosaveButton, _trackerButton, _motionButton, _languageButton, _resetButton, _closeButton };
            for (int i = 0; i < chain.Count; i++)
            {
                chain[i].navigation = new Navigation
                {
                    mode         = Navigation.Mode.Explicit,
                    selectOnUp   = i > 0 ? chain[i - 1] : null,
                    selectOnDown = i < chain.Count - 1 ? chain[i + 1] : null,
                };
            }
        }

        // ── Runtime ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsOpen) return;
            if (_capturing.HasValue) { PollCapture(); return; }
            // Reselect a frame after the capture ended, so the Submit key that was just bound
            // is not also read by the EventSystem as a click on the reselected button.
            if (_pendingSelect != null && Time.frameCount > _captureEndFrame)
            {
                EventSystem.current?.SetSelectedGameObject(_pendingSelect.gameObject);
                _pendingSelect = null;
            }
            if (_openedOverTitle && Input.GetKeyDown(KeyCode.Escape)) Hide();
        }

        /// <summary>Starts waiting for the next key for <paramref name="action"/>.</summary>
        private void BeginCapture(GameAction action)
        {
            RefreshKeys();
            _capturing         = action;
            _captureStartFrame = Time.frameCount;
            _pendingSelect     = null;
            // With nothing selected and the buttons disabled, Submit (Space/Enter) cannot
            // re-click Rebind, so those keys reach PollCapture and can be bound.
            EventSystem.current?.SetSelectedGameObject(null);
            SetRowsInteractable(false);
            _status.text       = Loc.F("settings.capture_prompt", EscapeKeyLabel);
            _keyLabels[action].text = CaptureMarker;
        }

        /// <summary>Escape cancels; otherwise the first bindable key pressed this frame is taken.</summary>
        private void PollCapture()
        {
            // The Enter/Space that pressed the Rebind button must not become the new binding.
            if (Time.frameCount == _captureStartFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { EndCapture(Loc.T("settings.rebind_cancelled")); return; }

            foreach (var key in AllKeys)
            {
                if (!InputBindings.IsBindable(key) || !Input.GetKeyDown(key)) continue;
                Apply(_capturing.Value, key);
                return;
            }
        }

        /// <summary>Binds the key, swapping with whichever action held it, and reports the swap.</summary>
        private void Apply(GameAction action, KeyCode key)
        {
            GameAction? displaced = InputBindings.Set(action, key);
            string message = Loc.F("settings.bound", InputBindings.ActionName(action), InputBindings.KeyLabel(key));
            if (displaced.HasValue)
                message += " " + Loc.F("settings.bound_moved", InputBindings.ActionName(displaced.Value),
                                       InputBindings.Label(displaced.Value));
            EndCapture(message);
            OnBindingsChanged();
        }

        private void EndCapture(string status)
        {
            int row = Array.IndexOf(InputBindings.AllActions, _capturing.Value);
            SetRowsInteractable(true);
            _pendingSelect   = row >= 0 && row < _rebindButtons.Count ? _rebindButtons[row] : null;
            _capturing       = null;
            _captureEndFrame = Time.frameCount;
            _status.text     = status;
            RefreshKeys();
        }

        private void ResetAll()
        {
            _capturing = null;
            InputBindings.ResetAll();
            _status.text = Loc.T("settings.controls_reset");
            OnBindingsChanged();
        }

        /// <summary>Updates the key column and the HUD's cheat-sheet.</summary>
        private void OnBindingsChanged()
        {
            RefreshKeys();
            if (_ui == null) _ui = FindAnyObjectByType<GameUI>();
            if (_ui != null && _ui.Title != null) _ui.Title.RefreshKeyHints();
        }

        /// <summary>Enables or disables the Rebind buttons and Reset while a key is being captured.</summary>
        private void SetRowsInteractable(bool interactable)
        {
            foreach (var button in _rebindButtons) button.interactable = interactable;
            if (_autosaveButton != null) _autosaveButton.interactable = interactable;
            if (_trackerButton != null) _trackerButton.interactable = interactable;
            if (_motionButton  != null) _motionButton.interactable  = interactable;
            if (_languageButton != null) _languageButton.interactable = interactable;
            if (_resetButton != null) _resetButton.interactable = interactable;
        }

        private void RefreshKeys()
        {
            foreach (var pair in _keyLabels)
                pair.Value.text = InputBindings.Label(pair.Key);
        }

        private void SelectFirstRow()
        {
            if (EventSystem.current == null || _rebindButtons.Count == 0) return;
            EventSystem.current.SetSelectedGameObject(_rebindButtons[0].gameObject);
        }
    }
}
