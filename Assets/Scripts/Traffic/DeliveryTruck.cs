using System;
using System.Collections.Generic;
using PetShop.Core;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// One supplier run: a box truck joins the street traffic at the start of the director's delivery
    /// lane, pulls up at the kerb outside the shop, drops its load (the caller's callback puts the crate
    /// on the forecourt), then drives on and leaves the street.
    ///
    /// The truck is ordinary traffic while it drives — it keeps its distance and others queue behind it
    /// while it unloads. When there is no street (no director, no delivery lane) or no truck model is
    /// installed, <see cref="Dispatch"/> returns false and the caller drops the crate straight away.
    /// </summary>
    public class DeliveryTruck : MonoBehaviour
    {
        /// <summary>Rough seconds from dispatch to pulling up outside the shop; deliveries are timed to subtract it.</summary>
        public const float EstimatedDriveSeconds = 8f;

        private const float TruckLength     = 6.5f;
        private const float CruiseSpeed     = 7f;
        private const float EntryClearance  = 14f;
        private const float UnloadSeconds   = 2.5f;
        private const float ParkedDepth     = -50f;

        /// <summary>Truck models, best first: SimplePoly City box truck, then the Kenney kit.</summary>
        private static readonly string[] Models =
        {
            ModelLibrary.CityCars + "Vehicle with Separated Wheels/Vehicle_Truck_color01_separate",
            ModelLibrary.Cars + "truck",
            ModelLibrary.Cars + "delivery",
        };

        private static readonly List<DeliveryTruck> Active = new List<DeliveryTruck>();

        private enum Phase { WaitingToEnter, Driving, Unloading, Leaving }

        private Phase _phase = Phase.WaitingToEnter;
        private Action _onDrop;
        private TrafficCar _car;
        private TrafficLane _lane;
        private float _stopAt;
        private float _unloadTimer;

        /// <summary>Trucks on their way or unloading.</summary>
        public static int ActiveCount => Active.Count;

        /// <summary>
        /// Sends a truck that calls <paramref name="onDrop"/> when it pulls up outside the shop.
        /// False when there is no street or truck model; the caller should then drop the load itself.
        /// </summary>
        public static bool Dispatch(Action onDrop)
        {
            TrafficDirector director = TrafficDirector.Instance;
            if (onDrop == null || director == null || !director.isActiveAndEnabled || director.DeliveryLane == null)
                return false;

            string path = ModelLibrary.FirstAvailable(Models);
            if (path == null) return false;

            var root = new GameObject("DeliveryTruck") { layer = GameLayers.Scenery };
            root.transform.SetParent(director.transform, false);
            root.transform.position = new Vector3(0f, ParkedDepth, 0f);
            GameObject body = ModelLibrary.Spawn(path, root.transform, root.transform.position, 0f,
                                                 ModelLibrary.Fit.Depth, TruckLength);
            if (body == null) { Destroy(root); return false; }
            MeshBuilder.StripColliders(body);
            MeshBuilder.SetLayerRecursive(body, GameLayers.Scenery);

            var truck = root.AddComponent<DeliveryTruck>();
            truck._onDrop = onDrop;
            truck._lane   = director.DeliveryLane;
            truck._stopAt = director.DeliveryStopDistance;
            truck._car    = root.AddComponent<TrafficCar>();   // measures the body for its collider
            Active.Add(truck);
            return true;
        }

        /// <summary>
        /// Drops every load still on the road and removes the trucks. Called at closing time so nothing
        /// ordered is left in limbo when the day is saved.
        /// </summary>
        public static void FlushAll()
        {
            foreach (DeliveryTruck truck in Active.ToArray())
            {
                if (truck == null) continue;
                truck.Drop();
                truck.Remove();
            }
            Active.Clear();
        }

        private void Update()
        {
            if (_car == null) { Remove(); return; }
            switch (_phase)
            {
                case Phase.WaitingToEnter:
                    if (TrafficCar.ClearanceAtStart(_lane) < EntryClearance) return;
                    _car.Configure(_lane, 0f, CruiseSpeed);
                    _car.HoldAt(_stopAt);
                    TrafficDirector.Register(_car);
                    _phase = Phase.Driving;
                    break;
                case Phase.Driving:
                    if (!_car.IsHeld) return;
                    Drop();
                    _unloadTimer = UnloadSeconds;
                    _phase = Phase.Unloading;
                    break;
                case Phase.Unloading:
                    _unloadTimer -= Time.deltaTime;
                    if (_unloadTimer > 0f) return;
                    _car.Release();
                    _car.DespawnAtLaneEnd();
                    _phase = Phase.Leaving;
                    break;
            }
        }

        private void Drop()
        {
            Action drop = _onDrop;
            _onDrop = null;
            drop?.Invoke();
        }

        private void Remove()
        {
            if (_car != null) TrafficDirector.Unregister(_car);
            Destroy(gameObject);
        }

        private void OnDestroy() => Active.Remove(this);
    }
}
