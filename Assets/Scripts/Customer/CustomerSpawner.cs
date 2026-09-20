using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetShop.Commerce;
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

        [HideInInspector] public List<ShelfUnit> Shelves = new();
        [HideInInspector] public List<PetPen>    PetPens = new();

        public int  SpawnedToday { get; private set; }
        public int  TargetToday  { get; private set; }
        public bool DoorsClosed  { get; private set; }

        /// <summary>Customers currently in the world.</summary>
        public int LiveCustomers { get; private set; }

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
                Mathf.Lerp(MinCustomersPerDay, MaxCustomersPerDay, ShopManager.Reputation / 100f));
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
                                       BaseIntervalSeconds * (1f - ShopManager.Reputation / 160f));

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

            SpawnedToday++;
            LiveCustomers++;
        }

        private int Shop_Day() => ShopManager != null ? ShopManager.Day : 0;
    }
}
