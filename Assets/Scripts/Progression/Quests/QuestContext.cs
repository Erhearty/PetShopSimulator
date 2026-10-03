using System.Collections.Generic;
using PetShop.Pets;

namespace PetShop.Progression.Quests
{
    /// <summary>
    /// A read-only snapshot of the game state that quest conditions look at. Built by the
    /// scene-side director (or by a test) and never changed by a quest. Collections default
    /// to empty, so a fresh context is a brand-new, empty shop.
    /// </summary>
    public sealed class QuestContext
    {
        private static readonly IReadOnlyDictionary<string, int> NoCounts    = new Dictionary<string, int>();
        private static readonly IReadOnlyCollection<Pet.Species> NoSpecies   = new HashSet<Pet.Species>();

        /// <summary>Cash in hand.</summary>
        public float Balance { get; set; }

        /// <summary>All sales income since the shop opened.</summary>
        public float LifetimeRevenue { get; set; }

        /// <summary>The best single day's profit so far.</summary>
        public float BestDayProfit { get; set; }

        /// <summary>Placed furniture by catalogue id (see BuildCatalog).</summary>
        public IReadOnlyDictionary<string, int> PlacedById { get; set; } = NoCounts;

        /// <summary>Placed furniture by catalogue type ("shelf", "counter", "pen"...).</summary>
        public IReadOnlyDictionary<string, int> PlacedByType { get; set; } = NoCounts;

        /// <summary>The highest lot stage any placed piece stands in (0 starter, 1 back strip, 2 yard).</summary>
        public int HighestPlacedLotStage { get; set; }

        /// <summary>Owned but unplaced furniture (unpacked crates) by catalogue id.</summary>
        public IReadOnlyDictionary<string, int> OwnedById { get; set; } = NoCounts;

        /// <summary>Furniture ordered but not yet collected, by catalogue id.</summary>
        public IReadOnlyDictionary<string, int> OnOrderById { get; set; } = NoCounts;

        /// <summary>Product units in the stockroom and on the shelves.</summary>
        public int StockUnits { get; set; }

        /// <summary>Stock orders placed but not yet delivered.</summary>
        public int PendingStockOrders { get; set; }

        /// <summary>Staff currently employed.</summary>
        public int StaffCount { get; set; }

        /// <summary>The highest reputation tier reached (see ProgressionRules).</summary>
        public int Tier { get; set; }

        /// <summary>Inspections passed (graded A or B).</summary>
        public int InspectionsPassed { get; set; }

        /// <summary>The best inspection grade so far ('A', 'B', 'C', 'F'); '\0' before the first.</summary>
        public char BestInspectionGrade { get; set; }

        /// <summary>Animals born in the shop through breeding.</summary>
        public int PetsBred { get; set; }

        /// <summary>Every species the shop has sold at least once.</summary>
        public IReadOnlyCollection<Pet.Species> SpeciesSold { get; set; } = NoSpecies;

        /// <summary>True when auto-reorder is on for at least one category.</summary>
        public bool AutoReorderOn { get; set; }

        /// <summary>One-shot: the furniture catalogue has been opened.</summary>
        public bool CatalogueOpened { get; set; }

        /// <summary>One-shot: a delivery crate has been collected.</summary>
        public bool CrateCollected { get; set; }

        /// <summary>One-shot: a stock order has been placed.</summary>
        public bool StockOrdered { get; set; }

        /// <summary>One-shot: a day has been closed.</summary>
        public bool DayClosed { get; set; }

        /// <summary>Placed pieces with catalogue id <paramref name="id"/>.</summary>
        public int Placed(string id) => Count(PlacedById, id);

        /// <summary>Placed pieces of catalogue type <paramref name="type"/>.</summary>
        public int PlacedOfType(string type) => Count(PlacedByType, type);

        /// <summary>Pieces of <paramref name="id"/> placed, owned or on order: the player has bought one.</summary>
        public int Acquired(string id) => Placed(id) + Count(OwnedById, id) + Count(OnOrderById, id);

        /// <summary>True when <paramref name="species"/> has been sold at least once.</summary>
        public bool HasSold(Pet.Species species)
        {
            if (SpeciesSold == null) return false;
            foreach (var s in SpeciesSold) if (s == species) return true;
            return false;
        }

        /// <summary>Distinct species sold.</summary>
        public int SpeciesSoldCount => SpeciesSold?.Count ?? 0;

        private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
            counts != null && key != null && counts.TryGetValue(key, out int n) ? n : 0;
    }
}
