using UnityEngine;

namespace PetShop.Shop
{
    /// <summary>
    /// Counter rule for build mode: a cashier stands behind the counter, on the side away from its
    /// customers, so a counter is refused where the cells straight behind its back face leave nowhere
    /// to stand — the room's wall ring, other furniture, or ground with no floor.
    /// </summary>
    public partial class BuildMode
    {
        /// <summary>Notice shown when a counter is aimed where its cashier would have no room behind it.</summary>
        public const string CounterNoRoomNotice = "Leave room behind the counter for the cashier";

        /// <summary>
        /// True when <paramref name="def"/> is a counter rooted at <paramref name="cell"/> with footprint
        /// <paramref name="size"/>, turned <paramref name="yaw"/> degrees, whose row (or column) of cells
        /// directly behind its back face — opposite its customer side, which is the prefab's +Z — has any
        /// cell on the room's wall ring, occupied, or off the floor. False for everything else.
        /// </summary>
        private bool BlocksCashier(PlacedObjectData def, Vector2Int cell, Vector2Int size, float yaw)
        {
            if (def == null || def.Id != BuildCatalog.Counter || GridManager == null) return false;
            Vector2Int back = -FacingStep(yaw);
            var footprint = new RectInt(cell, size);

            // The row/column just past the back face, spanning the footprint's width along that face.
            int xMin = back.x > 0 ? footprint.xMax : back.x < 0 ? footprint.xMin - 1 : footprint.xMin;
            int xMax = back.x != 0 ? xMin + 1 : footprint.xMax;
            int yMin = back.y > 0 ? footprint.yMax : back.y < 0 ? footprint.yMin - 1 : footprint.yMin;
            int yMax = back.y != 0 ? yMin + 1 : footprint.yMax;

            for (int x = xMin; x < xMax; x++)
            for (int y = yMin; y < yMax; y++)
            {
                var behind = new Vector2Int(x, y);
                if (Layout != null && Layout.IsRoomWallCell(behind)) return true;
                if (GridManager.TryGetObject(behind, out _) || !GridManager.HasFloor(behind)) return true;
            }
            return false;
        }

        /// <summary>
        /// The grid step an item's customer side (+Z in its prefab) faces once turned <paramref name="yaw"/>
        /// degrees, rounded to the nearest quarter turn: 0° → +Y, 90° → +X, 180° → −Y, 270° → −X.
        /// </summary>
        private static Vector2Int FacingStep(float yaw)
        {
            int quarters = Mathf.RoundToInt(Mathf.Repeat(yaw, FullTurn) / QuarterTurn) % QuartersPerTurn;
            return quarters switch
            {
                1 => Vector2Int.right,
                2 => Vector2Int.down,
                3 => Vector2Int.left,
                _ => Vector2Int.up,
            };
        }
    }
}
