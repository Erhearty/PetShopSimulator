using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>
    /// An authored one-way traffic lane. Its ordered child Transforms are waypoints forming a polyline.
    /// A lane with a <see cref="Next"/> lane hands its cars on to it at the end (a U-turn joining the
    /// two directions of the street, say); one without wraps its cars back to its own start.
    /// </summary>
    public class TrafficLane : MonoBehaviour
    {
        private const float ArrowSize = 0.6f;
        private const float ArrowAngle = 150f;
        private const float WaypointRadius = 0.2f;
        private const float ArrowSpacing = 6f;

        [Tooltip("Speed limit in metres per second; cars never cruise faster than this.")]
        [SerializeField] private float speedLimit = 8f;

        [Tooltip("Lane that cars continue onto at the end of this one. Empty: cars wrap to this lane's start.")]
        [SerializeField] private TrafficLane next;

        private readonly List<Vector3> _points = new List<Vector3>();
        private float _length;

        /// <summary>Speed limit in metres per second.</summary>
        public float SpeedLimit => speedLimit;

        /// <summary>The lane cars continue onto at the end, or null to wrap.</summary>
        public TrafficLane Next => next;

        /// <summary>World-space waypoint positions in order (rebuilt on <see cref="Rebuild"/>).</summary>
        public IReadOnlyList<Vector3> Points { get { if (_points.Count == 0) Rebuild(); return _points; } }

        /// <summary>Polyline length in metres.</summary>
        public float Length { get { if (_points.Count == 0) Rebuild(); return _length; } }

        private void Awake() => Rebuild();

        /// <summary>Re-reads the child transforms into <see cref="Points"/> and <see cref="Length"/>.</summary>
        public void Rebuild()
        {
            _points.Clear();
            foreach (Transform child in transform) _points.Add(child.position);
            _length = TrafficMath.Length(_points);
        }

        private void OnDrawGizmos()
        {
            Rebuild();
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _points.Count; i++)
            {
                Gizmos.DrawWireSphere(_points[i], WaypointRadius);
                if (i > 0) DrawSegment(_points[i - 1], _points[i]);
            }
        }

        private static void DrawSegment(Vector3 a, Vector3 b)
        {
            Gizmos.DrawLine(a, b);
            Vector3 d = b - a;
            if (d.sqrMagnitude < Mathf.Epsilon) return;
            Vector3 dir = d.normalized;
            int arrows = Mathf.Max(1, Mathf.FloorToInt(d.magnitude / ArrowSpacing));
            for (int k = 1; k <= arrows; k++)
                DrawArrow(a + d * (k / (arrows + 1f)), dir);
        }

        private static void DrawArrow(Vector3 tip, Vector3 dir)
        {
            Vector3 left = Quaternion.AngleAxis(ArrowAngle, Vector3.up) * dir;
            Vector3 right = Quaternion.AngleAxis(-ArrowAngle, Vector3.up) * dir;
            Gizmos.DrawLine(tip, tip + left * ArrowSize);
            Gizmos.DrawLine(tip, tip + right * ArrowSize);
        }
    }
}
