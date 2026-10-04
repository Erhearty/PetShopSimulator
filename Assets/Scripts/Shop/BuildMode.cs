using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using PetShop.Core;
using PetShop.Commerce;
using PetShop.Pets;

namespace PetShop.Shop
{
    /// <summary>
    /// 3D build mode — projects the mouse onto the floor plane, shows a translucent
    /// ghost of the item, and places real furniture on LMB.
    /// LMB place | R / Shift+R rotate (wheel too while holding an item) | Q cycle pen species |
    /// RMB (first person only) / Esc / B cancel | Delete-key or middle-click removes.
    /// The Remove tool lives in BuildMode.Remove.cs.
    /// Held-item placement (from the furniture inventory) lives in BuildMode.Held.cs, the ghost
    /// in BuildMode.Ghost.cs.
    /// </summary>
    public partial class BuildMode : MonoBehaviour
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

        /// <summary>
        /// Raised after the player places furniture interactively (paid or from the hand). Unlike
        /// <see cref="OnFurnitureSpawned"/> it is not raised by save loading, room seeding or dev
        /// furnishing, which call <see cref="Place"/> directly — game-feel effects listen here.
        /// </summary>
        public UnityEvent<GameObject>                   OnFurniturePlacedByPlayer = new();

        /// <summary>
        /// Pen species the player may pick with Q for the legacy <c>pet_pen</c>, as pen variant
        /// strings. Per-species pens ignore it. Null or empty leaves the factory default (Rabbit).
        /// </summary>
        public Func<IReadOnlyList<string>> PenVariantSource;

        /// <summary>Catalog type of pens, the only item whose species can be picked.</summary>
        private const string PenType = "pen";
        /// <summary>Share of an item's cost refunded when removed without a furniture inventory.</summary>
        private const float SellBackShare = 0.5f;
        /// <summary>Index into the current pen variant options; wrapped on use.</summary>
        private int _penVariantIndex;

        /// <summary>True while build mode is open.</summary>
        public bool             IsActive    { get; private set; }
        /// <summary>The catalogue entry being placed, or the last one placed.</summary>
        public PlacedObjectData CurrentItem { get; private set; }

        private Camera     _cam;
        private float      _rotation;
        private Vector2Int _hoverCell;
        private bool       _hoverValid;

        private readonly Dictionary<Vector2Int, GameObject> _placedNodes = new();

        /// <summary>The build bar sits under the cursor — clicks there must not also place.</summary>
        private static bool PointerOverUI =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Awake() => _cam = Camera.main;

        // ── Mode control ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens paid build mode for <paramref name="item"/>: placing charges its catalogue cost.
        /// Any item held from the furniture inventory goes back first.
        /// </summary>
        public void EnterBuildMode(PlacedObjectData item)
        {
            if (item == null) return;
            ReturnHeld();
            BeginMode(item);
        }

        /// <summary>Shared entry for paid and held placement: resets rotation and builds the ghost.</summary>
        private void BeginMode(PlacedObjectData item)
        {
            CurrentItem = item;
            IsActive    = true;
            IsRemoving  = false;
            _rotation   = 0f;
            CreateGhost();
            OnBuildModeEntered.Invoke(item);
            if (IsLegacyPen(item) && _heldVariant == null) AnnouncePenVariant();
        }

        /// <summary>Closes build mode; a held item goes back into the furniture inventory.</summary>
        public void ExitBuildMode()
        {
            if (!IsActive) return;
            IsActive   = false;
            IsRemoving = false;
            ReturnHeld();
            DestroyGhost();
            OnBuildModeExited.Invoke();
        }

        /// <summary>A disabled or destroyed BuildMode hands a held item back so it is never lost.</summary>
        private void OnDisable() => ExitBuildMode();

        private void Update()
        {
            if (!IsActive) return;

            // Escape is routed by GameUI so a single press cannot also close a panel.
            // RMB cancels in first person; in the build view it is the camera's.
            if (InputBindings.GetKeyDown(GameAction.BuildMode) || (Input.GetMouseButtonDown(1) && RightClickCancels))
            { ExitBuildMode(); return; }
            if (IsRemoving) { UpdateRemoveTool(); return; }

            HandleRotationInput();
            if (Input.GetKeyDown(KeyCode.Q)) CyclePenVariant();

            UpdateGhost();

            if (InputBindings.GetKeyDown(GameAction.BuildRemove)) { TryRemoveUnderCursor(); return; }

            if (PointerOverUI) return;
            if (Input.GetMouseButtonDown(0)) TryPlace();
            if (Input.GetMouseButtonDown(2)) TryRemoveUnderCursor();
        }

        // ── Placement ───────────────────────────────────────────────────────────

        private void TryPlace()
        {
            if (!RaycastFloor(out var worldPos)) return;
            var cell = GridManager.WorldToGrid(worldPos);
            if (IsHolding) { PlaceHeld(cell); return; }

            if (RefusePlacement(CurrentItem, cell, CurrentItem.Size)) return;
            if (Shop != null && Shop.Balance < CurrentItem.Cost)
            {
                OnBuildMessage.Invoke($"Not enough money — {CurrentItem.DisplayName} costs €{CurrentItem.Cost:N0}.");
                return;
            }

            PrepareFloor(CurrentItem, cell, CurrentItem.Size);
            string variant = IsLegacyPen(CurrentItem) ? CurrentPenVariant() : null;
            var go = Place(cell, CurrentItem, variant, _rotation, charge: true);
            AddStarterPair(go, CurrentItem);
            if (go != null) OnFurniturePlacedByPlayer.Invoke(go);
        }

