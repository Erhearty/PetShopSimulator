using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.UI;

namespace PetShop.Customer
{
    public enum CustomerState { Entering, Browsing, Queueing, Buying, Leaving, Done }

    /// <summary>
    /// A shopper: walks in, browses a few shelves and pens, picks things up, pays at the
    /// till at real catalogue prices, then leaves. Leaving empty-handed costs reputation.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CustomerAI : MonoBehaviour, CheckoutQueue.IShopper
    {
        [Header("Wiring")]
        public ShopManager     ShopManager;
        public Transform       EntryPoint;
        public Transform       ExitPoint;
        public CheckoutQueue   Queue;
        public Transform       RegisterPoint;
        public List<ShelfUnit> Shelves = new();
        public List<PetPen>    PetPens = new();

        [Header("Tuning")]
        public float MoveSpeed     = 2.4f;
        public float BrowseTimeMin = 1.2f;
        public float BrowseTimeMax = 3.0f;
        public int   MaxPurchases  = 4;
        [Tooltip("Chance per browsing stop that a customer falls for one of the animals.")]
        [Range(0f, 1f)] public float PetBuyChance = 0.12f;

        /// <summary>What kind of shopper this is. Set by the spawner before Start runs.</summary>
        [Header("Archetype")]
        public CustomerArchetype Archetype = CustomerArchetype.Regular;

        /// <summary>The resolved tuning for <see cref="Archetype"/>; valid from Start onwards.</summary>
        public CustomerProfile Profile { get; private set; } = CustomerProfile.For(CustomerArchetype.Regular);

        /// <summary>Chance per browsing stop, at normal prices, of taking something off a shelf.</summary>
        private const float ShelfBuyChance = 0.7f;

        /// <summary>
        /// Pure purchase decision: buys when <paramref name="roll"/> falls below the base chance
        /// scaled by the shop's current <paramref name="demand"/> factor.
        /// </summary>
        internal static bool WillBuy(float roll, float baseChance, float demand) => roll < baseChance * demand;

        public CustomerState State { get; private set; } = CustomerState.Entering;

        // ── Outcome events (read by playtest telemetry; gameplay does not listen) ──

        /// <summary>Raised when a customer pays at the till.</summary>
        public static event System.Action<CustomerAI> CheckedOut;

        /// <summary>Raised when a customer leaves having picked up nothing.</summary>
        public static event System.Action<CustomerAI> WalkedOutEmpty;

        /// <summary>Raised when a queueing customer gives up waiting and abandons the basket.</summary>
        public static event System.Action<CustomerAI> GaveUp;

        /// <summary>Raised each time a walk ends because its timeout expired before arrival.</summary>
        public static event System.Action<CustomerAI> NavigationTimedOut;

        /// <summary>Raised when the customer's GameObject is destroyed.</summary>
        public static event System.Action<CustomerAI> Despawned;

        /// <summary>
        /// Horizontal (XZ) distance in metres from the till at the moment of the last checkout;
        /// 0 before any checkout or when there is no till.
        /// </summary>
        public float DistanceToTillAtCheckout { get; private set; }

        private readonly List<(string id, string label, float price)> _basket = new();
        private NavMeshAgent    _agent;
        private CharacterVisual _visual;
        private WorldLabel      _bubble;

        // ── What this shopper came in for ───────────────────────────────────────

        /// <summary>The aisle this customer heads for first.</summary>
        public ProductCategory PreferredCategory { get; private set; }

        /// <summary>Some shoppers are here for an animal, not a bag of food.</summary>
        public bool WantsPet { get; private set; }

        /// <summary>Above this, they will not buy however much they liked it.</summary>
        public float BudgetCap { get; private set; }

        /// <summary>Most this shopper will pay for an animal; 0 if not after one.</summary>
        public float PetBudget { get; private set; }
        private bool  _boughtPet;
        private bool  _sawWantedPet;
        private bool  _served;
        private bool  _gaveUp;

        // ── CheckoutQueue.IShopper ──────────────────────────────────────────────

        public float BasketValue
        {
            get { float t = 0f; foreach (var (_, _, price) in _basket) t += price; return t; }
        }
        public int    BasketCount => _basket.Count;
        public string ShopperName => name;

        /// <summary>How close to the front-of-line spot counts as standing at the till, in metres.</summary>
        private const float AtTillReach = 1.5f;

        public bool IsAtTill
        {
            get
            {
                if (State != CustomerState.Queueing || Queue == null) return false;
                Vector3 offset = Queue.StandingPosition(0) - transform.position;
                offset.y = 0f;
                return offset.magnitude <= AtTillReach;
            }
        }

        public void OnServed() => _served = true;
        public void OnGaveUp() => _gaveUp = true;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed            = MoveSpeed;
            _agent.angularSpeed     = 300f;
            _agent.acceleration     = 6f;      // gentler than the default 8, so no lurching
            _agent.radius           = 0.32f;
            _agent.height           = 1.85f;
            _agent.stoppingDistance = 0.6f;
            _agent.autoBraking      = true;
            // Agents pushing each other apart is a common source of visible jitter in a queue.
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.avoidancePriority     = Random.Range(30, 70);

            BuildVisual();
        }

