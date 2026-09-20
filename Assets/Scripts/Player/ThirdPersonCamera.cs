using UnityEngine;
using PetShop.Core;

namespace PetShop.Player
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform Target;
        public Vector3 TargetOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Orbit")]
        public float Distance     = 7f;
        public float MinDistance  = 2f;
        public float MaxDistance  = 20f;
        public float Pitch        = 28f;
        public float MinPitch     = 5f;
        public float MaxPitch     = 75f;
        public float Yaw          = 0f;
        public float OrbitSpeed   = 180f;
        public float ZoomSpeed    = 4f;

        [Header("Smoothing")]
        public float FollowSmooth = 10f;
        public float OrbitSmooth  = 8f;

        [Header("Collision")]
        public float CollisionRadius = 0.25f;

        private float _collisionDistance = 7f;
        private float _desiredDist;
        private float _curYaw;
        private float _curPitch;
        private Vector3 _curFocus;

        private void Start()
        {
            _desiredDist = Distance;
            _curYaw      = Yaw;
            _curPitch    = Pitch;
            if (Target != null)
                _curFocus = Target.position + TargetOffset;
        }

        private static bool BuildModeActive =>
            GameManager.Instance != null && GameManager.Instance.IsBuildModeActive;

        private void LateUpdate()
        {
            if (Target == null) return;

            bool orbiting = Input.GetMouseButton(1) && !BuildModeActive;
            if (orbiting)
            {
                _curYaw   += Input.GetAxis("Mouse X") * OrbitSpeed * Time.deltaTime;
                _curPitch -= Input.GetAxis("Mouse Y") * OrbitSpeed * Time.deltaTime;
                _curPitch  = Mathf.Clamp(_curPitch, MinPitch, MaxPitch);
            }

            _desiredDist -= Input.GetAxis("Mouse ScrollWheel") * ZoomSpeed * 4f;
            _desiredDist  = Mathf.Clamp(_desiredDist, MinDistance, MaxDistance);
            Distance      = Mathf.Lerp(Distance, _desiredDist, OrbitSmooth * Time.deltaTime);

            Vector3 targetFocus = Target.position + TargetOffset;
            _curFocus = Vector3.Lerp(_curFocus, targetFocus, FollowSmooth * Time.deltaTime);

            Quaternion rot  = Quaternion.Euler(_curPitch, _curYaw, 0f);
            Vector3 offset  = rot * new Vector3(0f, 0f, -Distance);
            Vector3 desired = _curFocus + offset;

            // Only real building geometry pushes the camera in. Letting street props — lamp
            // posts, trees, parked cars — block it made the camera snap in and out every few
            // steps, which reads as stutter.
            float wanted = Vector3.Distance(_curFocus, desired);
            float allowed = wanted;

            if (Physics.SphereCast(_curFocus, CollisionRadius, (desired - _curFocus).normalized,
                                   out RaycastHit hit, wanted, GameLayers.CameraBlocker,
                                   QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(MinDistance * 0.5f, hit.distance - CollisionRadius * 0.5f);

            // Ease towards the allowed distance instead of teleporting to it.
            _collisionDistance = allowed < _collisionDistance
                ? allowed                                                        // duck in at once
                : Mathf.Lerp(_collisionDistance, allowed, 6f * Time.deltaTime);  // ease back out

            transform.position = _curFocus + (desired - _curFocus).normalized * _collisionDistance;
            transform.LookAt(_curFocus, Vector3.up);
        }
    }
}
