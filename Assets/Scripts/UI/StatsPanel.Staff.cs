using System.Text;
using UnityEngine;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Staff page: who is on the payroll, what they cost tonight, and the way
    /// through to the staff board to hire and fire.
    /// </summary>
    public partial class StatsPanel
    {
        private BookPage _staff;

        private void BuildStaffPage()
        {
            _staff = AddPage("Staff", "Your assistants, their wages, and hiring.", UIFactory.TextBody);
            // Hiring is a choice between named applicants, so it gets its own board.
            var board = UIFactory.Button("StaffBoard", _staff.Root.transform, "Open staff board — hire and fire",
                new Vector2(0f, LinkRowBottom), new Vector2(0.5f, LinkRowTop), UIFactory.TextSmall, UIFactory.Accent);
            board.onClick.AddListener(() => HandOff(() => GetComponent<StaffPanel>()?.Show()));
        }

        private void RefreshStaff()
        {
            var shop = _game.Shop;
            var sb   = new StringBuilder();
            sb.AppendLine($"{_game.StaffCount} assistant(s) on the payroll — € {shop.DailyWages:N0} in wages tonight.");
            foreach (var member in _game.Staff)
                if (member != null)
                    sb.AppendLine($"•  {member.StaffName}<pos=40%>€ {member.DailyWage:N0} a day<pos=65%>" +
                                  $"one customer every {member.ServiceSeconds:0.#} s");
            sb.AppendLine();
            sb.AppendLine(_game.StaffCount == 0
                ? UIFactory.Tint("Nobody on the till — you must serve every customer yourself.", UIFactory.Warning)
                : UIFactory.Tint("Assistants work the till on their own, slower than you; you can still help clear the queue.",
                                 UIFactory.InkMuted));
            sb.AppendLine();
            sb.AppendLine($"Tonight's bill: rent € {shop.DailyRent:N2} + wages € {shop.DailyWages:N2} = " +
                          $"<b>€ {shop.DailyOutgoings:N2}</b>, against € {shop.Balance:N2} in hand.");
            _staff.Body.text = sb.ToString();
        }
    }
}
