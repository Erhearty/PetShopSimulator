using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Traffic
{
    /// <summary>Pure, scene-free maths for street traffic: polylines, gap keeping, stop lines.</summary>
    public static class TrafficMath
    {
        /// <summary>Gap (metres) above the minimum gap over which a car eases from stopped up to cruise speed.</summary>
        public const float SlowZone = 6f;

        /// <summary>Extra distance (metres) before the stop line at which a braking car comes to rest.</summary>
        public const float StopMargin = 1f;

        private const float MinSegmentLength = 1e-5f;
        private const float HalfFactor = 2f;

        /// <summary>Total length of the polyline through <paramref name="points"/>.</summary>
        public static float Length(IReadOnlyList<Vector3> points)
        {
            float total = 0f;
            if (points == null) return total;
            for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
            return total;
        }

        /// <summary>World position at <paramref name="distance"/> along the polyline, clamped to both ends.</summary>
        public static Vector3 PositionAt(IReadOnlyList<Vector3> points, float distance)
        {
            if (points == null || points.Count == 0) return Vector3.zero;
            if (points.Count == 1 || distance <= 0f) return points[0];
            float remaining = distance;
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                if (remaining <= seg && seg > MinSegmentLength)
                    return Vector3.Lerp(points[i - 1], points[i], remaining / seg);
                remaining -= seg;
            }
            return points[points.Count - 1];
        }

        /// <summary>Unit direction of travel at <paramref name="distance"/>; the first/last segment outside the ends.</summary>
        public static Vector3 TangentAt(IReadOnlyList<Vector3> points, float distance)
        {
            if (points == null || points.Count < 2) return Vector3.forward;
            float remaining = distance;
            Vector3 last = Vector3.forward;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 d = points[i] - points[i - 1];
                float seg = d.magnitude;
                if (seg <= MinSegmentLength) continue;
                last = d / seg;
                if (remaining <= seg) return last;
                remaining -= seg;
            }
            return last;
        }

        /// <summary>
        /// Next speed for a car: eases toward cruise speed when the road ahead is free, toward zero as the gap
        /// closes on <paramref name="minGap"/>. Never exceeds cruise, never negative.
        /// </summary>
        public static float TargetSpeed(float speed, float cruise, float gap, float minGap,
                                        float accel, float brake, float dt)
        {
            float allowed = cruise * Mathf.Clamp01((gap - minGap) / SlowZone);
            if (speed < allowed) return Mathf.Min(allowed, speed + accel * dt);
            return Mathf.Max(allowed, speed - brake * dt);
        }

        /// <summary>
        /// True when a car must brake for an occupied crosswalk: it is still short of the stop line
        /// (<paramref name="distanceToLine"/> &gt;= 0) and inside its stopping distance plus <see cref="StopMargin"/>.
        /// </summary>
        public static bool ShouldBrakeForStopLine(float distanceToLine, bool occupied, float speed, float brake)
        {
            if (!occupied || distanceToLine < 0f) return false;
            float stopping = speed * speed / (HalfFactor * Mathf.Max(brake, MinSegmentLength));
            return distanceToLine <= stopping + StopMargin;
        }

        /// <summary>True when a car may wrap to the lane start: the nearest car there is at least <paramref name="minGap"/> away.</summary>
        public static bool CanWrap(float gapAtStart, float minGap) => gapAtStart >= minGap;
    }
}
