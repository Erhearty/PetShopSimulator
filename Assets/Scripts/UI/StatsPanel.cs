using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// The ledger (Tab): what is on the shelves, who lives in the pens, and what the
    /// catalogue costs. Read-only — it answers "what should I do next?" at a glance.
    /// </summary>
    public class StatsPanel : MonoBehaviour
    {
        private GameObject  _root;
        private GameManager _game;
        private TMP_Text    _shelvesText;
        private TMP_Text    _pensText;
        private TMP_Text    _catalogText;
        private TMP_Text    _headerText;

        private readonly List<Button> _tabs = new();
        private GameObject _shelvesPage, _pensPage, _catalogPage, _managePage;
        private TMP_Text   _manageText;
        private readonly List<GameObject> _manageControls = new();

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("StatsDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.03f, 0.05f, 0.08f, 0.62f));

            var panel = UIFactory.Panel("Stats", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-420f, -280f), new Vector2(420f, 280f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            _headerText = UIFactory.Label("Header", panel.transform, "The ledger",
                new Vector2(0.03f, 0.89f), new Vector2(0.97f, 0.97f), 24f, UIFactory.Ink);

            _shelvesPage = MakePage(panel.transform, out _shelvesText);
            _pensPage    = MakePage(panel.transform, out _pensText);
            _catalogPage = MakePage(panel.transform, out _catalogText);

            _managePage = MakePage(panel.transform, out _manageText);

            MakeTab(panel.transform, "Shelves",   0, 4, () => ShowPage(0));
            MakeTab(panel.transform, "Animals",   1, 4, () => ShowPage(1));
            MakeTab(panel.transform, "Catalogue", 2, 4, () => ShowPage(2));
            MakeTab(panel.transform, "Manage",    3, 4, () => ShowPage(3));

            BuildManageControls(panel.transform);

            var close = UIFactory.Button("Close", panel.transform, "Close  (Tab)",
                new Vector2(0.38f, 0.02f), new Vector2(0.62f, 0.08f), 15f);
            close.onClick.AddListener(Hide);

            _root.SetActive(false);
        }

        private static GameObject MakePage(Transform parent, out TMP_Text body)
        {
            var page = UIFactory.Node("Page", parent, new Vector2(0.03f, 0.10f), new Vector2(0.97f, 0.80f));
            body = UIFactory.Label("Body", page.transform, "",
                Vector2.zero, Vector2.one, 16f, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            body.richText = true;
            return page;
        }

        private void MakeTab(Transform parent, string label, int index, int count, UnityEngine.Events.UnityAction onClick)
        {
            float w = 0.9f / count;
            float x = 0.03f + index * w;
            var btn = UIFactory.Button($"Tab_{label}", parent, label,
                new Vector2(x, 0.815f), new Vector2(x + w - 0.01f, 0.875f), 16f);
            btn.onClick.AddListener(onClick);
            _tabs.Add(btn);
        }

        /// <summary>Select a tab by index. Used by the screenshot tour.</summary>
        public void ShowTab(int index)
        {
            Refresh();
            ShowPage(index);
        }

        private void ShowPage(int index)
        {
            _shelvesPage.SetActive(index == 0);
            _pensPage.SetActive(index == 1);
            _catalogPage.SetActive(index == 2);
            _managePage.SetActive(index == 3);
            foreach (var control in _manageControls) control.SetActive(index == 3);

            for (int i = 0; i < _tabs.Count; i++)
            {
                var image = _tabs[i].GetComponent<Image>();
                if (image != null) image.color = i == index ? UIFactory.ButtonOn : UIFactory.ButtonBg;
            }
        }

        /// <summary>Price and staffing controls — the two levers the player actually pulls.</summary>
        private void BuildManageControls(Transform panel)
        {
            var cheaper = UIFactory.Button("PriceDown", panel, "Prices  −10%",
                new Vector2(0.05f, 0.11f), new Vector2(0.27f, 0.18f), 15f);
            cheaper.onClick.AddListener(() => { _game.AdjustPrices(-0.1f); Refresh(); });

            var dearer = UIFactory.Button("PriceUp", panel, "Prices  +10%",
                new Vector2(0.28f, 0.11f), new Vector2(0.50f, 0.18f), 15f);
            dearer.onClick.AddListener(() => { _game.AdjustPrices(0.1f); Refresh(); });

            var hire = UIFactory.Button("Hire", panel, "Hire assistant",
                new Vector2(0.52f, 0.11f), new Vector2(0.74f, 0.18f), 15f, UIFactory.ButtonOn);
            hire.onClick.AddListener(() => { _game.HireAssistant(); Refresh(); });

            var fire = UIFactory.Button("Fire", panel, "Let one go",
                new Vector2(0.75f, 0.11f), new Vector2(0.95f, 0.18f), 15f);
            fire.onClick.AddListener(() => { _game.FireAssistant(); Refresh(); });

            _manageControls.AddRange(new[]
            {
                cheaper.gameObject, dearer.gameObject, hire.gameObject, fire.gameObject
            });
        }

        public void Show()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
            ShowPage(0);
            _game?.SetModalOpen(true);
        }

        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        // ── Content ─────────────────────────────────────────────────────────────

        private void Refresh()
        {
            var shop = _game.Shop;
            _headerText.text = $"The ledger    <color=#9FB2C4>Day {shop.Day}  ·  € {shop.Balance:N2}  ·  " +
                               $"reputation {shop.Reputation:0}/100  ·  rent tonight € {shop.DailyRent:N0}</color>";

            BuildShelvesPage();
            BuildPensPage();
            BuildCatalogPage();
            BuildManagePage();
        }

        private void BuildManagePage()
        {
            var shop = _game.Shop;
            var sb   = new StringBuilder();

            sb.AppendLine("<b>Pricing</b>");
            sb.AppendLine($"  Shelf prices are at <b>{shop.PriceMultiplier * 100f:0}%</b> of list.");
            sb.AppendLine($"  Shoppers are <b>{shop.DemandFactor * 100f:0}%</b> as likely to buy as at list price.");
            sb.AppendLine(shop.PriceMultiplier > 1.25f
                ? "  <color=#E8C468>Steep. Expect people to leave empty-handed.</color>"
                : shop.PriceMultiplier < 0.9f
                    ? "  <color=#9FB2C4>Undercutting: busier, but thinner margins.</color>"
                    : "  <color=#73DB95>About right.</color>");
            sb.AppendLine();

            sb.AppendLine("<b>Staff</b>");
            sb.AppendLine($"  {_game.StaffCount} assistant(s) at € {shop.WagePerAssistant:N0} a day each " +
                          $"= <b>€ {shop.DailyWages:N0}</b> tonight.");
            sb.AppendLine("  Assistants work the till on their own, slower than you do.");
            sb.AppendLine(_game.StaffCount == 0
                ? "  <color=#F27370>Nobody on the till — you must serve every customer yourself.</color>"
                : "  <color=#9FB2C4>You can still serve the queue yourself to clear it faster.</color>");
            sb.AppendLine();

            sb.AppendLine("<b>Tonight's bill</b>");
            sb.AppendLine($"  Rent   € {shop.DailyRent:N2}");
            sb.AppendLine($"  Wages  € {shop.DailyWages:N2}");
            sb.AppendLine($"  <b>Total € {shop.DailyOutgoings:N2}</b>   against € {shop.Balance:N2} in hand");

            _manageText.text = sb.ToString();
        }

        private void BuildShelvesPage()
        {
            var shelves = _game.Shelves;
            var sb = new StringBuilder();

            if (shelves.Count == 0)
            {
                sb.AppendLine("No shelves yet. Press <b>1</b> or <b>2</b> to build one.");
            }
            else
            {
                int empty = 0, totalUnits = 0;
                float restockBill = 0f;

                sb.AppendLine($"<color=#9FB2C4>{"Shelf",-16}{"Stock",-10}{"Value on shelf",-18}Refill cost</color>");
                foreach (var shelf in shelves)
                {
                    if (shelf == null) continue;
                    if (shelf.IsEmpty) empty++;
                    totalUnits += shelf.TotalUnits;

                    float value = 0f;
                    foreach (var line in shelf.Lines)
                        if (line.Product != null) value += line.Units * line.Product.basePrice;

                    float refill = shelf.RestockCost();
                    restockBill += refill;

                    string colour = shelf.IsEmpty ? "#F27370" : shelf.TotalUnits < 4 ? "#E8C468" : "#73DB95";
                    sb.AppendLine($"<color={colour}>{shelf.Category,-16}</color>" +
                                  $"{shelf.TotalUnits + " units",-10}{"€ " + value.ToString("N2"),-18}€ {refill:N2}");
                }
                sb.AppendLine();
                sb.AppendLine($"{shelves.Count} shelves · {totalUnits} units on display · {empty} empty");
                sb.AppendLine($"Refilling everything would cost <b>€ {restockBill:N2}</b>.");
                sb.AppendLine();
                sb.AppendLine("<color=#9FB2C4>Walk up to a shelf and press E to refill it.</color>");
            }
            _shelvesText.text = sb.ToString();
        }

        private void BuildPensPage()
        {
            var pens = _game.Pens;
            var sb = new StringBuilder();

            if (pens.Count == 0)
            {
                sb.AppendLine("No pens yet. Press <b>3</b> to build one.");
            }
            else
            {
                foreach (var pen in pens)
                {
                    if (pen == null) continue;
                    sb.AppendLine($"<b>{pen.PenSpecies} pen</b>  <color=#9FB2C4>({pen.Count}/{pen.Capacity})" +
                                  $"  ·  a new one costs € {Pet.WholesalePrice(pen.PenSpecies):N0}</color>");

                    if (pen.Count == 0)
                    {
                        sb.AppendLine("   <color=#F27370>empty — press E at the pen to buy one</color>");
                    }
                    else
                    {
                        foreach (var pet in pen.Residents)
                            sb.AppendLine($"   {pet.DisplayName(),-38}{pet.Condition,-11}€ {pet.SellPrice():N2}");

                        string careColour = pen.NeedsService ? "#F27370" : "#73DB95";
                    sb.AppendLine($"   <color={careColour}>feed {pen.FoodLevel * 100f:0}%  ·  " +
                                  $"bedding {pen.Cleanliness * 100f:0}%</color>" +
                                  (pen.NeedsService ? $"  — servicing costs € {pen.ServiceCost:N2}" : ""));

                    if (pen.AdultCount >= 2 && pen.HasSpace)
                            sb.AppendLine("   <color=#73DB95>two adults and room to spare — they may breed tonight</color>");
                        else if (pen.AdultCount < 2)
                            sb.AppendLine("   <color=#E8C468>needs two adults to breed</color>");
                    }
                    sb.AppendLine();
                }
            }
            _pensText.text = sb.ToString();
        }

        private void BuildCatalogPage()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<color=#9FB2C4>{"Product",-20}{"Category",-13}{"Buy",-10}{"Sell",-10}Margin</color>");

            var catalog = _game.Catalog;
            if (catalog != null)
                foreach (var p in catalog.All)
                    sb.AppendLine($"{p.displayName,-20}{p.category,-13}" +
                                  $"{"€ " + p.unitCost.ToString("N2"),-10}{"€ " + p.basePrice.ToString("N2"),-10}" +
                                  $"<color=#73DB95>€ {p.DefaultMargin:N2}</color>");

            sb.AppendLine();
            sb.AppendLine("<color=#9FB2C4>Animals</color>");
            foreach (Pet.Species species in System.Enum.GetValues(typeof(Pet.Species)))
                sb.AppendLine($"{species,-20}{"buy",-13}{"€ " + Pet.WholesalePrice(species).ToString("N0"),-10}" +
                              $"{"€ " + Pet.SpeciesBasePrice(species).ToString("N0"),-10}" +
                              "<color=#9FB2C4>× rarity, × 1.2 adult</color>");

            _catalogText.text = sb.ToString();
        }
    }
}
