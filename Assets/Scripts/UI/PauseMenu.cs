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
        private TMP_Text    _musicLabel;
        private TMP_Text    _status;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game, AudioManager audio)
        {
            _game  = game;
            _audio = audio;

            _root = UIFactory.Panel("PauseDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.03f, 0.05f, 0.08f, 0.72f));

            var panel = UIFactory.Panel("Pause", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-190f, -215f), new Vector2(190f, 215f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.Label("Heading", panel.transform, "Paused",
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f), 28f, UIFactory.Ink);

            _status = UIFactory.Label("Status", panel.transform, "",
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.85f), 15f, UIFactory.InkMuted);

            var resume = UIFactory.Button("Resume", panel.transform, "Resume",
                new Vector2(0.10f, 0.63f), new Vector2(0.90f, 0.72f), 19f, UIFactory.ButtonOn);
            resume.onClick.AddListener(Close);

            var save = UIFactory.Button("Save", panel.transform, "Save game",
                new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.61f), 17f);
            save.onClick.AddListener(() =>
            {
                bool saved = _game.SaveGame();
                if (_status != null) _status.text = saved ? "Saved." : "Save failed — see log.";
            });

            var music = UIFactory.Button("Music", panel.transform, "",
                new Vector2(0.10f, 0.41f), new Vector2(0.90f, 0.50f), 17f);
            _musicLabel = music.GetComponentInChildren<TMP_Text>();
            music.onClick.AddListener(() => { _audio?.ToggleMusic(); RefreshMusicLabel(); });

            var restart = UIFactory.Button("Restart", panel.transform, "Abandon shop & restart",
                new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.39f), 16f);
            restart.onClick.AddListener(() => { Close(); _game.RestartGame(); });

            var quit = UIFactory.Button("Quit", panel.transform, "Save & quit",
                new Vector2(0.10f, 0.13f), new Vector2(0.90f, 0.22f), 17f);
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
            if (_status != null) _status.text = $"Day {_game.Shop.Day} · € {_game.Shop.Balance:N0}";
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

        private void RefreshMusicLabel()
        {
            if (_musicLabel == null) return;
            _musicLabel.text = _audio != null && _audio.IsMusicPlaying ? "Music: on" : "Music: off";
        }
    }
}
