using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PetShop.Shop;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Customer;

namespace PetShop.Core
{
    /// <summary>
    /// Top-level orchestrator. Owns the day loop, the furniture registry and the keyboard
    /// shortcuts that are not tied to the player character, and is the public facade the
    /// rest of the game talks to. Save/load and new-game setup are delegated to
    /// <see cref="SaveLoadController"/>, shop-floor interactions to
    /// <see cref="ShopFloorActions"/>, and hiring/firing to <see cref="StaffRoster"/>.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Wiring (set by GameBootstrapper) ──────────────────────────────────
        [HideInInspector] public GridManager     Grid;
        [HideInInspector] public BuildMode       Build;
        [HideInInspector] public ShopManager     Shop;
        [HideInInspector] public CustomerSpawner Spawner;
        [HideInInspector] public AudioManager    Audio;
        [HideInInspector] public ShopGenerator   Generator;
        [HideInInspector] public CheckoutQueue    Queue;

        // ── Events for UI ─────────────────────────────────────────────────────
        public UnityEvent<DaySummary> OnDayEnded     = new();
        public UnityEvent<int>        OnDayStarted   = new();
        public UnityEvent<string>     OnNotification = new();
        public UnityEvent<string>     OnInfoPanel    = new();   // long text for the info popup
        public UnityEvent<string>     OnGameOver     = new();

        [Header("Day length")]
        [Tooltip("Real seconds of trading per in-game day. Override with -daylength <seconds>.")]
        public float DayLengthSeconds = 210f;

        public ItemDatabase Catalog { get; private set; }

        /// <summary>0 at opening time, 1 at closing time.</summary>
        public float DayProgress => DayLengthSeconds <= 0f ? 0f
            : Mathf.Clamp01(_dayElapsed / DayLengthSeconds);

        /// <summary>Shop-floor clock, 09:00 to 18:00 across the trading day.</summary>
        public string ClockText
        {
            get
            {
                float hours   = Mathf.Lerp(9f, 18f, DayProgress);
                int   hour    = Mathf.FloorToInt(hours);
                int   minutes = Mathf.FloorToInt((hours - hour) * 60f);
                return $"{hour:00}:{minutes:00}";
            }
        }
        public bool IsBuildModeActive => Build != null && Build.IsActive;

        /// <summary>True while a blocking panel (day results, game over) is up.</summary>
        public bool IsModalOpen { get; private set; }

        public void SetModalOpen(bool open) => IsModalOpen = open;
        public bool IsGameOver        { get; private set; }
        public bool IsDayRunning      { get; private set; }

        private readonly List<ShelfUnit> _shelves    = new();
        private readonly List<PetPen>    _pens       = new();

        /// <summary>Where staff stand, and which way they face. Set by the bootstrapper.</summary>
        [HideInInspector] public Transform StaffStation;

        /// <summary>Every shelf currently placed. Read-only view for the UI.</summary>
        public IReadOnlyList<ShelfUnit> Shelves => _shelves;

        /// <summary>Every pen currently placed. Read-only view for the UI.</summary>
        public IReadOnlyList<PetPen> Pens => _pens;

        /// <summary>How many shoppers are inside right now.</summary>
        public int CustomersInShop => Spawner != null ? Spawner.LiveCustomers : 0;
        private bool  _endingDay;
        private float _dayElapsed;
        private bool  _autoContinue;

        // ── Collaborators (created in Awake) ──────────────────────────────────
        private SaveLoadController _saveLoad;
        private ShopFloorActions   _floor;
        private StaffRoster        _roster;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _saveLoad = new SaveLoadController(this);
            _floor    = new ShopFloorActions(this);
            _roster   = new StaffRoster(this);
            Catalog  = ItemDatabase.Load();
            ApplyCommandLineOverrides();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by GameBootstrapper once the room and player exist.</summary>
        public void Begin()
        {
            Shop?.OnDeliveryArrived.AddListener(_floor.OnDeliveryArrived);

            var save = SaveSystem.Load();
            if (save != null) _saveLoad.LoadGame(save);
            else              _saveLoad.NewGame();

            Generator?.BakeNavMesh();
            StartDay();
        }

