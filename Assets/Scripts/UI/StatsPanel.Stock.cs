using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Stock page: every shelf with its stock and what waits in the stockroom,
    /// wholesale orders on the van, prices, and the order and reorder actions.
    /// </summary>
    public partial class StatsPanel
    {
        /// <summary>A shelf with fewer units than this shows as running low.</summary>
        private const int LowStockUnits = 4;

        /// <summary>Price step of the price buttons (10%).</summary>
        private const float PriceStep = 0.1f;

        /// <summary>Fallback unit cost when no catalogue is loaded.</summary>
        private const float FallbackUnitCost = 3.2f;

        private const float OrderRowBottom = 0.015f, OrderRowTop = 0.075f;
        private const float PriceRowBottom = 0.1f, PriceRowTop = 0.165f;
        private const float RowStart = 0f, RowWidth = 1f, ButtonGap = 0.012f;

        // Shelf section geometry, in reference pixels.
        private const float SectionPad = 6f, SectionHeaderPx = 32f, SectionLinePx = 30f;
        private const float SellLeft = 0.76f, LabelRight = 0.74f, ButtonInset = 0.08f;

        private BookPage _stock;
        private Transform _shelfHost;
        private TMP_Text  _stockText;

        private void BuildStockPage()
        {
            _stock = AddPage("Stock", UIFactory.TextSmall);
            _stock.Body.gameObject.SetActive(false);
            var content = BuildScrollContent(_stock.Root.transform);
            _shelfHost = AddColumn(content, "Shelves");
            _stockText = UIFactory.Label("Wholesale", content, "", Vector2.zero, Vector2.one,
                UIFactory.TextSmall, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _stockText.richText     = true;
            _stockText.overflowMode = TextOverflowModes.Overflow;
            BuildPriceControls(_stock.Root.transform);
            BuildOrderControls(_stock.Root.transform);
        }

        /// <summary>
        /// One order button per aisle. Ordering ahead is the cheap way to get stock; walking
        /// up to a shelf and pressing E is the expensive way, so this is where the planning
        /// happens.
        /// </summary>
        private void BuildOrderControls(Transform page)
        {
            var categories = (ProductCategory[])System.Enum.GetValues(typeof(ProductCategory));
            float w = RowWidth / (categories.Length + 1);

            for (int i = 0; i < categories.Length; i++)
            {
                ProductCategory category = categories[i];
                float x = RowStart + i * w;
                var btn = UIFactory.Button($"Order_{category}", page, "",
                    new Vector2(x, OrderRowBottom), new Vector2(x + w - ButtonGap, OrderRowTop), UIFactory.TextSmall);
                LocalizedText.Bind(btn.GetComponentInChildren<TMP_Text>(),
                    () => Loc.F("stock.order", OrderSize, LocNames.Category(category)));
                btn.onClick.AddListener(() => { _game.OrderStock(category, OrderSize); Refresh(); });
            }
            ReorderPanel.OpenButton(page, RowStart + categories.Length * w, w - ButtonGap,
                () => HandOff(() => _reorder?.Show()));
        }

        /// <summary>Price controls — one of the levers the player actually pulls.</summary>
        private void BuildPriceControls(Transform page)
        {
            var cheaper = UIFactory.ButtonKey("PriceDown", page, "stock.prices_down",
                new Vector2(0f, PriceRowBottom), new Vector2(0.24f, PriceRowTop), UIFactory.TextSmall);
            cheaper.onClick.AddListener(() => { _game.AdjustPrices(-PriceStep); Refresh(); });

            var dearer = UIFactory.ButtonKey("PriceUp", page, "stock.prices_up",
                new Vector2(0.25f, PriceRowBottom), new Vector2(0.49f, PriceRowTop), UIFactory.TextSmall);
            dearer.onClick.AddListener(() => { _game.AdjustPrices(PriceStep); Refresh(); });
        }

        private void RefreshStock()
        {
            RebuildShelfSections();
            var sb = new StringBuilder();
            sb.AppendLine(UIFactory.Tint(Loc.F("stock.refill_hint", InputBindings.Label(GameAction.Interact)), UIFactory.InkMuted));
            sb.AppendLine();
            AppendWholesale(sb);
            sb.AppendLine();
            var shop = _game.Shop;
            sb.AppendLine(Loc.F("stock.prices_line", shop.PriceMultiplier * 100f, shop.DemandFactor * 100f));
            _stockText.text = sb.ToString();
        }

        private static string Row(string a, string b, string c, string d) =>
            $"{a}<pos=30%>{b}<pos=52%>{c}<pos=76%>{d}";

        /// <summary>One clearly separated card per shelf: a header with Sell all, then a row per product with its own Sell.</summary>
        private void RebuildShelfSections()
        {
            ClearChildren(_shelfHost);
            int number = 0;
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null) continue;
                AddShelfSection(shelf, ++number);
            }
            if (number == 0)
            {
                var note = UIFactory.Label("NoShelves", _shelfHost, Loc.T("stock.no_shelves"),
                    Vector2.zero, Vector2.one, UIFactory.TextSmall, UIFactory.InkMuted, TextAlignmentOptions.TopLeft);
                note.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void AddShelfSection(ShelfUnit shelf, int number)
        {
            var lines = new System.Collections.Generic.List<ShelfUnit.StockLine>();
            foreach (var line in shelf.Lines)
                if (line.Product != null) lines.Add(line);

            int rows = Mathf.Max(1, lines.Count);
            var card = UIFactory.Card($"Shelf_{number}", _shelfHost, Vector2.zero, Vector2.one, UIFactory.CardBg);
            card.AddComponent<LayoutElement>().preferredHeight = SectionPad * 2f + SectionHeaderPx + rows * SectionLinePx;

            Color colour = shelf.IsEmpty ? UIFactory.Destructive
                         : shelf.TotalUnits < LowStockUnits ? UIFactory.Warning : UIFactory.Ink;
            var header = SectionRow(card.transform, "Header", 0f, SectionHeaderPx);
            var title = UIFactory.Label("Title", header, Loc.F("stock.section.title", number,
                LocNames.CategoryTitle(shelf.Category), Loc.Plural("stock.units", shelf.TotalUnits), shelf.RestockCost()),
                Vector2.zero, new Vector2(LabelRight, 1f), UIFactory.TextSmall, colour);
            title.fontStyle = FontStyles.Bold;
            var sellAll = UIFactory.ButtonKey("SellAll", header, "stock.sell_all",
                new Vector2(SellLeft, ButtonInset), new Vector2(1f, 1f - ButtonInset), UIFactory.TextSmall);
            sellAll.interactable = !shelf.IsEmpty;
            sellAll.onClick.AddListener(() => SellShelf(shelf));

            if (lines.Count == 0)
            {
                var empty = SectionRow(card.transform, "Empty", SectionHeaderPx, SectionLinePx);
                UIFactory.Label("Text", empty, Loc.T("stock.shelf_empty"), Vector2.zero, Vector2.one,
                    UIFactory.TextSmall, UIFactory.InkMuted);
                return;
            }

            var shop = _game.Shop;
            for (int i = 0; i < lines.Count; i++)
            {
                var product = lines[i].Product;
                int units   = lines[i].Units;
                float price = shop != null ? shop.PriceOf(product.basePrice) : product.basePrice;
                var row = SectionRow(card.transform, $"Line_{i}", SectionHeaderPx + i * SectionLinePx, SectionLinePx);
                UIFactory.Label("Text", row, Loc.F("stock.line", product.LocalizedName, units, shelf.MaxPerLine, price),
                    Vector2.zero, new Vector2(LabelRight, 1f), UIFactory.TextSmall, UIFactory.Ink);
                var sell = UIFactory.ButtonKey("Sell", row, "stock.sell",
                    new Vector2(SellLeft, ButtonInset), new Vector2(1f, 1f - ButtonInset), UIFactory.TextSmall);
                sell.interactable = units > 0;
                sell.onClick.AddListener(() => SellLine(shelf, product));
            }
        }

        /// <summary>A full-width strip of <paramref name="height"/> pixels, <paramref name="top"/> pixels below the card's padded top.</summary>
        private static Transform SectionRow(Transform card, string name, float top, float height)
        {
            float y = SectionPad + top;
            return UIFactory.Node(name, card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(SectionPad * 2f, -(y + height)), new Vector2(-SectionPad * 2f, -y)).transform;
        }

        /// <summary>Sells one product's units off <paramref name="shelf"/> at the shelf price.</summary>
        private void SellLine(ShelfUnit shelf, ProductItem product)
        {
            int units = shelf.SellLine(product, _game.Shop, out float revenue);
            if (units > 0)
                _game.Notify(Loc.F("stock.sold", units, product.LocalizedName, revenue));
            Refresh();
        }

        /// <summary>Sells everything on <paramref name="shelf"/>, product by product.</summary>
        private void SellShelf(ShelfUnit shelf)
        {
            int units = 0;
            float revenue = 0f;
            foreach (var line in new System.Collections.Generic.List<ShelfUnit.StockLine>(shelf.Lines))
            {
                if (line.Product == null) continue;
                units   += shelf.SellLine(line.Product, _game.Shop, out float r);
                revenue += r;
            }
            if (units > 0) _game.Notify(Loc.F("stock.sold_all", units, revenue));
            Refresh();
        }

        private void AppendWholesale(StringBuilder sb)
        {
            var shop = _game.Shop;
            var catalog = _game.Catalog;
            sb.AppendLine(UIFactory.Tint(Row(Loc.T("stock.col.category"), Loc.T("stock.col.in_stockroom"),
                Loc.F("stock.col.order_of", OrderSize), Loc.T("stock.col.at_shelf")), UIFactory.InkMuted));
            foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
            {
                float unit  = catalog != null ? catalog.AverageUnitCost(category) : FallbackUnitCost;
                float order = unit * ShopManager.WholesaleDiscount * shop.SupplierPriceMultiplier * OrderSize;
                sb.AppendLine(Row(LocNames.CategoryTitle(category), $"{shop.Warehouse(category)}", $"€ {order:N2}",
                    $"€ {unit * ShopManager.EmergencyMarkup * OrderSize:N2}"));
            }
            foreach (var order in shop.Orders)
                sb.AppendLine(UIFactory.Tint(Loc.F("stock.on_van", order.Units, LocNames.Category(order.Category),
                                                   ArrivalSeconds(order.ArrivalProgress)), UIFactory.Warning));
        }

        private int ArrivalSeconds(float arrivalProgress) =>
            Mathf.CeilToInt(Mathf.Max(0f, (arrivalProgress - _game.DayProgress) * _game.DayLengthSeconds)
                            + PetShop.Traffic.DeliveryTruck.EstimatedDriveSeconds);
    }
}
