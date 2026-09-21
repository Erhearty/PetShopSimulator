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

        public CustomerState State { get; private set; } = CustomerState.Entering;

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
        private bool  _boughtPet;
        private bool  _served;
        private bool  _gaveUp;

        // ── CheckoutQueue.IShopper ──────────────────────────────────────────────

        public float BasketValue
        {
            get { float t = 0f; foreach (var (_, _, price) in _basket) t += price; return t; }
        }
        public int    BasketCount => _basket.Count;
        public string ShopperName => name;

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
            WantsPet          = Random.value < 0.30f;
            BudgetCap         = WantsPet ? Random.Range(120f, 900f) : Random.Range(14f, 60f);

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
                                        $"<size=80%>{want}</size>", 0.085f, colour, width: 1.4f);
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

            if (_basket.Count > 0)
            {
                yield return WaitToBeServed();
            }
            else
            {
                // Walked out with nothing — mildly bad for the shop's standing
                ShopManager?.ChangeReputation(-0.6f);
            }

            State = CustomerState.Leaving;
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

            Queue.Join(this);
            SetBubble("waiting to pay", new Color(0.98f, 0.82f, 0.4f));
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
            }
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
                if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) break;
                if (_agent.remainingDistance <= _agent.stoppingDistance) break;
                yield return null;
            }

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
            float demand = ShopManager != null ? ShopManager.DemandFactor : 1f;

            var stocked = Shelves.FindAll(s => s != null && !s.IsEmpty);

            // Favour the aisle they came for; fall back to anything stocked.
            var preferred = stocked.FindAll(s => s.Category == PreferredCategory);
            if (preferred.Count > 0) stocked = preferred;

            if (stocked.Count > 0 && Random.value < 0.7f * demand)
            {
                var shelf   = stocked[Random.Range(0, stocked.Count)];
                var product = shelf.TakeOne();
                if (product != null)
                {
                    float price = ShopManager != null ? ShopManager.PriceOf(product.basePrice)
                                                      : product.basePrice;

                    if (price <= BudgetCap)
                    {
                        _basket.Add((product.id, product.displayName, price));
                        SetBubble($"got {product.displayName}", UIFactory.Good);
                    }
                    else
                    {
                        // Too dear: put it back, and let the player see why.
                        shelf.AddStock(product, 1);
                        SetBubble("too expensive", UIFactory.Bad);
                    }
                }
            }

            if (!_boughtPet && WantsPet && Random.value < PetBuyChance * 3f * demand)
            {
                var pens = PetPens.FindAll(p => p != null && p.HasAdults);
                if (pens.Count > 0)
                {
                    var pen = pens[Random.Range(0, pens.Count)];
                    Pet pet = pen.TakeAdult();
                    if (pet != null)
                    {
                        float price = ShopManager != null ? ShopManager.PriceOf(pet.SellPrice())
                                                          : pet.SellPrice();
                        _basket.Add(($"pet_{pet.species}", pet.DisplayName(), price));
                        _boughtPet = true;
                        SetBubble($"buying a {pet.species}", UIFactory.Good);
                    }
                }
            }
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
        }

    }
}
