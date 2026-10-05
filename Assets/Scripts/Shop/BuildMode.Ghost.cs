using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>The translucent placement preview: a copy of the item's prefab, or a box as fallback.</summary>
    public partial class BuildMode
    {
        /// <summary>Lift above the floor so the ghost never z-fights it.</summary>
        private const float GhostFloorClearance = 0.02f;
        private const float WallGhostDepth  = 0.24f;
        private const float FenceGhostHeight = 1.05f;
        private const float WallGhostHeight  = 3.2f;
        private const float PenGhostHeight   = 0.7f;
        private const float ItemGhostHeight  = 1.5f;
        private const float BoxGhostInset    = 0.9f;
        /// <summary>Half of a box's height: lifts its centre so the bottom sits on the floor.</summary>
        private const float HalfExtent       = 0.5f;
        /// <summary>Thickness of the Remove tool's red cell highlight.</summary>
        private const float RemoveGhostHeight = 0.1f;

        private GameObject _ghost;
        private Material   _ghostMat;
        private float      _ghostHeight = ItemGhostHeight;
        private float      _ghostLift;

        /// <summary>The live placement preview, or null when build mode is closed.</summary>
        public GameObject Ghost => _ghost;

        private void CreateGhost()
        {
            DestroyGhost();
            _ghostMat = MaterialFactory.CreateTransparent("ghost", ValidColor);

            var prefab = FurnitureFactory.Prefabs != null ? FurnitureFactory.Prefabs.Get(CurrentItem.Id) : null;
            _ghost = prefab != null ? CreatePrefabGhost(prefab) : CreateBoxGhost();
            _ghost.name = "Ghost";
            PrefabPreview.ApplyMaterial(_ghost, _ghostMat);
            PrefabPreview.DisableShadows(_ghost);
            MeshBuilder.SetLayerRecursive(_ghost, GameLayers.Ghost);
        }

        /// <summary>A behaviour-free copy of the real prefab; its pivot is bottom-centre.</summary>
        private GameObject CreatePrefabGhost(GameObject prefab)
        {
            _ghostLift = GhostFloorClearance;
            return PrefabPreview.CreateStripped(prefab, transform);
        }

        /// <summary>Fallback when no prefab is assigned: a box shaped like the item.</summary>
        private GameObject CreateBoxGhost()
        {
            Vector2 footprint = BoxGhostFootprint();
            var box = MeshBuilder.CreateBox(footprint.x, _ghostHeight, footprint.y, null, "Ghost");
            box.transform.SetParent(transform, false);
            foreach (var col in box.GetComponentsInChildren<Collider>(true)) PrefabPreview.DestroySafe(col);
            _ghostLift = _ghostHeight * HalfExtent + GhostFloorClearance;
            return box;
        }

        /// <summary>Width/depth of the fallback box; also sets <see cref="_ghostHeight"/>.</summary>
        private Vector2 BoxGhostFootprint()
        {
            float cs = GridManager.CellSize;

            // The ghost mimics the shape of what is being placed — a wall preview shaped
            // like a shelf tells you nothing about how it will sit against its neighbours.
            if (BuildCatalog.IsBuildingPiece(CurrentItem.Id))
            {
                _ghostHeight = CurrentItem.Id == BuildCatalog.Fence ? FenceGhostHeight : WallGhostHeight;
                return new Vector2(cs, WallGhostDepth);
            }
            _ghostHeight = CurrentItem.Type == PenType ? PenGhostHeight : ItemGhostHeight;
            return new Vector2(CurrentItem.Size.x, CurrentItem.Size.y) * (cs * BoxGhostInset);
        }

        /// <summary>The Remove tool's highlight: a flat red tile over the cell (or piece) under the cursor.</summary>
        private void CreateRemoveGhost()
        {
            DestroyGhost();
            _ghostMat = MaterialFactory.CreateTransparent("remove_ghost", InvalidColor);
            float cs = GridManager.CellSize;
            _ghost = MeshBuilder.CreateBox(cs, RemoveGhostHeight, cs, null, "RemoveGhost");
            _ghost.transform.SetParent(transform, false);
            foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) PrefabPreview.DestroySafe(col);
            PrefabPreview.ApplyMaterial(_ghost, _ghostMat);
            PrefabPreview.DisableShadows(_ghost);
            MeshBuilder.SetLayerRecursive(_ghost, GameLayers.Ghost);
            _ghostLift = RemoveGhostHeight * HalfExtent + GhostFloorClearance;
        }

        /// <summary>Covers the whole footprint of the piece under the cursor, or just the cell when empty.</summary>
        private void UpdateRemoveGhost()
        {
            if (_ghost == null || !RaycastFloor(out var worldPos)) return;

            _hoverCell = GridManager.WorldToGrid(worldPos);
            Vector2Int root = _hoverCell, size = Vector2Int.one;
            _hoverValid = GridManager.TryGetObject(_hoverCell, out var entry);
            if (_hoverValid) { root = entry.Root; size = entry.Size; }

            _ghost.transform.position   = GridManager.FootprintCenter(root, size) + Vector3.up * _ghostLift;
            _ghost.transform.rotation   = Quaternion.identity;
            _ghost.transform.localScale = new Vector3(size.x, 1f, size.y);
        }

        private void DestroyGhost()
        {
            PrefabPreview.DestroySafe(_ghost);
            PrefabPreview.DestroySafe(_ghostMat);
            _ghost = null; _ghostMat = null;
        }

        private void UpdateGhost()
        {
            if (_ghost == null || !RaycastFloor(out var worldPos)) return;

            _hoverCell = GridManager.WorldToGrid(worldPos);
            Vector2Int size = ActiveFootprint;
            _hoverValid = CanPlaceItem(CurrentItem, _hoverCell, size, _rotation)
                       && (IsHolding || Shop == null || Shop.Balance >= CurrentItem.Cost);

            Vector3 centre = GridManager.FootprintCenter(_hoverCell, size);
            _ghost.transform.position    = centre + Vector3.up * _ghostLift;
            _ghost.transform.eulerAngles = new Vector3(0f, _rotation, 0f);

            if (_ghostMat != null)
                MaterialFactory.SetColor(_ghostMat, _hoverValid ? ValidColor : InvalidColor);
        }
    }
}
