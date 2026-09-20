using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>
    /// The screen shown at boot, over the already-generated shop. Starting a game is the
    /// only way past it, so nothing ticks until the player chooses.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        private GameObject _root;
        private Action<bool> _onStart;   // true = continue a save, false = fresh shop

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, bool hasSave, Action<bool> onStart)
        {
            _onStart = onStart;

            _root = UIFactory.Panel("TitleScreen", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.04f, 0.06f, 0.09f, 0.78f));

            // Left-hand title slab, so the shop stays visible on the right
            var slab = UIFactory.Panel("TitleSlab", _root.transform,
                                       new Vector2(0f, 0f), new Vector2(0.44f, 1f),
                                       new Color(0.06f, 0.08f, 0.12f, 0.96f));

            UIFactory.Panel("Accent", slab.transform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(-4f, 0f), Vector2.zero);

            UIFactory.Label("Kicker", slab.transform, "A COSY MANAGEMENT SIM",
                new Vector2(0.12f, 0.74f), new Vector2(0.95f, 0.78f), 15f, UIFactory.Accent);

            UIFactory.Label("Title", slab.transform, "Paws &\nWhiskers",
                new Vector2(0.12f, 0.55f), new Vector2(0.95f, 0.74f), 58f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            UIFactory.Label("Blurb", slab.transform,
                "Run the pet shop on the corner.\n" +
                "Keep the shelves full, raise the animals,\nand make rent.",
                new Vector2(0.12f, 0.40f), new Vector2(0.95f, 0.53f), 17f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);

            float y = 0.30f;
            if (hasSave)
            {
                var cont = UIFactory.Button("Continue", slab.transform, "Continue",
                    new Vector2(0.12f, y), new Vector2(0.72f, y + 0.07f), 20f, UIFactory.ButtonOn);
                cont.onClick.AddListener(() => Start(true));
                y -= 0.09f;
            }

            var fresh = UIFactory.Button("NewGame", slab.transform, hasSave ? "New shop" : "Open the shop",
                new Vector2(0.12f, y), new Vector2(0.72f, y + 0.07f), 20f,
                hasSave ? UIFactory.ButtonBg : UIFactory.ButtonOn);
            fresh.onClick.AddListener(() => Start(false));
            y -= 0.09f;

            var quit = UIFactory.Button("Quit", slab.transform, "Quit",
                new Vector2(0.12f, y), new Vector2(0.72f, y + 0.07f), 18f);
            quit.onClick.AddListener(Application.Quit);

            UIFactory.Label("Hint", slab.transform,
                "WASD move  ·  RMB orbit  ·  E interact  ·  1-4 build  ·  H help",
                new Vector2(0.12f, 0.05f), new Vector2(0.95f, 0.10f), 13f, UIFactory.InkMuted);

            if (hasSave)
                UIFactory.Label("SaveNote", slab.transform, "A saved shop was found.",
                    new Vector2(0.12f, 0.355f), new Vector2(0.95f, 0.39f), 13f, UIFactory.Good);
        }

        private void Start(bool continueSave)
        {
            Hide();
            _onStart?.Invoke(continueSave);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