        private void Start()
        {
            // A preference profile makes per-category pricing meaningful and gives the
            // thought bubble something true to show.
            var categories = (ProductCategory[])System.Enum.GetValues(typeof(ProductCategory));
            PreferredCategory = categories[Random.Range(0, categories.Length)];
            Profile           = CustomerProfile.For(Archetype);
            WantsPet          = Random.value < Profile.WantsPetChance;
            PetBudget         = WantsPet ? Profile.RollPetBudget() : 0f;
            BudgetCap         = WantsPet ? PetBudget : Profile.RollItemBudget();

            BuildBubble();
            StartCoroutine(RunBehaviour());
        }

        /// <summary>
        /// A bubble over the head saying what they are after. Shop sims universally show
        /// this — without it the player cannot tell which aisle to restock.
        /// </summary>
        private void BuildBubble()
        {
            string want = WantsPet ? "wants a pet" : PreferredCategory.ToString().ToLowerInvariant();
            Color colour = WantsPet ? new Color(0.98f, 0.78f, 0.38f) : UIFactory.Ink;

            _bubble = WorldLabel.Create(transform, new Vector3(0f, 2.15f, 0f),
                                        $"<size=80%><b>{Profile.Label}</b>: {want}</size>", 0.085f, colour, width: 1.4f);
            _bubble.MaxVisibleDistance = 22f;
        }

        private void SetBubble(string text, Color colour)
        {
            if (_bubble == null) return;
            _bubble.SetText($"<size=80%>{text}</size>");
            _bubble.SetColour(colour);
        }

        private void BuildVisual()
        {
            gameObject.layer = GameLayers.Character;
            _visual = CharacterFactory.Attach(gameObject, () => _agent != null ? _agent.velocity : Vector3.zero);
        }

        // ── Behaviour ───────────────────────────────────────────────────────────

        private IEnumerator RunBehaviour()
        {
            // Give the agent a frame to attach to the NavMesh
            yield return null;
            if (!_agent.isOnNavMesh && !TryWarpToNavMesh())
            {
                Debug.LogWarning("[CustomerAI] Could not reach the NavMesh — despawning.");
                Destroy(gameObject);
                yield break;
            }

            // Walk in off the pavement first
            State = CustomerState.Entering;
            if (EntryPoint != null) yield return NavigateTo(EntryPoint.position);

            State = CustomerState.Browsing;
            int stops = Random.Range(2, 5);
            for (int i = 0; i < stops; i++)
            {
                yield return NavigateTo(PickBrowseTarget());
                yield return new WaitForSeconds(Random.Range(BrowseTimeMin, BrowseTimeMax));
                TryPickUp();
                if (_basket.Count >= MaxPurchases) break;
            }

            // Rep hit for an empty basket is scaled by the customer's profile.
            ApplyBrowsePenalties();
            bool queued = _basket.Count > 0 && Queue != null;
            if (_basket.Count > 0) yield return WaitToBeServed();
            else WalkedOutEmpty?.Invoke(this);

            State = CustomerState.Leaving;
            // Off the queue line first: it runs from the till towards the door, and walking back
            // down it through the people still waiting deadlocked the agents (walk timeouts).
            if (queued)
            {
                // Still holding priority over the line (see WaitToBeServed) until clear of it.
                yield return NavigateTo(StepAsideFromTill());
                _agent.avoidancePriority = _walkingPriority;
            }
            if (EntryPoint != null) yield return NavigateTo(EntryPoint.position);
            Vector3 exit = ExitPoint != null ? ExitPoint.position : transform.position + Vector3.forward * 10f;
            exit.x += Random.Range(-14f, 14f);
            yield return NavigateTo(exit);

            State = CustomerState.Done;
            Destroy(gameObject);
        }

