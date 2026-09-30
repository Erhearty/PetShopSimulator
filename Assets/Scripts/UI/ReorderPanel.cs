using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>
    /// Auto-reorder settings: one row per product category with an on/off toggle, a stock
    /// threshold, an order size and the estimated cost of one order. Every change is written
    /// straight to <see cref="GameManager.AutoReorder"/>.
    ///
    /// Opened from the ledger, which hides itself (releasing the modal flag) first, so this
    /// panel owns the flag while it is open, the same way the staff board does.
    /// </summary>
    public class ReorderPanel : MonoBehaviour
    {
        public const int ThresholdStep = 2;
        public const int ThresholdMin  = 0;
        public const int ThresholdMax  = 100;
        public const int UnitsStep     = 4;
        public const int UnitsMin      = 4;
        public const int UnitsMax      = 48;

        private const float RowsTop    = 0.78f;
        private const float RowsBottom = 0.05f;

        /// <summary>Toggle-on fill: dark enough for white-ish text at ≥ 4.5:1.</summary>
        private static readonly Color ToggleOn = new Color(0.10f, 0.38f, 0.22f, 0.98f);

        private class Row
        {
            public ReorderRule Rule;
            public Image       Toggle;
            public TMP_Text    ToggleLabel, Threshold, Units, Cost;
        }

        private GameObject  _root;
        private GameManager _game;
        private readonly List<Row> _rows = new List<Row>();

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _root = UIFactory.Panel("ReorderDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.03f, 0.05f, 0.08f, 0.62f));
            var panel = UIFactory.Panel("Reorder", _root.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), UIFactory.PanelBg,
                new Vector2(-420f, -260f), new Vector2(420f, 260f));
            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);
            UIFactory.Label("Header", panel.transform, "Auto-reorder",
                new Vector2(0.03f, 0.89f), new Vector2(0.76f, 0.97f), 24f, UIFactory.Ink);
            UIFactory.Button("Close", panel.transform, "Close",
                new Vector2(0.78f, 0.895f), new Vector2(0.97f, 0.965f), 15f).onClick.AddListener(Hide);
            BuildHeadings(panel.transform);
            BuildRows(panel.transform);
            _root.SetActive(false);
        }

        public void Show()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
            _game?.SetModalOpen(true);
        }

        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        /// <summary>The ledger's way in: a button in slot <paramref name="x"/> of the order row.</summary>
        internal static GameObject OpenButton(Transform parent, float x, float width, Action onClick)
        {
            var btn = UIFactory.Button("AutoReorder", parent, "Auto-reorder…",
                new Vector2(x, 0.015f), new Vector2(x + width, 0.075f), 14f, UIFactory.ButtonOn);
            btn.onClick.AddListener(() => onClick());
            return btn.gameObject;
        }

        /// <summary>Cost of ordering <paramref name="units"/> of a category; matches the runner's estimate.</summary>
        internal static float EstimateCost(GameManager game, ProductCategory category, int units)
        {
            float unit = game.Catalog != null ? game.Catalog.AverageUnitCost(category)
                                              : AutoReorderRunner.FallbackUnitCost;
            return unit * ShopManager.WholesaleDiscount * units;
        }

        internal static int Step(int value, int delta, int step, int min, int max) =>
            Mathf.Clamp(value + delta * step, min, max);

        // ── Layout ──────────────────────────────────────────────────────────────

        private static void BuildHeadings(Transform panel)
        {
            Heading(panel, "Aisle",             0.02f, 0.16f);
            Heading(panel, "Auto",              0.17f, 0.28f);
            Heading(panel, "Order when below",  0.30f, 0.52f);
            Heading(panel, "Order size",        0.54f, 0.77f);
            Heading(panel, "Cost per order",    0.79f, 0.98f);
        }

        private static void Heading(Transform panel, string text, float x0, float x1) =>
            UIFactory.Label($"Head_{text}", panel, text, new Vector2(x0, 0.80f),
                new Vector2(x1, 0.86f), 13f, UIFactory.InkMuted, TextAlignmentOptions.Center);

        private void BuildRows(Transform panel)
        {
            var categories = (ProductCategory[])Enum.GetValues(typeof(ProductCategory));
            float h = (RowsTop - RowsBottom) / categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                float top = RowsTop - i * h;
                _rows.Add(BuildRow(panel, categories[i], top - h * 0.9f, top));
            }
        }

        private Row BuildRow(Transform panel, ProductCategory category, float y0, float y1)
        {
            var row = new Row { Rule = _game.AutoReorder.Rule(category) };
            UIFactory.Label($"Name_{category}", panel, category.ToString(),
                new Vector2(0.02f, y0), new Vector2(0.16f, y1), 16f);

            var toggle = UIFactory.Button($"Toggle_{category}", panel, "", new Vector2(0.17f, y0),
                new Vector2(0.28f, y1), 15f);
            row.Toggle      = toggle.GetComponent<Image>();
            row.ToggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            toggle.onClick.AddListener(() => { row.Rule.Enabled = !row.Rule.Enabled; Refresh(); });

            row.Threshold = Stepper(panel, category, "Th", 0.30f, y0, y1, ThresholdStep, ThresholdMin,
                ThresholdMax, () => row.Rule.Threshold, v => row.Rule.Threshold = v);
            row.Units = Stepper(panel, category, "Un", 0.54f, y0, y1, UnitsStep, UnitsMin,
                UnitsMax, () => row.Rule.Units, v => row.Rule.Units = v);
            row.Cost = UIFactory.Label($"Cost_{category}", panel, "",
                new Vector2(0.79f, y0), new Vector2(0.98f, y1), 15f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            return row;
        }

        /// <summary>A − value + control starting at <paramref name="x"/>; returns the value label.</summary>
        private TMP_Text Stepper(Transform panel, ProductCategory category, string tag, float x,
            float y0, float y1, int step, int min, int max, Func<int> get, Action<int> set)
        {
            MakeStep(panel, $"{tag}Down_{category}", "−", x, y0, y1,
                () => set(Step(get(), -1, step, min, max)));
            var value = UIFactory.Label($"{tag}Value_{category}", panel, "",
                new Vector2(x + 0.05f, y0), new Vector2(x + 0.17f, y1), 16f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            MakeStep(panel, $"{tag}Up_{category}", "+", x + 0.17f, y0, y1,
                () => set(Step(get(), 1, step, min, max)));
            return value;
        }

        private void MakeStep(Transform panel, string name, string text, float x, float y0,
            float y1, Action change)
        {
            var btn = UIFactory.Button(name, panel, text, new Vector2(x, y0),
                new Vector2(x + 0.05f, y1), 18f);
            btn.onClick.AddListener(() => { change(); Refresh(); });
        }

        // ── Content ─────────────────────────────────────────────────────────────

        private void Refresh()
        {
            foreach (var row in _rows) RefreshRow(row);
        }

        private void RefreshRow(Row row)
        {
            var rule = row.Rule;
            row.Toggle.color      = rule.Enabled ? ToggleOn : UIFactory.ButtonBg;
            row.ToggleLabel.text  = rule.Enabled ? "On" : "Off";
            row.Threshold.text    = rule.Threshold.ToString();
            row.Units.text        = rule.Units.ToString();
            row.Cost.text         = $"€ {EstimateCost(_game, rule.Category, rule.Units):N2}";
            row.Cost.color        = rule.Enabled ? UIFactory.Ink : UIFactory.InkMuted;
        }
    }
}
