using System;
using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Progression.Quests
{
    /// <summary>
    /// Scene-side quest runner: owns the <see cref="QuestBook"/>, snapshots the game into a
    /// <see cref="QuestContext"/> every <see cref="EvaluateIntervalSeconds"/> while the shop trades
    /// and once at day close, and pays, announces and records each quest as it completes.
    /// </summary>
    public class QuestDirector : MonoBehaviour
    {
        /// <summary>Game seconds between quest checks while the day is running.</summary>
        public const float EvaluateIntervalSeconds = 0.5f;

        /// <summary>Ledger reason given for a quest's cash reward.</summary>
        public const string RewardReason = "Quest reward";

        private const string LogTag = "[Quests]";

        /// <summary>The quest book: chapter, completed quests and raised flags.</summary>
        public QuestBook Book { get; } = new QuestBook();

        /// <summary>Raised once per quest, after its reward has been paid.</summary>
        public event Action<QuestDefinition> QuestCompleted;

        /// <summary>Headlines of quests completed since this morning; the day summary appends them.</summary>
        public IReadOnlyList<string> LatestQuestHeadlines => _latestHeadlines;

        private readonly List<string> _latestHeadlines = new();
        private GameManager _game;
        private float       _sinceEvaluate;

        /// <summary>Wires the director to the game. Called by GameBootstrapper before the game begins.</summary>
        public void Init(GameManager game)
        {
            _game = game;
            if (_game != null) _game.OnDayStarted.AddListener(OnDayStarted);
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnDayStarted.RemoveListener(OnDayStarted);
        }

        /// <summary>Raises a one-shot flag (see <see cref="QuestFlags"/>); it is saved with the book.</summary>
        public void RaiseFlag(string flag) => Book.RaiseFlag(flag);

        private void Update()
        {
            if (_game == null || !_game.IsDayRunning || _game.IsGameOver) return;
            _sinceEvaluate += Time.deltaTime;
            if (_sinceEvaluate < EvaluateIntervalSeconds) return;
            _sinceEvaluate = 0f;
            EvaluateNow();
        }

        /// <summary>Day-close hook: raises the day-closed flag and checks every quest.</summary>
        public void OnDayClosed()
        {
            RaiseFlag(QuestFlags.DayClosed);
            EvaluateNow();
        }

        /// <summary>
        /// Checks the unlocked quests against the current game state, rewarding each newly
        /// completed one. Returns those quests; empty when nothing changed or there is no shop.
        /// </summary>
        public List<QuestDefinition> EvaluateNow()
        {
            if (_game == null || _game.Shop == null) return new List<QuestDefinition>();
            var done = Book.Evaluate(BuildContext());
            foreach (var quest in done) Reward(quest);
            return done;
        }

        /// <summary>Snapshots the game state the quest conditions look at.</summary>
        public QuestContext BuildContext()
        {
            var shop = _game.Shop;
            var ctx = new QuestContext
            {
                Balance            = shop.Balance,
                LifetimeRevenue    = shop.LifetimeRevenue,
                BestDayProfit      = shop.BestDayProfit,
                StockUnits         = CountStockUnits(shop),
                PendingStockOrders = shop.Orders.Count,
                StaffCount         = _game.StaffCount,
                PetsBred           = BreedingSystem.PetsBred,
                SpeciesSold        = new List<Pet.Species>(shop.SpeciesSold),
                AutoReorderOn      = AnyAutoReorder(_game.AutoReorder),
            };
            AddProgression(ctx);
            AddPlacedFurniture(ctx);
            AddFurnitureSupply(ctx);
            AddFlags(ctx);
            return ctx;
        }

        /// <summary>Pays the reward, then announces the quest and adds it to today's headlines.</summary>
        private void Reward(QuestDefinition quest)
        {
            if (quest.Reward > 0f) _game.Shop.ChangeBalance(quest.Reward, RewardReason);
            string headline = Loc.F("quest.complete", quest.LocalizedTitle, quest.Reward);
            Debug.Log($"{LogTag} Completed '{quest.Id}' ({quest.Chapter}) — reward €{quest.Reward:N0}");
            _latestHeadlines.Add(headline);
            _game.Notify(headline);
            _game.Audio?.PlaySfx(ProgressionDirector.MilestoneSfx);
            QuestCompleted?.Invoke(quest);
        }

        private void OnDayStarted(int day) => _latestHeadlines.Clear();

        /// <summary>Units in the stockroom plus units on every placed shelf.</summary>
        private int CountStockUnits(ShopManager shop)
        {
            int units = shop.WarehouseTotal;
            foreach (var shelf in _game.Shelves)
                if (shelf != null) units += shelf.TotalUnits;
            return units;
        }

        private static bool AnyAutoReorder(AutoReorder reorder)
        {
            if (reorder == null) return false;
            foreach (var rule in reorder.Rules)
                if (rule.Enabled) return true;
            return false;
        }

        private void AddProgression(QuestContext ctx)
        {
            var progression = _game.Progression;
            if (progression == null) return;
            ctx.Tier                = progression.Tier;
            ctx.InspectionsPassed   = progression.InspectionsPassed;
            ctx.BestInspectionGrade = progression.BestInspectionGrade;
        }

        /// <summary>Counts placed pieces by id and type, and the furthest lot stage any stands in.</summary>
        private void AddPlacedFurniture(QuestContext ctx)
        {
            var byId   = new Dictionary<string, int>();
            var byType = new Dictionary<string, int>();
            int highest = ProgressionRules.StarterLotStage;
            var placed = _game.Grid != null ? _game.Grid.GetAllPlaced() : new List<GridEntry>();
            foreach (var entry in placed)
            {
                if (entry.Data == null) continue;
                Increment(byId, entry.Data.Id);
                Increment(byType, entry.Data.Type);
                highest = Mathf.Max(highest, LotStageOf(entry.Root));
            }
            ctx.PlacedById            = byId;
            ctx.PlacedByType          = byType;
            ctx.HighestPlacedLotStage = highest;
        }

        /// <summary>Copies the unplaced inventory and counts the furniture still on order.</summary>
        private void AddFurnitureSupply(QuestContext ctx)
        {
            var supply = _game.Furniture;
            if (supply == null) return;
            ctx.OwnedById = new Dictionary<string, int>(supply.Owned);
            var onOrder = new Dictionary<string, int>();
            foreach (var order in supply.Pending)
                if (order != null) Increment(onOrder, order.CatalogId);
            ctx.OnOrderById = onOrder;
        }

        private void AddFlags(QuestContext ctx)
        {
            ctx.CatalogueOpened = Book.HasFlag(QuestFlags.CatalogueOpened);
            ctx.CrateCollected  = Book.HasFlag(QuestFlags.CrateCollected);
            ctx.StockOrdered    = Book.HasFlag(QuestFlags.StockOrdered);
            ctx.DayClosed       = Book.HasFlag(QuestFlags.DayClosed);
        }

        /// <summary>The first (smallest) lot stage whose cells hold <paramref name="cell"/>; stages nest.</summary>
        private int LotStageOf(Vector2Int cell)
        {
            var layout = _game.Layout;
            if (layout == null) return ProgressionRules.StarterLotStage;
            for (int stage = ProgressionRules.StarterLotStage; stage < ProgressionRules.FullYardLotStage; stage++)
                if (layout.LotStageCells(stage).Contains(cell)) return stage;
            return ProgressionRules.FullYardLotStage;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (key == null) return;
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }
    }
}
