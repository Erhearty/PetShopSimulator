using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>Shown when the shop cannot pay its rent. Offers a fresh start.</summary>
    public class GameOverPanel : MonoBehaviour
    {
        private GameObject  _root;
        private TMP_Text    _body;
        private GameManager _game;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("GameOverDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.05f, 0.02f, 0.02f, 0.82f));

            var panel = UIFactory.Panel("GameOver", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-250f, -150f), new Vector2(250f, 150f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Bad, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.Label("Heading", panel.transform, "The shop has closed",
                new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.93f), 26f, UIFactory.Bad);

            _body = UIFactory.Label("Body", panel.transform, "",
                new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.74f), 18f, UIFactory.Ink,
                TextAlignmentOptions.Top);

            var again = UIFactory.Button("Restart", panel.transform, "Start over",
                new Vector2(0.28f, 0.10f), new Vector2(0.72f, 0.26f), 18f, UIFactory.ButtonOn);
            again.onClick.AddListener(() => _game?.RestartGame());

            _root.SetActive(false);
        }

        public void Show(string reason)
        {
            if (_root == null) return;
            _body.text = reason + "\n\nBetter luck next time.";
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            AudioManager.Instance?.PlaySfx("game_over");
        }
    }
}