        /// <summary>
        /// Join the line, shuffle forward as it moves, and wait to be served. Nobody pays
        /// themselves any more: either the player serves them or they walk out.
        /// </summary>
        private IEnumerator WaitToBeServed()
        {
            State = CustomerState.Queueing;

            if (Queue == null)
            {
                // No till in the shop — fall back to paying on the way out rather than
                // silently losing the sale.
                if (RegisterPoint != null) yield return NavigateTo(RegisterPoint.position);
                Checkout();
                yield break;
            }

            Queue.Join(this, Profile.PatienceMultiplier);
            SetBubble("waiting to pay", new Color(0.98f, 0.82f, 0.4f));
            // People standing in line hold their ground; shoppers walking past steer round them.
            _walkingPriority = _agent.avoidancePriority;
            _agent.avoidancePriority = QueueAvoidancePriority;
            int lastPlace = -1;

            while (!_served && !_gaveUp)
            {
                int place = Queue.PlaceOf(this);
                if (place < 0) break;

                if (place != lastPlace)
                {
                    lastPlace = place;
                    yield return NavigateTo(Queue.StandingPosition(place));
                    // Face the till while waiting.
                    if (Queue.TillPoint != null)
                    {
                        Vector3 look = Queue.TillPoint.position - transform.position;
                        look.y = 0f;
                        if (look.sqrMagnitude > 0.01f)
                            transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                    }
                }
                yield return null;
            }

            Queue.Leave(this);
            // The next in line moves up onto this spot at once, while this shopper is still standing
            // on it. Given way to (QueueAvoidancePriority) it shoved them off the line, often onto the
            // side away from the door, boxed in between the counter and the queue with no way round:
            // stuck. Outranking the line until RunBehaviour has stepped aside keeps them where they
            // are and lets them cut across in front of it to the door side.
            _agent.avoidancePriority = ClearingTillAvoidancePriority;

            if (_served)
            {
                State = CustomerState.Buying;
                yield return new WaitForSeconds(0.4f);
                Checkout();
            }
            else if (_gaveUp)
            {
                // Abandon the basket: stock is lost from the shelf either way, and the
                // shop's standing takes a real hit.
                ShopManager?.ChangeReputation(-3.5f);
                GameManager.Instance?.Notify($"{name} gave up waiting and walked out.");
                SetBubble("gave up!", UIFactory.Bad);
                FloatingText.Spawn(transform.position + Vector3.up * 2.4f, "−3.5 rep", UIFactory.Bad, 0.26f);
                _basket.Clear();
                GaveUp?.Invoke(this);
            }
        }

        /// <summary>Avoidance priority while standing in line: lower numbers are given way to.</summary>
        private const int QueueAvoidancePriority = 10;

        /// <summary>
        /// Avoidance priority from leaving the line until stepped aside off it: below
        /// QueueAvoidancePriority, so the people moving up give way instead of shoving.
        /// </summary>
        private const int ClearingTillAvoidancePriority = 5;

        /// <summary>Avoidance priority to walk with, saved on joining the line and restored once off it.</summary>
        private int _walkingPriority;

        /// <summary>How far beside the queue line a served shopper steps before heading out.</summary>
        private const float StepAsideDistance = 1.8f;

        /// <summary>A point beside the front of the line, on the side nearer the way out.</summary>
        private Vector3 StepAsideFromTill()
        {
            Vector3 front = Queue.StandingPosition(0);
            Vector3 along = Queue.QueueDirection;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
            Vector3 exit = EntryPoint != null ? EntryPoint.position : front + along * 10f;
            if (Vector3.Dot(exit - front, side) < 0f) side = -side;
            return front + side * StepAsideDistance;
        }

