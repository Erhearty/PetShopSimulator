using UnityEngine;

namespace PetShop.Commerce
{
    /// <summary>
    /// Pure per-role decisions for assistants: who stays at the till, how far and how long an
    /// idle worker roams the yard. No scene access, so it is unit-testable.
    /// </summary>
    public static class StaffRoleBehavior
    {
        /// <summary>Only the cashier is tied to the till.</summary>
        public static bool StaysAtTill(StaffRole role) => role == StaffRole.Cashier;

        /// <summary>Whether a worker with nothing to do wanders the yard.</summary>
        public static bool RoamsWhenIdle(StaffRole role) => !StaysAtTill(role);

        /// <summary>Furthest an idle worker strays from the till, in metres.</summary>
        public static float RoamRadius(StaffRole role) => role switch
        {
            StaffRole.Restocker => 9f,
            StaffRole.Feeder    => 6f,
            _                   => 0f,
        };

        /// <summary>Shortest and longest pause at a roam point, in seconds.</summary>
        public static Vector2 PauseRange(StaffRole role) => role switch
        {
            StaffRole.Restocker => new Vector2(1f, 3f),
            StaffRole.Feeder    => new Vector2(3f, 7f),
            _                   => Vector2.zero,
        };

        /// <summary>Pause for a 0..1 roll, within <see cref="PauseRange"/>.</summary>
        public static float PauseSeconds(StaffRole role, float roll)
        {
            var range = PauseRange(role);
            return Mathf.Lerp(range.x, range.y, Mathf.Clamp01(roll));
        }

        /// <summary>
        /// A point in the disc of <see cref="RoamRadius"/> around <paramref name="center"/> on the
        /// XZ plane (uniform by area) from two 0..1 rolls. A role that does not roam returns the centre.
        /// </summary>
        public static Vector3 IdleTarget(StaffRole role, Vector3 center, float angleRoll, float distanceRoll)
        {
            float radius = RoamRadius(role);
            if (!RoamsWhenIdle(role) || radius <= 0f) return center;
            float angle = Mathf.Clamp01(angleRoll) * Mathf.PI * 2f;
            float dist  = Mathf.Sqrt(Mathf.Clamp01(distanceRoll)) * radius;
            return center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        }
    }
}
