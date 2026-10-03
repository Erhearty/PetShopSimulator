using System;
using System.Collections.Generic;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Progression.Quests;

namespace PetShop.Commerce
{
    /// <summary>Lifetime trading counters the quest book reads, saved with the game.</summary>
    public partial class ShopManager
    {
        /// <summary>Prefix of a sale's item id when the sale is an animal, e.g. "pet_Cat".</summary>
        public const string PetItemPrefix = "pet_";

        private readonly HashSet<Pet.Species> _speciesSold = new();

        /// <summary>All sales income since the shop opened. Grants and quest rewards do not count.</summary>
        public float LifetimeRevenue { get; private set; }

        /// <summary>The best single day's profit: sales less that day's spending, rent and wages.</summary>
        public float BestDayProfit { get; private set; }

        /// <summary>Every species sold at least once.</summary>
        public IReadOnlyCollection<Pet.Species> SpeciesSold => _speciesSold;

        /// <summary>Restores the counters from a save. Unknown species names are ignored.</summary>
        public void RestoreStats(float lifetimeRevenue, float bestDayProfit, IEnumerable<string> speciesSold)
        {
            LifetimeRevenue = Math.Max(0f, lifetimeRevenue);
            BestDayProfit   = Math.Max(0f, bestDayProfit);
            _speciesSold.Clear();
            if (speciesSold == null) return;
            foreach (var name in speciesSold)
                if (Enum.TryParse(name, out Pet.Species species)) _speciesSold.Add(species);
        }

        /// <summary>Adds a sale to the lifetime revenue, and its species when it was an animal.</summary>
        private void TrackSale(SaleRecord record)
        {
            LifetimeRevenue += record.Revenue;
            string id = record.ItemId;
            if (id == null || !id.StartsWith(PetItemPrefix, StringComparison.Ordinal)) return;
            if (Enum.TryParse(id.Substring(PetItemPrefix.Length), out Pet.Species species)) _speciesSold.Add(species);
        }

        /// <summary>Keeps the best day's profit, from the summary of the day being closed.</summary>
        private void TrackDayProfit(DaySummary summary)
        {
            float profit = summary.TotalRevenue - summary.Spend - summary.Rent - summary.Wages;
            if (profit > BestDayProfit) BestDayProfit = profit;
        }

        /// <summary>Announces a placed supplier order and raises the quest book's stock-ordered flag.</summary>
        private void NotifyOrderPlaced(SupplierOrder order)
        {
            OnOrderPlaced.Invoke(order);
            GameManager.Instance?.Quests?.RaiseFlag(QuestFlags.StockOrdered);
        }
    }
}
