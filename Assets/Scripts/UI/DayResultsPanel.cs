using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;

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

        public bool IsOpen => _root != null && _root.activeSelf;

        private static readonly (string label, Color colour)[] Buckets =
        {
            ("Animals",   new Color(0.95f, 0.62f, 0.35f)),
            ("Food",      new Color(0.92f, 0.78f, 0.34f)),
            ("Toys",      new Color(0.88f, 0.42f, 0.45f)),
            ("Accessory", new Color(0.44f, 0.66f, 0.92f)),
            ("Medicine",  new Color(0.42f, 0.84f, 0.62f)),
        };

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("DayResultsDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.02f, 0.04f, 0.07f, 0.66f));

            var panel = UIFactory.Panel("DayResults", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-330f, -270f), new Vector2(330f, 270f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            _heading = UIFactory.Label("Heading", panel.transform, "End of day",
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.96f), 26f, UIFactory.Ink);

            _figures = UIFactory.Label("Figures", panel.transform, "",
                new Vector2(0.06f, 0.55f), new Vector2(0.52f, 0.87f), 17f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            UIFactory.Label("BreakdownTitle", panel.transform, "Where the money came from",
                new Vector2(0.55f, 0.82f), new Vector2(0.94f, 0.87f), 14f, UIFactory.InkMuted);

            // Stacked proportion bar
            var track = UIFactory.Panel("BarTrack", panel.transform, new Vector2(0.55f, 0.76f),
                                        new Vector2(0.94f, 0.81f), new Color(1f, 1f, 1f, 0.10f));
            _barRow = track.transform;

            _breakdown = UIFactory.Label("Breakdown", panel.transform, "",
                new Vector2(0.55f, 0.55f), new Vector2(0.94f, 0.75f), 15f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            _net = UIFactory.Label("Net", panel.transform, "",
                new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.53f), 22f, UIFactory.Good);

            _advice = UIFactory.Label("Advice", panel.transform, "",
                new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.42f), 15f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);

            var cont = UIFactory.Button("Continue", panel.transform, "Open up tomorrow  →",
                new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.15f), 19f, UIFactory.ButtonOn);
            cont.onClick.AddListener(Continue);

            _root.SetActive(false);
        }

        public void Show(DaySummary s)
        {
            if (_root == null) return;
            _root.SetActive(true);
            _game?.SetModalOpen(true);

            _heading.text = $"Day {s.Day} — closing time";

            float profit = s.TotalRevenue - s.Rent - s.Wages;
            _figures.text =
                $"Customers served   <b>{s.SaleCount}</b>\n" +
                $"Items sold         <b>{s.TotalUnits}</b>\n" +
                $"Revenue            <b>€ {s.TotalRevenue:N2}</b>\n" +
                $"Rent              <color=#F27370>-€ {s.Rent:N2}</color>\n" +
                $"Wages             <color=#F27370>-€ {s.Wages:N2}</color>\n" +
                $"Reputation         <b>{s.Reputation:0}</b> / 100\n" +
                $"Balance            <b>€ {s.ClosingBalance:N2}</b>";

            BuildBreakdown(s);

            _net.text  = profit >= 0f ? $"Profit on the day:  + € {profit:N2}"
                                      : $"Loss on the day:  − € {-profit:N2}";
            _net.color = profit >= 0f ? UIFactory.Good : UIFactory.Bad;

            _advice.text = Advice(s, profit);
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
                text.AppendLine("<color=#9FB2C4>Nothing sold today.</color>");
            }
            else
            {
                float cursor = 0f;
                for (int i = 0; i < Buckets.Length; i++)
                {
                    if (totals[i] <= 0.01f) continue;
                    float share = totals[i] / sum;

                    var seg = UIFactory.Panel($"Seg_{Buckets[i].label}", _barRow,
                                              new Vector2(cursor, 0f), new Vector2(cursor + share, 1f),
                                              Buckets[i].colour);
                    seg.GetComponent<Image>().raycastTarget = false;
                    cursor += share;

                    string hex = ColorUtility.ToHtmlStringRGB(Buckets[i].colour);
                    text.AppendLine($"<color=#{hex}>■</color> {Buckets[i].label,-11} " +
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

            int emptyShelves = 0;
            float refill = 0f;
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null) continue;
                if (shelf.IsEmpty) emptyShelves++;
                refill += shelf.RestockCost();
            }
            if (emptyShelves > 0)
                notes.Add($"· <color=#F27370>{emptyShelves} shelf/shelves are empty.</color> Press E at a shelf to refill — " +
                          $"the whole shop would cost € {refill:N0}.");

            int emptyPens = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.Count == 0) emptyPens++;
            if (emptyPens > 0)
                notes.Add($"· {emptyPens} pen(s) stand empty. Press E at a pen to buy from the breeder.");

            if (s.Reputation < 35f)
                notes.Add("· Reputation is low, so few customers come. Keep stock on the shelves.");

            int hungry = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.NeedsService) hungry++;
            if (hungry > 0)
                notes.Add($"· <color=#F27370>{hungry} pen(s) went unfed.</color> Neglected animals lose " +
                          "condition and sell for far less.");

            if (profit < 0f)
                notes.Add($"· You lost money today. Rent rises to € {s.Rent + 6f:N0} tomorrow" +
                          (s.Wages > 0f ? $", plus € {s.Wages:N0} in wages." : "."));

            if (s.ClosingBalance < s.Rent * 2f)
                notes.Add("· <color=#E8C468>Cash is tight — do not overspend on building.</color>");

            if (notes.Count == 0)
                notes.Add("· The shop is in good shape. Consider another shelf or pen to grow.");

            return string.Join("\n", notes.GetRange(0, Mathf.Min(3, notes.Count)));
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
