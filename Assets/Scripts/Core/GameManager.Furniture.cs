using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Shop;
using PetShop.Player;

namespace PetShop.Core
{
    /// <summary>
    /// The furniture supply chain as seen from the facade: the inventory and orders live in a
    /// <see cref="FurnitureSupply"/>; ordering and crate handling are delegated to
    /// <see cref="ShopFloorActions"/>. Also keeps the registry of placed shelves, pens and counters.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The furniture inventory and the catalogue orders on their way or on the forecourt.</summary>
        public FurnitureSupply Furniture { get; } = new FurnitureSupply();

        /// <summary>
        /// Orders one <paramref name="catalogId"/>, paid now at catalogue cost, to arrive later today
        /// as a crate on the forecourt. False (with a notification) when unknown or unaffordable.
        /// </summary>
        public bool OrderFurniture(string catalogId) => _floor.OrderFurniture(catalogId);

        /// <summary>Drops a crate on the forecourt whenever a furniture order lands. Called from Awake.</summary>
        private void WireFurnitureSupply() => Furniture.OnArrived += _floor.OnFurnitureArrived;

        /// <summary>Lands the furniture orders due by now. Called every trading frame.</summary>
        private void TickFurniture() => Furniture.Tick(DayProgress);

        /// <summary>Furniture still on the road at close lands overnight.</summary>
        private void LandFurnitureOvernight() => Furniture.ArriveAll();

        // ── Furniture registry ────────────────────────────────────────────────

        /// <summary>Placed counters; customers only come once at least one exists.</summary>
        private readonly List<CounterInteractable> _counters = new();

        /// <summary>Adds a placed shelf, pen or counter to the registry the customers shop from.</summary>
        public void RegisterFurniture(GameObject go)
        {
            if (go == null) return;
            var shelf = go.GetComponent<ShelfUnit>();
            if (shelf != null && !_shelves.Contains(shelf)) _shelves.Add(shelf);

            var pen = go.GetComponent<PetPen>();
            if (pen != null && !_pens.Contains(pen)) _pens.Add(pen);

            var counter = go.GetComponent<CounterInteractable>();
            if (counter != null && !_counters.Contains(counter)) _counters.Add(counter);

            PushListsToSpawner();
        }

        /// <summary>Removes a shelf, pen or counter that is being taken off the floor from the registry.</summary>
        public void UnregisterFurniture(GameObject go)
        {
            if (go == null) return;
            var shelf = go.GetComponent<ShelfUnit>();
            if (shelf != null) _shelves.Remove(shelf);

            var pen = go.GetComponent<PetPen>();
            if (pen != null) _pens.Remove(pen);

            var counter = go.GetComponent<CounterInteractable>();
            if (counter != null) _counters.Remove(counter);

            PushListsToSpawner();
        }

        private void PushListsToSpawner()
        {
            _shelves.RemoveAll(s => s == null);
            _pens.RemoveAll(p => p == null);
            _counters.RemoveAll(c => c == null);
            if (Spawner == null) return;
            Spawner.Shelves    = _shelves;
            Spawner.PetPens    = _pens;
            Spawner.HasCounter = _counters.Count > 0;
            PlaceTillAtCounter();
        }

        /// <summary>Gap in metres between the counter's back edge and the staff standing behind it.</summary>
        private const float StaffBehindCounter = 0.5f;

        /// <summary>
        /// The till is wherever the first placed counter is: customers queue out from its front
        /// (+Z, the customer side of every furniture prefab) and staff stand behind it. Without
        /// this the queue sat at the shop's old fixed till spot, metres from any placed counter.
        /// </summary>
        private void PlaceTillAtCounter()
        {
            if (_counters.Count == 0 || Spawner == null || Spawner.RegisterPoint == null) return;
            Transform counter = _counters[0].transform;

            Vector3 forward = counter.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            float halfDepth = HalfDepthAlong(counter.gameObject, forward);
            Vector3 centre  = new Vector3(counter.position.x, 0f, counter.position.z);

            Spawner.RegisterPoint.position = centre + forward * halfDepth;
            if (Queue != null)
            {
                Queue.TillPoint      = Spawner.RegisterPoint;
                Queue.QueueDirection = forward;
            }
            if (StaffStation != null)
            {
                StaffStation.SetPositionAndRotation(centre - forward * (halfDepth + StaffBehindCounter),
                                                    Quaternion.LookRotation(forward, Vector3.up));
                _roster?.RepositionStaff();
            }
        }

        /// <summary>Half the object's rendered extent along <paramref name="axis"/> (a flat unit vector).</summary>
        private static float HalfDepthAlong(GameObject go, Vector3 axis)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0.5f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Abs(axis.x) * b.extents.x + Mathf.Abs(axis.z) * b.extents.z;
        }
    }
}
