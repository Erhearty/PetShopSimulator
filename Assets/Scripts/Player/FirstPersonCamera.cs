using UnityEngine;
using PetShop.Core;

namespace PetShop.Player
{
    /// <summary>
    /// First-person mouse-look. The body turns with yaw so movement, the interaction
    /// SphereCast and the build raycast all agree with where you are looking.
    ///
    /// The player model is kept in the scene but rendered shadows-only while in first
    /// person: you still cast a shadow and appear in reflections, without the inside of
    /// your own head filling the screen.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform Body;
        public float EyeHeight = 1.62f;

        [Header("Look")]
        public float Sensitivity = 2.2f;
        public float MinPitch    = -85f;
        public float MaxPitch    =  85f;
        [Tooltip("Smoothing on the look, in seconds. 0 is raw.")]
        public float Smoothing   = 0.02f;

        [Header("Feel")]
        public float BobAmount = 0.035f;
        public float BobSpeed  = 9f;

        public float Yaw   { get; private set; }
        public float Pitch { get; private set; }

        private float   _yawVel, _pitchVel, _targetYaw, _targetPitch;
        private float   _bobPhase;
        private PlayerController _controller;
        private Renderer[] _bodyRenderers;

        private static bool CursorFree =>
            GameManager.Instance != null &&
            (GameManager.Instance.IsModalOpen || GameManager.Instance.IsGameOver);

        private void Start()
        {
            if (Body == null) return;

            _controller    = Body.GetComponent<PlayerController>();
            _bodyRenderers = Body.GetComponentsInChildren<Renderer>(true);
            SetBodyVisible(false);

            _targetYaw = Yaw = Body.eulerAngles.y;
            ApplyCursor();
        }

        private void OnDisable()
        {
            SetBodyVisible(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        /// <summary>Shadows-only rather than disabled, so the player still grounds the scene.</summary>
        private void SetBodyVisible(bool visible)
        {
            if (_bodyRenderers == null) return;
            foreach (var r in _bodyRenderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = visible
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        private void ApplyCursor()
        {
            bool free = CursorFree;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = free;
        }

        private void LateUpdate()
        {
            if (Body == null) return;
            ApplyCursor();

            if (!CursorFree)
            {
                _targetYaw   += Input.GetAxisRaw("Mouse X") * Sensitivity;
                _targetPitch -= Input.GetAxisRaw("Mouse Y") * Sensitivity;
                _targetPitch  = Mathf.Clamp(_targetPitch, MinPitch, MaxPitch);
            }

            Yaw   = Smoothing <= 0f ? _targetYaw
                                    : Mathf.SmoothDampAngle(Yaw, _targetYaw, ref _yawVel, Smoothing);
            Pitch = Smoothing <= 0f ? _targetPitch
                                    : Mathf.SmoothDamp(Pitch, _targetPitch, ref _pitchVel, Smoothing);

            // The body follows yaw only; pitch belongs to the camera.
            Body.rotation = Quaternion.Euler(0f, Yaw, 0f);

            float bob = 0f;
            if (_controller != null && _controller.IsGrounded)
            {
                float speed = _controller.HorizontalSpeed;
                if (speed > 0.3f)
                {
                    _bobPhase += Time.deltaTime * BobSpeed * Mathf.Clamp01(speed / 4.5f);
                    bob = Mathf.Sin(_bobPhase) * BobAmount * Mathf.Clamp01(speed / 4.5f);
                }
            }

            transform.position = Body.position + Vector3.up * (EyeHeight + bob);
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
        }
    }
}
