using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Localization;

namespace PetShop.UI
{
    /// <summary>
    /// The staff board: three applicants a day, each with a speed, a wage and a sign-on fee,
    /// against whoever is already on the payroll.
    ///
    /// Hiring used to be a single anonymous button, which made staffing a yes/no question.
    /// Cards turn it into a choice — pay up for someone quick, or take a plodder cheap and
    /// let the queue grow.
    /// </summary>
    public class StaffPanel : MonoBehaviour
    {
        private GameObject  _root;
        private GameManager _game;

        private TMP_Text _summaryLabel;
        private Transform _cardRoot;
        private Transform _payrollRoot;

        private readonly List<GameObject> _spawned = new();

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("StaffDim", canvas, Vector2.zero, Vector2.one,
                                    UIFactory.Dim);

            var panel = UIFactory.ModalPanel("Staff", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-430f, -250f), new Vector2(430f, 250f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.LabelKey("Header", panel.transform, "staffpanel.title",
                new Vector2(0.03f, 0.89f), new Vector2(0.5f, 0.97f), 24f, UIFactory.Ink);

            var close = UIFactory.ButtonKey("Close", panel.transform, "common.close_esc",
                new Vector2(0.79f, 0.895f), new Vector2(0.97f, 0.965f), 15f, null, "Esc");
            close.onClick.AddListener(Hide);

            _summaryLabel = UIFactory.Label("Summary", panel.transform, "",
                new Vector2(0.03f, 0.79f), new Vector2(0.97f, 0.875f), 16f, UIFactory.InkMuted);

            UIFactory.LabelKey("Applicants", panel.transform, "staffpanel.applicants",
                new Vector2(0.03f, 0.70f), new Vector2(0.60f, 0.77f), 17f, UIFactory.Ink);

            _cardRoot = UIFactory.Node("Cards", panel.transform,
                                       new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.69f)).transform;

            UIFactory.LabelKey("OnShift", panel.transform, "staffpanel.on_payroll",
                new Vector2(0.03f, 0.22f), new Vector2(0.60f, 0.29f), 17f, UIFactory.Ink);

            _payrollRoot = UIFactory.Node("Payroll", panel.transform,
                                          new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.21f)).transform;

            _root.SetActive(false);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Rebuilds the cards and payroll in the new language while the board is open.</summary>
        private void OnLanguageChanged()
        {
            if (IsOpen) Refresh();
        }

        public void Show()
        {
            if (_root == null) return;

            // Candidates are generated each morning; a save loaded mid-run may have none yet.
            if (_game != null && _game.Candidates.Count == 0) _game.RefreshCandidates();

            Refresh();
            _root.SetActive(true);
            _game?.SetModalOpen(true);
        }

        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        private void Refresh()
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();

            if (_game == null) return;

            var shop = _game.Shop;
            _summaryLabel.text = Loc.F("staffpanel.summary", RoleCounts(), _game.Payroll,
                shop != null ? shop.Balance : 0f, Mathf.Max(0, StaffRoster.MaxStaff - _game.StaffCount));

