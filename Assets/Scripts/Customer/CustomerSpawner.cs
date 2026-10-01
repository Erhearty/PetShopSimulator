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
    /// reputation, so a well-run shop gets busier.
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
        public float FirstCustomerDelay  = 2f;
        public float BaseIntervalSeconds = 11f;
        public float MinIntervalSeconds  = 3.5f;
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

        /// <summary>A child is drawn at this fraction of adult height.</summary>
        private const float ChildHeightScale = 0.6f;
        /// <summary>How far behind the parent the child first appears, in metres.</summary>
        private const float ChildSpawnBehind = 0.5f;

        [HideInInspector] public List<ShelfUnit> Shelves = new();
        [HideInInspector] public List<PetPen>    PetPens = new();

        public int  SpawnedToday { get; private set; }
        public int  TargetToday  { get; private set; }
        public bool DoorsClosed  { get; private set; }

        /// <summary>Customers currently in the world.</summary>
        public int LiveCustomers { get; private set; }

        /// <summary>Event-driven multiplier on the spawn interval. Below 1 means busier; 1 = normal.</summary>
        public float IntervalMultiplier { get; set; } = 1f;

        /// <summary>Event-driven multiplier on today's customer target, applied at StartDay. 1 = normal.</summary>
        public float TargetMultiplier { get; set; } = 1f;

        private bool  _dayActive;
        private float _timer;
        private float _censusTimer;

        public void StartDay()
        {
            SpawnedToday = 0;
            DoorsClosed  = false;
            _dayActive   = true;
            _timer       = BaseIntervalSeconds - FirstCustomerDelay;
            TargetToday  = ShopManager == null ? MinCustomersPerDay : Mathf.RoundToInt(
                Mathf.Lerp(MinCustomersPerDay, MaxCustomersPerDay, ShopManager.Reputation / 100f) * TargetMultiplier);
        }

        /// <summary>Stop letting new customers in, but let those inside finish shopping.</summary>
        public void CloseDoors() => DoorsClosed = true;

        public void EndDay()
        {
            _dayActive  = false;
            DoorsClosed = true;
        }

        private void Update()
        {
            _censusTimer -= Time.deltaTime;
            if (_censusTimer <= 0f) { _censusTimer = 0.5f; LiveCustomers = CountLive(); }

            if (!_dayActive || DoorsClosed || ShopManager == null) return;
            if (SpawnedToday >= TargetToday) return;

            if (LiveCustomers >= MaxConcurrent) return;

            float interval = Mathf.Max(MinIntervalSeconds,
                                       BaseIntervalSeconds * (1f - ShopManager.Reputation / 160f) * IntervalMultiplier);

            _timer += Time.deltaTime;
            if (_timer < interval) return;

            _timer = 0f;
            SpawnCustomer();
        }

        private int CountLive()
        {
            int n = 0;
            foreach (Transform child in transform)
                if (child.GetComponent<CustomerAI>() != null) n++;
            return n;
        }

        private void SpawnCustomer()
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
