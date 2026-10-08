using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PetShop.Shop;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Customer;
using PetShop.Progression;
using PetShop.Events;

namespace PetShop.Core
{
    /// <summary>
    /// Top-level orchestrator. Owns the day loop, the furniture registry and the keyboard
    /// shortcuts that are not tied to the player character, and is the public facade the
    /// rest of the game talks to. Save/load and new-game setup are delegated to
    /// <see cref="SaveLoadController"/>, shop-floor interactions to
    /// <see cref="ShopFloorActions"/>, and hiring/firing to <see cref="StaffRoster"/>.
    /// </summary>
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Wiring (set by GameBootstrapper) ──────────────────────────────────
        [HideInInspector] public GridManager     Grid;
        [HideInInspector] public BuildMode       Build;
        [HideInInspector] public ShopManager     Shop;
        [HideInInspector] public CustomerSpawner Spawner;
        [HideInInspector] public AudioManager    Audio;
        [HideInInspector] public ShopLayout      Layout;
        [HideInInspector] public CheckoutQueue    Queue;
        [HideInInspector] public ProgressionDirector Progression;
        [HideInInspector] public ShopEventDirector   Events;

        // ── Events for UI ─────────────────────────────────────────────────────
        public UnityEvent<DaySummary> OnDayEnded     = new();
        public UnityEvent<int>        OnDayStarted   = new();
        public UnityEvent<string>     OnNotification = new();
        public UnityEvent<string>     OnGameOver     = new();

        [Header("Day length")]
        [Tooltip("Real seconds of trading per in-game day. 540 = 9 trading hours (09:00-18:00) at 1 real second per game minute. Override with -daylength <seconds>.")]
        public float DayLengthSeconds = 540f;

        public ItemDatabase Catalog { get; private set; }

        /// <summary>The weekly pet show: current entry and last result.</summary>
        public PetShow Show { get; } = new PetShow();

        /// <summary>Per-category automatic supplier reorder rules.</summary>
        public AutoReorder AutoReorder { get; } = new AutoReorder();

        /// <summary>0 at opening time, 1 at closing time.</summary>
        public float DayProgress => DayLengthSeconds <= 0f ? 0f
            : Mathf.Clamp01(_dayElapsed / DayLengthSeconds);

        /// <summary>Opening time on the shop-floor clock, in hours.</summary>
        public const float OpeningHour = 9f;
        /// <summary>Closing time on the shop-floor clock, in hours.</summary>
        public const float ClosingHour = 18f;

        /// <summary>The shop-floor clock in game hours: 9 at opening, 18 at closing.</summary>
        public float CurrentGameHour => Mathf.Lerp(OpeningHour, ClosingHour, DayProgress);

        /// <summary>Shop-floor clock, 09:00 to 18:00 across the trading day.</summary>
        public string ClockText
        {
            get
            {
                float hours   = CurrentGameHour;
                int   hour    = Mathf.FloorToInt(hours);
                int   minutes = Mathf.FloorToInt((hours - hour) * 60f);
                return $"{hour:00}:{minutes:00}";
            }
        }
        public bool IsBuildModeActive => Build != null && Build.IsActive;

        /// <summary>True while a blocking panel (day results, game over) is up.</summary>
        public bool IsModalOpen { get; private set; }

        public void SetModalOpen(bool open) => IsModalOpen = open;

        /// <summary>
        /// True while the top-down build view is showing. It gates player movement, look and
        /// interaction like <see cref="IsModalOpen"/>, but leaves the day clock, deliveries and
        /// the quicksave and end-day keys running.
        /// </summary>
        public bool IsBuildViewActive { get; private set; }

        /// <summary>Marks the top-down build view as showing or closed.</summary>
        public void SetBuildViewActive(bool active) => IsBuildViewActive = active;
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
            WireFurnitureSupply();
            Catalog  = ItemDatabase.Load();
            ApplyCommandLineOverrides();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by GameBootstrapper once the room and player exist.</summary>
        public void Begin(bool skipTutorial = false)
        {
            Shop?.OnDeliveryArrived.AddListener(_floor.OnDeliveryArrived);

            LineageRegistry.Reset(); // statics survive scene reloads
            var save = SaveSystem.Load();
            if (save != null) _saveLoad.LoadGame(save);
            else              StartNewGame(skipTutorial);

            Layout?.BakeNavMesh();
            StartDay();
        }

