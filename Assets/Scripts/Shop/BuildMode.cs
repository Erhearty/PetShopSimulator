using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using PetShop.Core;
using PetShop.Commerce;

namespace PetShop.Shop
{
    /// <summary>
    /// 3D build mode — projects the mouse onto the floor plane, shows a translucent
    /// ghost of the item, and places real furniture on LMB.
    /// LMB place | R rotate | RMB/Esc/B cancel | Delete-key or middle-click removes.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        [Header("References")]
        public GridManager GridManager;
        public ShopManager Shop;
        public Transform   ObjectRoot;

        [Header("Placement")]
        [Tooltip("How far from the camera furniture may be placed, in metres.")]
        public float MaxPlacementDistance = 14f;

        [Header("Ghost colours")]
        public Color ValidColor   = new(0.30f, 0.90f, 0.45f, 0.45f);
        public Color InvalidColor = new(0.90f, 0.25f, 0.20f, 0.45f);

        public UnityEvent<Vector2Int, PlacedObjectData> OnObjectPlacedVisually  = new();
        public UnityEvent<Vector2Int>                   OnObjectRemovedVisually = new();
        public UnityEvent<PlacedObjectData>             OnBuildModeEntered      = new();
        public UnityEvent                               OnBuildModeExited       = new();
        public UnityEvent<string>                       OnBuildMessage          = new();
        public UnityEvent<GameObject>                   OnFurnitureSpawned      = new();
        public UnityEvent<GameObject>                   OnFurnitureDespawning   = new();

        public bool             IsActive    { get; private set; }
        public PlacedObjectData CurrentItem { get; private set; }

        private GameObject _ghost;
        private Material   _ghostMat;
        private Camera     _cam;
        private float      _rotation;
        private float      _ghostHeight = 1.5f;
        private Vector2Int _hoverCell;
        private bool       _hoverValid;

        private readonly Dictionary<Vector2Int, GameObject> _placedNodes = new();

        /// <summary>The build bar sits under the cursor — clicks there must not also place.</summary>
        private static bool PointerOverUI =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Awake() => _cam = Camera.main;

        // ── Mode control ────────────────────────────────────────────────────────

        public void EnterBuildMode(PlacedObjectData item)
        {
            if (item == null) return;
            CurrentItem = item;
            IsActive    = true;
            _rotation   = 0f;
            CreateGhost();
            OnBuildModeEntered.Invoke(item);
        }

        public void ExitBuildMode()
        {
            if (!IsActive) return;
            IsActive = false;
            DestroyGhost();
            OnBuildModeExited.Invoke();
        }

        private void Update()
        {
            if (!IsActive) return;

            // Escape is routed by GameUI so a single press cannot also close a panel.
            if (Input.GetKeyDown(KeyCode.B) || Input.GetMouseButtonDown(1))
            { ExitBuildMode(); return; }

            if (Input.GetKeyDown(KeyCode.R)) _rotation = (_rotation + 90f) % 360f;

            UpdateGhost();

            if (Input.GetKeyDown(KeyCode.Delete)) { TryRemoveUnderCursor(); return; }

            if (PointerOverUI) return;
            if (Input.GetMouseButtonDown(0)) TryPlace();
            if (Input.GetMouseButtonDown(2)) TryRemoveUnderCursor();
        }

        // ── Placement ───────────────────────────────────────────────────────────

        private void TryPlace()
        {
            if (!RaycastFloor(out var worldPos)) return;
            var cell = GridManager.WorldToGrid(worldPos);

            if (!GridManager.CanPlace(cell, CurrentItem.Size))
            {
                OnBuildMessage.Invoke("Can't build there.");
                return;
            }
            if (Shop != null && Shop.Balance < CurrentItem.Cost)
            {
                OnBuildMessage.Invoke($"Not enough money — {CurrentItem.DisplayName} costs €{CurrentItem.Cost:N0}.");
                return;
            }

            Place(cell, CurrentItem, null, _rotation, charge: true);
        }

        /// <summary>
        /// Place furniture and register it on the grid. Shared by the player, the starter
        /// layout and save loading.
        /// </summary>
        public GameObject Place(Vector2Int cell, PlacedObjectData def, string variant,
                                float rotation, bool charge)
        {
            if (def == null || !GridManager.PlaceObject(cell, def, def.Size)) return null;

            if (charge && Shop != null)
                Shop.ChangeBalance(-def.Cost, $"Build {def.DisplayName}");

            var go = FurnitureFactory.Spawn(def, cell, variant, GridManager, ObjectRoot, rotation);
            _placedNodes[cell] = go;

            if (GridManager.TryGetObject(cell, out var entry))
            {
                entry.Instance = go;
                entry.Variant  = variant;
            }

            OnFurnitureSpawned.Invoke(go);
            OnObjectPlacedVisually.Invoke(cell, def);
            return go;
        }

