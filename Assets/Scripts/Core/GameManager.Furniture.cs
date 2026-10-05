using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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

        /// <summary>True while at least one counter is placed: cashiers can be hired and can serve.</summary>
        public bool HasCounter => _counters.Exists(c => c != null);

        /// <summary>
        /// True while <see cref="StaffStation"/> stands behind a placed counter. False before the first
        /// counter and again once the last one is removed: the station is then stale, so staff keep
        /// their jobs and wages but cashiers do not serve until a counter is placed again.
        /// </summary>
        public bool StaffStationValid { get; private set; }

        /// <summary>
        /// True when <paramref name="candidate"/> could be hired now (sign-on fee aside); otherwise
        /// false with a short reason — a cashier needs a counter (<see cref="HasCounter"/>).
        /// </summary>
        public bool CanHire(StaffCandidate candidate, out string reason) => _roster.CanHire(candidate, out reason);

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

        /// <summary>How far from the wanted spot behind the counter the NavMesh is searched for standing room.</summary>
        private const float StationSampleRadius = 1f;

        /// <summary>Corners of a box collider, walked to find how far it reaches along an axis.</summary>
        private const int BoxCorners = 8;
        /// <summary>Half of a box collider's size: centre to face.</summary>
        private const float HalfExtent = 0.5f;
        /// <summary>Reach assumed for a counter with neither colliders nor renderers.</summary>
        private const float DefaultHalfDepth = 0.5f;

        /// <summary>
        /// The till is wherever the first placed counter is: customers queue out from its front
        /// (+Z, the customer side of every furniture prefab) and staff stand behind it. Without
        /// this the queue sat at the shop's old fixed till spot, metres from any placed counter.
        /// The front and back faces come from the counter's colliders — what people actually bump
        /// into — so the first place in line is always clear of the counter on its customer side.
        /// </summary>
        private void PlaceTillAtCounter()
        {
            // No counter left: the old station is stale. Staff stay put; cashiers stop serving.
            StaffStationValid = _counters.Count > 0;
            if (!StaffStationValid || Spawner == null || Spawner.RegisterPoint == null) return;
            Transform counter = _counters[0].transform;

            Vector3 forward = counter.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            float front    = ReachAlong(counter.gameObject, forward);
            float back     = ReachAlong(counter.gameObject, -forward);
            Vector3 centre = new Vector3(counter.position.x, 0f, counter.position.z);

            Spawner.RegisterPoint.position = centre + forward * front;
            if (Queue != null)
            {
                Queue.TillPoint      = Spawner.RegisterPoint;
                Queue.QueueDirection = forward;
            }
            if (StaffStation != null)
            {
                StaffStation.SetPositionAndRotation(StationBehind(centre, forward, back),
                                                    Quaternion.LookRotation(forward, Vector3.up));
                _roster?.RepositionStaff();
            }
        }

        /// <summary>
        /// Where staff stand behind a counter centred on <paramref name="centre"/> whose customer side
        /// faces <paramref name="forward"/> and whose back face is <paramref name="back"/> metres behind
        /// its centre: <see cref="StaffBehindCounter"/> past the back face, moved to the nearest walkable
        /// NavMesh point when that spot is not walkable (a wall or other furniture behind the counter) —
        /// but only to a point that is still behind the back face, never round to the customers' side.
        /// </summary>
        internal static Vector3 StationBehind(Vector3 centre, Vector3 forward, float back)
        {
            Vector3 wanted = centre - forward * (back + StaffBehindCounter);
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, StationSampleRadius, NavMesh.AllAreas))
                return wanted;
            Vector3 found = new(hit.position.x, wanted.y, hit.position.z);
            return Vector3.Dot(found - centre, -forward) > back ? found : wanted;
        }

        /// <summary>
        /// How far the object's box colliders reach from its pivot along <paramref name="axis"/>
        /// (a flat unit vector). Read from the collider shapes and transforms rather than
        /// <c>Collider.bounds</c>, which is stale until physics syncs a freshly spawned object.
        /// Falls back to the rendered extent when the object has no box collider.
        /// </summary>
        internal static float ReachAlong(GameObject go, Vector3 axis)
        {
            var boxes = go.GetComponentsInChildren<BoxCollider>();
            if (boxes.Length == 0) return HalfDepthAlong(go, axis);

            Vector3 pivot = go.transform.position;
            float reach = 0f;
            foreach (var box in boxes)
                for (int i = 0; i < BoxCorners; i++)
                {
                    Vector3 local = box.center + Vector3.Scale(box.size * HalfExtent, CornerSign(i));
                    reach = Mathf.Max(reach, Vector3.Dot(box.transform.TransformPoint(local) - pivot, axis));
                }
            return reach;
        }

        /// <summary>The ±1 signs of box corner <paramref name="i"/> (0-7) on each axis.</summary>
        private static Vector3 CornerSign(int i) =>
            new((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);

        /// <summary>Half the object's rendered extent along <paramref name="axis"/> (a flat unit vector).</summary>
        private static float HalfDepthAlong(GameObject go, Vector3 axis)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return DefaultHalfDepth;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Abs(axis.x) * b.extents.x + Mathf.Abs(axis.z) * b.extents.z;
        }
    }
}
