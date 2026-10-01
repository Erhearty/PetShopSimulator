using System;
using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;

namespace PetShop.Core
{
    /// <summary>
    /// The payroll and the daily applicants: hiring, firing, the wage bill and saving/restoring
    /// who is on staff. Plain class owned by <see cref="GameManager"/>; Shop, Queue and
    /// StaffStation are read through the manager at call time, because the bootstrapper wires
    /// those fields after Awake. New assistants are parented under the manager's transform.
    /// </summary>
    internal sealed class StaffRoster
    {
        /// <summary>How many assistants fit behind the counter.</summary>
        public const int MaxStaff = 3;

        /// <summary>How many applicants turn up each morning.</summary>
        private const int CandidateCount = 3;

        /// <summary>Sideways gap between assistants standing at the till.</summary>
        private const float StationSpacing = 1.1f;

        /// <summary>Offset of the first assistant from the station centre.</summary>
        private const float StationStartOffset = -0.55f;

        /// <summary>Reputation lost when someone is let go.</summary>
        private const float FireReputationCost = 0.5f;

        private readonly GameManager _game;

        private readonly List<Assistant> _assistants = new();

        private readonly List<StaffCandidate> _candidates = new();

        /// <summary>Creates an empty roster for <paramref name="game"/>; nothing is read yet.</summary>
        public StaffRoster(GameManager game)
        {
            _game = game;
        }

        /// <summary>How many people are on the payroll.</summary>
        public int StaffCount => _assistants.Count;

        /// <summary>Everyone currently on the payroll, for the staff board.</summary>
        public IReadOnlyList<Assistant> Staff => _assistants;

        /// <summary>The people currently looking for work. Refreshed every morning.</summary>
        public IReadOnlyList<StaffCandidate> Candidates => _candidates;

        /// <summary>Replaces today's applicants with a fresh batch.</summary>
        public void RefreshCandidates()
        {
            _candidates.Clear();
            for (int i = 0; i < CandidateCount; i++) _candidates.Add(StaffCandidate.Generate());
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

        /// <summary>Hire one random assistant. Their first day's wage is due at close, not now.</summary>
        public bool HireAssistant() => HireCandidate(StaffCandidate.Generate());

        /// <summary>Takes a named applicant on: pays their sign-on fee and puts them to work.</summary>
        public bool HireCandidate(StaffCandidate candidate)
        {
            if (candidate == null) return false;
            if (_assistants.Count >= MaxStaff)
            {
                _game.Notify("There is no room behind that counter for another assistant.");
                return false;
            }
            if (!PaySignOnFee(candidate)) return false;

            Spawn(candidate);
            _candidates.Remove(candidate);
            PushToShop();
            _game.Notify($"Hired {candidate.Name} — {candidate.Role}, {candidate.SkillWord}, " +
                         $"€{candidate.DailyWage:N0} a day, {_assistants.Count} on the payroll.");
            return true;
        }

        /// <summary>Charges the candidate's sign-on fee; false (player told) when it can't be covered.</summary>
        private bool PaySignOnFee(StaffCandidate candidate)
        {
            if (candidate.SignOnFee <= 0f) return true;
            if (_game.Shop.ChangeBalance(-candidate.SignOnFee, $"Sign-on fee for {candidate.Name}"))
                return true;
            _game.Notify($"You cannot cover {candidate.Name}'s €{candidate.SignOnFee:N0} sign-on fee.");
            _game.Audio?.PlaySfx("deny");
            return false;
        }

        /// <summary>Builds an assistant from <paramref name="c"/> at the next till slot. Charges nothing.</summary>
        private Assistant Spawn(StaffCandidate c)
        {
            var staffStation = _game.StaffStation;
            Vector3 station = staffStation != null ? staffStation.position : Vector3.zero;
            Vector3 facing  = staffStation != null ? staffStation.forward  : Vector3.forward;
            Vector3 offset  = Vector3.right * (_assistants.Count * StationSpacing + StationStartOffset);

            var assistant = Assistant.Create(_game.transform, station + offset, facing,
                                             _game.Queue, _game.Shop, _assistants.Count);
            assistant.DailyWage      = c.DailyWage;
            assistant.ServiceSeconds = c.ServiceSeconds;
            assistant.StaffName      = c.Name;
            // Restockers need the catalogue to fill a shelf that has no product lines yet.
            assistant.Catalog        = _game.Catalog;
            assistant.SetSkill(c.Skill);
            assistant.SetRole(c.Role);
            _assistants.Add(assistant);
            return assistant;
        }

        /// <summary>Tells the shop the current head count and wage bill.</summary>
        private void PushToShop()
        {
            _game.Shop.SetStaff(_assistants.Count);
            _game.Shop.SetPayroll(Payroll);
        }

        /// <summary>Lets the most recently hired assistant go.</summary>
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
            PushToShop();

            // People talk: sacking staff costs you a little standing locally.
            _game.Shop.ChangeReputation(-FireReputationCost);
            _game.Notify($"Let {name} go — {_assistants.Count} left on the payroll.");
            return true;
        }

