using System.Text;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
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
            _stock = AddPage("Stock", "What is on the shelves and in the stockroom, and ordering more.", UIFactory.TextSmall);
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
                var btn = UIFactory.Button($"Order_{category}", page, $"Order {OrderSize} {category}",
                    new Vector2(x, OrderRowBottom), new Vector2(x + w - ButtonGap, OrderRowTop), UIFactory.TextSmall);
                btn.onClick.AddListener(() => { _game.OrderStock(category, OrderSize); Refresh(); });
            }
            ReorderPanel.OpenButton(page, RowStart + categories.Length * w, w - ButtonGap,
                () => HandOff(() => _reorder?.Show()));
        }

        /// <summary>Price controls — one of the levers the player actually pulls.</summary>
        private void BuildPriceControls(Transform page)
        {
            var cheaper = UIFactory.Button("PriceDown", page, "Prices  −10%",
                new Vector2(0f, PriceRowBottom), new Vector2(0.24f, PriceRowTop), UIFactory.TextSmall);
            cheaper.onClick.AddListener(() => { _game.AdjustPrices(-PriceStep); Refresh(); });

            var dearer = UIFactory.Button("PriceUp", page, "Prices  +10%",
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
            sb.AppendLine($"Prices are at <b>{shop.PriceMultiplier * 100f:0}%</b> of list; shoppers are " +
                          $"<b>{shop.DemandFactor * 100f:0}%</b> as likely to buy as at list price.");
            _stock.Body.text = sb.ToString();
        }

        private static string Row(string a, string b, string c, string d) =>
            $"{a}<pos=30%>{b}<pos=52%>{c}<pos=76%>{d}";

        private void AppendShelfTable(StringBuilder sb)
        {
            var shelves = _game.Shelves;
            if (shelves.Count == 0)
            {
                sb.AppendLine("No shelves yet. Order one on the Build page.");
                return;
            }
            sb.AppendLine(UIFactory.Tint(Row("Shelf", "On shelf", "In stockroom", "Refill cost"), UIFactory.InkMuted));
            foreach (var shelf in shelves)
            {
                if (shelf == null) continue;
                Color colour = shelf.IsEmpty ? UIFactory.Destructive
                             : shelf.TotalUnits < LowStockUnits ? UIFactory.Warning : UIFactory.Ink;
                sb.AppendLine(UIFactory.Tint(Row(shelf.Category.ToString(), $"{shelf.TotalUnits} units",
                    $"{_game.Shop.Warehouse(shelf.Category)}", $"€ {shelf.RestockCost():N2}"), colour));
            }
            sb.AppendLine(UIFactory.Tint($"Walk up to a shelf and press {InputBindings.Label(GameAction.Interact)} to refill it " +
                                         "from the stockroom.", UIFactory.InkMuted));
        }

        private void AppendWholesale(StringBuilder sb)
        {
            var shop = _game.Shop;
            var catalog = _game.Catalog;
            sb.AppendLine(UIFactory.Tint(Row("Category", "In stockroom", $"Order of {OrderSize}", "At the shelf"), UIFactory.InkMuted));
            foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
            {
                float unit  = catalog != null ? catalog.AverageUnitCost(category) : FallbackUnitCost;
                float order = unit * ShopManager.WholesaleDiscount * shop.SupplierPriceMultiplier * OrderSize;
                sb.AppendLine(Row(category.ToString(), $"{shop.Warehouse(category)}", $"€ {order:N2}",
                    $"€ {unit * ShopManager.EmergencyMarkup * OrderSize:N2}"));
            }
            foreach (var order in shop.Orders)
                sb.AppendLine(UIFactory.Tint($"On the van: {order.Units} {order.Category} — arriving in about " +
                                             $"{ArrivalSeconds(order.ArrivalProgress)} s", UIFactory.Warning));
        }

        private int ArrivalSeconds(float arrivalProgress) =>
            Mathf.CeilToInt(Mathf.Max(0f, (arrivalProgress - _game.DayProgress) * _game.DayLengthSeconds)
                            + PetShop.Traffic.DeliveryTruck.EstimatedDriveSeconds);
    }
}
