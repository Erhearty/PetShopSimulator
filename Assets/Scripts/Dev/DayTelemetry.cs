using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Customer;

namespace PetShop.Dev
{
    /// <summary>
    /// Soak-run recorder, added only when -telemetry or -quitafterdays is given. Counts
    /// customer outcomes through the day, appends one JSON line per closed day, logs every
    /// <see cref="SoakBands"/> violation as "[Soak] VIOLATION ...", and quits the player once
    /// the requested number of days has ended (exit code 0 when clean, 3 otherwise).
    /// </summary>
    public class DayTelemetry : MonoBehaviour
    {
        /// <summary>Process exit code when every band held.</summary>
        public const int ExitClean = 0;

        /// <summary>Process exit code when at least one band was violated.</summary>
        public const int ExitViolations = 3;

        /// <summary>Summary line written when the run ends.</summary>
        [System.Serializable]
        public class RunSummary
        {
            /// <summary>Always true, so a reader can tell this line from a day line.</summary>
            public bool summary = true;
            /// <summary>Days recorded this run.</summary>
            public int days;
            /// <summary>Gameplay seed, or <see cref="SoakBands.NoSeed"/>.</summary>
            public int seed;
            /// <summary>Total band violations logged this run.</summary>
            public int violations;
            /// <summary>Exit code passed to Application.Quit.</summary>
            public int exitCode;
        }

        private readonly List<DayRecord> _history = new();
        private DayRecord   _current = new();
        private GameManager _game;
        private ShopManager _shop;
        private string      _path;
        private int?        _quitAfterDays;
        private int         _violations;
        private bool        _finished;

        /// <summary>Wires the recorder up. <paramref name="path"/> may be null to skip the file.</summary>
        public void Init(GameManager game, ShopManager shop, string path, int? quitAfterDays)
        {
            _game = game;
            _shop = shop;
            _path = string.IsNullOrEmpty(path) ? null : path;
            _quitAfterDays = quitAfterDays;

            _game.OnDayEnded.AddListener(OnDayEnded);
            _game.OnGameOver.AddListener(OnGameOver);
            CustomerAI.CheckedOut         += OnCheckedOut;
            CustomerAI.WalkedOutEmpty     += OnWalkedOutEmpty;
            CustomerAI.GaveUp             += OnGaveUp;
            CustomerAI.NavigationTimedOut += OnNavigationTimedOut;
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.OnDayEnded.RemoveListener(OnDayEnded);
                _game.OnGameOver.RemoveListener(OnGameOver);
            }
            CustomerAI.CheckedOut         -= OnCheckedOut;
            CustomerAI.WalkedOutEmpty     -= OnWalkedOutEmpty;
            CustomerAI.GaveUp             -= OnGaveUp;
            CustomerAI.NavigationTimedOut -= OnNavigationTimedOut;
        }

        // ── Customer outcomes ──────────────────────────────────────────────────────────

        private void OnCheckedOut(CustomerAI customer)
        {
            _current.checkouts++;
            if (customer != null && customer.DistanceToTillAtCheckout > SoakBands.StrandedDistanceMetres)
                _current.strandedCheckouts++;
        }

        private void OnWalkedOutEmpty(CustomerAI _)       => _current.walkoutsEmpty++;
        private void OnGaveUp(CustomerAI _)               => _current.gaveUp++;
        private void OnNavigationTimedOut(CustomerAI _)   => _current.navTimeouts++;

        // ── Day close ──────────────────────────────────────────────────────────────────────

        private void OnDayEnded(DaySummary summary)
        {
            if (_finished || summary == null) return;

            DayRecord record = CompleteRecord(summary);
            _history.Add(record);
            _current = new DayRecord();

            AppendLine(JsonUtility.ToJson(record));
            ReportViolations(SoakBands.Check(record));
            if (_history.Count >= SoakBands.SalesWindowDays)
                ReportViolations(SoakBands.CheckWindow(
                    _history.GetRange(_history.Count - SoakBands.SalesWindowDays, SoakBands.SalesWindowDays)));

            if (_quitAfterDays.HasValue && _history.Count >= _quitAfterDays.Value) Finish();
        }

        /// <summary>A bankrupt shop never starts another day, so a timed run ends here instead.</summary>
        private void OnGameOver(string _)
        {
            if (_quitAfterDays.HasValue) Finish();
        }

        private DayRecord CompleteRecord(DaySummary summary)
        {
            DayRecord record = _current;
            record.day        = summary.Day;
            record.seed       = PlaytestOptions.Seed ?? SoakBands.NoSeed;
            record.sales      = summary.SaleCount;
            record.revenue    = summary.TotalRevenue;
            record.balance    = _shop != null ? _shop.Balance    : summary.ClosingBalance;
            record.reputation = _shop != null ? _shop.Reputation : summary.Reputation;
            return record;
        }

        private void ReportViolations(IReadOnlyList<string> violations)
        {
            foreach (string violation in violations)
            {
                _violations++;
                Debug.LogWarning($"[Soak] VIOLATION {violation}");
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            var summary = new RunSummary
            {
                days       = _history.Count,
                seed       = PlaytestOptions.Seed ?? SoakBands.NoSeed,
                violations = _violations,
                exitCode   = _violations == 0 ? ExitClean : ExitViolations,
            };
            AppendLine(JsonUtility.ToJson(summary));
            Debug.Log($"[Soak] Finished after {summary.days} day(s) with {summary.violations} violation(s).");
            Application.Quit(summary.exitCode);
        }

        private void AppendLine(string json)
        {
            if (_path == null) return;
            try
            {
                System.IO.File.AppendAllText(_path, json + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Soak] Could not write telemetry to {_path}: {e.Message}");
            }
        }
    }
}
