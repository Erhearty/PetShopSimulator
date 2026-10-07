using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Customer
{
    /// <summary>
    /// Releases customers through the door across the day. Footfall scales with
    /// reputation, so a well-run shop gets busier. Arrivals follow the day's
    /// <see cref="ArrivalSchedule"/>: a normal curve peaking at 14:00, with at least
    /// <see cref="ArrivalSchedule.MinQueueJoinsPerHour"/> guaranteed buyers reaching the till
    /// in every trading hour.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        [Header("Wiring")]
        public ShopManager ShopManager;
        public Transform   SpawnPoint;      // on the pavement outside
        public Transform   EntryPoint;      // just inside the shop door
        public Transform   ExitPoint;
        public Transform   RegisterPoint;
        public CheckoutQueue Queue;

        [Tooltip("How far along the pavement customers may appear, either side of SpawnPoint.")]
        public float SpawnSpreadX = 20f;

        [Header("Tuning")]
        public int   MaxCustomersPerDay  = 18;
        public int   MinCustomersPerDay  = 3;
        public int   MaxConcurrent       = 8;

        [Header("Archetype mix")]
        [Tooltip("Relative weight of ordinary shoppers.")]
        [Min(0f)] public float RegularWeight         = CustomerProfile.DefaultRegularWeight;
        [Tooltip("Relative weight of bargain hunters, who flock to discounts and flee markups.")]
        [Min(0f)] public float BargainHunterWeight   = CustomerProfile.DefaultBargainHunterWeight;
        [Tooltip("Relative weight of rare-pet collectors at zero reputation.")]
        [Min(0f)] public float RareCollectorWeight   = CustomerProfile.DefaultRareCollectorWeight;
        [Tooltip("Collector weight added at full reputation (scaled linearly), so famous shops draw collectors.")]
        [Min(0f)] public float RareCollectorPerReputation = CustomerProfile.DefaultRareCollectorReputationBonus;
        [Tooltip("Relative weight of parents bringing a child.")]
        [Min(0f)] public float ParentWithChildWeight = CustomerProfile.DefaultParentWithChildWeight;

        /// <summary>
        /// Game seconds (real seconds at time scale 1) from a customer appearing on the pavement to
        /// joining the checkout queue, as measured by ArrivalCurvePlayTests in the furnished shop on
        /// a 540 s day: 28.8 s mean, 14.3-42.5 s over 16 joins (the walk in, two to four browsing
        /// stops of 1.2-3 s each, then the walk to the line). Customers are let in this much ahead
        /// of their planned queue join (converted to game hours with the day length). On a 540 s
        /// day the ±15 s spread is ±0.25 game hours, inside the quarter hour the guaranteed joins
        /// (:22 and :37, <see cref="ArrivalSchedule.GuaranteedSpread"/>) keep clear of their hour's
        /// edges. Walks and browsing run on scaled time, so this holds at any Time.timeScale.
        /// </summary>
        public const float SpawnToQueueSeconds = 29f;

        /// <summary>
        /// Guaranteed buyers may go over <see cref="MaxConcurrent"/> by at most this many: one
        /// hour's floor, so a full shop never holds up the hour's guarantee, while a day whose
        /// arrivals bunch up (a short day collapses the lead onto 09:00) cannot flood the floor.
        /// </summary>
        public const int GuaranteedOverflow = ArrivalSchedule.MinQueueJoinsPerHour;

        /// <summary>
        /// While the shop cannot trade, a planned arrival is kept this many game hours past its
        /// time and then dropped, so furnishing the shop mid-day does not release a crowd at once.
        /// </summary>
        public const float MissedArrivalGraceHours = 0.25f;

        /// <summary>Day length used when there is no GameManager, in real seconds.</summary>
        private const float FallbackDayLengthSeconds = 540f;

        /// <summary>A child is drawn at this fraction of adult height.</summary>
        private const float ChildHeightScale = 0.6f;
        /// <summary>How far behind the parent the child first appears, in metres.</summary>
        private const float ChildSpawnBehind = 0.5f;

        [HideInInspector] public List<ShelfUnit> Shelves = new();
        [HideInInspector] public List<PetPen>    PetPens = new();

        /// <summary>True once at least one counter is placed. Pushed by the GameManager furniture registry.</summary>
        [HideInInspector] public bool HasCounter;

        /// <summary>Shown once per day when the doors stay shut for lack of furniture.</summary>
        public const string NotTradingMessage = "Place a counter and a shelf or pen to open for customers.";

        public int  SpawnedToday { get; private set; }
        public int  TargetToday  { get; private set; }
        public bool DoorsClosed  { get; private set; }

        /// <summary>Customers currently in the world.</summary>
        public int LiveCustomers { get; private set; }

        /// <summary>
        /// Event-driven multiplier on the gap between customers. Below 1 means busier; 1 = normal.
        /// Arrivals are scheduled rather than timed, so at StartDay it is inverted into a count
        /// multiplier on the reputation target (0.6 => 1/0.6 as many). That only moves customers
        /// above the per-hour floor: the floor of guaranteed buyers is never scaled.
        /// </summary>
        public float IntervalMultiplier { get; set; } = 1f;

        /// <summary>Event-driven multiplier on today's customer target, applied at StartDay. 1 = normal.</summary>
        public float TargetMultiplier { get; set; } = 1f;

        private bool  _dayActive;
        private float _dayElapsed;
        private float _censusTimer;
        private bool  _warnedNotTrading;

        /// <summary>Today's arrivals not yet let in, earliest first.</summary>
        private readonly List<ArrivalSchedule.Slot> _pending = new();

        /// <summary>Arrivals still to come today (scheduled but not yet let in or dropped).</summary>
        public int PendingArrivals => _pending.Count;

        /// <summary>
        /// The shop can trade once it has somewhere to pay (a counter) and something to sell
        /// (at least one shelf or pen). Until then no customers are let in.
        /// </summary>
        public static bool CanTrade(bool hasCounter, int shelfCount, int penCount) =>
            hasCounter && (shelfCount > 0 || penCount > 0);

        /// <summary>
        /// Today's customer target before the hourly floor: the reputation lerp between
        /// <paramref name="min"/> and <paramref name="max"/>, times the event
        /// <paramref name="targetMultiplier"/>, times the inverse of the event
        /// <paramref name="intervalMultiplier"/> (a shorter gap means more customers).
        /// </summary>
        public static int TargetFor(float reputation, float targetMultiplier, float intervalMultiplier,
                                    int min, int max)
        {
            float countMultiplier = intervalMultiplier > 0f ? 1f / intervalMultiplier : 1f;
            return Mathf.RoundToInt(Mathf.Lerp(min, max, reputation / 100f) * targetMultiplier * countMultiplier);
        }

        /// <summary>The current day length, or the 540 s default without a GameManager.</summary>
        private static float CurrentDayLengthSeconds() =>
            GameManager.Instance != null ? GameManager.Instance.DayLengthSeconds : FallbackDayLengthSeconds;

        /// <summary>Whether the furniture currently placed lets the shop trade.</summary>
        public bool CanTradeNow => CanTrade(HasCounter, Shelves.Count, PetPens.Count);

        public void StartDay()
        {
            SpawnedToday = 0;
            DoorsClosed  = false;
            _dayActive   = true;
            _dayElapsed  = 0f;
            _warnedNotTrading = false;

            int target = ShopManager == null ? MinCustomersPerDay
                : TargetFor(ShopManager.Reputation, TargetMultiplier, IntervalMultiplier,
                            MinCustomersPerDay, MaxCustomersPerDay);
            TargetToday = ArrivalSchedule.TotalFor(target);

            float dayLength = CurrentDayLengthSeconds();
            float lead = ArrivalSchedule.LeadGameHours(SpawnToQueueSeconds, dayLength);
            if (!ArrivalSchedule.GuaranteeHolds(lead))
                Debug.LogWarning($"[CustomerSpawner] A {dayLength:0.#} s day gives a {lead:0.##} h arrival lead " +
                                 $"(over {ArrivalSchedule.MaxGuaranteedLeadHours} h): the guarantee of " +
                                 $"{ArrivalSchedule.MinQueueJoinsPerHour} queue joins per hour can't hold at this day length.");
            _pending.Clear();
            _pending.AddRange(ArrivalSchedule.Slots(ArrivalSchedule.CountsPerHour(target), lead));
        }

        /// <summary>Stop letting new customers in, but let those inside finish shopping.</summary>
        public void CloseDoors() => DoorsClosed = true;

        public void EndDay()
        {
            _dayActive  = false;
            DoorsClosed = true;
            _pending.Clear();
        }

        private void Update()
        {
            _censusTimer -= Time.deltaTime;
            if (_censusTimer <= 0f) { _censusTimer = 0.5f; LiveCustomers = CountLive(); }

            if (!_dayActive) return;
            _dayElapsed += Time.deltaTime;
            if (DoorsClosed || ShopManager == null) return;

            float now = CurrentGameHour();
            if (!CanTradeNow)
            {
                WarnNotTradingOnce();
                _pending.RemoveAll(s => s.Hour < now - MissedArrivalGraceHours);
                return;
            }

            // Due arrivals in order. Guaranteed buyers may exceed MaxConcurrent by up to
            // GuaranteedOverflow; anyone else waits for room, without holding up the guaranteed
            // buyers behind them.
            for (int i = 0; i < _pending.Count && _pending[i].Hour <= now;)
            {
                var slot = _pending[i];
                int cap  = slot.Guaranteed ? MaxConcurrent + GuaranteedOverflow : MaxConcurrent;
                if (LiveCustomers >= cap) { i++; continue; }
                _pending.RemoveAt(i);
                SpawnCustomer(slot.Guaranteed);
            }
        }

        /// <summary>The shop clock in game hours (9..18), from the GameManager or the spawner's own day timer.</summary>
        private float CurrentGameHour() =>
            GameManager.Instance != null ? GameManager.Instance.CurrentGameHour
                : ArrivalSchedule.FirstHour + ArrivalSchedule.TradingDayHours
                  * Mathf.Clamp01(_dayElapsed / FallbackDayLengthSeconds);

        /// <summary>Tells the player, once per day, why no customers are coming.</summary>
        private void WarnNotTradingOnce()
        {
            if (_warnedNotTrading) return;
            _warnedNotTrading = true;
            GameManager.Instance?.Notify(NotTradingMessage);
        }

        private int CountLive()
        {
            int n = 0;
            foreach (Transform child in transform)
                if (child.GetComponent<CustomerAI>() != null) n++;
            return n;
        }

        private void SpawnCustomer(bool mustBuy)
        {
            Vector3 pos = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
            pos.x += Random.Range(-SpawnSpreadX, SpawnSpreadX);
            if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas)) pos = hit.position;
            else
            {
                Debug.LogWarning("[CustomerSpawner] No NavMesh near the door — skipping spawn.");
                return;
            }

            var go = new GameObject($"Customer_{Shop_Day()}_{SpawnedToday}");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, pos.x > 0f ? 270f : 90f, 0f);

            var ai = go.AddComponent<CustomerAI>();
            ai.ShopManager   = ShopManager;
            ai.EntryPoint    = EntryPoint;
            ai.ExitPoint     = ExitPoint;
            ai.RegisterPoint = RegisterPoint;
            ai.Queue         = Queue;
            ai.Shelves       = new List<ShelfUnit>(Shelves);
            ai.PetPens       = new List<PetPen>(PetPens);
            ai.MustBuy       = mustBuy;
            // Awake has run inside AddComponent; Start has not, so the archetype still takes effect.
            ai.Archetype     = CustomerProfile.Roll(ShopManager != null ? ShopManager.Reputation : 0f, CurrentMix());
            if (ai.Archetype == CustomerArchetype.ParentWithChild) SpawnChild(go);

            SpawnedToday++;
            LiveCustomers++;
        }

        private int Shop_Day() => ShopManager != null ? ShopManager.Day : 0;

        private ArchetypeWeights CurrentMix() => new(RegularWeight, BargainHunterWeight, RareCollectorWeight,
                                                     RareCollectorPerReputation, ParentWithChildWeight);

        /// <summary>
        /// A small companion that lerp-follows <paramref name="parent"/> and is destroyed with it.
        /// Kept as a sibling rather than a child transform so it can trail behind smoothly, and
        /// without a CustomerAI so it never counts towards the live-customer census.
        /// </summary>
        private void SpawnChild(GameObject parent)
        {
            var child = new GameObject($"{parent.name}_Child");
            child.transform.SetParent(transform, false);
            child.transform.SetPositionAndRotation(parent.transform.position - parent.transform.forward * ChildSpawnBehind,
                                                   parent.transform.rotation);
            child.layer = GameLayers.Character;

            var follower = child.AddComponent<ChildFollower>();
            follower.Target = parent.transform;
            CharacterFactory.Attach(child, () => follower != null ? follower.Velocity : Vector3.zero,
                                    height: CharacterFactory.AdultHeight * ChildHeightScale);
        }
    }
}