        /// <summary>
        /// The species the next legacy pen will hold, or null for the factory default. Per-species
        /// pens take their species from their id and save no variant.
        /// </summary>
        private string CurrentPenVariant()
        {
            if (!IsLegacyPen(CurrentItem)) return null;
            var options = PenVariantSource?.Invoke();
            if (options == null || options.Count == 0) return null;
            return options[_penVariantIndex % options.Count];
        }

        /// <summary>Q: step to the next unlocked pen species and announce it.</summary>
        private void CyclePenVariant()
        {
            if (!IsLegacyPen(CurrentItem)) return;
            var options = PenVariantSource?.Invoke();
            if (options == null || options.Count == 0) return;

            _penVariantIndex = (_penVariantIndex % options.Count + 1) % options.Count;
            AnnouncePenVariant();
        }

        /// <summary>Tells the player which species the next pen will hold.</summary>
        private void AnnouncePenVariant()
        {
            string name = CurrentPenVariant();
            if (name != null) OnBuildMessage.Invoke($"Pen species: {name} — Q to change");
        }

        /// <summary>
        /// Place furniture and register it on the grid. Shared by the player and save
        /// loading. <paramref name="footprintRotated"/> swaps the catalogue
        /// footprint's x/y (hand-placed items turned 90/270°); false keeps <c>def.Size</c>.
        /// </summary>
        public GameObject Place(Vector2Int cell, PlacedObjectData def, string variant,
                                float rotation, bool charge, bool footprintRotated = false)
        {
            if (def == null) return null;
            Vector2Int size = FootprintSize(def, footprintRotated);
            if (!GridManager.PlaceObject(cell, def, size)) return null;

            if (charge && Shop != null)
                Shop.ChangeBalance(-def.Cost, $"Build {def.DisplayName}");

            var go = FurnitureFactory.Spawn(def, cell, variant, GridManager, ObjectRoot, rotation, size);
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

        /// <summary>Removes whatever stands on the floor cell under the aim point.</summary>
        public void TryRemoveUnderCursor()
        {
            if (!RaycastFloor(out var worldPos)) return;
            RemoveAtWorldPos(worldPos);
        }

        /// <summary>
        /// Removes the object covering <paramref name="worldPos"/>. With a furniture
        /// <see cref="Supply"/> the item goes back into the inventory (shelf stock to the
        /// warehouse, an occupied pen refuses); without one it is sold back for half its cost.
        /// </summary>
        public void RemoveAtWorldPos(Vector3 worldPos)
        {
            var cell = GridManager.WorldToGrid(worldPos);
            if (!GridManager.TryGetObject(cell, out var entry)) return;
            if (Supply != null && PenHasPets(entry.Instance))
            {
                OnBuildMessage.Invoke("Move the pets out before packing this pen away.");
                return;
            }

            var root = entry.Root;
            var def  = entry.Data;
            if (Supply != null) ReturnShelfStock(entry.Instance);
            if (!GridManager.RemoveObject(cell)) return;
            ReleaseFloor(def, root, entry.Size);

            DespawnNode(root);
            string message = SettleRemoval(def);
            OnObjectRemovedVisually.Invoke(root);
            OnBuildMessage.Invoke(message);
        }

        /// <summary>Destroys the spawned object rooted at <paramref name="root"/>, if any.</summary>
        private void DespawnNode(Vector2Int root)
        {
            if (!_placedNodes.TryGetValue(root, out var go)) return;
            OnFurnitureDespawning.Invoke(go);
            PrefabPreview.DestroySafe(go);
            _placedNodes.Remove(root);
        }

        /// <summary>Returns the removed item to the inventory, or refunds half its cost; gives the message.</summary>
        private string SettleRemoval(PlacedObjectData def)
        {
            if (def == null) return "Removed.";
            if (Supply != null)
            {
                if (def.Type == PenType) Supply.AddPacked(def.Id);
                else                     Supply.AddOwned(def.Id);
                return $"Packed {def.DisplayName} back into your furniture inventory.";
            }
            float refund = def.Cost * SellBackShare;
            if (Shop != null) Shop.ChangeBalance(refund, $"Sold {def.DisplayName}");
            return $"Removed {def.DisplayName} (+€{refund:N0})";
        }

        /// <summary>True when <paramref name="go"/> is a pen with animals still in it.</summary>
        private static bool PenHasPets(GameObject go)
        {
            var pen = go != null ? go.GetComponent<PetPen>() : null;
            return pen != null && pen.Count > 0;
        }

        /// <summary>Moves every unit on a shelf being packed away back into the warehouse.</summary>
        private void ReturnShelfStock(GameObject go)
        {
            var shelf = go != null ? go.GetComponent<ShelfUnit>() : null;
            if (shelf == null || Shop == null) return;
            foreach (var line in shelf.Lines)
                if (line.Product != null) Shop.AddToWarehouse(line.Product.category, line.Units);
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
            // furniture on the far side of the map. The overhead build view aims with a free
            // cursor at whatever is on screen, so it is not limited.
            if (Cursor.lockState != CursorLockMode.Locked) return true;
            Vector3 from = _cam.transform.position; from.y = 0f;
            Vector3 flat = worldPos;                flat.y = 0f;
            if (Vector3.Distance(from, flat) > MaxPlacementDistance)
                worldPos = from + (flat - from).normalized * MaxPlacementDistance;

            return true;
        }
    }
}
