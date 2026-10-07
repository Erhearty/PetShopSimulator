using System.Collections.Generic;
using System.Text;
using PetShop.Core;
using PetShop.Localization;

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
        public static string NothingUrgent => Loc.T("overview.nothing_urgent");

        private BookPage _overview;

        private void BuildOverviewPage()
        {
            _overview = AddPage("Overview", UIFactory.TextBody);
        }

        private void RefreshOverview()
        {
            var shop = _game.Shop;
            var sb   = new StringBuilder();
            sb.AppendLine(Loc.F("common.day", shop.Day));
            sb.AppendLine(Loc.F("overview.balance", shop.Balance));
            sb.AppendLine(Loc.F("overview.reputation", shop.Reputation));
            sb.AppendLine(UIFactory.Tint(Loc.F("overview.bill", shop.DailyOutgoings), UIFactory.InkMuted));
            sb.AppendLine();
            sb.AppendLine($"<b>{Loc.T("overview.todo")}</b>");
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
            todos.Add(Loc.Plural("overview.todo.queue", queue.Length, InteractKey));
        }

        private void AddBillTodo(List<string> todos)
        {
            var shop = _game.Shop;
            if (shop.Balance >= shop.DailyOutgoings) return;
            todos.Add(Loc.F("overview.todo.bill", shop.DailyOutgoings));
        }

        private void AddShelfTodos(List<string> todos)
        {
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null || !shelf.IsEmpty) continue;
                todos.Add(_game.Shop.Warehouse(shelf.Category) > 0
                    ? Loc.F("overview.todo.shelf_restock", LocNames.CategoryTitle(shelf.Category), InteractKey)
                    : Loc.F("overview.todo.shelf_order", LocNames.CategoryTitle(shelf.Category)));
            }
        }

        private void AddPenTodos(List<string> todos)
        {
            foreach (var pen in _game.Pens)
            {
                if (pen == null) continue;
                if (pen.NeedsService)
                    todos.Add(Loc.F(pen.NeedsFeeding ? "overview.todo.pen_feed" : "overview.todo.pen_clean",
                                    LocNames.Species(pen.PenSpecies), InteractKey, pen.ServiceCost));
                else if (pen.Count == 0)
                    todos.Add(Loc.F("overview.todo.pen_empty", LocNames.Species(pen.PenSpecies), InteractKey));
            }
        }
    }
}
