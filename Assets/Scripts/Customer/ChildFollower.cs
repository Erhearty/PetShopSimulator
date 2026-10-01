using UnityEngine;

namespace PetShop.Customer
{
    /// <summary>
    /// Keeps a child character trotting along beside its parent with a simple lerp-follow.
    /// No NavMeshAgent of its own, so it never competes for space in the checkout line.
    /// Destroys itself as soon as the parent is gone.
    /// </summary>
    public class ChildFollower : MonoBehaviour
    {
        /// <summary>The adult being followed. When it is destroyed, so is this child.</summary>
        public Transform Target;

        [Tooltip("Where the child walks relative to the parent: x = to the side, z = behind.")]
        public Vector2 Offset = new(0.55f, 0.45f);
        [Tooltip("How quickly the child closes the gap; higher is tighter.")]
        public float FollowSharpness = 4f;
        [Tooltip("How quickly the child turns to face where it is going.")]
        public float TurnSharpness = 8f;

        /// <summary>Speeds below this count as standing still for facing purposes.</summary>
        private const float MinTurnSpeed = 0.05f;

        /// <summary>World-space velocity this frame; drives the walk animation.</summary>
        public Vector3 Velocity { get; private set; }

        private void LateUpdate()
        {
            if (Target == null) { Destroy(gameObject); return; }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 goal = Target.position + Target.right * Offset.x - Target.forward * Offset.y;
            Vector3 next = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-FollowSharpness * dt));
            Velocity = (next - transform.position) / dt;
            transform.position = next;
            Face(dt);
        }

        private void Face(float dt)
        {
            Vector3 flat = new(Velocity.x, 0f, Velocity.z);
            Vector3 look = flat.magnitude > MinTurnSpeed ? flat : Target.forward;
            look.y = 0f;
            if (look.sqrMagnitude < Mathf.Epsilon) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up),
                                                  1f - Mathf.Exp(-TurnSharpness * dt));
        }
    }
}
