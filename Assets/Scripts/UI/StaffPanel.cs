using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Commerce;

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
                                    new Color(0.03f, 0.05f, 0.08f, 0.62f));

            var panel = UIFactory.Panel("Staff", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-430f, -250f), new Vector2(430f, 250f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.Label("Header", panel.transform, "Staff",
                new Vector2(0.03f, 0.89f), new Vector2(0.5f, 0.97f), 24f, UIFactory.Ink);

            var close = UIFactory.Button("Close", panel.transform, "Close  (Esc)",
                new Vector2(0.79f, 0.895f), new Vector2(0.97f, 0.965f), 15f);
            close.onClick.AddListener(Hide);

            _summaryLabel = UIFactory.Label("Summary", panel.transform, "",
                new Vector2(0.03f, 0.79f), new Vector2(0.97f, 0.875f), 16f, UIFactory.InkMuted);

            UIFactory.Label("Applicants", panel.transform, "Looking for work today",
                new Vector2(0.03f, 0.70f), new Vector2(0.60f, 0.77f), 17f, UIFactory.Ink);

            _cardRoot = UIFactory.Node("Cards", panel.transform,
                                       new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.69f)).transform;

            UIFactory.Label("OnShift", panel.transform, "On the payroll",
                new Vector2(0.03f, 0.22f), new Vector2(0.60f, 0.29f), 17f, UIFactory.Ink);

            _payrollRoot = UIFactory.Node("Payroll", panel.transform,
                                          new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.21f)).transform;

            _root.SetActive(false);
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
            _summaryLabel.text =
                $"{_game.StaffCount} on the till  ·  wages € {_game.Payroll:N0} tonight  ·  " +
                $"balance € {(shop != null ? shop.Balance : 0f):N0}  ·  room for {3 - _game.StaffCount} more";

            BuildCards();
            BuildPayroll();
        }

        /// <summary>One card per applicant, side by side.</summary>
        private void BuildCards()
        {
            var candidates = _game.Candidates;
            if (candidates.Count == 0)
            {
                var none = UIFactory.Label("NoCandidates", _cardRoot,
                    "Nobody is looking for work today — try again tomorrow.",
                    Vector2.zero, Vector2.one, 15f, UIFactory.InkMuted);
                _spawned.Add(none.gameObject);
                return;
            }

            float w = 1f / candidates.Count;

            for (int i = 0; i < candidates.Count; i++)
            {
                StaffCandidate candidate = candidates[i];
                float x = i * w;

                var card = UIFactory.Panel($"Card_{i}", _cardRoot,
                    new Vector2(x + 0.006f, 0f), new Vector2(x + w - 0.006f, 1f),
                    new Color(0.11f, 0.14f, 0.19f, 0.95f));
                _spawned.Add(card);

                UIFactory.Label("Name", card.transform, candidate.Name,
                    new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.95f), 17f, UIFactory.Ink);

                UIFactory.Label("Speed", card.transform,
                    $"{candidate.SpeedWord}  —  one customer every {candidate.ServiceSeconds:0.#} s",
                    new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.72f), 13f, UIFactory.Accent);

                UIFactory.Label("Terms", card.transform,
                    $"€ {candidate.DailyWage:N0} a day\n€ {candidate.SignOnFee:N0} to sign",
                    new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.52f), 14f, UIFactory.InkMuted);

                bool room = _game.StaffCount < 3;
                var hire = UIFactory.Button($"Hire_{i}", card.transform,
                    room ? "Hire" : "No room",
                    new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.22f), 15f,
                    room ? UIFactory.ButtonOn : UIFactory.ButtonBg);

                hire.interactable = room;
                StaffCandidate captured = candidate;
                hire.onClick.AddListener(() => { _game.HireCandidate(captured); Refresh(); });
            }
        }

        /// <summary>Who is on shift, with a button to let each of them go.</summary>
        private void BuildPayroll()
        {
            var staff = _game.Staff;
            if (staff.Count == 0)
            {
                var none = UIFactory.Label("NoStaff", _payrollRoot,
                    "Nobody on the till — you will have to serve every customer yourself.",
                    Vector2.zero, Vector2.one, 14f, UIFactory.Bad);
                _spawned.Add(none.gameObject);
                return;
            }

            float rowHeight = 1f / Mathf.Max(3, staff.Count);

            for (int i = 0; i < staff.Count; i++)
            {
                var member = staff[i];
                if (member == null) continue;

                float top    = 1f - i * rowHeight;
                float bottom = top - rowHeight * 0.9f;

                var row = UIFactory.Label($"Staff_{i}", _payrollRoot,
                    $"{member.StaffName}   ·   € {member.DailyWage:N0} a day   ·   " +
                    $"one customer every {member.ServiceSeconds:0.#} s",
                    new Vector2(0f, bottom), new Vector2(0.78f, top), 14f, UIFactory.Ink);
                _spawned.Add(row.gameObject);

                var fire = UIFactory.Button($"Fire_{i}", _payrollRoot, "Let go",
                    new Vector2(0.80f, bottom), new Vector2(1f, top), 13f);
                fire.onClick.AddListener(() => { _game.FireAssistant(); Refresh(); });
                _spawned.Add(fire.gameObject);
            }
        }
    }
}