        /// <summary>Snapshot of everyone on staff for the save file.</summary>
        public List<SaveData.StaffSaveData> ToSave()
        {
            var list = new List<SaveData.StaffSaveData>();
            foreach (var a in _assistants)
            {
                if (a == null) continue;
                list.Add(new SaveData.StaffSaveData
                {
                    name           = a.StaffName,
                    role           = a.Role.ToString(),
                    skill          = a.Skill,
                    wage           = a.DailyWage,
                    serviceSeconds = a.ServiceSeconds,
                });
            }
            return list;
        }

        /// <summary>
        /// Rebuilds the staff from a save without charging any sign-on fee. A saved staff list
        /// comes back exactly as written; older saves with only a head count get that many fresh
        /// cashiers (zero stays zero).
        /// </summary>
        public void Restore(SaveData data)
        {
            ClearAll();
            if (data != null && data.StaffList != null && data.StaffList.Count > 0)
                foreach (var saved in data.StaffList) RestoreOne(saved);
            else if (data != null)
                for (int i = 0; i < Mathf.Min(data.Staff, MaxStaff); i++) Spawn(FreeCashier());
            PushToShop();
        }

        /// <summary>Removes everyone without the reputation hit of firing (used before a load).</summary>
        private void ClearAll()
        {
            foreach (var a in _assistants)
                if (a != null) UnityEngine.Object.Destroy(a.gameObject);
            _assistants.Clear();
        }

        /// <summary>Spawns one saved assistant, skipping entries beyond the cap.</summary>
        private void RestoreOne(SaveData.StaffSaveData saved)
        {
            if (saved == null || _assistants.Count >= MaxStaff) return;
            if (!Enum.TryParse(saved.role, out StaffRole role)) role = StaffRole.Cashier;
            int skill = Mathf.Clamp(saved.skill, StaffCandidate.MinSkill, StaffCandidate.MaxSkill);
            // Hand-edited or damaged entries fall back to the skill's defaults rather than
            // producing an instant-serving or negative-wage assistant.
            Spawn(new StaffCandidate
            {
                Name           = string.IsNullOrEmpty(saved.name) ? "Assistant" : saved.name,
                Role           = role,
                Skill          = skill,
                DailyWage      = saved.wage >= 0f ? saved.wage : StaffCandidate.WageForSkill(skill),
                ServiceSeconds = saved.serviceSeconds > 0f
                               ? saved.serviceSeconds : StaffCandidate.ServiceSecondsForSkill(skill),
                SignOnFee      = 0f,
            });
        }

        /// <summary>A freshly generated cashier with no sign-on fee, for head-count-only saves.</summary>
        private static StaffCandidate FreeCashier()
        {
            var c = StaffCandidate.Generate();
            c.SignOnFee = 0f;
            c.Role      = StaffRole.Cashier;
            return c;
        }
    }
}
