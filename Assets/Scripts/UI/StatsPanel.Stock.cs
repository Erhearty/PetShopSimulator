using System.Text;
using UnityEngine;
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

        private BookPage _stock;

        private void BuildStockPage()
        {
            _stock = AddPage("Stock", UIFactory.TextSmall);
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
            var sb = new StringBuilder();
            AppendShelfTable(sb);
            sb.AppendLine();
            AppendWholesale(sb);
            sb.AppendLine();
            var shop = _game.Shop;
            sb.AppendLine(Loc.F("stock.prices_line", shop.PriceMultiplier * 100f, shop.DemandFactor * 100f));
            _stock.Body.text = sb.ToString();
        }

        private static string Row(string a, string b, string c, string d) =>
            $"{a}<pos=30%>{b}<pos=52%>{c}<pos=76%>{d}";

        private void AppendShelfTable(StringBuilder sb)
        {
            var shelves = _game.Shelves;
            if (shelves.Count == 0)
            {
                sb.AppendLine(Loc.T("stock.no_shelves"));
                return;
            }
            sb.AppendLine(UIFactory.Tint(Row(Loc.T("stock.col.shelf"), Loc.T("stock.col.on_shelf"),
                Loc.T("stock.col.in_stockroom"), Loc.T("stock.col.refill_cost")), UIFactory.InkMuted));
            foreach (var shelf in shelves)
            {
                if (shelf == null) continue;
                Color colour = shelf.IsEmpty ? UIFactory.Destructive
                             : shelf.TotalUnits < LowStockUnits ? UIFactory.Warning : UIFactory.Ink;
                sb.AppendLine(UIFactory.Tint(Row(LocNames.CategoryTitle(shelf.Category), Loc.Plural("stock.units", shelf.TotalUnits),
                    $"{_game.Shop.Warehouse(shelf.Category)}", $"€ {shelf.RestockCost():N2}"), colour));
            }
            sb.AppendLine(UIFactory.Tint(Loc.F("stock.refill_hint", InputBindings.Label(GameAction.Interact)), UIFactory.InkMuted));
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
