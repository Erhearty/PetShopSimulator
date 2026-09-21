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
    /// Top-level orchestrator. Owns the day cycle, the furniture registry, save/load and
    /// the keyboard shortcuts that are not tied to the player character.
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
        private readonly List<Assistant> _assistants = new();

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

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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
            Shop?.OnDeliveryArrived.AddListener(OnDeliveryArrived);

            var save = SaveSystem.Load();
            if (save != null) LoadGame(save);
            else              NewGame();

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

        // ── New game / layout ─────────────────────────────────────────────────

        private void NewGame()
        {
            foreach (var p in Generator.StarterLayout(Grid))
            {
                var def = BuildCatalog.Get(p.CatalogId);
                var go  = Build.Place(p.Cell, def, p.Variant, p.Rotation, charge: false);
                if (go == null) continue;

                var shelf = go.GetComponent<ShelfUnit>();
                if (shelf != null) SeedShelf(shelf);

                var pen = go.GetComponent<PetPen>();
                if (pen != null)
                {
                    pen.AddPet(BreedingSystem.GenerateRandom(pen.PenSpecies));
                    pen.AddPet(BreedingSystem.GenerateRandom(pen.PenSpecies));
                }
            }
            // You inherit one member of staff — without anyone on the till a new shop cannot
            // trade at all while you are out in the yard. Inherited, so no sign-on fee.
            var inherited = StaffCandidate.Generate();
            inherited.SignOnFee = 0f;
            HireCandidate(inherited);
            Notify("Welcome to your pet shop! B to build, E to interact, Enter to close up.");
        }

        /// <summary>Stocks a fresh shelf for free — the starter inventory.</summary>
        private void SeedShelf(ShelfUnit shelf)
        {
            if (Catalog == null) return;
            var products = Catalog.GetByCategory(shelf.Category);
            for (int i = 0; i < products.Count && i < shelf.MaxLines; i++)
                shelf.AddStock(products[i], shelf.MaxPerLine);
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

        // ── Interactions ──────────────────────────────────────────────────────

        /// <summary>Orders a pallet of stock for a category, to arrive later today.</summary>
        public bool OrderStock(ProductCategory category, int units)
        {
            if (Shop == null) return false;

            float unitCost = Catalog != null ? Catalog.AverageUnitCost(category) : 3.2f;
            var order = Shop.PlaceOrder(category, units, unitCost, DayProgress);
            if (order == null)
            {
                Notify("Not enough money for that order.");
                Audio?.PlaySfx("deny");
                return false;
            }

            Notify($"Ordered {units} {category} units for €{order.Cost:N2} — the van is on its way.");
            Audio?.PlaySfx("restock");
            return true;
        }

        /// <summary>Puts the pallet on the forecourt and tells the player it has landed.</summary>
        private void OnDeliveryArrived(SupplierOrder order)
        {
            Vector3 spot = Generator != null ? Generator.ForecourtPosition : Vector3.zero;
            // Spread pallets out so two deliveries never stack in the same spot.
            spot += new Vector3(UnityEngine.Random.Range(-2.4f, 2.4f), 0f, UnityEngine.Random.Range(-1f, 1.4f));

            DeliveryCrate.Spawn(spot, order.Category, order.Units);
            Notify($"Delivery: {order.Units} {order.Category} units are on the forecourt. Press E to collect.");
            Audio?.PlaySfx("restock");
        }

        /// <summary>Carries a delivered pallet into the stockroom.</summary>
        public void CollectDelivery(DeliveryCrate crate)
        {
            if (crate == null) return;

            int units = crate.Collect(Shop);
            Notify($"Collected {units} units — they are in the stockroom, ready to shelve.");
            Audio?.PlaySfx("restock");
        }

        public void RestockShelf(ShelfUnit shelf)
        {
            if (shelf == null) return;

            var result = shelf.Restock(Shop, Catalog);

            if (result.Units <= 0)
                Notify(shelf.HasSpace ? "Not enough money to restock." : "Shelf is already full.");
            else if (result.Spent <= 0.01f)
                Notify($"Shelved {result.Units} {shelf.Category} units from the stockroom " +
                       $"({Shop.Warehouse(shelf.Category)} left).");
            else if (result.FromWarehouse > 0)
                Notify($"Shelved {result.FromWarehouse} from the stockroom and bought {result.Units - result.FromWarehouse} " +
                       $"at the cash-and-carry for €{result.Spent:N2}.");
            else
                Notify($"Restocked {shelf.Category} for €{result.Spent:N2} at cash-and-carry prices " +
                       $"— ordering ahead is {(1f - ShopManager.WholesaleDiscount / ShopManager.EmergencyMarkup) * 100f:0}% cheaper.");

            Audio?.PlaySfx(result.Units > 0 ? "restock" : "deny");
            OnInfoPanel.Invoke(shelf.Describe());
        }

        /// <summary>
        /// Interacting with a pen buys a young pet from the breeder when there is room —
        /// without this the pens could be sold out for good and the shop would stall.
        /// </summary>
        public void InspectPen(PetPen pen)
        {
            if (pen == null) return;

            // Care comes first: an animal that needs feeding matters more than buying another.
            if (pen.NeedsService)
            {
                float cost = pen.ServiceCost;
                if (Shop.ChangeBalance(-cost, "Pen upkeep"))
                {
                    pen.Service();
                    Notify($"Fed and mucked out the {pen.PenSpecies} pen — €{cost:N2}");
                    Audio?.PlaySfx("restock");
                }
                else
                {
                    Notify($"Servicing that pen costs €{cost:N2} — not enough money.");
                    Audio?.PlaySfx("deny");
                }
                OnInfoPanel.Invoke(pen.Describe());
                return;
            }

            if (pen.HasSpace)
            {
                float price = Pet.WholesalePrice(pen.PenSpecies);
                if (Shop.ChangeBalance(-price, $"Buy {pen.PenSpecies}"))
                {
                    var pet = BreedingSystem.GenerateRandom(pen.PenSpecies);
                    pet.growthStage = Pet.GrowthStage.Juvenile;
                    pet.ageDays     = 1;
                    pen.AddPet(pet);
                    Notify($"Bought {pet.DisplayName()} for €{price:N2}");
                    Audio?.PlaySfx("restock");
                }
                else
                {
                    Notify($"A {pen.PenSpecies} costs €{price:N2} — not enough money.");
                    Audio?.PlaySfx("deny");
                }
            }
            else
            {
                Audio?.PlaySfx("click");
            }

            OnInfoPanel.Invoke(pen.Describe());
        }

        /// <summary>
        /// Interacting with the counter serves whoever is next in line; with nobody waiting
        /// it just opens the books.
        /// </summary>
        public void UseCounter()
        {
            if (Queue != null && Queue.AnyWaiting)
            {
                var shopper = Queue.Front;
                float value = shopper.BasketValue;
                int   items = shopper.BasketCount;

                Queue.ServeFront();
                Audio?.PlaySfx("sale");
                Notify($"Served {shopper.ShopperName} — {items} item(s), €{value:N2}");

                if (Queue.AnyWaiting)
                    Notify($"{Queue.Length} still waiting (€{Queue.WaitingValue:N0}).");
                return;
            }

            OpenShopSummary();
        }

        public void OpenShopSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Day {Shop.Day}   ·   Balance €{Shop.Balance:N2}   ·   Rep {Shop.Reputation:0}/100");
            sb.AppendLine($"Rent due tonight: €{Shop.DailyRent:N2}");
            sb.AppendLine($"Prices at {Shop.PriceMultiplier * 100f:0}% — demand {Shop.DemandFactor * 100f:0}%");
            sb.AppendLine($"Shelves: {_shelves.Count}   Pens: {_pens.Count}");
            sb.AppendLine();

            if (Shop.TodaysSales.Count == 0)
            {
                sb.AppendLine("No sales yet today.");
            }
            else
            {
                sb.AppendLine("Today's sales:");
                float total = 0f;
                int shown = 0;
                foreach (var s in Shop.TodaysSales)
                {
                    total += s.Revenue;
                    if (shown++ < 8) sb.AppendLine($"  {s.Label}  €{s.Revenue:0.00}");
                }
                if (Shop.TodaysSales.Count > 8) sb.AppendLine($"  ... and {Shop.TodaysSales.Count - 8} more");
                sb.AppendLine($"  Total: €{total:N2}");
            }
            OnInfoPanel.Invoke(sb.ToString().TrimEnd());
            Audio?.PlaySfx("click");
        }

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

        // ── Staff ─────────────────────────────────────────────────────────────

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
                Notify("There is no room behind that counter for another assistant.");
                return false;
            }

            if (candidate.SignOnFee > 0f &&
                !Shop.ChangeBalance(-candidate.SignOnFee, $"Sign-on fee for {candidate.Name}"))
            {
                Notify($"You cannot cover {candidate.Name}'s €{candidate.SignOnFee:N0} sign-on fee.");
                Audio?.PlaySfx("deny");
                return false;
            }

            Vector3 station = StaffStation != null ? StaffStation.position : Vector3.zero;
            Vector3 facing  = StaffStation != null ? StaffStation.forward  : Vector3.forward;
            Vector3 offset  = Vector3.right * (_assistants.Count * 1.1f - 0.55f);

            var assistant = Assistant.Create(transform, station + offset, facing,
                                             Queue, Shop, _assistants.Count);
            assistant.DailyWage      = candidate.DailyWage;
            assistant.ServiceSeconds = candidate.ServiceSeconds;
            assistant.StaffName      = candidate.Name;
            _assistants.Add(assistant);

            _candidates.Remove(candidate);

            Shop.SetStaff(_assistants.Count);
            Shop.SetPayroll(Payroll);
            Notify($"Hired {candidate.Name} — {candidate.SpeedWord} at the till, " +
                   $"€{candidate.DailyWage:N0} a day, {_assistants.Count} on the payroll.");
            return true;
        }

        public bool FireAssistant()
        {
            if (_assistants.Count == 0) { Notify("There is nobody to let go."); return false; }
            return FireAssistant(_assistants[_assistants.Count - 1]);
        }

        /// <summary>Lets one named member of staff go, rather than whoever happens to be last.</summary>
        public bool FireAssistant(Assistant member)
        {
            if (member == null || !_assistants.Contains(member))
            {
                Notify("There is nobody to let go.");
                return false;
            }

            string name = member.StaffName;
            _assistants.Remove(member);
            Destroy(member.gameObject);

            Shop.SetStaff(_assistants.Count);
            Shop.SetPayroll(Payroll);

            // People talk: sacking staff costs you a little standing locally.
            Shop.ChangeReputation(-0.5f);
            Notify($"Let {name} go — {_assistants.Count} left on the payroll.");
            return true;
        }

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

        // ── Save / load ───────────────────────────────────────────────────────

        public void SaveGame()
        {
            var data = new SaveData
            {
                Balance         = Shop.Balance,
                Reputation      = Shop.Reputation,
                Day             = Shop.Day,
                Staff           = _assistants.Count,
                PriceMultiplier = Shop.PriceMultiplier,
            };

            foreach (var kvp in Shop.Stock)
                data.Stock.Add(new SaveData.StockEntry { id = kvp.Key, qty = kvp.Value });

            foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
            {
                int units = Shop.Warehouse(category);
                if (units > 0)
                    data.Warehouse.Add(new SaveData.StockEntry { id = category.ToString(), qty = units });
            }

            foreach (var entry in Grid.GetAllPlaced())
            {
                if (entry.Data == null) continue;
                var item = new SaveData.PlacedItem
                {
                    catalogId = entry.Data.Id,
                    cellX     = entry.Root.x,
                    cellY     = entry.Root.y,
                    variant   = entry.Variant,
                    rotation  = entry.Instance != null ? entry.Instance.transform.eulerAngles.y : 0f,
                };

                if (entry.Instance != null)
                {
                    var shelf = entry.Instance.GetComponent<ShelfUnit>();
                    if (shelf != null)
                    {
                        item.variant = shelf.Category.ToString();
                        foreach (var line in shelf.Lines)
                            if (line.Product != null)
                                item.shelfStock.Add(new SaveData.StockEntry { id = line.Product.id, qty = line.Units });
                    }

                    var pen = entry.Instance.GetComponent<PetPen>();
                    if (pen != null)
                    {
                        item.variant = pen.PenSpecies.ToString();
                        foreach (var pet in pen.Residents)
                            item.pets.Add(SaveSystem.PetToSaveData(pet));
                    }
                }
                data.PlacedObjects.Add(item);
            }

            SaveSystem.Save(data);
            Notify("Game saved.");
        }

        private void LoadGame(SaveData data)
        {
            Shop.SetBalance(data.Balance);
            Shop.SetReputation(data.Reputation);
            Shop.SetDay(data.Day);

            foreach (var entry in data.Stock)
                Shop.ChangeStock(entry.id, entry.qty);

            foreach (var entry in data.Warehouse)
                if (System.Enum.TryParse(entry.id, out ProductCategory category))
                    Shop.AddToWarehouse(category, entry.qty);

            foreach (var item in data.PlacedObjects)
            {
                var def = BuildCatalog.Get(item.catalogId);
                if (def == null) continue;

                var go = Build.Place(new Vector2Int(item.cellX, item.cellY), def,
                                     item.variant, item.rotation, charge: false);
                if (go == null) continue;

                var shelf = go.GetComponent<ShelfUnit>();
                if (shelf != null)
                    foreach (var line in item.shelfStock)
                    {
                        var product = Catalog?.Get(line.id);
                        if (product != null) shelf.AddStock(product, line.qty);
                    }

                var pen = go.GetComponent<PetPen>();
                if (pen != null)
                    foreach (var petData in item.pets)
                        pen.AddPet(SaveSystem.SaveDataToPet(petData));
            }

            for (int i = 0; i < Mathf.Max(1, data.Staff); i++) HireAssistant();
            Shop.SetPriceMultiplier(data.PriceMultiplier <= 0f ? 1f : data.PriceMultiplier);

            Notify($"Save loaded — day {data.Day}, €{data.Balance:N0}");
        }

        // ── Utility ───────────────────────────────────────────────────────────

        public void Notify(string msg)
        {
            Debug.Log($"[Game] {msg}");
            OnNotification.Invoke(msg);
        }
    }
}
