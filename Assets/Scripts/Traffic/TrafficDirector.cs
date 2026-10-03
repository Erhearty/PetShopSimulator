using System;
using System.Collections.Generic;
using PetShop.Core;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// The single scene object that owns street traffic: registers lanes and cars, jitters cruise speeds
    /// from the playtest seed and ticks every car while running (not paused, not frozen).
    /// </summary>
    public class TrafficDirector : MonoBehaviour
    {
        private const int DefaultSeed = 12345;
        private const float SpeedJitter = 0.15f;
        private const double Two = 2.0;
        private const double Half = 0.5;

        private static readonly List<TrafficCar> CarList = new List<TrafficCar>();

        private TrafficLane[] _lanes = Array.Empty<TrafficLane>();

        [Header("Deliveries")]
        [Tooltip("Lane the delivery truck drives along to reach the shop.")]
        [SerializeField] private TrafficLane deliveryLane;
        [Tooltip("Distance along the delivery lane at which the truck pulls up outside the shop.")]
        [SerializeField] private float deliveryStopDistance;

        /// <summary>The active director, or null when the scene has none.</summary>
        public static TrafficDirector Instance { get; private set; }

        /// <summary>Lane the delivery truck drives along, or null when deliveries come without one.</summary>
        public TrafficLane DeliveryLane => deliveryLane;

        /// <summary>Where along <see cref="DeliveryLane"/> the truck stops outside the shop.</summary>
        public float DeliveryStopDistance => deliveryStopDistance;

        /// <summary>Adds a car built at runtime (a delivery truck) to the ticked set.</summary>
        public static void Register(TrafficCar car)
        {
            if (car != null && !CarList.Contains(car)) CarList.Add(car);
        }

        /// <summary>Removes a car from the ticked set.</summary>
        public static void Unregister(TrafficCar car) => CarList.Remove(car);

        private void Awake() => Instance = this;

        /// <summary>Cars registered by the active director.</summary>
        public static IReadOnlyList<TrafficCar> Cars => CarList;

        /// <summary>True while traffic is paused (gameplay pause).</summary>
        public bool IsPaused { get; private set; }

        /// <summary>True while traffic is frozen (e.g. for screenshots).</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>True when cars are being updated.</summary>
        public bool IsRunning => isActiveAndEnabled && !IsPaused && !IsFrozen;

        /// <summary>Stops cars until <see cref="Resume"/>.</summary>
        public void Pause() => IsPaused = true;

        /// <summary>Lets cars move again.</summary>
        public void Resume() => IsPaused = false;

        /// <summary>Stops cars until <see cref="Unfreeze"/>.</summary>
        public void Freeze() => IsFrozen = true;

        /// <summary>Releases a freeze.</summary>
        public void Unfreeze() => IsFrozen = false;

        private void Start()
        {
            _lanes = FindObjectsByType<TrafficLane>(FindObjectsSortMode.None);
            if (_lanes.Length == 0)
            {
                Debug.Log("[Traffic] No TrafficLane in the scene; traffic director disabled.");
                enabled = false;
                return;
            }
            RegisterCars();
        }

        private void RegisterCars()
        {
            CarList.Clear();
            TrafficCar[] cars = FindObjectsByType<TrafficCar>(FindObjectsSortMode.None);
            Array.Sort(cars, (a, b) => string.CompareOrdinal(a.name, b.name));
            var rng = new System.Random(PlaytestOptions.Seed ?? DefaultSeed);
            foreach (TrafficCar car in cars)
            {
                double jitter = (rng.NextDouble() - Half) * Two * SpeedJitter;
                car.Setup(1f + (float)jitter);
                CarList.Add(car);
            }
        }

        private void Update()
        {
            if (!IsRunning) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < CarList.Count; i++)
                if (CarList[i] != null) CarList[i].Tick(dt);

            // Cars that left the street (delivery trucks) are removed after the loop, never during it.
            for (int i = CarList.Count - 1; i >= 0; i--)
            {
                if (CarList[i] != null && !CarList[i].Finished) continue;
                if (CarList[i] != null) Destroy(CarList[i].gameObject);
                CarList.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            CarList.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
