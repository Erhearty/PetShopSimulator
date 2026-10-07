using UnityEngine;
using UnityEngine.AI;

namespace PetShop.Customer
{
    /// <summary>
    /// Outcome bookkeeping read by soak telemetry: why an empty-handed shopper left, and which leg
    /// of the visit a navigation timeout happened on. Gameplay never reads any of this.
    /// </summary>
    public partial class CustomerAI
    {
        /// <summary>Why this shopper walked out empty-handed; set just before <see cref="WalkedOutEmpty"/> fires.</summary>
        public WalkoutReason WalkoutReason { get; private set; } = WalkoutReason.None;

        /// <summary>Leg of the last walk that timed out; set just before <see cref="NavigationTimedOut"/> fires.</summary>
        public NavLeg TimedOutLeg { get; private set; } = NavLeg.None;

        /// <summary>Game seconds since this shopper's Start ran.</summary>
        public float SecondsSinceSpawn => Time.time - _spawnTime;

        private float _spawnTime;
        private bool  _lastWalkArrived;
        private Vector3 _legStart;
        private bool  _missedNoStock;
        private bool  _missedTooExpensive;
        private bool  _missedUnreachable;

        /// <summary>
        /// Pure walkout reason from what the shopper ran into while browsing. An unreachable shelf wins
        /// (the shopper never got a fair look), then empty shelves, then prices; nothing at all means
        /// every purchase roll simply failed.
        /// </summary>
        public static WalkoutReason ResolveWalkoutReason(bool unreachable, bool noStock, bool tooExpensive)
        {
            if (unreachable)  return WalkoutReason.CouldNotReachShelf;
            if (noStock)      return WalkoutReason.NoStockForWant;
            if (tooExpensive) return WalkoutReason.TooExpensive;
            return WalkoutReason.NotTempted;
        }

        /// <summary>Records the reason and raises <see cref="WalkedOutEmpty"/>.</summary>
        private void ReportWalkout()
        {
            WalkoutReason = ResolveWalkoutReason(_missedUnreachable, _missedNoStock, _missedTooExpensive);
            WalkedOutEmpty?.Invoke(this);
        }

        /// <summary>Logs a structured timeout line, records the leg and raises <see cref="NavigationTimedOut"/>.</summary>
        private void ReportNavTimeout(Vector3 target, NavLeg leg)
        {
            TimedOutLeg = leg;
            Debug.LogWarning($"[CustomerAI] {name} ran out of time walking to {target}. leg={leg} " +
                             $"status={_agent.pathStatus} remaining={_agent.remainingDistance:F2} " +
                             $"at={transform.position} corners={_agent.path.corners.Length} " +
                             $"elapsed={SecondsSinceSpawn:F1} vel={_agent.velocity} desired={_agent.desiredVelocity} " +
                             $"next={_agent.nextPosition} steer={_agent.steeringTarget} path=[{CornerList()}] " +
                             $"from={_legStart} speed={_agent.speed:F2}");
            NavigationTimedOut?.Invoke(this);
        }

        /// <summary>The current path's corners, for the timeout line only (allocates; never per frame).</summary>
        private string CornerList()
        {
            var sb = new System.Text.StringBuilder();
            foreach (Vector3 c in _agent.path.corners) sb.Append(c).Append(' ');
            return sb.ToString();
        }
    }
}
