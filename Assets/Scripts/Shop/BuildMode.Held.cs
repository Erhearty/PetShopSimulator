using UnityEngine;
using PetShop.Core;
using PetShop.Player;

namespace PetShop.Shop
{
    /// <summary>
    /// Placing furniture from the hand: an item is taken out of the <see cref="FurnitureSupply"/>
    /// inventory, placed for free, and goes back when the player cancels. Also owns rotation and
    /// the rotated-footprint rule for hand-placed items.
    /// </summary>
    public partial class BuildMode
    {
        /// <summary>Rotation step for purely visual decor, in degrees.</summary>
        public const float DecorRotationStep = 45f;
        /// <summary>Rotation step for furniture and building pieces, in degrees.</summary>
        public const float DefaultRotationStep = 90f;
        private const float FullTurn    = 360f;
        private const float QuarterTurn = 90f;
        private const int   QuartersPerTurn = 4;
        private const string ScrollAxis = "Mouse ScrollWheel";

        /// <summary>
        /// The furniture inventory items are placed from. Null keeps the legacy behaviour:
        /// no held placement, and removal sells back for half the cost.
        /// </summary>
        public FurnitureSupply Supply { get; set; }

        /// <summary>True while an item taken from the furniture inventory is in hand.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>Current yaw of the item being placed, in degrees [0, 360).</summary>
        public float CurrentRotation => _rotation;

        /// <summary>Degrees one rotate press turns the current item: 45 for decor, 90 otherwise.</summary>
        public float RotationStep =>
            CurrentItem != null && BuildCatalog.IsDecoration(CurrentItem.Id) ? DecorRotationStep : DefaultRotationStep;

        /// <summary>
        /// One-line hint with the current angle and the rotate keys, for the HUD to show while
        /// building. Empty when build mode is closed.
        /// </summary>
        public string PlacementHint
        {
            get
            {
                if (!IsActive || CurrentItem == null) return string.Empty;
                string key   = InputBindings.Label(GameAction.BuildRotate);
                string wheel = IsHolding ? PetShop.Localization.Loc.T("build.rotate_wheel") : string.Empty;
                return PetShop.Localization.Loc.F("build.rotate", _rotation, key, wheel);
            }
        }

        private string       _heldVariant;
        private HeldItemView _heldView;

        /// <summary>
        /// Takes one <paramref name="catalogId"/> out of the furniture inventory into the hand and
        /// opens build mode for it. False (nothing changes) when none is owned or no supply is set.
        /// <paramref name="variant"/> is the shelf category / pen species, or null for the default.
        /// </summary>
        public bool EnterPlacement(string catalogId, string variant = null)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null || Supply == null) return false;
            int heldSame = IsHolding && CurrentItem != null && CurrentItem.Id == catalogId ? 1 : 0;
            if (Supply.OwnedCount(catalogId) + heldSame <= 0) return false;