        private bool TryWarpToNavMesh()
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 6f, NavMesh.AllAreas)) return false;
            _agent.Warp(hit.position);
            return _agent.isOnNavMesh;
        }

        /// <summary>Walks to a point, giving up after a timeout so nobody stalls forever.</summary>
        private IEnumerator NavigateTo(Vector3 target)
        {
            if (!_agent.isOnNavMesh) yield break;

            target.y = 0f;
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas))
                target = hit.position;

            _agent.isStopped = false;
            if (!_agent.SetDestination(target)) yield break;

            float timeout = 20f;
            while (timeout > 0f)
            {
                timeout -= Time.deltaTime;
                if (_agent.pathPending) { yield return null; continue; }
                if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) yield break;
                if (_agent.remainingDistance <= _agent.stoppingDistance) yield break;
                yield return null;
            }

            Debug.LogWarning($"[CustomerAI] {name} ran out of time walking to {target}.");
            NavigationTimedOut?.Invoke(this);

            // Deliberately not setting isStopped here: toggling it between legs makes the
            // agent lurch. autoBraking already eases it into each stop.
        }

        private Vector3 PickBrowseTarget()
        {
            var liveShelves = Shelves.FindAll(s => s != null);
            var livePens    = PetPens.FindAll(p => p != null);

            if (liveShelves.Count > 0 && Random.value > 0.35f)
            {
                var shelf = liveShelves[Random.Range(0, liveShelves.Count)];
                return shelf.transform.position + shelf.transform.forward * 1.3f
                     + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f);
            }
            if (livePens.Count > 0 && Random.value > 0.4f)
            {
                var pen = livePens[Random.Range(0, livePens.Count)];
                return pen.transform.position + new Vector3(Random.Range(-1f, 1f), 0f, pen.PenSize * 0.5f + 1f);
            }
            // Fall back to a point inside the shop rather than drifting back onto the pavement
            return EntryPoint != null
                ? EntryPoint.position + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-9f, -1f))
                : transform.position + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
        }

        private void TryPickUp()
        {
            float demand = ShopManager != null
                ? ShopManager.DemandFor(Profile.PriceSensitivity, Profile.MaxDemand) : 1f;
            TryPickUpItem(demand);
            TryPickUpPet(demand);
        }

        private void TryPickUpItem(float demand)
        {
            var stocked = Shelves.FindAll(s => s != null && !s.IsEmpty);

            // Favour the aisle they came for; fall back to anything stocked.
            var preferred = stocked.FindAll(s => s.Category == PreferredCategory);
            if (preferred.Count > 0) stocked = preferred;
            if (stocked.Count == 0 || !WillBuy(Random.value, ShelfBuyChance, demand)) return;

            var shelf   = stocked[Random.Range(0, stocked.Count)];
            var product = shelf.TakeOne();
            if (product == null) return;

            float price = ShopManager != null ? ShopManager.PriceOf(product.basePrice) : product.basePrice;
            if (price <= BudgetCap)
            {
                _basket.Add((product.id, product.displayName, price));
                SetBubble($"got {product.displayName}", UIFactory.Good);
                return;
            }
            // Too dear: put it back, and let the player see why.
            shelf.AddStock(product, 1);
            SetBubble("too expensive", UIFactory.Bad);
        }

        /// <summary>Adults only, filtered by the profile's rarity; price checked against PetBudget first.</summary>
        private void TryPickUpPet(float demand)
        {
            if (_boughtPet || !WantsPet) return;
            var pens = PetPens.FindAll(p => p != null && Profile.FirstWantedPet(p) != null);
            if (pens.Count > 0) _sawWantedPet = true;
            if (!WillBuy(Random.value, PetBuyChance * 3f, demand) || pens.Count == 0) return;

            var pen = pens[Random.Range(0, pens.Count)];
            Pet pet = Profile.FirstWantedPet(pen);
            float price = ShopManager != null ? ShopManager.PriceOf(pet.SellPrice()) : pet.SellPrice();
            if (price > PetBudget)
            {
                // Skip this pet but keep browsing: shelf items may still sell (as before archetypes).
                SetBubble($"{pet.species}? too pricey", UIFactory.Bad);
                return;
            }
            if (!pen.RemovePet(pet)) return;
            _basket.Add(($"pet_{pet.species}", pet.DisplayName(), price));
            _boughtPet = true;
            SetBubble($"buying a {pet.species}", UIFactory.Good);
        }

        /// <summary>Rep hits for an empty basket, or for never seeing an acceptable animal.</summary>
        private void ApplyBrowsePenalties()
        {
            if (ShopManager == null) return;
            // Walked out with nothing — bad for the shop's standing, how bad depends on who
            if (_basket.Count == 0) ShopManager.ChangeReputation(-Profile.EmptyShelfRepPenalty);

            bool missedPet = WantsPet && !_boughtPet && !_sawWantedPet;
            if (missedPet && Profile.NoMatchingPetRepPenalty > 0f)
                ShopManager.ChangeReputation(-Profile.NoMatchingPetRepPenalty);
        }

        private void Checkout()
        {
            if (ShopManager == null) return;

            float total = 0f;
            foreach (var (id, label, price) in _basket)
            {
                ShopManager.RecordSale(id, label, 1, price, name);
                total += price;
            }
            ShopManager.ChangeReputation(0.4f + _basket.Count * 0.5f);
            if (_basket.Count >= 3) _visual?.PlayTrigger("happy_dance");

            SetBubble("thanks!", UIFactory.Good);
            FloatingText.Spawn(transform.position + Vector3.up * 2.3f,
                               $"+€ {total:N2}", UIFactory.Good, 0.3f);
            GameManager.Instance?.Notify($"Sold {_basket.Count} item(s) for €{total:N2}");
            AudioManager.Instance?.PlaySfx("sale");

            DistanceToTillAtCheckout = HorizontalDistanceToTill();
            CheckedOut?.Invoke(this);
        }

        /// <summary>XZ distance to the register (or the queue's till); 0 when neither exists.</summary>
        private float HorizontalDistanceToTill()
        {
            Transform till = RegisterPoint != null ? RegisterPoint
                           : Queue != null ? Queue.TillPoint : null;
            if (till == null) return 0f;

            Vector3 offset = till.position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void OnDestroy() => Despawned?.Invoke(this);
    }
}
