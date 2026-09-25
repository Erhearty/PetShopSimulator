using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;

namespace PetShop.Core
{
    /// <summary>
    /// The payroll and the daily applicants: hiring, firing and the wage bill. Plain class
    /// owned by <see cref="GameManager"/>; Shop, Queue and StaffStation are read through the
    /// manager at call time, because the bootstrapper wires those fields after Awake. New
    /// assistants are parented under the manager's transform.
    /// </summary>
    internal sealed class StaffRoster
    {
        private readonly GameManager _game;

        private readonly List<Assistant> _assistants = new();

        /// <summary>Creates an empty roster for <paramref name="game"/>; nothing is read yet.</summary>
        public StaffRoster(GameManager game)
        {
            _game = game;
        }

        public int StaffCount => _assistants.Count;

        /// <summary>Everyone currently on the payroll, for the staff board.</summary>
        public IReadOnlyList<Assistant> Staff => _assistants;

        /// <summary>Hire one assistant. Their first day's wage is due at close, not now.</summary>
        /// <summary>The three people currently looking for work. Refreshed every morning.</summary>
        public IReadOnlyList<StaffCandidate> Candidates => _candidates;

        private readonly List<StaffCandidate> _candidates = new();

        public void RefreshCandidates()
        {
            _candidates.Clear();
            for (int i = 0; i < 3; i++) _candidates.Add(StaffCandidate.Generate());
        }

        /// <summary>Total wages owed tonight, from the people actually on the payroll.</summary>
        public float Payroll
        {
            get
            {
                float total = 0f;
                foreach (var a in _assistants) if (a != null) total += a.DailyWage;
                return total;
            }
        }

        public bool HireAssistant() => HireCandidate(StaffCandidate.Generate());

        /// <summary>Takes a named applicant on: pays their sign-on fee and puts them on the till.</summary>
        public bool HireCandidate(StaffCandidate candidate)
        {
            if (candidate == null) return false;

            if (_assistants.Count >= 3)
            {
                _game.Notify("There is no room behind that counter for another assistant.");
                return false;
            }

            var shop = _game.Shop;
            if (candidate.SignOnFee > 0f &&
                !shop.ChangeBalance(-candidate.SignOnFee, $"Sign-on fee for {candidate.Name}"))
            {
                _game.Notify($"You cannot cover {candidate.Name}'s €{candidate.SignOnFee:N0} sign-on fee.");
                _game.Audio?.PlaySfx("deny");
                return false;
            }

            var staffStation = _game.StaffStation;
            Vector3 station = staffStation != null ? staffStation.position : Vector3.zero;
            Vector3 facing  = staffStation != null ? staffStation.forward  : Vector3.forward;
            Vector3 offset  = Vector3.right * (_assistants.Count * 1.1f - 0.55f);

            var assistant = Assistant.Create(_game.transform, station + offset, facing,
                                             _game.Queue, shop, _assistants.Count);
            assistant.DailyWage      = candidate.DailyWage;
            assistant.ServiceSeconds = candidate.ServiceSeconds;
            assistant.StaffName      = candidate.Name;
            _assistants.Add(assistant);

            _candidates.Remove(candidate);

            shop.SetStaff(_assistants.Count);
            shop.SetPayroll(Payroll);
            _game.Notify($"Hired {candidate.Name} — {candidate.SpeedWord} at the till, " +
                         $"€{candidate.DailyWage:N0} a day, {_assistants.Count} on the payroll.");
            return true;
        }

        public bool FireAssistant()
        {
            if (_assistants.Count == 0) { _game.Notify("There is nobody to let go."); return false; }
            return FireAssistant(_assistants[_assistants.Count - 1]);
        }

        /// <summary>Lets one named member of staff go, rather than whoever happens to be last.</summary>
        public bool FireAssistant(Assistant member)
        {
            if (member == null || !_assistants.Contains(member))
            {
                _game.Notify("There is nobody to let go.");
                return false;
            }

            string name = member.StaffName;
            _assistants.Remove(member);
            UnityEngine.Object.Destroy(member.gameObject);

            var shop = _game.Shop;
            shop.SetStaff(_assistants.Count);
            shop.SetPayroll(Payroll);

            // People talk: sacking staff costs you a little standing locally.
            shop.ChangeReputation(-0.5f);
            _game.Notify($"Let {name} go — {_assistants.Count} left on the payroll.");
            return true;
        }
    }
}
