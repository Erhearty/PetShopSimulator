using UnityEngine;

namespace PetShop.Customer
{
    /// <summary>
    /// How long a single walk may take before <see cref="CustomerAI"/> gives up on it. The limit grows
    /// with the planned path, so a long but healthy walk is never cut off: since the shopfront moved to
    /// the yard's front edge, shoppers spawned at the far end of the pavement walk ~52 m to the forecourt,
    /// which takes ~22 s at walking speed - over the old flat 20 s limit (Logs/build/soak.log, 2026-10-05).
    /// </summary>
    public partial class CustomerAI
    {
        /// <summary>Shortest walk limit in game seconds; the old flat limit, kept for short walks.</summary>
        public const float MinWalkTimeoutSeconds = 20f;

        /// <summary>Longest walk limit in game seconds, so a genuinely stuck shopper is still freed.</summary>
        public const float MaxWalkTimeoutSeconds = 90f;

        /// <summary>Seconds without real progress on a valid path before a shopper is nudged.</summary>
        public const float StallSeconds = 2f;

        /// <summary>Metres a shopper must move within <see cref="StallSeconds"/> to not count as stalled.</summary>
        public const float StallMinMove = 0.1f;

        /// <summary>Most re-targets per walk; after that the normal timeout applies.</summary>
        public const int MaxNudges = 3;

        /// <summary>
        /// Multiplier on the ideal path time covering acceleration, braking, corners and steering round
        /// other shoppers.
        /// </summary>
        public const float WalkTimeoutSlack = 1.5f;

        /// <summary>
        /// Walk limit for a path of <paramref name="pathMetres"/> at <paramref name="speed"/> m/s:
        /// <see cref="WalkTimeoutSlack"/> times the ideal time, clamped to
        /// [<see cref="MinWalkTimeoutSeconds"/>, <see cref="MaxWalkTimeoutSeconds"/>]. An unknown
        /// (non-finite) length or a non-positive speed gets the maximum.
        /// </summary>
        public static float WalkTimeout(float pathMetres, float speed)
        {
            if (float.IsNaN(pathMetres) || float.IsInfinity(pathMetres) || speed <= 0f)
                return MaxWalkTimeoutSeconds;
            float ideal = Mathf.Max(0f, pathMetres) / speed;
            return Mathf.Clamp(ideal * WalkTimeoutSlack, MinWalkTimeoutSeconds, MaxWalkTimeoutSeconds);
        }
    }
}
