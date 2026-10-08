using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Free (unsnapped) placement: furniture and decor stand at any floor position and any yaw. They
    /// are kept off the grid's cell occupancy — walls, doorways and the roof still live on the grid —
    /// and validity comes from the floor cells under the footprint plus a physics overlap check
    /// against already placed pieces.
    /// </summary>
    public partial class BuildMode
    {
        /// <summary>Degrees one mouse-wheel notch turns a freely placed item.</summary>
        public const float FineRotationStep = 15f;

        /// <summary>How far the overlap box is pulled in from the footprint, so neighbours may touch.</summary>
        private const float OverlapInset = 0.1f;
        private const float MinOverlapHalf = 0.05f;
        private const float OverlapCentreY = 1f;
        private const float OverlapHalfHeight = 0.85f;
        /// <summary>How far an edge sample is pulled in from the footprint edge when finding covered cells.</summary>
        private const float CellEdgeInset = 0.05f;
        private const float CellSampleStep = GridManager.CellSize * 0.5f;
        /// <summary>Gap between a counter's back face and the cashier's standing spot.</summary>
        private const float CashierGap = 0.5f;
        private const float CashierEdgeMargin = 0.3f;
        private const float CashierSpotRadius = 0.35f;
        private const float CashierSpotHeight = 0.9f;
        private const int   CashierSpots = 1;

        /// <summary>How far a wall piece's overlap box is shortened at each end, so walls may meet to form corners.</summary>
        private const float WallOverlapEndInset = 0.3f;
        /// <summary>Thinning of a wall piece's overlap box, so parallel neighbours may touch.</summary>
        private const float WallOverlapThinning = 0.05f;
        /// <summary>Extra reach beside a wall piece when asking whether a point lies on it (walls are thin).</summary>
        private const float WallContainsMargin = 0.15f;

        /// <summary>True for items placed freely: everything, building pieces (walls, fences, doorways) included.</summary>
        public static bool IsFreeItem(PlacedObjectData def) => def != null;

        /// <summary>Half the footprint of a free piece along its own X and Z; a wall piece is a thin slab.</summary>
        internal static Vector2 FreeHalfExtents(PlacedObjectData def, Vector2Int size)
        {
            float hx = size.x * GridManager.CellSize * 0.5f;
            float hz = def != null && BuildCatalog.IsBuildingPiece(def.Id)
                ? ShopLayout.WallPieceThickness * 0.5f
                : size.y * GridManager.CellSize * 0.5f;
            return new Vector2(hx, hz);
        }

        /// <summary>
        /// The grid cells a free piece covers: its footprint's cells, or for a thin building piece the cells its
        /// centre line runs through.
        /// </summary>
        internal static List<Vector2Int> FreeCellsFor(PlacedObjectData def, Vector3 pos, float yaw)
        {
            if (def == null) return FreeCells(pos, yaw, Vector2Int.one);
            if (!BuildCatalog.IsBuildingPiece(def.Id)) return FreeCells(pos, yaw, def.Size);

            var cells = new List<Vector2Int>();
            float hx = Mathf.Max(MinOverlapHalf, def.Size.x * GridManager.CellSize * 0.5f - CellEdgeInset);
            int n = Mathf.Max(1, Mathf.CeilToInt(2f * hx / CellSampleStep));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i <= n; i++)
            {
                Vector3 w = pos + rot * new Vector3(-hx + 2f * hx * i / n, 0f, 0f);
                var cell = new Vector2Int(Mathf.FloorToInt(w.x / GridManager.CellSize),
                                          Mathf.FloorToInt(w.z / GridManager.CellSize));
                if (!cells.Contains(cell)) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>Turns a freely placed item by <paramref name="direction"/> fine steps (+1 / -1).</summary>
        public void RotateFine(int direction)
        {
            _rotation = Mathf.Repeat(_rotation + direction * FineRotationStep, FullTurn);
        }

        /// <summary>
        /// The grid cells under a footprint of <paramref name="size"/> cells centred on <paramref name="pos"/> and
        /// turned <paramref name="yaw"/> degrees.
        /// </summary>
        internal static List<Vector2Int> FreeCells(Vector3 pos, float yaw, Vector2Int size)
        {
            var cells = new List<Vector2Int>();
            float hx = Mathf.Max(MinOverlapHalf, size.x * GridManager.CellSize * 0.5f - CellEdgeInset);
            float hz = Mathf.Max(MinOverlapHalf, size.y * GridManager.CellSize * 0.5f - CellEdgeInset);
            int nx = Mathf.Max(1, Mathf.CeilToInt(2f * hx / CellSampleStep));
            int nz = Mathf.Max(1, Mathf.CeilToInt(2f * hz / CellSampleStep));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                var local = new Vector3(-hx + 2f * hx * i / nx, 0f, -hz + 2f * hz * j / nz);
                Vector3 w = pos + rot * local;
                var cell = new Vector2Int(Mathf.FloorToInt(w.x / GridManager.CellSize),
                                          Mathf.FloorToInt(w.z / GridManager.CellSize));
                if (!cells.Contains(cell)) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>True when the world point lies inside a free entry's turned footprint.</summary>
        internal static bool FreeContains(GridEntry entry, Vector3 point)
        {
            if (entry == null || !entry.IsFree) return false;
            Vector3 local = Quaternion.Euler(0f, -entry.Yaw, 0f) * (point - entry.Position);
            Vector2 half = FreeHalfExtents(entry.Data, entry.Size);
            bool thin = entry.Data != null && BuildCatalog.IsBuildingPiece(entry.Data.Id);
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.z) <= half.y + (thin ? WallContainsMargin : 0f);
        }

        /// <summary>
        /// True when <paramref name="def"/> fits at the free position <paramref name="pos"/> turned
        /// <paramref name="yaw"/> degrees: floor (or yard, for pens) under all of it, off the wall ring and
        /// doorway, clear of other pieces — and, for a counter, with room behind it for its cashier.
        /// </summary>
        public bool CanPlaceFree(PlacedObjectData def, Vector3 pos, float yaw) =>
            FitsFree(def, pos, yaw) && !BlocksCashierFree(def, pos, yaw);

        private bool FitsFree(PlacedObjectData def, Vector3 pos, float yaw)
        {
            if (def == null || GridManager == null) return false;
            bool structure = Layout != null && BuildCatalog.IsBuildingPiece(def.Id);
            RectInt yard = structure ? Layout.LotStageCells(ShopLayout.FullYardLotStage) : default;
            foreach (var cell in FreeCellsFor(def, pos, yaw))
            {
                if (Layout != null)
                {
                    if (!Layout.AllowsOnRoomWallRing(def, cell, Vector2Int.one)) return false;
                    if (structure)
                    {
                        // Building pieces go anywhere in the yard, needing no floor, but keep the doorway clear.
                        if (!yard.Contains(cell)) return false;
                        if (Layout.IsDoorwayCell(cell) && !Layout.IsRoomWallCell(cell)) return false;
                        continue;
                    }
                    if (Layout.OverlapsDoorway(new RectInt(cell, Vector2Int.one))) return false;
                }
                if (UsesYardRule(def))
                {
                    if (!Layout.CanPlacePen(GridManager, cell, Vector2Int.one)) return false;
                }
                else if (!GridManager.HasFloor(cell)) return false;
            }
            return !OverlapsPlaced(def, pos, yaw);
        }

        /// <summary>True when the turned footprint overlaps the collider of any placed piece.</summary>
        private static bool OverlapsPlaced(PlacedObjectData def, Vector3 pos, float yaw)
        {
            Physics.SyncTransforms();
            float hx, hz;
            if (BuildCatalog.IsBuildingPiece(def.Id))
            {
                // Thin slab, shortened at both ends so a wall may meet another to form a corner.
                Vector2 half = FreeHalfExtents(def, def.Size);
                hx = Mathf.Max(MinOverlapHalf, half.x - WallOverlapEndInset);
                hz = Mathf.Max(MinOverlapHalf, half.y - WallOverlapThinning);
            }
            else
            {
                hx = Mathf.Max(MinOverlapHalf, def.Size.x * GridManager.CellSize * 0.5f - OverlapInset);
                hz = Mathf.Max(MinOverlapHalf, def.Size.y * GridManager.CellSize * 0.5f - OverlapInset);
            }
            return Physics.CheckBox(new Vector3(pos.x, OverlapCentreY, pos.z),
                                    new Vector3(hx, OverlapHalfHeight, hz), Quaternion.Euler(0f, yaw, 0f),
                                    GameLayers.InteractMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// True for a counter whose cashier would have nowhere to stand: the spots just past its back face
        /// (opposite its customer side, the prefab's -Z) must be free floor off the wall ring.
        /// </summary>
        private bool BlocksCashierFree(PlacedObjectData def, Vector3 pos, float yaw)
        {
            if (def == null || def.Id != BuildCatalog.Counter || GridManager == null) return false;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 back  = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            float depth = def.Size.y * GridManager.CellSize * 0.5f;
            float width = def.Size.x * GridManager.CellSize * 0.5f;
            for (int i = -CashierSpots; i <= CashierSpots; i++)
            {
                Vector3 p = pos + back * (depth + CashierGap) + right * (i * Mathf.Max(0f, width - CashierEdgeMargin));
                var cell = new Vector2Int(Mathf.FloorToInt(p.x / GridManager.CellSize),
                                          Mathf.FloorToInt(p.z / GridManager.CellSize));
                if (Layout != null && Layout.IsRoomWallCell(cell)) return true;
                if (!GridManager.HasFloor(cell)) return true;
                if (Physics.CheckSphere(p + Vector3.up * CashierSpotHeight, CashierSpotRadius,
                                        GameLayers.InteractMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        /// <summary>Refuses a free placement that does not fit, posting the matching notice; true when refused.</summary>
        private bool RefuseFree(PlacedObjectData def, Vector3 pos, float yaw)
        {
            if (CanPlaceFree(def, pos, yaw)) return false;
            bool fits = FitsFree(def, pos, yaw);
            OnBuildMessage.Invoke(UsesYardRule(def) ? PenInShopNotice
                                : fits              ? CounterNoRoomNotice : CantBuildNotice);
            return true;
        }

        /// <summary>Lays yard floor under a free pen so its cells are buildable; other items keep the lot floor.</summary>
        internal void PrepareFloorFree(PlacedObjectData def, Vector3 pos, float yaw)
        {
            if (!UsesYardRule(def)) return;
            foreach (var cell in FreeCells(pos, yaw, def.Size)) GridManager.EnsureFloor(cell, Vector2Int.one);
        }

        /// <summary>Takes back the yard floor a removed free pen laid, except under other free pens.</summary>
        private void ReleaseFloorFree(GridEntry entry)
        {
            if (!UsesYardRule(entry.Data)) return;
            foreach (var cell in FreeCells(entry.Position, entry.Yaw, entry.Data.Size))
            {
                bool shared = false;
                foreach (var other in GridManager.FreePieces)
                {
                    if (other == entry || !UsesYardRule(other.Data)) continue;
                    if (FreeCells(other.Position, other.Yaw, other.Data.Size).Contains(cell)) { shared = true; break; }
                }
                if (!shared) Layout.ReleasePenFloor(GridManager, cell, Vector2Int.one);
            }
        }

        /// <summary>
        /// Places furniture at an exact world position and yaw, off the grid's cell occupancy. Shared by the
        /// player and save loading. Does not validate; callers check <see cref="CanPlaceFree"/>.
        /// </summary>
        public GameObject PlaceFree(Vector3 position, PlacedObjectData def, string variant, float yaw, bool charge)
        {
            if (def == null || GridManager == null) return null;
            position.y = 0f;
            var go = FurnitureFactory.SpawnFree(def, position, variant, ObjectRoot, yaw);
            if (go == null) return null;

            if (charge && Shop != null) Shop.ChangeBalance(-def.Cost, $"Build {def.DisplayName}");

            var entry = GridManager.PlaceFree(def, position, yaw);
            entry.Instance = go;
            entry.Variant  = variant;

            OnFurnitureSpawned.Invoke(go);
            OnObjectPlacedVisually.Invoke(entry.Root, def);
            return go;
        }

        /// <summary>Paid placement of a free item at the aimed floor point.</summary>
        private void TryPlaceFree(Vector3 pos)
        {
            if (IsHolding) { PlaceHeldFree(pos); return; }

            var def = CurrentItem;
            if (RefuseFree(def, pos, _rotation)) return;
            if (Shop != null && Shop.Balance < def.Cost)
            {
                OnBuildMessage.Invoke(PetShop.Localization.Loc.F("build.no_money", def.LocalizedName, def.Cost));
                return;
            }

            PrepareFloorFree(def, pos, _rotation);
            string variant = IsLegacyPen(def) ? CurrentPenVariant() : null;
            var go = PlaceFree(pos, def, variant, _rotation, charge: true);
            AddStarterPair(go, def);
            if (go != null) OnFurniturePlacedByPlayer.Invoke(go);
        }

        /// <summary>
        /// Places the held item at <paramref name="pos"/> for free, consuming it; the next owned unit is
        /// taken into the hand, or build mode closes.
        /// </summary>
        public GameObject PlaceHeldFree(Vector3 pos)
        {
            if (!IsHolding || CurrentItem == null) return null;
            var def = CurrentItem;
            if (RefuseFree(def, pos, _rotation)) return null;
            PrepareFloorFree(def, pos, _rotation);

            var go = PlaceFree(pos, def, HeldVariant(), _rotation, charge: false);
            if (go == null) return null;
            if (!_heldPacked) AddStarterPair(go, def);
            OnFurniturePlacedByPlayer.Invoke(go);
            IsHolding = false;
            TakeNextOrExit(def);
            return go;
        }

        /// <summary>Moves the free ghost to the aimed point, turned by the current rotation, and colours it by validity.</summary>
        private void UpdateFreeGhost(Vector3 worldPos)
        {
            worldPos.y = 0f;
            _hoverCell  = GridManager.WorldToGrid(worldPos);
            _hoverValid = CanPlaceFree(CurrentItem, worldPos, _rotation)
                       && (IsHolding || Shop == null || Shop.Balance >= CurrentItem.Cost);

            _ghost.transform.position    = worldPos + Vector3.up * _ghostLift;
            _ghost.transform.eulerAngles = new Vector3(0f, _rotation, 0f);

            if (_ghostMat != null)
                MaterialFactory.SetColor(_ghostMat, _hoverValid ? ValidColor : InvalidColor);
        }

        /// <summary>The free piece whose turned footprint contains <paramref name="world"/>, or null.</summary>
        private GridEntry FreeEntryContaining(Vector3 world)
        {
            var pieces = GridManager.FreePieces;
            for (int i = pieces.Count - 1; i >= 0; i--)
                if (FreeContains(pieces[i], world)) return pieces[i];
            return null;
        }

        /// <summary>The free piece whose centre lies in <paramref name="cell"/>, or null.</summary>
        private GridEntry FreeEntryRootedAt(Vector2Int cell)
        {
            foreach (var entry in GridManager.FreePieces)
                if (entry.Root == cell) return entry;
            return null;
        }

        /// <summary>True when <paramref name="ray"/> hits a collider of a free piece.</summary>
        private bool TryFreePieceAlongRay(Ray ray, out GridEntry found)
        {
            found = null;
            float reach = Cursor.lockState == CursorLockMode.Locked || _cam == null
                ? MaxPlacementDistance : _cam.farClipPlane;
            if (!Physics.Raycast(ray, out var hit, reach, GameLayers.InteractMask, QueryTriggerInteraction.Ignore))
                return false;
            for (var t = hit.collider.transform; t != null; t = t.parent)
                foreach (var entry in GridManager.FreePieces)
                    if (entry.Instance != null && entry.Instance == t.gameObject) { found = entry; return true; }
            return false;
        }

        /// <summary>Packs away or sells a free piece, as <see cref="RemoveAtWorldPos"/> does for a grid piece.</summary>
        private void RemoveFreeEntry(GridEntry entry)
        {
            if (entry == null) return;
            if (Supply != null && PenHasPets(entry.Instance))
            {
                OnBuildMessage.Invoke(PenHasPetsNotice);
                return;
            }

            var def      = entry.Data;
            var instance = entry.Instance;
            if (!GridManager.RemoveFree(entry)) { OnBuildMessage.Invoke(CannotRemoveNotice); return; }
            if (Supply != null) ReturnShelfStock(instance);
            ReleaseFloorFree(entry);

            if (instance != null)
            {
                OnFurnitureDespawning.Invoke(instance);
                PrefabPreview.DestroySafe(instance);
            }
            string message = SettleRemoval(def);
            OnObjectRemovedVisually.Invoke(entry.Root);
            OnBuildMessage.Invoke(message);
        }
    }
}
