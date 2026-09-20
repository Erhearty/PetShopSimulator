using UnityEngine;
using PetShop.Core;

namespace PetShop.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float MoveSpeed     = 4.5f;
        public float RotateSpeed   = 900f;
        public float Gravity       = -22f;
        [Tooltip("Seconds to reach full speed. Raw axis input is instant, which reads as jerky.")]
        public float Acceleration  = 0.09f;

        [Header("Jump")]
        public KeyCode JumpKey     = KeyCode.Space;
        public float   JumpHeight  = 1.1f;
        [Tooltip("Grace period after walking off an edge during which a jump still counts.")]
        public float   CoyoteTime  = 0.12f;
        [Tooltip("A jump pressed this long before landing still fires on touchdown.")]
        public float   JumpBuffer  = 0.12f;
        [Range(0f, 1f)] public float AirControl = 0.7f;

        [Header("Interaction")]
        public KeyCode InteractKey = KeyCode.E;

        private CharacterController _cc;
        private Camera              _cam;
        private InteractionSystem   _interact;
        private CharacterVisual     _visual;
        private float               _yVelocity;
        private Vector3             _velocity;         // smoothed horizontal velocity
        private Vector3             _velocityRef;
        private float               _lastGroundedAt = -99f;
        private float               _jumpPressedAt  = -99f;

        public bool  IsGrounded      { get; private set; }
        public float HorizontalSpeed => _velocity.magnitude;

        [Tooltip("First person: the camera owns facing, so the body must not steer itself.")]
        public bool SteerTowardsMovement = false;

        private static bool Blocked =>
            GameManager.Instance != null && (GameManager.Instance.IsModalOpen || GameManager.Instance.IsGameOver);

        private void Awake()
        {
            _cc       = GetComponent<CharacterController>();
            _interact = GetComponent<InteractionSystem>();
            _visual   = GetComponent<CharacterVisual>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(JumpKey) && !Blocked) _jumpPressedAt = Time.time;

            HandleMove();
            if (Input.GetKeyDown(InteractKey) && !Blocked) _interact?.TryInteract();
        }

        private void HandleMove()
        {
            if (_cam == null) _cam = Camera.main;

            // Movement is frozen while placing furniture so LMB is unambiguous,
            // and while a blocking panel is up.
            bool frozen = Blocked || (GameManager.Instance != null && GameManager.Instance.IsBuildModeActive);
            float h = frozen ? 0f : Input.GetAxisRaw("Horizontal");
            float v = frozen ? 0f : Input.GetAxisRaw("Vertical");

            Vector3 camForward = _cam != null
                ? Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 camRight = _cam != null
                ? Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized
                : Vector3.right;

            Vector3 wish = camForward * v + camRight * h;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // GetAxisRaw is a hard 0/1, so stepping straight to full speed looks stuttery.
            // Smoothing the horizontal velocity is what makes movement read as continuous.
            float responsiveness = IsGrounded ? Acceleration : Acceleration / Mathf.Max(0.05f, AirControl);
            _velocity = Vector3.SmoothDamp(_velocity, wish * MoveSpeed, ref _velocityRef, responsiveness);

            IsGrounded = _cc.isGrounded;
            if (IsGrounded) _lastGroundedAt = Time.time;

            bool canJump = Time.time - _lastGroundedAt <= CoyoteTime
                        && Time.time - _jumpPressedAt  <= JumpBuffer;

            if (canJump)
            {
                _yVelocity     = Mathf.Sqrt(2f * JumpHeight * -Gravity);
                _jumpPressedAt = -99f;
                _lastGroundedAt = -99f;
                _visual?.PlayTrigger("jump");
                AudioManager.Instance?.PlaySfx("click", 0.5f);
            }
            else if (IsGrounded && _yVelocity < 0f)
            {
                _yVelocity = -2f;          // keep it pinned to the ground
            }
            else
            {
                _yVelocity += Gravity * Time.deltaTime;
            }

            _cc.Move((_velocity + Vector3.up * _yVelocity) * Time.deltaTime);

            if (SteerTowardsMovement && _velocity.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(_velocity, Vector3.up),
                    RotateSpeed * Time.deltaTime);
        }
    }
}
