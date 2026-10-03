using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// An authored car park fed from one <see cref="TrafficLane"/>. Cars turn in at
    /// <see cref="EntryDistance"/>, drive the entry path to the aisle and nose into a free bay; later
    /// they reverse out, drive the exit path to its stop line and pull back into the lane at
    /// <see cref="ExitDistance"/> once the lane is clear.
    ///
    /// Only one car manoeuvres at a time (entering or leaving), so cars inside the lot can never
    /// meet. Children wired in the inspector: <c>entryPath</c> and <c>exitPath</c> (ordered
    /// waypoints) and <c>bays</c> (one child per bay, its forward axis the direction the car's
    /// nose points when parked). The aisle runs from the last entry waypoint to the first exit one.
    /// </summary>
    public class ParkingLot : MonoBehaviour
    {
        private const float ApproachBeforeAisle = 3.5f;
        private const float NoseInLength        = 2f;
        private const float ReverseClear        = 3f;
        private const float TurnRadius          = 3f;
        private const float ReverseTurnRadius   = 1f;
        private const float MergeTurnLead       = 6f;
        private const float MergeTurnRadius     = 4.5f;
        private const float MergeClearBehind    = 40f;
        private const float MergeClearAhead     = 12f;

        private static readonly List<ParkingLot> AllLots = new List<ParkingLot>();

        [SerializeField] private TrafficLane lane;
        [Tooltip("Distance along the lane at which cars turn in.")]
        [SerializeField] private float entryDistance;
        [Tooltip("Distance along the lane at which leaving cars rejoin it (downstream of the entry).")]
        [SerializeField] private float exitDistance;
        [SerializeField] private Transform entryPath;
        [SerializeField] private Transform exitPath;
        [SerializeField] private Transform bays;
        [Tooltip("Distance from a bay's centre back to the aisle's centre line.")]
        [SerializeField] private float aisleOffset = 5f;
        [Tooltip("Chance that a passing car pulls in, when a bay is free.")]
        [Range(0f, 1f)] [SerializeField] private float visitChance = 0.35f;
        [Tooltip("Seconds a car stays parked: random between x and y.")]
        [SerializeField] private Vector2 stayRange = new Vector2(15f, 45f);

        private TrafficCar[] _occupants = new TrafficCar[0];
        private TrafficCar _manoeuvring;

        /// <summary>Every enabled car park.</summary>
        public static IReadOnlyList<ParkingLot> All => AllLots;

        /// <summary>The lane that feeds the car park and that leaving cars rejoin.</summary>
        public TrafficLane Lane => lane;

        /// <summary>Distance along <see cref="Lane"/> where cars turn in.</summary>
        public float EntryDistance => entryDistance;

        /// <summary>Distance along <see cref="Lane"/> where leaving cars rejoin it.</summary>
        public float ExitDistance => exitDistance;

        /// <summary>Chance a passing car pulls in.</summary>
        public float VisitChance => visitChance;

        /// <summary>Number of bays.</summary>
        public int BayCount => bays != null ? bays.childCount : 0;

        /// <summary>True while a car is driving in or out.</summary>
        public bool IsBusy => _manoeuvring != null;

        /// <summary>The car currently driving in or out, or null.</summary>
        public TrafficCar Manoeuvring => _manoeuvring;

        /// <summary>How many bays hold (or are reserved for) a car.</summary>
        public int OccupiedCount
        {
            get { EnsureBays(); int n = 0; foreach (var c in _occupants) if (c != null) n++; return n; }
        }

        private void OnEnable()  { if (!AllLots.Contains(this)) AllLots.Add(this); }
        private void OnDisable() => AllLots.Remove(this);

        private void EnsureBays()
        {
            if (_occupants.Length != BayCount) System.Array.Resize(ref _occupants, BayCount);
        }

        /// <summary>A random parked time for a car arriving now.</summary>
        public float RollStay() => Random.Range(stayRange.x, Mathf.Max(stayRange.x, stayRange.y));

        /// <summary>World pose of <paramref name="bay"/>.</summary>
        public Pose BayPose(int bay)
        {
            Transform t = bays.GetChild(bay);
            return new Pose(t.position, Quaternion.LookRotation(Flat(t.forward), Vector3.up));
        }

        /// <summary>Reserves a random free bay and the right to manoeuvre for <paramref name="car"/>.</summary>
        public bool TryReserve(TrafficCar car, out int bay)
        {
            bay = -1;
            EnsureBays();
            if (IsBusy || car == null) return false;
            var free = new List<int>();
            for (int i = 0; i < _occupants.Length; i++) if (_occupants[i] == null) free.Add(i);
            if (free.Count == 0) return false;
            bay = free[Random.Range(0, free.Count)];
            _occupants[bay] = car;
            _manoeuvring = car;
            return true;
        }

        /// <summary>Puts <paramref name="car"/> straight into <paramref name="bay"/> (scene start).</summary>
        public bool TryClaimBay(TrafficCar car, int bay)
        {
            EnsureBays();
            if (bay < 0 || bay >= _occupants.Length || _occupants[bay] != null) return false;
            _occupants[bay] = car;
            return true;
        }

        /// <summary>Takes the right to manoeuvre so a parked car can leave; false while another car moves.</summary>
        public bool TryStartLeaving(TrafficCar car)
        {
            if (IsBusy) return false;
            _manoeuvring = car;
            return true;
        }

        /// <summary>Gives up the right to manoeuvre.</summary>
        public void FinishManoeuvre(TrafficCar car)
        {
            if (_manoeuvring == car) _manoeuvring = null;
        }

        /// <summary>Frees <paramref name="bay"/>.</summary>
        public void Vacate(int bay)
        {
            EnsureBays();
            if (bay >= 0 && bay < _occupants.Length) _occupants[bay] = null;
        }

        /// <summary>Route from the turn-in point on the lane, through the entry path and aisle, nose-first into the bay.</summary>
        public List<Vector3> EntryRoute(Vector3 from, int bay)
        {
            Pose p = BayPose(bay);
            Vector3 forward = p.rotation * Vector3.forward;
            Vector3 aisle   = p.position - forward * aisleOffset;
            var points = new List<Vector3> { from };
            AddChildren(points, entryPath);
            points.Add(aisle - AisleDirection() * ApproachBeforeAisle);
            points.Add(p.position - forward * NoseInLength);
            points.Add(p.position);
            return TrafficMath.Fillet(points, TurnRadius);
        }

        /// <summary>Route a car reverses along out of its bay, ending in the aisle facing the way out.</summary>
        public List<Vector3> ReverseRoute(int bay)
        {
            Pose p = BayPose(bay);
            Vector3 aisle = p.position - (p.rotation * Vector3.forward) * aisleOffset;
            // Straight back until the car's centre is on the aisle line, only then swing round: turning
            // any earlier sweeps the nose across the neighbouring bay.
            var points = new List<Vector3> { p.position, aisle, aisle - AisleDirection() * ReverseClear };
            return TrafficMath.Fillet(points, ReverseTurnRadius);
        }

        /// <summary>Route from the aisle along the exit path to its last waypoint, the stop line before the lane.</summary>
        public List<Vector3> ExitRoute(Vector3 from)
        {
            var points = new List<Vector3> { from };
            AddChildren(points, exitPath);
            return TrafficMath.Fillet(points, TurnRadius);
        }

        /// <summary>Route from the stop line onto the lane at <see cref="ExitDistance"/>.</summary>
        public List<Vector3> MergeRoute(Vector3 from)
        {
            var points = new List<Vector3>
            {
                from,
                TrafficMath.PositionAt(lane.Points, exitDistance - MergeTurnLead),
                TrafficMath.PositionAt(lane.Points, exitDistance),
            };
            return TrafficMath.Fillet(points, MergeTurnRadius);
        }

        /// <summary>True when the lane is clear enough around the exit for a car to pull out.</summary>
        public bool MergeClear(TrafficCar self)
        {
            var distances = new List<float>();
            foreach (TrafficCar car in TrafficDirector.Cars)
                if (car != null && car != self && car.Lane == lane) distances.Add(car.Distance);
            return TrafficMath.MergeWindowClear(distances, exitDistance, MergeClearBehind, MergeClearAhead);
        }

        /// <summary>Direction of travel along the aisle: last entry waypoint towards the first exit one.</summary>
        private Vector3 AisleDirection()
        {
            if (entryPath == null || exitPath == null || entryPath.childCount == 0 || exitPath.childCount == 0)
                return transform.forward;
            Vector3 d = Flat(exitPath.GetChild(0).position - entryPath.GetChild(entryPath.childCount - 1).position);
            return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
        }

        private static void AddChildren(List<Vector3> points, Transform parent)
        {
            if (parent == null) return;
            foreach (Transform child in parent) points.Add(child.position);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            DrawChain(entryPath);
            DrawChain(exitPath);
            if (bays == null) return;
            Gizmos.color = Color.white;
            foreach (Transform b in bays)
            {
                Gizmos.DrawWireCube(b.position + Vector3.up * 0.1f, new Vector3(0.3f, 0.2f, 0.3f));
                Gizmos.DrawLine(b.position, b.position + b.forward * 2f);
            }
        }

        private static void DrawChain(Transform parent)
        {
            if (parent == null) return;
            for (int i = 1; i < parent.childCount; i++)
                Gizmos.DrawLine(parent.GetChild(i - 1).position, parent.GetChild(i).position);
        }
    }
}
