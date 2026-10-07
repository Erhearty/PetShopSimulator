using System.Text;
using UnityEngine;
using PetShop.Localization;

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
            _staff = AddPage("Staff", UIFactory.TextBody);
            // Hiring is a choice between named applicants, so it gets its own board.
            var board = UIFactory.ButtonKey("StaffBoard", _staff.Root.transform, "staffpanel.open_board",
                new Vector2(0f, LinkRowBottom), new Vector2(0.5f, LinkRowTop), UIFactory.TextSmall, UIFactory.Accent);
            board.onClick.AddListener(() => HandOff(() => GetComponent<StaffPanel>()?.Show()));
        }

        private void RefreshStaff()
        {
            var shop = _game.Shop;
            var sb   = new StringBuilder();
            sb.AppendLine(Loc.Plural("staffpanel.payroll_summary", _game.StaffCount, shop.DailyWages));
            foreach (var member in _game.Staff)
                if (member != null)
                    sb.AppendLine($"•  {member.StaffName}<pos=40%>{Loc.F("staffpanel.wage_day", member.DailyWage)}<pos=65%>" +
                                  Loc.F("staffpanel.service_every", member.ServiceSeconds));
            sb.AppendLine();
            sb.AppendLine(_game.StaffCount == 0
                ? UIFactory.Tint(Loc.T("staffpanel.nobody_till"), UIFactory.Warning)
                : UIFactory.Tint(Loc.T("staffpanel.assistants_note"), UIFactory.InkMuted));
            sb.AppendLine();
            sb.AppendLine(Loc.F("staffpanel.bill", shop.DailyRent, shop.DailyWages, shop.DailyOutgoings, shop.Balance));
            _staff.Body.text = sb.ToString();
        }
    }
}
