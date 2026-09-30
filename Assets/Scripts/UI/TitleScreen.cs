using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The screen shown at boot, over the already-generated shop. Starting a game in one of the
    /// save slots is the only way past it, so nothing ticks until the player chooses.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        private const string OverwritePrompt = "Overwrite?";
        private const string NewShopText     = "New shop";
        private const string ContinueText    = "Continue";

        // Slot blocks stack downwards from SlotTop, one SlotStep apart: a caption line, then buttons.
        private const float SlotTop       = 0.385f;
        private const float SlotStep      = 0.078f;
        private const float CaptionHeight = 0.03f;
        private const float ButtonGap     = 0.004f;
        private const float ButtonHeight  = 0.04f;
        private const float FooterBottom  = 0.08f;

        private const float LeftX        = 0.12f;
        private const float MidLeftX     = 0.40f;
        private const float MidRightX    = 0.44f;
        private const float ButtonRightX = 0.72f;
        private const float TextRightX   = 0.95f;

        private const float SlotFont    = 15f;
        private const float ButtonFont  = 17f;
        private const float HintFont    = 13f;

        private GameObject _root;
        private Action<int, bool> _onStart;   // slot, true = continue that slot's save
        private TMP_Text _hint;
        private readonly List<Selectable[]> _navRows = new();

        /// <summary>True while the title screen is visible.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>
        /// Builds and shows the title screen. <paramref name="onStart"/> receives the chosen slot and
        /// whether to continue it (false = fresh shop); <paramref name="onSettings"/> runs when Settings is clicked.
        /// </summary>
        public void Build(Transform canvas, Action<int, bool> onStart, Action onSettings = null)
        {
            _onStart = onStart;
            _navRows.Clear();

            _root = UIFactory.Panel("TitleScreen", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.04f, 0.06f, 0.09f, 0.78f));
            var slab = BuildSlab();

            for (int slot = 1; slot <= SaveSystem.SlotCount; slot++)
                BuildSlotRow(slab, slot, SlotTop - (slot - 1) * SlotStep);
            BuildFooter(slab, onSettings);

            _hint = UIFactory.Label("Hint", slab, "",
                new Vector2(LeftX, 0.015f), new Vector2(TextRightX, 0.065f), HintFont, UIFactory.InkMuted);
            RefreshKeyHints();

            WireNavigation();
            SelectFirst();
        }

        /// <summary>Left-hand title slab with the heading and blurb, so the shop stays visible on the right.</summary>
        private Transform BuildSlab()
        {
            var slab = UIFactory.Panel("TitleSlab", _root.transform,
                                       new Vector2(0f, 0f), new Vector2(0.44f, 1f),
                                       new Color(0.06f, 0.08f, 0.12f, 0.96f)).transform;

            UIFactory.Panel("Accent", slab, new Vector2(1f, 0f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(-4f, 0f), Vector2.zero);

            UIFactory.Label("Kicker", slab, "A COSY MANAGEMENT SIM",
                new Vector2(LeftX, 0.74f), new Vector2(TextRightX, 0.78f), 15f, UIFactory.Accent);

            UIFactory.Label("Title", slab, "Paws &\nWhiskers",
                new Vector2(LeftX, 0.55f), new Vector2(TextRightX, 0.74f), 58f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            UIFactory.Label("Blurb", slab,
                "Run the pet shop on the corner.\n" +
                "Keep the shelves full, raise the animals,\nand make rent.",
                new Vector2(LeftX, 0.40f), new Vector2(TextRightX, 0.53f), 17f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);
            return slab;
        }

        /// <summary>Caption plus Continue (occupied slots only) and New shop buttons for <paramref name="slot"/>.</summary>
        private void BuildSlotRow(Transform slab, int slot, float top)
        {
            var summary = SaveSystem.Peek(slot);
            UIFactory.Label($"Slot{slot}", slab, SlotCaption(slot, summary),
                new Vector2(LeftX, top - CaptionHeight), new Vector2(TextRightX, top), SlotFont,
                summary != null ? UIFactory.Ink : UIFactory.InkMuted);

            float buttonTop = top - CaptionHeight - ButtonGap;
            var min = new Vector2(LeftX, buttonTop - ButtonHeight);
            var row = new List<Selectable>();
            if (summary != null)
            {
                var cont = UIFactory.Button($"Continue{slot}", slab, ContinueText,
                    min, new Vector2(MidLeftX, buttonTop), ButtonFont, UIFactory.ButtonOn);
                cont.onClick.AddListener(() => Start(slot, true));
                row.Add(cont);
                min.x = MidRightX;
            }
            row.Add(BuildNewShopButton(slab, slot, summary != null, min, new Vector2(ButtonRightX, buttonTop)));
            _navRows.Add(row.ToArray());
        }

        /// <summary>A New shop button; on an occupied slot the first click only asks "Overwrite?".</summary>
        private Button BuildNewShopButton(Transform slab, int slot, bool occupied, Vector2 min, Vector2 max)
        {
            var button = UIFactory.Button($"NewGame{slot}", slab, NewShopText, min, max, ButtonFont,
                                          occupied ? UIFactory.ButtonBg : UIFactory.ButtonOn);
            var label = button.GetComponentInChildren<TMP_Text>();
            bool armed = false;
            button.onClick.AddListener(() =>
            {
                if (occupied && !armed)
                {
                    armed = true;
                    label.text = OverwritePrompt;
                    return;
                }
                Start(slot, false);
            });
            return button;
        }

        /// <summary>Quit and Settings, side by side under the slots.</summary>
        private void BuildFooter(Transform slab, Action onSettings)
        {
            var min = new Vector2(LeftX, FooterBottom);
            var quit = UIFactory.Button("Quit", slab, "Quit",
                min, new Vector2(MidLeftX, FooterBottom + ButtonHeight), ButtonFont);
            quit.onClick.AddListener(Application.Quit);

            var settings = UIFactory.Button("Settings", slab, "Settings",
                new Vector2(MidRightX, FooterBottom), new Vector2(ButtonRightX, FooterBottom + ButtonHeight), ButtonFont);
            settings.onClick.AddListener(() => onSettings?.Invoke());
            _navRows.Add(new Selectable[] { quit, settings });
        }

        /// <summary>"Slot i — Day N · €balance · saved date", or "Slot i — empty".</summary>
        private static string SlotCaption(int slot, SlotSummary summary)
        {
            if (summary == null) return $"Slot {slot} — empty";
            return $"Slot {slot} — Day {summary.Day} · €{summary.Balance:N0} · saved {FormatSavedAt(summary.SavedAt)}";
        }

        /// <summary>The save time in local time, or the raw string when it does not parse.</summary>
        private static string FormatSavedAt(string savedAt)
        {
            if (DateTime.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
                return when.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return savedAt ?? "";
        }

        /// <summary>Explicit navigation: left/right within a row, up/down to the nearest button of the adjacent row.</summary>
        private void WireNavigation()
        {
            for (int r = 0; r < _navRows.Count; r++)
            {
                var row = _navRows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    row[c].navigation = new Navigation
                    {
                        mode          = Navigation.Mode.Explicit,
                        selectOnLeft  = c > 0 ? row[c - 1] : null,
                        selectOnRight = c < row.Length - 1 ? row[c + 1] : null,
                        selectOnUp    = r > 0 ? Pick(_navRows[r - 1], c) : null,
                        selectOnDown  = r < _navRows.Count - 1 ? Pick(_navRows[r + 1], c) : null,
                    };
                }
            }
        }

        /// <summary>The button in <paramref name="row"/> at <paramref name="column"/>, or its last one.</summary>
        private static Selectable Pick(Selectable[] row, int column) => row[Mathf.Min(column, row.Length - 1)];

        /// <summary>Gives keyboard focus to the first slot button.</summary>
        private void SelectFirst()
        {
            if (EventSystem.current == null || _navRows.Count == 0) return;
            EventSystem.current.SetSelectedGameObject(_navRows[0][0].gameObject);
        }

        /// <summary>Rewrites the controls hint from the current key bindings.</summary>
        public void RefreshKeyHints()
        {
            if (_hint == null) return;
            _hint.text = $"{InputBindings.MoveLabel()} move  ·  RMB orbit  ·  " +
                         $"{InputBindings.Label(GameAction.Interact)} interact  ·  " +
                         $"1-{BuildCatalog.HotkeyOrder.Length} build  ·  {InputBindings.Label(GameAction.Help)} help";
        }

        private void Start(int slot, bool continueSave)
        {
            Hide();
            _onStart?.Invoke(slot, continueSave);
        }

        /// <summary>Hides the title screen.</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