            BuildCards();
            BuildPayroll();
        }

        /// <summary>How many staff work each job, e.g. "1 cashier · 1 restocker · 0 feeders".</summary>
        private string RoleCounts()
        {
            var parts = new List<string>();
            foreach (StaffRole role in System.Enum.GetValues(typeof(StaffRole)))
            {
                int n = 0;
                foreach (var member in _game.Staff) if (member != null && member.Role == role) n++;
                parts.Add(Loc.Plural("staffpanel.role_count." + LocNames.ToKey(role.ToString()), n));
            }
            return string.Join("  ·  ", parts);
        }

        /// <summary>Role, stars and skill word, e.g. "Cashier  ★★★☆☆ capable".</summary>
        private static string RoleAndSkill(StaffRole role, int skill) =>
            $"{LocNames.Role(role)}  {StaffCandidate.Stars(skill)} {StaffCandidate.SkillWordFor(skill)}";

        /// <summary>The job after <paramref name="role"/>, wrapping round.</summary>
        private static StaffRole NextRole(StaffRole role)
        {
            int count = System.Enum.GetValues(typeof(StaffRole)).Length;
            return (StaffRole)(((int)role + 1) % count);
        }

        /// <summary>One card per applicant, side by side.</summary>
        private void BuildCards()
        {
            var candidates = _game.Candidates;
            if (candidates.Count == 0)
            {
                var none = UIFactory.Label("NoCandidates", _cardRoot,
                    Loc.T("staffpanel.no_candidates"),
                    Vector2.zero, Vector2.one, 15f, UIFactory.InkMuted);
                _spawned.Add(none.gameObject);
                return;
            }

            float w = 1f / candidates.Count;
            for (int i = 0; i < candidates.Count; i++) BuildCard(i, i * w, w, candidates[i]);
        }

        /// <summary>One applicant: name, job and skill, speed, terms and a hire button.</summary>
        private void BuildCard(int i, float x, float w, StaffCandidate candidate)
        {
            var card = UIFactory.Panel($"Card_{i}", _cardRoot,
                new Vector2(x + 0.006f, 0f), new Vector2(x + w - 0.006f, 1f),
                UIFactory.Raised);
            _spawned.Add(card);

            UIFactory.Label("Name", card.transform, candidate.Name,
                new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.96f), 17f, UIFactory.Ink);

            UIFactory.Label("Role", card.transform, RoleAndSkill(candidate.Role, candidate.Skill),
                new Vector2(0.06f, 0.64f), new Vector2(0.94f, 0.80f), 14f, UIFactory.Ink);

            UIFactory.Label("Speed", card.transform,
                Loc.F("staffpanel.speed", candidate.SpeedWord, candidate.ServiceSeconds),
                new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.64f), 13f, UIFactory.Accent);

            UIFactory.Label("Terms", card.transform,
                Loc.F("staffpanel.terms", candidate.DailyWage, candidate.SignOnFee),
                new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.48f), 14f, UIFactory.InkMuted);

            BuildHireButton(i, card.transform, candidate);
        }

        /// <summary>Type size of a hire button that says why it is disabled; smaller to fit the reason.</summary>
        private const float HireReasonFontSize = 12f;
        /// <summary>Type size of an enabled hire button.</summary>
        private const float HireFontSize = 15f;

        /// <summary>
        /// Hire button for a card, disabled with the reason on it when the applicant cannot be taken
        /// on (the shop is full, or a cashier has no counter to work behind).
        /// </summary>
        private void BuildHireButton(int i, Transform card, StaffCandidate candidate)
        {
            bool canHire = _game.CanHire(candidate, out string reason);
            var hire = UIFactory.Button($"Hire_{i}", card,
                canHire ? Loc.T("staffpanel.hire") : ReasonText(reason),
                new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.21f),
                canHire ? HireFontSize : HireReasonFontSize,
                canHire ? UIFactory.ButtonOn : UIFactory.ButtonBg);

            hire.interactable = canHire;
            hire.onClick.AddListener(() => { _game.HireCandidate(candidate); Refresh(); });
        }

        /// <summary>
        /// The hire refusal <paramref name="reason"/> (an English id from <see cref="StaffRoster"/>) in the
        /// current language; an unknown reason is shown as given.
        /// </summary>
        private static string ReasonText(string reason) =>
            reason == StaffRoster.NoCounterReason ? Loc.T("staffpanel.reason.no_counter")
          : reason == StaffRoster.NoRoomReason    ? Loc.T("staffpanel.reason.no_room")
          : reason;

        /// <summary>Who is on shift, with a button to let each of them go.</summary>
        private void BuildPayroll()
        {
            var staff = _game.Staff;
            if (staff.Count == 0)
            {
                var none = UIFactory.Label("NoStaff", _payrollRoot,
                    Loc.T("staffpanel.no_staff"),
                    Vector2.zero, Vector2.one, 14f, UIFactory.Bad);
                _spawned.Add(none.gameObject);
                return;
            }

            float rowHeight = 1f / Mathf.Max(StaffRoster.MaxStaff, staff.Count);

            for (int i = 0; i < staff.Count; i++)
            {
                if (staff[i] == null) continue;
                float top = 1f - i * rowHeight;
                BuildPayrollRow(i, staff[i], top, top - rowHeight * 0.9f);
            }
        }

        /// <summary>One member of staff: terms, a button to change job, and one to let them go.</summary>
        private void BuildPayrollRow(int i, Assistant member, float top, float bottom)
        {
            var row = UIFactory.Label($"Staff_{i}", _payrollRoot,
                $"{member.StaffName}   ·   {RoleAndSkill(member.Role, member.Skill)}   ·   " +
                $"{Loc.F("staffpanel.wage_day", member.DailyWage)}   ·   {Loc.F("staffpanel.service_every", member.ServiceSeconds)}",
                new Vector2(0f, bottom), new Vector2(0.62f, top), 13f, UIFactory.Ink);
            _spawned.Add(row.gameObject);

            var role = UIFactory.Button($"Role_{i}", _payrollRoot, Loc.F("staffpanel.make_role", LocNames.Role(NextRole(member.Role))),
                new Vector2(0.63f, bottom), new Vector2(0.80f, top), 12f);
            role.onClick.AddListener(() => { member.SetRole(NextRole(member.Role)); Refresh(); });
            _spawned.Add(role.gameObject);

            var fire = UIFactory.Button($"Fire_{i}", _payrollRoot, Loc.T("staffpanel.let_go"),
                new Vector2(0.81f, bottom), new Vector2(1f, top), 13f);
            fire.onClick.AddListener(() => { _game.FireAssistant(member); Refresh(); });
            _spawned.Add(fire.gameObject);
        }
    }
}
