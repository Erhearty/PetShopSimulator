using System.Collections.Generic;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Progression.Quests
{
    /// <summary>
    /// Every quest in the game, as data, chapter by chapter in the order they are shown.
    /// Tutorial quests run strictly in list order; quests in later chapters may complete in any
    /// order. Instructions name keys with <see cref="QuestDefinition.KeyToken"/> tokens so a
    /// rebind shows up in the text. To add a quest, append a line to its chapter's array.
    /// </summary>
    public static class QuestCatalog
    {
        /// <summary>Catalogue type shared by both shelf sizes.</summary>
        public const string ShelfType = "shelf";

        /// <summary>Reward for each ordinary tutorial step.</summary>
        public const float TutorialStepReward = 25f;
        /// <summary>Reward for the last tutorial step.</summary>
        public const float TutorialFinalReward = 100f;
        /// <summary>Reward for an Early chapter quest.</summary>
        public const float EarlyReward = 150f;
        /// <summary>Reward for a Mid chapter quest.</summary>
        public const float MidReward = 300f;
        /// <summary>Reward for an End chapter quest.</summary>
        public const float EndReward = 600f;

        /// <summary>Shelves to own in the Early chapter.</summary>
        public const int EarlyShelfTarget = 3;
        /// <summary>Lifetime revenue to reach in the Early chapter.</summary>
        public const int EarlyRevenueTarget = 1000;
        /// <summary>Distinct species to sell in the Mid chapter.</summary>
        public const int MidSpeciesTarget = 3;
        /// <summary>Staff to employ in the Mid chapter.</summary>
        public const int MidStaffTarget = 2;
        /// <summary>Best single-day profit to reach in the Mid chapter.</summary>
        public const int MidDayProfitTarget = 500;
        /// <summary>Lifetime revenue to reach in the End chapter.</summary>
        public const int EndRevenueTarget = 25000;

        private const int One = 1;
        private const char GradeA = 'A';

        // Key tokens. Declared before the quest arrays: static fields initialise in text order.
        private static readonly string Interact = QuestDefinition.KeyToken(GameAction.Interact);
        private static readonly string Build    = QuestDefinition.KeyToken(GameAction.BuildMode);
        private static readonly string Rotate   = QuestDefinition.KeyToken(GameAction.BuildRotate);
        private static readonly string Ledger   = QuestDefinition.KeyToken(GameAction.Ledger);
        private static readonly string EndDay   = QuestDefinition.KeyToken(GameAction.EndDay);

        private static readonly string OpenCatalogue = $"press {Build} (or 1-{BuildCatalog.HotkeyOrder.Length})";

        private static readonly QuestDefinition[] Tutorial =
        {
            new("tut_open_catalogue", QuestChapter.Tutorial, "Open the furniture catalogue",
                $"Your shop is empty. Press {Build} (or a number key 1-{BuildCatalog.HotkeyOrder.Length}) to open the furniture catalogue.",
                TutorialStepReward, c => c.CatalogueOpened),
            new("tut_order_counter", QuestChapter.Tutorial, "Order a counter",
                "In the catalogue, find the Counter row and click Order. It arrives as a crate on the forecourt.",
                TutorialStepReward, c => c.Acquired(BuildCatalog.Counter) >= One),
            new("tut_collect_crate", QuestChapter.Tutorial, "Collect the crate",
                $"Walk to the delivery crate on the forecourt and press {Interact} to unpack it.",
                TutorialStepReward, c => c.CrateCollected),
            new("tut_place_counter", QuestChapter.Tutorial, "Place the counter",
                $"Open the catalogue ({OpenCatalogue}), click Place on the Counter row, then left-click the floor to set it down. {Rotate} rotates.",
                TutorialStepReward, c => c.Placed(BuildCatalog.Counter) >= One),
            new("tut_order_shelf", QuestChapter.Tutorial, "Order a shelf",
                $"Open the catalogue ({OpenCatalogue}) and click Order on the Small Shelf row.",
                TutorialStepReward, c => c.Acquired(BuildCatalog.ShelfSmall) + c.Acquired(BuildCatalog.ShelfLarge) >= One),
            new("tut_place_shelf", QuestChapter.Tutorial, "Place the shelf",
                $"Unpack the shelf's crate with {Interact}, then open the catalogue ({OpenCatalogue}), click Place on the shelf row and left-click the floor.",
                TutorialStepReward, c => c.PlacedOfType(ShelfType) >= One),
            new("tut_order_stock", QuestChapter.Tutorial, "Order stock",
                $"Press {Ledger} to open the ledger and click one of the Order buttons along the bottom. Restock a shelf with {Interact}.",
                TutorialStepReward, c => c.StockOrdered),
            new("tut_first_sale", QuestChapter.Tutorial, "Serve a customer",
                $"Your shop is open during the day. When a customer queues, stand at the counter and press {Interact} to serve them.",
                TutorialStepReward, c => c.LifetimeRevenue > 0f),
            new("tut_close_day", QuestChapter.Tutorial, "Close the day",
                $"Press {EndDay} to close up and see the day's results.",
                TutorialFinalReward, c => c.DayClosed),
        };

        private static readonly QuestDefinition[] Early =
        {
            new("early_three_shelves", QuestChapter.Early, $"Own {EarlyShelfTarget} shelves",
                $"Order shelves in the catalogue ({OpenCatalogue}), unpack each crate with {Interact} and place them.",
                EarlyReward, c => c.PlacedOfType(ShelfType) >= EarlyShelfTarget,
                c => (c.PlacedOfType(ShelfType), EarlyShelfTarget)),
            new("early_first_pen", QuestChapter.Early, "Open a pet corner",
                $"Order a Pet Pen in the catalogue ({OpenCatalogue}), unpack it with {Interact} and place it. Press Q while placing to pick the species.",
                EarlyReward, c => c.Placed(BuildCatalog.PetPen) >= One),
            new("early_first_pet_sale", QuestChapter.Early, "Sell a pet",
                $"Walk up to a pen and press {Interact} to buy an animal for it. Customers will buy it from the pen.",
                EarlyReward, c => c.SpeciesSoldCount >= One),
            new("early_first_staff", QuestChapter.Early, "Hire your first worker",
                $"Press {Ledger}, click 'Staff board — hire and fire' and hire an applicant.",
                EarlyReward, c => c.StaffCount >= One),
            new("early_auto_reorder", QuestChapter.Early, "Switch on auto-reorder",
                $"Press {Ledger}, click 'Auto-reorder…' and switch on at least one aisle.",
                EarlyReward, c => c.AutoReorderOn),
            new("early_revenue", QuestChapter.Early, $"Take €{EarlyRevenueTarget:N0} in sales",
                $"Keep shelves stocked ({Interact} at a shelf) and serve customers at the counter ({Interact}).",
                EarlyReward, c => c.LifetimeRevenue >= EarlyRevenueTarget,
                c => ((int)c.LifetimeRevenue, EarlyRevenueTarget)),
            new("early_local_favourite", QuestChapter.Early, $"Become a {ProgressionRules.LocalFavouriteName}",
                $"Raise reputation to {ProgressionRules.LocalFavouriteReputation:0}: keep shelves full and pets fed. Check it in the ledger ({Ledger}).",
                EarlyReward, c => c.Tier >= ProgressionRules.LocalFavouriteTier),
        };

        private static readonly QuestDefinition[] Mid =
        {
            new("mid_back_strip", QuestChapter.Mid, "Build on the back strip",
                $"Open the catalogue ({OpenCatalogue}) and place any furniture on the newly opened strip behind the shop.",
                MidReward, c => c.HighestPlacedLotStage >= ProgressionRules.BackStripLotStage),
            new("mid_pass_inspection", QuestChapter.Mid, "Pass an inspection",
                $"The inspector calls every {InspectorGrader.InspectionIntervalDays} days. Feed and clean pens ({Interact} at a pen) and keep shelves stocked to earn an A or B.",
                MidReward, c => c.InspectionsPassed >= One),
            new("mid_breed", QuestChapter.Mid, "Breed a pet",
                $"Press {Ledger}, click 'Plan tonight's breeding' to pair two animals, then close the day ({EndDay}).",
                MidReward, c => c.PetsBred >= One),
            new("mid_species", QuestChapter.Mid, $"Sell {MidSpeciesTarget} different species",
                $"Place pens for different species (Q while placing picks the species) and stock them with {Interact}.",
                MidReward, c => c.SpeciesSoldCount >= MidSpeciesTarget,
                c => (c.SpeciesSoldCount, MidSpeciesTarget)),
            new("mid_staff", QuestChapter.Mid, $"Employ {MidStaffTarget} staff",
                $"Press {Ledger} and open 'Staff board — hire and fire' to hire another worker.",
                MidReward, c => c.StaffCount >= MidStaffTarget,
                c => (c.StaffCount, MidStaffTarget)),
            new("mid_day_profit", QuestChapter.Mid, $"Make €{MidDayProfitTarget:N0} profit in a day",
                $"Order stock ahead in the ledger ({Ledger}) — it is cheaper than restocking from the cash-and-carry.",
                MidReward, c => c.BestDayProfit >= MidDayProfitTarget,
                c => ((int)c.BestDayProfit, MidDayProfitTarget)),
            new("mid_trusted_name", QuestChapter.Mid, $"Become a {ProgressionRules.TrustedNameName}",
                $"Raise reputation to {ProgressionRules.TrustedNameReputation:0}. Good inspections help; check it in the ledger ({Ledger}).",
                MidReward, c => c.Tier >= ProgressionRules.TrustedNameTier),
        };

        private static readonly QuestDefinition[] End =
        {
            new("end_sell_horse", QuestChapter.End, "Sell a horse",
                $"Place a Pet Pen ({OpenCatalogue}), press Q while placing to pick Horse, then buy a horse for it with {Interact}.",
                EndReward, c => c.HasSold(Pet.Species.Horse)),
            new("end_grade_a", QuestChapter.End, "Earn an A from the inspector",
                $"Before an inspection day, feed and clean every pen ({Interact}) and fill every shelf.",
                EndReward, c => c.BestInspectionGrade == GradeA),
            new("end_revenue", QuestChapter.End, $"Take €{EndRevenueTarget:N0} in sales",
                $"Grow the shop: more shelves and pens, staff to serve, and auto-reorder in the ledger ({Ledger}).",
                EndReward, c => c.LifetimeRevenue >= EndRevenueTarget,
                c => ((int)c.LifetimeRevenue, EndRevenueTarget)),
            new("end_town_landmark", QuestChapter.End, $"Become a {ProgressionRules.TownLandmarkName}",
                $"Raise reputation to {ProgressionRules.TownLandmarkReputation:0}. Check it in the ledger ({Ledger}).",
                EndReward, c => c.Tier >= ProgressionRules.TownLandmarkTier),
            new("end_full_yard", QuestChapter.End, "Build in the yard",
                $"Open the catalogue ({OpenCatalogue}) and place furniture in the whole yard, unlocked at {ProgressionRules.TownLandmarkName}.",
                EndReward, c => c.HighestPlacedLotStage >= ProgressionRules.FullYardLotStage),
            new("end_sell_tiger", QuestChapter.End, "Sell a tiger",
                $"Place a Pet Pen ({OpenCatalogue}), press Q while placing to pick Tiger, then buy a tiger for it with {Interact}.",
                EndReward, c => c.HasSold(Pet.Species.Tiger)),
        };

        /// <summary>Every quest, tutorial first, in display order.</summary>
        public static readonly IReadOnlyList<QuestDefinition> All = Concat(Tutorial, Early, Mid, End);

        /// <summary>The quests of <paramref name="chapter"/>, in display order.</summary>
        public static List<QuestDefinition> InChapter(QuestChapter chapter) => InChapter(All, chapter);

        /// <summary>The quests of <paramref name="chapter"/> within <paramref name="quests"/>, in order.</summary>
        public static List<QuestDefinition> InChapter(IReadOnlyList<QuestDefinition> quests, QuestChapter chapter)
        {
            var list = new List<QuestDefinition>();
            foreach (var q in quests) if (q.Chapter == chapter) list.Add(q);
            return list;
        }

        /// <summary>The quest with <paramref name="id"/>, or null when unknown.</summary>
        public static QuestDefinition Get(string id)
        {
            foreach (var q in All) if (q.Id == id) return q;
            return null;
        }

        private static IReadOnlyList<QuestDefinition> Concat(params QuestDefinition[][] chapters)
        {
            var list = new List<QuestDefinition>();
            foreach (var chapter in chapters) list.AddRange(chapter);
            return list;
        }
    }
}
