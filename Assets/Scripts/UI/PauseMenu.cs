using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>Escape menu. Freezes the clock while it is up.</summary>
    public class PauseMenu : MonoBehaviour
    {
        /// <summary>Label of the destructive restart button before it is armed.</summary>
        public const string AbandonLabel        = "Abandon shop";
        /// <summary>Label of the destructive restart button once armed: a second press confirms.</summary>
        public const string AbandonConfirmLabel = "Press again to abandon";
        /// <summary>Status line shown while the abandon button is armed.</summary>
        public const string AbandonWarning      = "Unsaved progress will be lost. Esc to cancel.";

        private GameObject  _root;
        private GameManager _game;
        private AudioManager _audio;
        private SettingsPanel _settings;
        private GuidePanel  _guide;
        private TMP_Text    _musicLabel;
        private TMP_Text    _status;
        private TMP_Text    _abandonLabel;
        private Button      _resume;

        // Button column: row i spans [RowTop - i*RowStep - RowHeight, RowTop - i*RowStep].
        private const float RowTop    = 0.74f;
        private const float RowStep   = 0.09f;
        private const float RowHeight = 0.075f;

        private const float PanelHalfWidth  = 190f;
        private const float PanelHalfHeight = 250f;
        private const float RowLeft   = 0.10f;
        private const float RowRight  = 0.90f;

        /// <summary>True while the menu is showing.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>True while the abandon button is armed, waiting for its confirming press.</summary>
        public bool IsConfirmingAbandon { get; private set; }

        /// <summary>
        /// Builds the hidden menu; <paramref name="settings"/> is opened by its Settings button and
        /// <paramref name="guide"/> by its Guide button.
        /// </summary>
        public void Build(Transform canvas, GameManager game, AudioManager audio, SettingsPanel settings = null,
                          GuidePanel guide = null)
        {
            _game     = game;
            _audio    = audio;
            _settings = settings;
            _guide    = guide;

            _root = UIFactory.Panel("PauseDim", canvas, Vector2.zero, Vector2.one, UIFactory.Dim);

            var panel = UIFactory.ModalPanel("Pause", _root.transform,
                                             new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                             UIFactory.Surface,
                                             new Vector2(-PanelHalfWidth, -PanelHalfHeight),
                                             new Vector2(PanelHalfWidth, PanelHalfHeight));

            UIFactory.Header(panel.transform, "Paused", new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f));

            _status = UIFactory.Label("Status", panel.transform, "",
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.85f), UIFactory.TextSmall, UIFactory.InkMuted);

            BuildButtons(panel.transform);

            UIFactory.Label("Version", panel.transform, "Esc to resume",
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.10f), UIFactory.TextSmall, UIFactory.InkMuted,
                TextAlignmentOptions.Center);

            _root.SetActive(false);
        }

        /// <summary>The button column: Resume (primary), utilities, then Abandon (destructive) and Quit.</summary>
        private void BuildButtons(Transform panel)
        {
            _resume = Row(panel, "Resume", "Resume", 0, UIFactory.Accent);
            _resume.onClick.AddListener(Close);

            Row(panel, "Save", "Save game", 1).onClick.AddListener(() =>
            {
                bool saved = _game.SaveGame();
                if (_status != null) _status.text = saved ? "Saved." : "Save failed — see log.";
            });

            var music = Row(panel, "Music", "", 2);
            _musicLabel = music.GetComponentInChildren<TMP_Text>();
            music.onClick.AddListener(() => { _audio?.ToggleMusic(); RefreshMusicLabel(); });

            Row(panel, "Settings", "Settings", 3).onClick.AddListener(() => _settings?.Show());
            Row(panel, "Guide", "Guide", 4).onClick.AddListener(() => _guide?.Show());

            var restart = Row(panel, "Restart", AbandonLabel, 5, UIFactory.DestructiveFill);
            _abandonLabel = restart.GetComponentInChildren<TMP_Text>();
            restart.onClick.AddListener(PressAbandon);

            Row(panel, "Quit", "Save and quit", 6).onClick.AddListener(() =>
            {
                if (_game.SaveGame()) { Application.Quit(); return; }
                if (_status != null) _status.text = "Save failed — not quitting.";
            });
        }

        private static Button Row(Transform panel, string name, string text, int row, Color? fill = null) =>
            UIFactory.Button(name, panel, text, RowMin(row), RowMax(row), UIFactory.TextBody, fill);

        /// <summary>First press arms the button; the second abandons the shop and restarts.</summary>
        public void PressAbandon()
        {
            if (!IsConfirmingAbandon)
            {
                IsConfirmingAbandon = true;
                if (_abandonLabel != null) _abandonLabel.text = AbandonConfirmLabel;
                if (_status != null) _status.text = AbandonWarning;
                return;
            }
            CancelAbandon();
            Close();
            _game.RestartGame();
        }

        /// <summary>Disarms the abandon button. Returns true if it was armed.</summary>
        public bool CancelAbandon()
        {
            if (!IsConfirmingAbandon) return false;
            IsConfirmingAbandon = false;
            if (_abandonLabel != null) _abandonLabel.text = AbandonLabel;
            if (_status != null) _status.text = StatusLine();
            return true;
        }

        /// <summary>Shows the menu, freezes time and selects Resume for keyboard navigation.</summary>
        public void Open()
        {
            if (_root == null) return;
            CancelAbandon();
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            Time.timeScale = 0f;
            if (_status != null) _status.text = StatusLine();
            RefreshMusicLabel();
            if (_resume != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
        }

        /// <summary>Hides the menu and resumes time.</summary>
        public void Close()
        {
            if (_root == null) return;
            CancelAbandon();
            _root.SetActive(false);
            _game?.SetModalOpen(false);
            Time.timeScale = 1f;
        }

        /// <summary>Esc: cancels an armed abandon first, otherwise opens or closes the menu.</summary>
        public void Toggle()
        {
            if (IsOpen && CancelAbandon()) return;
            if (IsOpen) Close(); else Open();
        }

        /// <summary>The slot / day / balance line, or empty when there is no shop to describe.</summary>
        private string StatusLine()
        {
            if (_game == null || _game.Shop == null) return string.Empty;
            return $"Slot {SaveSystem.ActiveSlot} · Day {_game.Shop.Day} · € {_game.Shop.Balance:N0}";
        }

        private static Vector2 RowMin(int row) => new(RowLeft, RowTop - row * RowStep - RowHeight);

        private static Vector2 RowMax(int row) => new(RowRight, RowTop - row * RowStep);

        private void RefreshMusicLabel()
        {
            if (_musicLabel == null) return;
            _musicLabel.text = _audio != null && _audio.IsMusicPlaying ? "Music: on" : "Music: off";
        }
    }
}
