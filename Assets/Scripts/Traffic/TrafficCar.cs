using System.Collections.Generic;
using PetShop.Core;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// A car that drives a <see cref="TrafficLane"/> and can visit a <see cref="ParkingLot"/> on that lane.
    /// Ticked by <see cref="TrafficDirector"/> only, never by itself.
    ///
    /// On a lane it keeps its distance from the car ahead — including across the join into the lane's
    /// <see cref="TrafficLane.Next"/> — and stops for occupied crossings. Off the lane (parking) it follows
    /// a route handed out by the car park, which lets only one car manoeuvre at a time.
    /// </summary>
    public class TrafficCar : MonoBehaviour
    {
        private const float GroundRayHeight = 3f;
        private const float GroundRayLength = 10f;
        private const int RayBufferSize = 8;
        private const float DefaultHalfLength = 2f;
        private const float HalfFactor = 0.5f;

        private const float ParkSpeed        = 3f;
        private const float ReverseSpeed     = 1.3f;
        private const float TurnInSpeed      = 3.5f;
        private const float ComfortBrake     = 2.5f;
        private const float RouteBrake       = 2f;
        private const float RouteCreep       = 0.3f;
        private const float RouteDoneSlack   = 0.05f;
        private const float DecisionWindow   = 35f;
        private const float StillInLaneSpan  = 7f;
        private const float HeldSpeed        = 0.05f;
        private const float HeldSlack        = 0.6f;
        private const float HoldCreep        = 0.3f;

        [SerializeField] private TrafficLane lane;
        [SerializeField] private float startDistance;
        [SerializeField] private float cruiseSpeed = 6f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float brakeDeceleration = 8f;
        [SerializeField] private float minGap = 1.5f;

        [Header("Parking (optional)")]
        [Tooltip("Start parked in this car park instead of on the lane.")]
        [SerializeField] private ParkingLot startParkedIn;
        [SerializeField] private int startBay = -1;

        private enum Mode { Driving, TurningIn, Parked, Reversing, Leaving, WaitingToMerge, Merging }

        private readonly RaycastHit[] _hits = new RaycastHit[RayBufferSize];
        private float _distance;
        private float _speed;
        private float _halfLength = DefaultHalfLength;

        private Mode _mode = Mode.Driving;
        private List<Vector3> _route;
        private float _routeLength;
        private float _routePos;
        private ParkingLot _lot;
        private int _bay = -1;
        private float _parkedUntil;
        private bool _decidedThisLap;
        private float _holdAt = -1f;
        private bool _despawnAtEnd;

        /// <summary>The lane this car is driving, or null while it is parking, parked or pulling out.</summary>
        public TrafficLane Lane => _mode == Mode.Driving ? lane : null;

        /// <summary>Distance travelled along the lane in metres (meaningful while <see cref="Lane"/> is set).</summary>
        public float Distance => _distance;

        /// <summary>Half the car's length along its travel axis.</summary>
        public float HalfLength => _halfLength;

        /// <summary>Current speed in metres per second.</summary>
        public float Speed => _speed;

        /// <summary>True while the car sits in a bay.</summary>
        public bool IsParked => _mode == Mode.Parked;

        /// <summary>True while the car is driving in or out of a car park.</summary>
        public bool IsManoeuvring => _mode != Mode.Driving && _mode != Mode.Parked;

        /// <summary>True once a car set to despawn has reached the end of its lane; the director removes it.</summary>
        public bool Finished { get; private set; }

        /// <summary>True while the car is stopped at the point set by <see cref="HoldAt"/>.</summary>
        public bool IsHeld => _mode == Mode.Driving && _holdAt >= 0f && _speed < HeldSpeed && _holdAt - _distance < HeldSlack;

        /// <summary>
        /// Puts a car built at runtime (a delivery truck) on <paramref name="onLane"/> at
        /// <paramref name="atDistance"/>. Authored cars are set up in the scene instead.
        /// </summary>
        public void Configure(TrafficLane onLane, float atDistance, float cruise)
        {
            lane = onLane;
            _distance = atDistance;
            cruiseSpeed = cruise;
            _mode = Mode.Driving;
            _decidedThisLap = true;   // trucks do not visit the car park
            if (lane != null) Place();
        }

        /// <summary>Makes the car pull up and wait at <paramref name="distance"/> along its current lane.</summary>
        public void HoldAt(float distance) => _holdAt = distance;

        /// <summary>Lets a held car drive on.</summary>
        public void Release() => _holdAt = -1f;

        /// <summary>When set, the car leaves the street at the end of its lane instead of carrying on.</summary>
        public void DespawnAtLaneEnd() => _despawnAtEnd = true;

        private void Awake()
        {
            _distance = startDistance;
            EnsurePhysics();
        }

        /// <summary>Scales the authored cruise speed (director jitter) and puts the car where it starts.</summary>
        public void Setup(float cruiseMultiplier)
        {
            cruiseSpeed *= cruiseMultiplier;
            if (startParkedIn != null && startParkedIn.TryClaimBay(this, startBay))
            {
                _lot = startParkedIn;
                _bay = startBay;
                _mode = Mode.Parked;
                _parkedUntil = Time.time + _lot.RollStay();
                Pose pose = _lot.BayPose(_bay);
                Vector3 pos = pose.position;
                pos.y = SampleGround(pos);
                transform.SetPositionAndRotation(pos, pose.rotation);
                return;
            }
            if (lane != null) Place();
        }

        /// <summary>Advances the car by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            switch (_mode)
            {
                case Mode.Driving:   TickDriving(dt); break;
                case Mode.TurningIn:
                    if (FollowRoute(dt, ParkSpeed, false)) Park();
                    break;
                case Mode.Parked:
                    if (Time.time >= _parkedUntil && _lot.TryStartLeaving(this))
                        StartRoute(Mode.Reversing, _lot.ReverseRoute(_bay));
                    break;
                case Mode.Reversing:
                    if (FollowRoute(dt, ReverseSpeed, true))
                    {
                        _lot.Vacate(_bay);
                        _bay = -1;
                        StartRoute(Mode.Leaving, _lot.ExitRoute(transform.position));
                    }
                    break;
                case Mode.Leaving:
                    if (FollowRoute(dt, ParkSpeed, false)) { _mode = Mode.WaitingToMerge; _speed = 0f; }
                    break;
                case Mode.WaitingToMerge:
                    if (_lot.MergeClear(this)) StartRoute(Mode.Merging, _lot.MergeRoute(transform.position));
                    break;
                case Mode.Merging:
                    if (FollowRoute(dt, TurnInSpeed, false)) Rejoin();
                    break;
            }
        }

        // ── On the lane ─────────────────────────────────────────────────────────

        private void TickDriving(float dt)
        {
            if (lane == null || lane.Points.Count < 2) return;
            float cruise = Mathf.Min(cruiseSpeed, lane.SpeedLimit);
            if (_bay >= 0) cruise = Mathf.Min(cruise, ApproachSpeed(_lot.EntryDistance - _distance));
            if (_holdAt >= 0f) cruise = Mathf.Min(cruise, StopSpeed(_holdAt - _distance));

            float previous = _speed;
            _speed = TrafficMath.TargetSpeed(_speed, cruise, GapAhead(), minGap, acceleration, brakeDeceleration, dt);
            // Brake from the speed held before this tick so the real stopping distance matches the stop-line rule.
            if (StopLineBrake()) _speed = Mathf.Max(0f, Mathf.Min(_speed, previous - brakeDeceleration * dt));
            Advance(dt);
            if (Finished) return;
            if (_holdAt >= 0f && _distance > _holdAt) { _distance = _holdAt; _speed = 0f; }
            ConsiderParking();

            if (_bay >= 0 && _distance >= _lot.EntryDistance)
            {
                StartRoute(Mode.TurningIn, _lot.EntryRoute(transform.position, _bay));
                return;
            }
            Place();
        }

        /// <summary>Highest speed from which the car can still stop in <paramref name="toGo"/> metres.</summary>
        private static float StopSpeed(float toGo) =>
            toGo <= HoldCreep ? 0f : Mathf.Sqrt(2f * ComfortBrake * toGo);

        /// <summary>Highest speed from which the car can still slow to turn-in speed in <paramref name="toGo"/> metres.</summary>
        private static float ApproachSpeed(float toGo) =>
            Mathf.Sqrt(TurnInSpeed * TurnInSpeed + 2f * ComfortBrake * Mathf.Max(0f, toGo));

        private void ConsiderParking()
        {
            if (_bay >= 0 || _decidedThisLap) return;
            foreach (ParkingLot lot in ParkingLot.All)
            {
                if (lot.Lane != lane) continue;
                float toGo = lot.EntryDistance - _distance;
                if (toGo > DecisionWindow || toGo < 0f) continue;
                _decidedThisLap = true;
                if (Random.value < lot.VisitChance && lot.TryReserve(this, out int bay))
                {
                    _lot = lot;
                    _bay = bay;
                }
                return;
            }
        }

        private void Advance(float dt)
        {
            _distance += _speed * dt;
            float length = lane.Length;
            if (_distance < length) return;
            if (_despawnAtEnd) { Finished = true; return; }

            TrafficLane next = lane.Next;
            if (next != null)
            {
                float overshoot = _distance - length;
                if (GapAtStart(next) - overshoot >= minGap + _halfLength)
                {
                    lane = next;
                    _distance = overshoot;
                    _decidedThisLap = false;
                }
                else { _distance = length; _speed = 0f; }
                return;
            }
            if (TrafficMath.CanWrap(GapAtStart(lane), minGap + _halfLength))
            {
                _distance -= length;
                _decidedThisLap = false;
            }
            else { _distance = length; _speed = 0f; }
        }

        private void Place()
        {
            Vector3 pos = TrafficMath.PositionAt(lane.Points, _distance);
            Vector3 tangent = TrafficMath.TangentAt(lane.Points, _distance);
            SetPose(pos, tangent, false);
        }

        /// <summary>
        /// Bumper-to-bumper distance to whatever is ahead: a car further along this lane, a car on the next
        /// lane (or the one after), or a car pulling into or out of a car park on this lane.
        /// </summary>
        private float GapAhead()
        {
            float gap = float.PositiveInfinity;
            TrafficLane next = lane.Next;
            TrafficLane afterNext = next != null ? next.Next : null;
            foreach (TrafficCar other in TrafficDirector.Cars)
            {
                if (other == null || other == this) continue;
                float ahead = other.DistanceAheadOn(lane, next, afterNext, this);
                if (float.IsNaN(ahead) || ahead <= _distance) continue;
                gap = Mathf.Min(gap, ahead - _distance - _halfLength - other._halfLength);
            }
            return gap;
        }

        /// <summary>
        /// Where this car sits, as a distance along <paramref name="onLane"/>'s frame of reference, as seen by
        /// <paramref name="viewer"/>; NaN when it is not in the way.
        /// </summary>
        private float DistanceAheadOn(TrafficLane onLane, TrafficLane next, TrafficLane afterNext, TrafficCar viewer)
        {
            switch (_mode)
            {
                case Mode.Driving:
                    if (lane == onLane) return _distance;
                    if (next != null && next != onLane && lane == next) return onLane.Length + _distance;
                    if (afterNext != null && afterNext != onLane && afterNext != next && lane == afterNext)
                        return onLane.Length + next.Length + _distance;
                    return float.NaN;
                // Still physically in the lane for the first few metres of the turn-in.
                case Mode.TurningIn:
                    return _lot.Lane == onLane && _routePos < StillInLaneSpan ? _lot.EntryDistance : float.NaN;
                // Pulling out: hold the traffic behind back from the exit until the car is on the lane.
                case Mode.Merging:
                    return _lot.Lane == onLane ? _lot.ExitDistance - StillInLaneSpan : float.NaN;
                default:
                    return float.NaN;
            }
        }

        private float GapAtStart(TrafficLane target) => ClearanceAtStart(target, this);

        /// <summary>Free road at the start of <paramref name="target"/>: distance to the nearest car's rear bumper.</summary>
        public static float ClearanceAtStart(TrafficLane target, TrafficCar except = null)
        {
            float gap = float.PositiveInfinity;
            foreach (TrafficCar other in TrafficDirector.Cars)
            {
                if (other == null || other == except || other.Lane != target) continue;
                gap = Mathf.Min(gap, other._distance - other._halfLength);
            }
            return gap;
        }

        private bool StopLineBrake()
        {
            Vector3 fwd = transform.forward;
            foreach (CrosswalkZone zone in CrosswalkZone.All)
            {
                if (!zone.IsOccupied) continue;
                Vector3 toLine = zone.StopLinePosition(fwd) - transform.position;
                float along = Vector3.Dot(toLine, fwd) - _halfLength;
                // Only cars whose path actually runs through the crossing: lateral offset within the zone.
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                Bounds b = zone.WorldBounds;
                float reach = Mathf.Abs(side.x) * b.extents.x + Mathf.Abs(side.z) * b.extents.z;
                float lateral = Vector3.Dot(b.center - transform.position, side);
                if (Mathf.Abs(lateral) > reach) continue;
                if (TrafficMath.ShouldBrakeForStopLine(along, true, _speed, brakeDeceleration)) return true;
            }
            return false;
        }

        // ── In and out of a car park ────────────────────────────────────────────

        private void StartRoute(Mode mode, List<Vector3> route)
        {
            _mode = mode;
            _route = route;
            _routeLength = TrafficMath.Length(route);
            _routePos = 0f;
        }

        /// <summary>Moves along the current route; true once at its end.</summary>
        private bool FollowRoute(float dt, float maxSpeed, bool reversing)
        {
            float remaining = _routeLength - _routePos;
            float target = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * RouteBrake * Mathf.Max(0f, remaining)) + RouteCreep);
            _speed = Mathf.MoveTowards(_speed, target, (target > _speed ? acceleration : brakeDeceleration) * dt);
            _routePos = Mathf.Min(_routeLength, _routePos + _speed * dt);
            SetPose(TrafficMath.PositionAt(_route, _routePos), TrafficMath.TangentAt(_route, _routePos), reversing);
            return _routePos >= _routeLength - RouteDoneSlack;
        }

        private void Park()
        {
            _mode = Mode.Parked;
            _speed = 0f;
            _parkedUntil = Time.time + _lot.RollStay();
            _lot.FinishManoeuvre(this);
        }

        private void Rejoin()
        {
            lane = _lot.Lane;
            _distance = _lot.ExitDistance;
            _mode = Mode.Driving;
            _decidedThisLap = true;   // do not pull straight back in
            _lot.FinishManoeuvre(this);
            _lot = null;
        }

        private void SetPose(Vector3 pos, Vector3 tangent, bool reversing)
        {
            pos.y = SampleGround(pos);
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z) * (reversing ? -1f : 1f);
            Quaternion rot = flat.sqrMagnitude > Mathf.Epsilon ? Quaternion.LookRotation(flat) : transform.rotation;
            transform.SetPositionAndRotation(pos, rot);
        }

        private float SampleGround(Vector3 pos)
        {
            Vector3 origin = pos + Vector3.up * GroundRayHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, GroundRayLength,
                1 << GameLayers.Scenery, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = pos.y;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform) || _hits[i].distance >= best) continue;
                if (_hits[i].collider.GetComponentInParent<TrafficCar>() != null) continue;
                best = _hits[i].distance;
                y = _hits[i].point.y;
            }
            return y;
        }

        private void EnsurePhysics()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Renderer[] renders = GetComponentsInChildren<Renderer>();
            if (renders.Length == 0) return;
            Bounds b = renders[0].bounds;
            for (int i = 1; i < renders.Length; i++) b.Encapsulate(renders[i].bounds);
            // Measured along the car's own forward axis, whatever way it was authored facing.
            Vector3 f = transform.forward;
            _halfLength = (Mathf.Abs(f.x) * b.size.x + Mathf.Abs(f.z) * b.size.z) * HalfFactor;
            if (GetComponentInChildren<Collider>() != null) return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.lossyScale;
            Vector3 local = Quaternion.Inverse(transform.rotation) * b.size;
            box.size = new Vector3(Mathf.Abs(local.x) / Mathf.Max(s.x, Mathf.Epsilon), b.size.y / Mathf.Max(s.y, Mathf.Epsilon),
                                   Mathf.Abs(local.z) / Mathf.Max(s.z, Mathf.Epsilon));
        }
    }
}
