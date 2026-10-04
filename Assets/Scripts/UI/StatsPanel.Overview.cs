using System.Collections.Generic;
using System.Text;
using PetShop.Core;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Overview page: day, balance and reputation, plus the three most urgent
    /// to-dos worked out from the real state of the shop.
    /// </summary>
    public partial class StatsPanel
    {
        /// <summary>How many to-dos the Overview lists.</summary>
        public const int MaxTodos = 3;

        /// <summary>Shown in place of the to-dos when nothing needs doing.</summary>
        public const string NothingUrgent = "Nothing urgent — the shop is running smoothly";

        private BookPage _overview;

        private void BuildOverviewPage()
        {
            _overview = AddPage("Overview", "How the shop is doing today, and what needs you next.", UIFactory.TextBody);
        }

        private void RefreshOverview()
        {
            var shop = _game.Shop;
            var sb   = new StringBuilder();
            sb.AppendLine($"Day {shop.Day}");
            sb.AppendLine($"Balance  € {shop.Balance:N2}");
            sb.AppendLine($"Reputation  {shop.Reputation:0}/100");
            sb.AppendLine(UIFactory.Tint($"Tonight's bill  € {shop.DailyOutgoings:N2}  (rent and wages)", UIFactory.InkMuted));
            sb.AppendLine();
            sb.AppendLine("<b>To do</b>");
            foreach (var todo in Todos()) sb.AppendLine($"•  {todo}");
            _overview.Body.text = sb.ToString();
        }

        /// <summary>
        /// Up to <see cref="MaxTodos"/> things that need the player, most urgent first: customers
        /// at the till, a bill the shop cannot pay, empty shelves, pens needing care or animals.
        /// Returns just <see cref="NothingUrgent"/> when there is nothing to do.
        /// </summary>
        public IReadOnlyList<string> Todos()
        {
            var todos = new List<string>();
            if (_game != null && _game.Shop != null)
            {
                AddQueueTodo(todos);
                AddBillTodo(todos);
                AddShelfTodos(todos);
                AddPenTodos(todos);
            }
            if (todos.Count == 0) todos.Add(NothingUrgent);
            return todos.Count > MaxTodos ? todos.GetRange(0, MaxTodos) : todos;
        }

        private static string InteractKey => InputBindings.Label(GameAction.Interact);

        private void AddQueueTodo(List<string> todos)
        {
            var queue = _game.Queue;
            if (queue == null || !queue.AnyWaiting) return;
            string who = queue.Length == 1 ? "1 customer is" : $"{queue.Length} customers are";
            todos.Add($"Customers waiting at the till — {who} queuing; serve with {InteractKey} at the counter");
        }

        private void AddBillTodo(List<string> todos)
        {
            var shop = _game.Shop;
            if (shop.Balance >= shop.DailyOutgoings) return;
            todos.Add($"Tonight's bill (€ {shop.DailyOutgoings:N0}) is more than you have — sell more before closing");
        }

        private void AddShelfTodos(List<string> todos)
        {
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null || !shelf.IsEmpty) continue;
                todos.Add(_game.Shop.Warehouse(shelf.Category) > 0
                    ? $"Shelf {shelf.Category} empty — restock ({InteractKey})"
                    : $"Shelf {shelf.Category} empty and the stockroom is out — order on the Stock page");
            }
        }

        private void AddPenTodos(List<string> todos)
        {
            foreach (var pen in _game.Pens)
            {
                if (pen == null) continue;
                if (pen.NeedsService)
                    todos.Add($"{pen.PenSpecies} pen {(pen.NeedsFeeding ? "needs feeding" : "needs cleaning")} " +
                              $"— {InteractKey} at the pen (€ {pen.ServiceCost:N2})");
                else if (pen.Count == 0)
                    todos.Add($"{pen.PenSpecies} pen is empty — buy an animal with {InteractKey} at the pen");
            }
        }
    }
}