            ReturnHeld();
            if (!Supply.TakeOwned(catalogId, out _heldPacked)) return false;
            IsHolding    = true;
            _heldVariant = variant;
            BeginMode(def);
            ShowHeldView();
            return true;
        }

        /// <summary>
        /// Places the held item at <paramref name="cell"/> for free, consuming it. When more of
        /// the same id are owned the next one is taken into the hand; otherwise build mode closes.
        /// </summary>
        public GameObject PlaceHeld(Vector2Int cell)
        {
            if (!IsHolding || CurrentItem == null) return null;
            var  def     = CurrentItem;
            bool swapped = SwapsFootprint(_rotation);
            Vector2Int size = FootprintSize(def, swapped);
            if (RefusePlacement(def, cell, size, _rotation)) return null;
            PrepareFloor(def, cell, size);

            var go = Place(cell, def, HeldVariant(), _rotation, charge: false, footprintRotated: swapped);
            if (go == null && !GridManager.TryGetObject(cell, out _)) return null;
            if (!_heldPacked) AddStarterPair(go, def);
            if (go != null) OnFurniturePlacedByPlayer.Invoke(go);
            IsHolding = false;
            TakeNextOrExit(def);
            return go;
        }

        /// <summary>Turns the current item by <paramref name="direction"/> steps (+1 / -1).</summary>
        public void Rotate(int direction)
        {
            _rotation = Mathf.Repeat(_rotation + direction * RotationStep, FullTurn);
        }

        /// <summary>
        /// True when <paramref name="rotation"/>, rounded to the nearest quarter turn, is 90 or
        /// 270° — the footprint of a hand-placed item then lies sideways.
        /// </summary>
        public static bool SwapsFootprint(float rotation)
        {
            int quarters = Mathf.RoundToInt(Mathf.Repeat(rotation, FullTurn) / QuarterTurn) % QuartersPerTurn;
            return quarters % 2 == 1;
        }

        /// <summary>The cells <paramref name="def"/> occupies: its Size, with x/y swapped when rotated.</summary>
        public static Vector2Int FootprintSize(PlacedObjectData def, bool footprintRotated)
        {
            if (def == null) return Vector2Int.one;
            return footprintRotated ? new Vector2Int(def.Size.y, def.Size.x) : def.Size;
        }

        /// <summary>
        /// True when a placed entry occupies its catalogue footprint sideways. Square items never
        /// report rotated, since swapping their footprint changes nothing.
        /// </summary>
        public static bool IsFootprintRotated(GridEntry entry) =>
            entry?.Data != null && entry.Size != entry.Data.Size;

        /// <summary>R = +step, Shift+R = -step; the wheel turns too while an item is in hand.</summary>
        private void HandleRotationInput()
        {
            if (InputBindings.GetKeyDown(GameAction.BuildRotate)) Rotate(ShiftHeld ? -1 : 1);
            if (!IsHolding) return;
            float wheel = Input.GetAxis(ScrollAxis);
            if (wheel > 0f)      Rotate(1);
            else if (wheel < 0f) Rotate(-1);
        }

        private static bool ShiftHeld =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>Footprint the ghost checks: rotated only for items placed from the hand.</summary>
        private Vector2Int ActiveFootprint =>
            IsHolding ? FootprintSize(CurrentItem, SwapsFootprint(_rotation)) : CurrentItem.Size;

        /// <summary>
        /// The variant a held item is placed with: the one chosen, else the Q pick for the legacy
        /// pen. Per-species pens carry no variant.
        /// </summary>
        private string HeldVariant()
        {
            if (CurrentItem.Type != PenType) return _heldVariant;
            return IsLegacyPen(CurrentItem) ? _heldVariant ?? CurrentPenVariant() : null;
        }

        /// <summary>Takes the next unit of the same id into the hand, or closes build mode.</summary>
        private void TakeNextOrExit(PlacedObjectData def)
        {
            if (Supply != null && Supply.TakeOwned(def.Id, out _heldPacked))
            {
                IsHolding = true;
                ShowHeldView();
                return;
            }
            HideHeldView();
            _heldVariant = null;
            ExitBuildMode();
        }

        /// <summary>Puts a held item back into the inventory (cancel, exit, or switching item).</summary>
        private void ReturnHeld()
        {
            if (!IsHolding) return;
            IsHolding = false;
            if (Supply != null && CurrentItem != null)
            {
                if (_heldPacked) Supply.AddPacked(CurrentItem.Id);
                else             Supply.AddOwned(CurrentItem.Id);
            }
            _heldPacked  = false;
            _heldVariant = null;
            HideHeldView();
        }

        /// <summary>Shows the held item in front of the camera, creating the viewmodel on first use.</summary>
        private void ShowHeldView()
        {
            if (_heldView == null) _heldView = GetComponent<HeldItemView>();
            if (_heldView == null) _heldView = gameObject.AddComponent<HeldItemView>();
            _heldView.Show(FurnitureFactory.Prefabs != null ? FurnitureFactory.Prefabs.Get(CurrentItem.Id) : null);
        }

        private void HideHeldView()
        {
            if (_heldView != null) _heldView.Hide();
        }
    }
}
