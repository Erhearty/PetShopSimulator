using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>Escape menu. Freezes the clock while it is up.</summary>
    public class PauseMenu : MonoBehaviour
    {
        private GameObject  _root;
        private GameManager _game;
        private AudioManager _audio;
        private SettingsPanel _settings;
        private GuidePanel  _guide;
        private TMP_Text    _musicLabel;
        private TMP_Text    _status;

        // Button column: row i spans [RowTop - i*RowStep - RowHeight, RowTop - i*RowStep].
        private const float RowTop    = 0.74f;
        private const float RowStep   = 0.09f;
        private const float RowHeight = 0.075f;

        private const float PanelHalfWidth  = 190f;
        private const float PanelHalfHeight = 250f;
        private const float RowLeft   = 0.10f;
        private const float RowRight  = 0.90f;

        public bool IsOpen => _root != null && _root.activeSelf;

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

            var panel = UIFactory.Panel("Pause", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-PanelHalfWidth, -PanelHalfHeight),
                                        new Vector2(PanelHalfWidth, PanelHalfHeight));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.Label("Heading", panel.transform, "Paused",
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f), 28f, UIFactory.Ink);

            _status = UIFactory.Label("Status", panel.transform, "",
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.85f), 15f, UIFactory.InkMuted);

            var resume = UIFactory.Button("Resume", panel.transform, "Resume",
                RowMin(0), RowMax(0), 19f, UIFactory.ButtonOn);
            resume.onClick.AddListener(Close);

            var save = UIFactory.Button("Save", panel.transform, "Save game",
                RowMin(1), RowMax(1), 17f);
            save.onClick.AddListener(() =>
            {
                bool saved = _game.SaveGame();
                if (_status != null) _status.text = saved ? "Saved." : "Save failed — see log.";
            });

            var music = UIFactory.Button("Music", panel.transform, "",
                RowMin(2), RowMax(2), 17f);
            _musicLabel = music.GetComponentInChildren<TMP_Text>();
            music.onClick.AddListener(() => { _audio?.ToggleMusic(); RefreshMusicLabel(); });

            var settingsButton = UIFactory.Button("Settings", panel.transform, "Settings",
                RowMin(3), RowMax(3), 17f);
            settingsButton.onClick.AddListener(() => _settings?.Show());

            var guideButton = UIFactory.Button("Guide", panel.transform, "Guide",
                RowMin(4), RowMax(4), 17f);
            guideButton.onClick.AddListener(() => _guide?.Show());

            var restart = UIFactory.Button("Restart", panel.transform, "Abandon shop & restart",
                RowMin(5), RowMax(5), 16f);
            restart.onClick.AddListener(() => { Close(); _game.RestartGame(); });

            var quit = UIFactory.Button("Quit", panel.transform, "Save & quit",
                RowMin(6), RowMax(6), 17f);
            quit.onClick.AddListener(() =>
            {
                if (_game.SaveGame()) { Application.Quit(); return; }
                if (_status != null) _status.text = "Save failed — not quitting.";
            });

            UIFactory.Label("Version", panel.transform, "Esc to resume",
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.10f), 13f, UIFactory.InkMuted,
                TextAlignmentOptions.Center);

            _root.SetActive(false);
        }

        public void Open()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            Time.timeScale = 0f;
            if (_status != null) _status.text = $"Slot {SaveSystem.ActiveSlot} · Day {_game.Shop.Day} · € {_game.Shop.Balance:N0}";
            RefreshMusicLabel();
        }

        public void Close()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
            Time.timeScale = 1f;
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
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