        public void TryRemoveUnderCursor()
        {
            if (!RaycastFloor(out var worldPos)) return;
            RemoveAtWorldPos(worldPos);
        }

        public void RemoveAtWorldPos(Vector3 worldPos)
        {
            var cell = GridManager.WorldToGrid(worldPos);
            if (!GridManager.TryGetObject(cell, out var entry)) return;

            var root = entry.Root;
            var def  = entry.Data;
            if (!GridManager.RemoveObject(cell)) return;

            if (_placedNodes.TryGetValue(root, out var go))
            {
                OnFurnitureDespawning.Invoke(go);
                Destroy(go);
                _placedNodes.Remove(root);
            }
            if (Shop != null && def != null)
                Shop.ChangeBalance(def.Cost * 0.5f, $"Sold {def.DisplayName}");

            OnObjectRemovedVisually.Invoke(root);
            OnBuildMessage.Invoke(def != null ? $"Removed {def.DisplayName} (+€{def.Cost * 0.5f:N0})" : "Removed.");
        }

        // ── Ghost ───────────────────────────────────────────────────────────────

        private void CreateGhost()
        {
            DestroyGhost();
            float cs = GridManager.CellSize;

            _ghostMat = MaterialFactory.CreateTransparent("ghost", ValidColor);

            // The ghost mimics the shape of what is being placed — a wall preview shaped
            // like a shelf tells you nothing about how it will sit against its neighbours.
            bool isWall = BuildCatalog.IsBuildingPiece(CurrentItem.Id);
            float ghostW, ghostD;

            if (isWall)
            {
                _ghostHeight = CurrentItem.Id == BuildCatalog.Fence ? 1.05f : 3.2f;
                ghostW = cs;
                ghostD = 0.24f;
            }
            else
            {
                _ghostHeight = CurrentItem.Type == "pen" ? 0.7f : 1.5f;
                ghostW = CurrentItem.Size.x * cs * 0.9f;
                ghostD = CurrentItem.Size.y * cs * 0.9f;
            }

            _ghost = MeshBuilder.CreateBox(ghostW, _ghostHeight, ghostD, null, "Ghost");
            _ghost.transform.SetParent(transform, false);
            MeshBuilder.SetMaterialRecursive(_ghost, _ghostMat);
            MeshBuilder.SetLayerRecursive(_ghost, GameLayers.Ghost);
            foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) Destroy(col);
        }

        private void DestroyGhost()
        {
            if (_ghost    != null) Destroy(_ghost);
            if (_ghostMat != null) Destroy(_ghostMat);
            _ghost = null; _ghostMat = null;
        }

        private void UpdateGhost()
        {
            if (_ghost == null || !RaycastFloor(out var worldPos)) return;

            _hoverCell  = GridManager.WorldToGrid(worldPos);
            _hoverValid = GridManager.CanPlace(_hoverCell, CurrentItem.Size)
                       && (Shop == null || Shop.Balance >= CurrentItem.Cost);

            Vector3 centre = GridManager.FootprintCenter(_hoverCell, CurrentItem.Size);
            _ghost.transform.position      = centre + Vector3.up * (_ghostHeight * 0.5f + 0.02f);
            _ghost.transform.eulerAngles   = new Vector3(0f, _rotation, 0f);

            if (_ghostMat != null)
                MaterialFactory.SetColor(_ghostMat, _hoverValid ? ValidColor : InvalidColor);
        }

        /// <summary>
        /// Projects the aim point onto the y = 0 floor plane.
        ///
        /// In first person the cursor is locked, so there is no pointer to aim with — the
        /// screen centre is the aim point instead, and placement follows where you look.
        /// </summary>
        private bool RaycastFloor(out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return false; }

            Vector3 aim = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;

            Ray ray = _cam.ScreenPointToRay(aim);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return false;

            worldPos = ray.origin + ray.direction * t;

            // Keep placement within arm's reach-ish, so looking at the horizon does not put
            // furniture on the far side of the map.
            Vector3 from = _cam.transform.position; from.y = 0f;
            Vector3 flat = worldPos;                flat.y = 0f;
            if (Vector3.Distance(from, flat) > MaxPlacementDistance)
                worldPos = from + (flat - from).normalized * MaxPlacementDistance;

            return true;
        }
    }
}
