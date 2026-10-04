using System.Text;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The ledger's Catalogue tab: stock prices and margins, what waits in the stockroom or on
    /// the van, and what each species costs.
    /// </summary>
    public partial class StatsPanel
    {
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
            sb.AppendLine("<color=#9FB2C4>Stockroom and deliveries</color>");

            var shop = _game.Shop;
            if (shop != null)
            {
                foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
                {
                    float unit  = catalog != null ? catalog.AverageUnitCost(category) : 3.2f;
                    float order = unit * ShopManager.WholesaleDiscount * shop.SupplierPriceMultiplier * OrderSize;
                    int   ready = shop.Warehouse(category);

                    sb.AppendLine($"{category,-20}{ready + " in stockroom",-18}" +
                                  $"{"€ " + order.ToString("N2") + " per " + OrderSize,-20}" +
                                  $"<color=#9FB2C4>vs € {unit * ShopManager.EmergencyMarkup * OrderSize:N2} at the door</color>");
                }

                foreach (var order in shop.Orders)
                    sb.AppendLine($"<color=#F0C46A>on the van: {order.Units} {order.Category} " +
                                  $"— arriving in about {Mathf.CeilToInt(Mathf.Max(0f, (order.ArrivalProgress - _game.DayProgress) * _game.DayLengthSeconds) + PetShop.Traffic.DeliveryTruck.EstimatedDriveSeconds)} s</color>");
            }

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