        private void Update()
        {
            if (IsGameOver || IsModalOpen) return;

            if (Input.GetKeyDown(KeyCode.F5)) SaveGame();

            if (!IsBuildModeActive &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                EndDay();
                return;
            }

            if (!IsDayRunning || DayLengthSeconds <= 0f) return;

            _dayElapsed += Time.deltaTime;

            Shop?.PollDeliveries(DayProgress);

            // Stop letting new customers in shortly before closing time
            if (Spawner != null && !Spawner.DoorsClosed && DayProgress > 0.88f)
                Spawner.CloseDoors();

            if (_dayElapsed >= DayLengthSeconds) EndDay();
        }

        /// <summary>
        /// Honours -daylength &lt;seconds&gt; so the game can be driven headlessly, and
        /// so players can pick a pace without rebuilding.
        /// </summary>
        private void ApplyCommandLineOverrides()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-daylength" && float.TryParse(args[i + 1], out float seconds) && seconds > 0f)
                {
                    DayLengthSeconds = seconds;
                    Debug.Log($"[Game] Day length set to {seconds}s from the command line.");
                }
            }
            _autoContinue = Application.isBatchMode;
        }

        // ── Furniture registry ────────────────────────────────────────────────

        public void RegisterFurniture(GameObject go)
        {
            if (go == null) return;
            var shelf = go.GetComponent<ShelfUnit>();
            if (shelf != null && !_shelves.Contains(shelf)) _shelves.Add(shelf);

            var pen = go.GetComponent<PetPen>();
            if (pen != null && !_pens.Contains(pen)) _pens.Add(pen);

            PushListsToSpawner();
        }

        public void UnregisterFurniture(GameObject go)
        {
            if (go == null) return;
            var shelf = go.GetComponent<ShelfUnit>();
            if (shelf != null) _shelves.Remove(shelf);

            var pen = go.GetComponent<PetPen>();
            if (pen != null) _pens.Remove(pen);

            PushListsToSpawner();
        }

        private void PushListsToSpawner()
        {
            _shelves.RemoveAll(s => s == null);
            _pens.RemoveAll(p => p == null);
            if (Spawner == null) return;
            Spawner.Shelves = _shelves;
            Spawner.PetPens = _pens;
        }

        // ── Interactions (delegated to ShopFloorActions) ─────────────────────

        /// <summary>Orders a pallet of stock for a category, to arrive later today.</summary>
        public bool OrderStock(ProductCategory category, int units) => _floor.OrderStock(category, units);

        /// <summary>Carries a delivered pallet into the stockroom.</summary>
        public void CollectDelivery(DeliveryCrate crate) => _floor.CollectDelivery(crate);

        public void RestockShelf(ShelfUnit shelf) => _floor.RestockShelf(shelf);

        /// <summary>
        /// Interacting with a pen buys a young pet from the breeder when there is room —
        /// without this the pens could be sold out for good and the shop would stall.
        /// </summary>
        public void InspectPen(PetPen pen) => _floor.InspectPen(pen);

        /// <summary>
        /// Interacting with the counter serves whoever is next in line; with nobody waiting
        /// it just opens the books.
        /// </summary>
        public void UseCounter() => _floor.UseCounter();

        public void OpenShopSummary() => _floor.OpenShopSummary();

        // ── Day cycle ─────────────────────────────────────────────────────────

        public void StartDay()
        {
            IsModalOpen  = false;
            _dayElapsed  = 0f;
            IsDayRunning = true;

            // A fresh set of applicants each morning gives the staff board a reason to be
            // checked more than once.
            RefreshCandidates();
            Spawner?.StartDay();
            OnDayStarted.Invoke(Shop.Day);
            Notify($"Day {Shop.Day} — open. Tonight: rent €{Shop.DailyRent:N0}" +
                   (Shop.DailyWages > 0f ? $" + wages €{Shop.DailyWages:N0}" : ""));
            Audio?.PlaySfx("day_start");
        }

        public void EndDay()
        {
            if (_endingDay || IsGameOver) return;
            StartCoroutine(EndDaySequence());
        }

        public IEnumerator EndDaySequence()
        {
            _endingDay  = true;
            IsDayRunning = false;

            Notify("Closing up for the night...");
            Spawner?.EndDay();
            yield return new WaitForSeconds(0.6f);

            var born = BreedingSystem.AdvanceDay(_pens);
            if (born.Count > 0)
                Notify(born.Count == 1 ? "A pet was born overnight!" : $"{born.Count} pets were born overnight!");

            DaySummary summary = Shop.CloseDay();
            SaveGame();

            Audio?.PlaySfx("day_end");
            OnDayEnded.Invoke(summary);
            _endingDay = false;

            if (summary.ClosingBalance < 0f)
            {
                IsGameOver = true;
                Spawner?.EndDay();
                Debug.Log($"[Game] GAME OVER on day {summary.Day} — balance €{summary.ClosingBalance:N2}.");
                OnGameOver.Invoke(
                    $"You could not cover day {summary.Day}'s bills — " +
                    $"€{summary.Rent:N0} rent and €{summary.Wages:N0} in wages.\n" +
                    $"The shop closed with €{summary.ClosingBalance:N2}.");
                yield break;
            }

            // Headless runs have nobody to click Continue
            if (_autoContinue)
            {
                yield return new WaitForSeconds(0.5f);
                StartNewDay();
            }
        }

        // ── Staff (delegated to StaffRoster) ─────────────────────────────────

        public int StaffCount => _roster.StaffCount;

        /// <summary>Everyone currently on the payroll, for the staff board.</summary>
        public IReadOnlyList<Assistant> Staff => _roster.Staff;

        /// <summary>The three people currently looking for work. Refreshed every morning.</summary>
        public IReadOnlyList<StaffCandidate> Candidates => _roster.Candidates;

        public void RefreshCandidates() => _roster.RefreshCandidates();

        /// <summary>Total wages owed tonight, from the people actually on the payroll.</summary>
        public float Payroll => _roster.Payroll;

        /// <summary>Hire one assistant. Their first day's wage is due at close, not now.</summary>
        public bool HireAssistant() => _roster.HireAssistant();

        /// <summary>Takes a named applicant on: pays their sign-on fee and puts them on the till.</summary>
        public bool HireCandidate(StaffCandidate candidate) => _roster.HireCandidate(candidate);

        public bool FireAssistant() => _roster.FireAssistant();

        /// <summary>Lets one named member of staff go, rather than whoever happens to be last.</summary>
        public bool FireAssistant(Assistant member) => _roster.FireAssistant(member);

        /// <summary>Nudge shelf prices up or down. Wired to the ledger's price buttons.</summary>
        public void AdjustPrices(float delta)
        {
            Shop.SetPriceMultiplier(Shop.PriceMultiplier + delta);
            Notify($"Prices now {Shop.PriceMultiplier * 100f:0}% of list — demand {Shop.DemandFactor * 100f:0}%.");
        }

        /// <summary>Pens that want feeding or mucking out.</summary>
        public int PensNeedingService
        {
            get
            {
                int n = 0;
                foreach (var pen in _pens) if (pen != null && pen.NeedsService) n++;
                return n;
            }
        }

        /// <summary>Hooked to the Continue button on the day-results panel.</summary>
        public void StartNewDay()
        {
            if (IsGameOver) return;
            StartDay();
        }

        public void RestartGame()
        {
            SaveSystem.Delete();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // ── Save / load (delegated to SaveLoadController) ─────────────────────

        /// <summary>
        /// Snapshots the shop and writes it to the save slot, notifying the player of the outcome.
        /// </summary>
        /// <returns>True when the save was written; false when it failed.</returns>
        public bool SaveGame() => _saveLoad.SaveGame();

        // ── Utility ───────────────────────────────────────────────────────────

        public void Notify(string msg)
        {
            Debug.Log($"[Game] {msg}");
            OnNotification.Invoke(msg);
        }
    }
}
