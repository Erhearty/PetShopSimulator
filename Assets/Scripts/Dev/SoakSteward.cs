using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;

namespace PetShop.Dev
{
    /// <summary>
    /// Dev-only stand-in for a diligent player's stock chores in unattended <c>-furnish</c>
    /// command-line runs (the headless soak): turns on auto-reorder for every shelved category,
    /// then every few game seconds carries delivered pallets into the stockroom and shelves
    /// stockroom units, through the same <see cref="GameManager"/> calls the player's interact
    /// key uses. It hires nobody and changes no prices, wages or rules.
    /// </summary>
    public class SoakSteward : MonoBehaviour
    {
        /// <summary>Game seconds (scaled time) between chore passes.</summary>
        public const float ChoreIntervalSeconds = 3f;

        private GameManager _game;
        private float _timer;

        /// <summary>
        /// Sets the auto-reorder rules for the game's shelves and starts the chores.
        /// Call after the shop is furnished and before the first day starts.
        /// </summary>
        public static SoakSteward Begin(GameManager game)
        {
            if (game == null) return null;
            var capacities = CapacityByCategory(game.Shelves
                .Where(s => s != null)
                .Select(s => (s.Category, s.Lines.Count, s.MaxPerLine)));
            foreach (var pair in capacities)
            {
                var rule = game.AutoReorder.Rule(pair.Key);
                if (rule == null) continue;
                rule.Enabled   = true;
                rule.Threshold = ThresholdFor(pair.Value);
                rule.Units     = UnitsFor(pair.Value);
            }

            var steward = game.gameObject.GetComponent<SoakSteward>();
            if (steward == null) steward = game.gameObject.AddComponent<SoakSteward>();
            steward._game = game;
            string cats = capacities.Count > 0 ? string.Join(", ", capacities.Keys) : "none";
            Debug.Log($"[Soak] steward: auto-reorder on for {cats}, collecting deliveries and restocking shelves");
            return steward;
        }

        /// <summary>Units one shelf holds when every line is full.</summary>
        public static int ShelfCapacity(int lines, int maxPerLine) =>
            Mathf.Max(0, lines) * Mathf.Max(0, maxPerLine);

        /// <summary>Total shelf capacity per category, from (category, lines, units per line) rows.</summary>
        public static Dictionary<ProductCategory, int> CapacityByCategory(
            IEnumerable<(ProductCategory category, int lines, int maxPerLine)> shelves)
        {
            var result = new Dictionary<ProductCategory, int>();
            if (shelves == null) return result;
            foreach (var (category, lines, maxPerLine) in shelves)
            {
                result.TryGetValue(category, out int total);
                result[category] = total + ShelfCapacity(lines, maxPerLine);
            }
            return result;
        }

        /// <summary>Reorder once on-hand stock drops below what the shelves hold.</summary>
        public static int ThresholdFor(int capacity) => capacity;

        /// <summary>Order at least the default batch, or enough to refill every shelf.</summary>
        public static int UnitsFor(int capacity) => Mathf.Max(AutoReorder.DefaultUnits, capacity);

        private void Update()
        {
            if (_game == null || _game.IsGameOver || _game.Shop == null) return;
            _timer += Time.deltaTime;
            if (_timer < ChoreIntervalSeconds) return;
            _timer = 0f;
            CollectDeliveries();
            RestockShelves();
        }

        private void CollectDeliveries()
        {
            foreach (var crate in FindObjectsByType<DeliveryCrate>(FindObjectsSortMode.None))
                if (crate != null && !crate.IsFurniture && crate.Units > 0)
                    _game.CollectDelivery(crate);
        }

        private void RestockShelves()
        {
            var shelves = _game.Shelves.ToList();
            foreach (var shelf in shelves)
            {
                if (shelf == null || !shelf.HasSpace) continue;
                // Like a player waiting for the pallet: only restock when the stockroom covers the
                // gap, since the player path tops up the rest at cash-and-carry prices.
                int gap = shelf.Lines.Count * shelf.MaxPerLine - shelf.TotalUnits;
                int stock = _game.Shop.Warehouse(shelf.Category);
                if (stock > 0 && stock >= gap) _game.RestockShelf(shelf);
            }
        }
    }
}
