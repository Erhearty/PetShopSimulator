using PetShop.Core;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// A car that drives a <see cref="TrafficLane"/>. Ticked by <see cref="TrafficDirector"/> only, never by itself.
    /// </summary>
    public class TrafficCar : MonoBehaviour
    {
        private const float GroundRayHeight = 3f;
        private const float GroundRayLength = 10f;
        private const int RayBufferSize = 8;
        private const float LaneMatchRadius = 4f;
        private const float DefaultHalfLength = 2f;
        private const float HalfFactor = 0.5f;

        [SerializeField] private TrafficLane lane;
        [SerializeField] private float startDistance;
        [SerializeField] private float cruiseSpeed = 6f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float brakeDeceleration = 8f;
        [SerializeField] private float minGap = 1.5f;

        private readonly RaycastHit[] _hits = new RaycastHit[RayBufferSize];
        private float _distance;
        private float _speed;
        private float _halfLength = DefaultHalfLength;

        /// <summary>The lane this car drives.</summary>
        public TrafficLane Lane => lane;

        /// <summary>Distance travelled along the lane in metres.</summary>
        public float Distance => _distance;

        /// <summary>Half the car's length along its travel axis.</summary>
        public float HalfLength => _halfLength;

        /// <summary>Current speed in metres per second.</summary>
        public float Speed => _speed;

        private void Awake()
        {
            _distance = startDistance;
            EnsurePhysics();
        }

        /// <summary>Scales the authored cruise speed (director jitter) and snaps the car onto its lane.</summary>
        public void Setup(float cruiseMultiplier)
        {
            cruiseSpeed *= cruiseMultiplier;
            if (lane != null) Place();
        }

        /// <summary>Advances the car by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (lane == null || lane.Points.Count < 2) return;
            float cruise = Mathf.Min(cruiseSpeed, lane.SpeedLimit);
            float previous = _speed;
            _speed = TrafficMath.TargetSpeed(_speed, cruise, GapAhead(), minGap, acceleration, brakeDeceleration, dt);
            // Brake from the speed held before this tick so the real stopping distance matches the stop-line rule.
            if (StopLineBrake()) _speed = Mathf.Max(0f, Mathf.Min(_speed, previous - brakeDeceleration * dt));
            Advance(dt);
            Place();
        }

        private void Advance(float dt)
        {
            _distance += _speed * dt;
            float length = lane.Length;
            if (_distance < length) return;
            if (TrafficMath.CanWrap(GapAtStart(), minGap + _halfLength)) _distance -= length;
            else { _distance = length; _speed = 0f; }
        }

        private void Place()
        {
            Vector3 pos = TrafficMath.PositionAt(lane.Points, _distance);
            Vector3 tangent = TrafficMath.TangentAt(lane.Points, _distance);
            pos.y = SampleGround(pos);
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
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
                best = _hits[i].distance;
                y = _hits[i].point.y;
            }
            return y;
        }

        private float GapAhead()
        {
            float gap = float.PositiveInfinity;
            foreach (TrafficCar other in TrafficDirector.Cars)
            {
                if (other == this || other.lane != lane || other._distance <= _distance) continue;
                gap = Mathf.Min(gap, other._distance - _distance - _halfLength - other._halfLength);
            }
            return gap;
        }

        private float GapAtStart()
        {
            float gap = float.PositiveInfinity;
            foreach (TrafficCar other in TrafficDirector.Cars)
            {
                if (other == this || other.lane != lane) continue;
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
                float side = Vector3.Cross(fwd, toLine).y;
                if (Mathf.Abs(side) > LaneMatchRadius) continue;
                if (TrafficMath.ShouldBrakeForStopLine(along, true, _speed, brakeDeceleration)) return true;
            }
            return false;
        }

        private void EnsurePhysics()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            if (GetComponentInChildren<Collider>() != null) return;
            Renderer[] renders = GetComponentsInChildren<Renderer>();
            if (renders.Length == 0) return;
            Bounds b = renders[0].bounds;
            for (int i = 1; i < renders.Length; i++) b.Encapsulate(renders[i].bounds);
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Max(s.x, Mathf.Epsilon), b.size.y / Mathf.Max(s.y, Mathf.Epsilon),
                                   b.size.z / Mathf.Max(s.z, Mathf.Epsilon));
            _halfLength = b.size.z * HalfFactor;
        }
    }
}
