using UnityEngine;
using PetShop.Core;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>
    /// The top-down build view. Entering lerps <see cref="Camera.main"/> from the player's eyes to
    /// an overhead view of the shop and yard; WASD or pushing the cursor to a screen edge pans,
    /// the scroll wheel zooms, and the view stays over the yard. While active the cursor is free
    /// and player input is gated through <see cref="GameManager.SetBuildViewActive"/>, which leaves
    /// the day clock, deliveries and the save/end-day keys running. Exiting puts the camera back
    /// on the player exactly as it was.
    /// </summary>
    public class BuildCamera : MonoBehaviour
    {
        /// <summary>Seconds the camera takes to swing from first person to overhead.</summary>
        public const float TransitionSeconds = 0.6f;
        /// <summary>Downward tilt of the overhead view, in degrees.</summary>
        public const float ViewPitch = 65f;
        /// <summary>Camera height on entering the view, in metres.</summary>
        public const float StartHeight = 34f;
        /// <summary>Closest the camera may zoom in, in metres above the floor.</summary>
        public const float MinHeight = 10f;
        /// <summary>Furthest the camera may zoom out, in metres above the floor.</summary>
        public const float MaxHeight = 60f;
        /// <summary>Pan speed per metre of height, so panning feels the same at every zoom.</summary>
        public const float PanSpeedPerHeight = 0.9f;
        /// <summary>Metres of height per unit of scroll-wheel axis.</summary>
        public const float ZoomPerScroll = 80f;
        /// <summary>Width of the screen border that pans the view, in pixels.</summary>
        public const float EdgePanPixels = 12f;

        private const string ScrollAxis = "Mouse ScrollWheel";

        /// <summary>True while the build view is showing.</summary>
        public bool IsActive { get; private set; }

        private GameManager       _game;
        private ShopLayout        _layout;
        private BuildMode         _build;
        private Camera            _cam;
        private FirstPersonCamera _fpc;
        private Transform         _savedParent;
        private Vector3           _savedLocalPosition;
        private Quaternion        _savedLocalRotation;
        private Vector3           _fromPosition;
        private Quaternion        _fromRotation;
        private Vector3           _focus;
        private float             _height;
        private float             _blend;

        /// <summary>Wires the view to the game, the layout whose yard bounds it, and build mode.</summary>
        public void Init(GameManager game, ShopLayout layout, BuildMode build)
        {
            _game   = game;
            _layout = layout;
            _build  = build;
        }

        /// <summary>Enters the build view when it is closed, leaves it when it is open.</summary>
        public void Toggle()
        {
            if (IsActive) Exit();
            else          Enter();
        }

        /// <summary>Swings the main camera overhead, frees the cursor and gates player input.</summary>
        public void Enter()
        {
            if (IsActive) return;
            _cam = Camera.main;
            if (_cam == null) return;

            SavePlayerPose(_cam.transform);
            _fpc = _cam.GetComponent<FirstPersonCamera>();
            if (_fpc != null) _fpc.enabled = false;
            _cam.transform.SetParent(null, true);

            _fromPosition = _cam.transform.position;
            _fromRotation = _cam.transform.rotation;
            _focus  = _layout != null ? _layout.ShopCentre : Flat(_fromPosition);
            _height = StartHeight;
            _blend  = 0f;
            IsActive = true;
            SetRoofVisible(false);
            _game?.SetBuildViewActive(true);
            FreeCursor();
        }

        /// <summary>Puts the camera back on the player as it was and releases the input gate.</summary>
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            SetRoofVisible(true);
            if (_cam != null)
            {
                var t = _cam.transform;
                t.SetParent(_savedParent, false);
                t.localPosition = _savedLocalPosition;
                t.localRotation = _savedLocalRotation;
            }
            if (_fpc != null) _fpc.enabled = true;
            _game?.SetBuildViewActive(false);
        }

        /// <summary>A disabled view hands the camera back, so the player never loses it.</summary>
        private void OnDisable() => Exit();

        private void LateUpdate()
        {
            if (!IsActive || _cam == null) return;
            FreeCursor();

            float dt = Time.unscaledDeltaTime;
            Pan(dt);
            Zoom();
            ClampFocus();
            ApplyPose(dt);
        }

        private void SavePlayerPose(Transform t)
        {
            _savedParent        = t.parent;
            _savedLocalPosition = t.localPosition;
            _savedLocalRotation = t.localRotation;
        }

        /// <summary>Moves the focus with WASD and with the cursor pushed against a screen edge.</summary>
        private void Pan(float dt)
        {
            Vector2 dir = KeyPan() + EdgePan();
            if (dir == Vector2.zero) return;
            float speed = PanSpeedPerHeight * _height * dt;
            Vector2 step = dir.normalized * speed;
            _focus += new Vector3(step.x, 0f, step.y);
        }

        private static Vector2 KeyPan()
        {
            float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            return new Vector2(x, z);
        }

        private static Vector2 EdgePan()
        {
            Vector3 m = Input.mousePosition;
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;
            float x = (m.x >= Screen.width  - EdgePanPixels ? 1f : 0f) - (m.x <= EdgePanPixels ? 1f : 0f);
            float z = (m.y >= Screen.height - EdgePanPixels ? 1f : 0f) - (m.y <= EdgePanPixels ? 1f : 0f);
            return new Vector2(x, z);
        }

        /// <summary>Scroll zooms; while an item is held the wheel rotates it instead.</summary>
        private void Zoom()
        {
            if (_build != null && _build.IsHolding) return;
            float wheel = Input.GetAxis(ScrollAxis);
            if (wheel == 0f) return;
            _height = Mathf.Clamp(_height - wheel * ZoomPerScroll, MinHeight, MaxHeight);
        }

        /// <summary>Keeps the point being looked at over the yard.</summary>
        private void ClampFocus()
        {
            if (_layout == null) return;
            float halfX = _layout.YardWidth * 0.5f;
            float halfZ = _layout.YardDepth * 0.5f;
            _focus.x = Mathf.Clamp(_focus.x, -halfX, halfX);
            _focus.z = Mathf.Clamp(_focus.z, -halfZ, halfZ);
        }

        /// <summary>Blends from the first-person pose towards the overhead one, then follows it.</summary>
        private void ApplyPose(float dt)
        {
            _blend = Mathf.MoveTowards(_blend, 1f, dt / TransitionSeconds);
            float k = Mathf.SmoothStep(0f, 1f, _blend);
            Quaternion rotation = Quaternion.Euler(ViewPitch, 0f, 0f);
            float distance = _height / Mathf.Sin(ViewPitch * Mathf.Deg2Rad);
            Vector3 position = _focus - rotation * Vector3.forward * distance;
            _cam.transform.SetPositionAndRotation(Vector3.Lerp(_fromPosition, position, k),
                                                  Quaternion.Slerp(_fromRotation, rotation, k));
        }

        private static void FreeCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        /// <summary>The overhead view looks into the room, so the roof hides while it shows.</summary>
        private static void SetRoofVisible(bool visible)
        {
            if (RoofBuilder.Instance != null) RoofBuilder.Instance.SetVisible(visible);
        }

        private static Vector3 Flat(Vector3 p) => new(p.x, 0f, p.z);
    }
}