        /// <summary>A fresh, empty shop; -furnish (dev/test runs only) places the starter furniture.</summary>
        private void StartNewGame(bool skipTutorial)
        {
            _saveLoad.NewGame(skipTutorial);
            if (PlaytestOptions.Furnish) Dev.DevFurnisher.Furnish(this, PlaytestOptions.Seed ?? Dev.DevFurnisher.DefaultSeed);
            if (PlaytestOptions.Furnish && (Application.isBatchMode || Debug.isDebugBuild)) Dev.SoakSteward.Begin(this); // unattended stock chores
        }

        private void Update()
        {
            if (IsGameOver || IsModalOpen) return;

            if (InputBindings.GetKeyDown(GameAction.QuickSave)) SaveGame();

            if (!IsBuildModeActive &&
                (InputBindings.GetKeyDown(GameAction.EndDay) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                EndDay();
                return;
            }

            if (!IsDayRunning || DayLengthSeconds <= 0f) return;

            _dayElapsed += Time.deltaTime;

            Shop?.PollDeliveries(DayProgress);
            TickFurniture();
            _floor.FlushTrucks();   // everything due this frame (e.g. one Tab session's orders) rides one truck

            // Stop letting new customers in shortly before closing time
            if (Spawner != null && !Spawner.DoorsClosed && DayProgress > 0.88f)
                Spawner.CloseDoors();

            if (_dayElapsed >= DayLengthSeconds) EndDay();
        }

        /// <summary>Slowest -timescale honoured.</summary>
        public const float MinTimeScale = 1f;
        /// <summary>Fastest -timescale honoured.</summary>
        public const float MaxTimeScale = 20f;

        /// <summary>
        /// Unity's own Time.maximumDeltaTime default (1/3 s): the cap at 1x, and the floor of
        /// <see cref="MaxDeltaTimeFor"/>.
        /// </summary>
        public const float DefaultMaxDeltaTime = 1f / 3f;

        /// <summary>
        /// Slowest real frame, in real seconds, that still advances the full -timescale. Unity caps
        /// the SCALED Time.deltaTime with Time.maximumDeltaTime, so a fixed cap would also cap the
        /// speed-up (0.1 s held -timescale 20 to ~6x at 60 fps); the cap is this many real seconds
        /// times the time scale instead.
        /// </summary>
        public const float MaxRealFrameSeconds = 1f / 30f;

        /// <summary>
        /// Time.maximumDeltaTime for <paramref name="timeScale"/>: one <see cref="MaxRealFrameSeconds"/>
        /// frame of game time, never below <see cref="DefaultMaxDeltaTime"/>. Any frame at 30 fps or
        /// better then advances the full timescale (so -timescale N really delivers N), while a
        /// hitch slower than that is clipped rather than throwing NavMesh agents metres past their
        /// stopping distance in one step. Time.fixedDeltaTime is left alone: physics keeps its normal
        /// step (this cap also bounds how many fixed steps a frame may catch up), just run more
        /// often per real second.
        /// </summary>
        public static float MaxDeltaTimeFor(float timeScale) =>
            Mathf.Max(DefaultMaxDeltaTime, MaxRealFrameSeconds * timeScale);

        /// <summary>
        /// The value after -timescale in <paramref name="args"/>, clamped to
        /// [<see cref="MinTimeScale"/>, <see cref="MaxTimeScale"/>]; null when absent or not a number.
        /// </summary>
        public static float? ParseTimeScale(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-timescale") continue;
                if (!float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float scale)
                    || float.IsNaN(scale)) return null;
                return Mathf.Clamp(scale, MinTimeScale, MaxTimeScale);
            }
            return null;
        }

        /// <summary>
        /// Honours -daylength &lt;seconds&gt; so the game can be driven headlessly, and
        /// so players can pick a pace without rebuilding; and -timescale &lt;N&gt; (1-20) so a
        /// soak run can fast-forward a real-length day.
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

            float? timeScale = ParseTimeScale(args);
            if (timeScale.HasValue)
            {
                Time.timeScale        = timeScale.Value;
                Time.maximumDeltaTime = MaxDeltaTimeFor(timeScale.Value);
                Debug.Log($"[Game] Time scale set to {timeScale.Value}x from the command line.");
            }
            _autoContinue = Application.isBatchMode;
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

        /// <summary>Feeds and cleans <paramref name="pen"/>'s pets (the pen's feed pad was clicked).</summary>
        public void FeedPen(PetPen pen) => _floor.FeedPen(pen);

        /// <summary>
        /// Interacting with the counter serves whoever is next in line; with nobody waiting
        /// it does nothing.
        /// </summary>
        public void UseCounter() => _floor.UseCounter();

        // ── Day cycle ─────────────────────────────────────────────────────────

        public void StartDay()
        {
            IsModalOpen  = false;
            _dayElapsed  = 0f;
            IsDayRunning = true;

            // A fresh set of applicants each morning gives the staff board a reason to be
            // checked more than once.
            RefreshCandidates();
            Events?.BeginDay(Shop.Day);
            Spawner?.StartDay();
            OnDayStarted.Invoke(Shop.Day);
            Notify(PetShop.Localization.Loc.F("day.open", Shop.Day, Shop.DailyRent) +
                   (Shop.DailyWages > 0f ? PetShop.Localization.Loc.F("day.open_wages", Shop.DailyWages) : ""));
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

            Notify(PetShop.Localization.Loc.T("day.closing"));
            Spawner?.EndDay();
            yield return new WaitForSeconds(0.6f);

            var born = BreedingSystem.AdvanceDay(_pens, Events?.CareDrainMultiplier ?? 1f);
            if (born.Count > 0)
                Notify(PetShop.Localization.Loc.Plural("day.born", born.Count));

            // Loads still on a truck land now, so the save below sees every crate.
            _floor.DropWaitingLoads();
            Traffic.DeliveryTruck.FlushAll();
            LandFurnitureOvernight();
            ShowJudging.Run(this);

            DaySummary summary = CloseDayWithProgression();
            Events?.EndDay(summary);
            SaveGame();

            Audio?.PlaySfx("day_end");
            OnDayEnded.Invoke(summary);
            _endingDay = false;

            if (summary.ClosingBalance < 0f)
            {
                IsGameOver = true;
                Spawner?.EndDay();
                Debug.Log($"[Game] GAME OVER on day {summary.Day} — balance €{summary.ClosingBalance:N2}.");
                OnGameOver.Invoke(PetShop.Localization.Loc.F("day.game_over",
                    summary.Day, summary.Rent, summary.Wages, summary.ClosingBalance));
                yield break;
            }

            // Headless runs have nobody to click Continue
            if (_autoContinue)
            {
                yield return new WaitForSeconds(0.5f);
                StartNewDay();
            }
        }

        /// <summary>
        /// Closes the day's books, running any due inspection first, then adds the inspection
        /// result and any tier-ups to the summary's headlines.
        /// </summary>
        private DaySummary CloseDayWithProgression()
        {
            // The inspector grades the day being closed, so the result lands in its books.
            var inspection = Progression?.RunInspectionIfDue(Shop.Day);
            DaySummary summary = Shop.CloseDay();
            CollectProgressionHeadlines(summary, inspection);
            CollectQuestHeadlines(summary);
            return summary;
        }

        /// <summary>Adds the inspection result and any tier-ups to the day's summary.</summary>
        private void CollectProgressionHeadlines(DaySummary summary, List<string> inspection)
        {
            if (inspection != null)
                foreach (var line in inspection) { summary.Headlines.Add(line); Notify(line); }
            if (Progression == null) return;
            // The director notifies milestones itself; only the summary needs them here.
            Progression.CheckMilestones();
            summary.Headlines.AddRange(Progression.LatestMilestones);
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

        /// <summary>Everyone on staff, for the save file.</summary>
        internal List<SaveData.StaffSaveData> StaffToSave() => _roster.ToSave();

        /// <summary>Rebuilds staff from a save without charging sign-on fees.</summary>
        internal void RestoreStaff(SaveData data) => _roster.Restore(data);

        /// <summary>Nudge shelf prices up or down. Wired to the ledger's price buttons.</summary>
        public void AdjustPrices(float delta)
        {
            Shop.SetPriceMultiplier(Shop.PriceMultiplier + delta);
            Notify(PetShop.Localization.Loc.F("prices.now", Shop.PriceMultiplier * 100f, Shop.DemandFactor * 100f));
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
            if (GameSettings.AutosaveEachMorning && !IsGameOver) SaveGame(quiet: true);
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
        /// <param name="quiet">True for an automatic save: the success toast reads "Autosaved.".</param>
        /// <returns>True when the save was written; false when it failed.</returns>
        public bool SaveGame(bool quiet = false) => _saveLoad.SaveGame(quiet);

        // ── Utility ───────────────────────────────────────────────────────────

        public void Notify(string msg)
        {
            Debug.Log($"[Game] {msg}");
            OnNotification.Invoke(msg);
        }
    }
}
