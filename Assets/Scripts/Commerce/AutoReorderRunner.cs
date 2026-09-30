using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Commerce
{
    /// <summary>
    /// Evaluates the game's <see cref="AutoReorder"/> rules while a day is running and places
    /// supplier orders through <see cref="GameManager.OrderStock"/>.
    /// </summary>
    public class AutoReorderRunner : MonoBehaviour
    {
        public const float AutoReorderIntervalSeconds = 5f;
        public const float FallbackUnitCost = 3.2f;

        private GameManager _game;
        private float _timer;
        private readonly HashSet<ProductCategory> _cantAffordNotified = new();

        /// <summary>Hooks the runner to <paramref name="game"/>'s day start.</summary>
        public void Attach(GameManager game)
        {
            _game = game;
            _game.OnDayStarted.AddListener(OnDayStarted);
        }

        private void OnDestroy()
        {
            if (_game != null) _game.OnDayStarted.RemoveListener(OnDayStarted);
        }

        private void OnDayStarted(int day)
        {
            _timer = 0f;
            _cantAffordNotified.Clear();
            Evaluate();
        }

        private void Update()
        {
            if (_game == null || !_game.IsDayRunning || _game.IsGameOver) return;
            _timer += Time.deltaTime;
            if (_timer < AutoReorderIntervalSeconds) return;
            _timer = 0f;
            Evaluate();
        }

        private void Evaluate()
        {
            var shop = _game.Shop;
            if (shop == null) return;
            var orders = _game.AutoReorder.Decide(OnHand, OrderCost, shop.Balance,
                                                  shop.DailyRent + shop.DailyWages).ToList();
            foreach (var (category, units) in orders)
                if (_game.OrderStock(category, units))
                    _game.Notify($"Auto-reorder: {units} {category} units ordered.");
            foreach (var category in _game.AutoReorder.LastSkipped)
                if (_cantAffordNotified.Add(category))
                    _game.Notify($"Auto-reorder: can't afford {category} without touching tonight's rent and wages.");
        }

        private int OnHand(ProductCategory c)
        {
            int total = _game.Shop.Warehouse(c) + _game.Shop.PendingUnits(c);
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null || shelf.Category != c) continue;
                foreach (var line in shelf.Lines) total += line.Units;
            }
            return total;
        }

        private float OrderCost(ProductCategory c)
        {
            var rule  = _game.AutoReorder.Rule(c);
            float unit = _game.Catalog != null ? _game.Catalog.AverageUnitCost(c) : FallbackUnitCost;
            return unit * ShopManager.WholesaleDiscount * (rule?.Units ?? 0);
        }
    }
}
