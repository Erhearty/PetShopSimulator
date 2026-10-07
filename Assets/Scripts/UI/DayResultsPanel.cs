using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// End-of-day books: the numbers, a breakdown bar of where the money came from, and the
    /// best sellers. Blocks play until the player opens up again.
    /// </summary>
    public class DayResultsPanel : MonoBehaviour
    {
        private GameObject  _root;
        private TMP_Text    _heading;
        private TMP_Text    _figures;
        private TMP_Text    _breakdown;
        private TMP_Text    _net;
        private TMP_Text    _advice;
        private Transform   _barRow;
        private GameManager _game;
        private DaySummary  _shown;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>Revenue buckets: an id (object names), the label's text key and the chart colour.</summary>
        private static readonly (string id, string key, Color colour)[] Buckets =
        {
            ("Animals",   "results.bucket.animals",   UIFactory.ChartAnimals),
            ("Food",      "results.bucket.food",      UIFactory.ChartFood),
            ("Toys",      "results.bucket.toys",      UIFactory.ChartToys),
            ("Accessory", "results.bucket.accessory", UIFactory.ChartAccessory),
            ("Medicine",  "results.bucket.medicine",  UIFactory.ChartMedicine),
        };

        private void OnEnable()  => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Re-renders the open books in the new language.</summary>
        private void OnLanguageChanged()
        {
            if (IsOpen && _shown != null) Render(_shown);
        }

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("DayResultsDim", canvas, Vector2.zero, Vector2.one,
                                    UIFactory.Dim);

            var panel = UIFactory.ModalPanel("DayResults", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-330f, -270f), new Vector2(330f, 270f));

            _heading = UIFactory.Header(panel.transform, "",
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.96f));
            _heading.text = Loc.T("results.title");

            _figures = UIFactory.Label("Figures", panel.transform, "",
                new Vector2(0.06f, 0.55f), new Vector2(0.52f, 0.87f), 17f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            UIFactory.LabelKey("BreakdownTitle", panel.transform, "results.breakdown_title",
                new Vector2(0.55f, 0.82f), new Vector2(0.94f, 0.87f), 14f, UIFactory.InkMuted);

            // Stacked proportion bar
            var track = UIFactory.Panel("BarTrack", panel.transform, new Vector2(0.55f, 0.76f),
                                        new Vector2(0.94f, 0.81f), UIFactory.TrackFaint);
            _barRow = track.transform;

            _breakdown = UIFactory.Label("Breakdown", panel.transform, "",
                new Vector2(0.55f, 0.55f), new Vector2(0.94f, 0.75f), 15f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            _net = UIFactory.Label("Net", panel.transform, "",
                new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.53f), 22f, UIFactory.Good);

            _advice = UIFactory.Label("Advice", panel.transform, "",
                new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.42f), 15f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);

            var cont = UIFactory.ButtonKey("Continue", panel.transform, "results.continue",
                new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.15f), 19f, UIFactory.ButtonOn);
            cont.onClick.AddListener(Continue);

            _root.SetActive(false);
        }

        public void Show(DaySummary s)
        {
            if (_root == null) return;
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            _shown = s;
            Render(s);
        }

        /// <summary>Writes the books for <paramref name="s"/> in the current language.</summary>
        private void Render(DaySummary s)
        {
            _heading.text = Loc.F("results.heading", s.Day);

            float profit = s.TotalRevenue - s.Rent - s.Wages;
            _figures.text = Loc.F("results.figures", s.SaleCount, s.TotalUnits, s.TotalRevenue,
                                  s.Rent, s.Wages, s.Reputation, s.ClosingBalance);

            BuildBreakdown(s);

            _net.text  = profit >= 0f ? Loc.F("results.profit", profit)
                                      : Loc.F("results.loss", -profit);
            _net.color = profit >= 0f ? UIFactory.Good : UIFactory.Bad;

            _advice.text = Advice(s, profit);
            if (s.Headlines != null && s.Headlines.Count > 0)
                _advice.text += "\n" + string.Join("\n", s.Headlines);
        }

        private void BuildBreakdown(DaySummary s)
        {
            for (int i = _barRow.childCount - 1; i >= 0; i--)
                Destroy(_barRow.GetChild(i).gameObject);

            var totals = new float[Buckets.Length];
            foreach (var record in s.Records)
                totals[BucketOf(record)] += record.Revenue;

            float sum = 0f;
            foreach (float t in totals) sum += t;

            var text = new System.Text.StringBuilder();
            if (sum <= 0.01f)
            {
                text.AppendLine($"<color=#9FB2C4>{Loc.T("results.nothing_sold")}</color>");
            }
            else
            {
                float cursor = 0f;
                for (int i = 0; i < Buckets.Length; i++)
                {
                    if (totals[i] <= 0.01f) continue;
                    float share = totals[i] / sum;

                    var seg = UIFactory.Panel($"Seg_{Buckets[i].id}", _barRow,
                                              new Vector2(cursor, 0f), new Vector2(cursor + share, 1f),
                                              Buckets[i].colour);
                    seg.GetComponent<Image>().raycastTarget = false;
                    cursor += share;

                    string hex = ColorUtility.ToHtmlStringRGB(Buckets[i].colour);
                    text.AppendLine($"<color=#{hex}>•</color> {Loc.T(Buckets[i].key),-11} " +
                                    $"€ {totals[i],8:N2}   {share * 100f:0}%");
                }
            }
            _breakdown.text = text.ToString();
        }

        private static int BucketOf(SaleRecord record)
        {
            if (record.ItemId != null && record.ItemId.StartsWith("pet_")) return 0;

            var catalog = GameManager.Instance != null ? GameManager.Instance.Catalog : null;
            var product = catalog != null ? catalog.Get(record.ItemId) : null;
            if (product == null) return 1;

            return product.category switch
            {
                ProductCategory.Food      => 1,
                ProductCategory.Toy       => 2,
                ProductCategory.Accessory => 3,
                ProductCategory.Medicine  => 4,
                _                         => 1,
            };
        }

        /// <summary>A nudge towards whatever is most obviously wrong with the shop.</summary>
        private string Advice(DaySummary s, float profit)
        {
            var notes = new List<string>();
            AddShowNotes(notes, s.Day);

            int emptyShelves = 0;
            float refill = 0f;
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null) continue;
                if (shelf.IsEmpty) emptyShelves++;
                refill += shelf.RestockCost();
            }
            if (emptyShelves > 0)
                notes.Add(Loc.Plural("results.advice.empty_shelves", emptyShelves,
                                     InputBindings.Label(GameAction.Interact), refill));

            int emptyPens = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.Count == 0) emptyPens++;
            if (emptyPens > 0)
                notes.Add(Loc.Plural("results.advice.empty_pens", emptyPens, InputBindings.Label(GameAction.Interact)));

            if (s.Reputation < 35f)
                notes.Add(Loc.T("results.advice.low_rep"));

            int hungry = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.NeedsService) hungry++;
            if (hungry > 0)
                notes.Add(Loc.Plural("results.advice.unfed", hungry));

            if (profit < 0f)
                notes.Add(s.Wages > 0f ? Loc.F("results.advice.loss_wages", s.Rent + 6f, s.Wages)
                                       : Loc.F("results.advice.loss", s.Rent + 6f));

            if (s.ClosingBalance < s.Rent * 2f)
                notes.Add(Loc.T("results.advice.cash_tight"));

            if (notes.Count == 0)
                notes.Add(Loc.T("results.advice.good"));

            return string.Join("\n", notes.GetRange(0, Mathf.Min(3, notes.Count)));
        }

        private void AddShowNotes(List<string> notes, int day)
        {
            var last = _game.Show.LastResult;
            if (PetShow.IsShowDay(day) && last.HasValue && _game.Show.LastResultDay == day)
            {
                var r = last.Value;
                notes.Add(r.disqualified ? Loc.T("results.show.disqualified")
                    : Loc.F("results.show.placed", r.placement, r.prize));
            }
            if (PetShow.IsShowDay(day + 1) && _game.Show.EntryPetId == null)
                notes.Add(Loc.F("results.show.tomorrow", LocNames.Species(ShowJudging.CurrentTheme(_game, day + 1).species)));
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        private void Continue()
        {
            Hide();
            _game?.StartNewDay();
        }
    }
}
