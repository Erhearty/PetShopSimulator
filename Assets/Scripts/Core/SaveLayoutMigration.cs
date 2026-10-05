using System.Collections.Generic;
using UnityEngine;
using PetShop.Shop;

namespace PetShop.Core
{
    /// <summary>
    /// Moves a save's grid cells onto the current shop layout. Layout 0 stood the room back from the street;
    /// layout 1 moved it forward by <see cref="ShopLayout.LegacyLayoutCellShift"/>, so every saved piece —
    /// furniture, pens and wall pieces alike — moves with it. A piece the shift pushes off the yard (onto the
    /// pavement or street) is not placed: it goes back into the furniture inventory, as the Remove tool would —
    /// except a pen with pets in it, which the Remove tool refuses to pack: that pen is moved to the nearest free
    /// spot in the yard instead (see SaveLayoutMigration.Pens.cs).
    /// </summary>
    internal static partial class SaveLayoutMigration
    {
        /// <summary>
        /// Shifts every placed item in <paramref name="data"/> by the room's cell delta when the save predates
        /// the current layout, then stamps it current. Does nothing for a current save. When
        /// <paramref name="layout"/> is given, every shifted item whose footprint is not wholly inside the full
        /// yard is settled: an occupied pen is moved to the nearest free yard spot, anything else is taken out of
        /// the placed list and returned to the saved furniture inventory — pens as packed units, everything else
        /// as owned ones. What happened is recorded in <paramref name="report"/> so the caller can settle the
        /// packed pieces' contents (shelf stock) and tell the player.
        /// </summary>
        /// <returns>True when the cells were shifted.</returns>
        public static bool Apply(SaveData data, ShopLayout layout = null, LayoutMigrationReport report = null)
        {
            if (data == null || data.layoutVersion >= ShopLayout.CurrentLayoutVersion) return false;
            Vector2Int shift = ShopLayout.LegacyLayoutCellShift;
            foreach (var item in data.PlacedObjects)
            {
                item.cellX += shift.x;
                item.cellY += shift.y;
            }
            if (layout != null) SettleOutsideYard(data, layout, report);
            data.layoutVersion = ShopLayout.CurrentLayoutVersion;
            return true;
        }

        /// <summary>
        /// Settles every placed item of <paramref name="data"/> whose footprint leaves the full yard: pens with
        /// pets are relocated in the yard (or, failing that, emptied into other pens), everything else goes into
        /// the saved furniture inventory. Items with an unknown catalogue id are left for the loader, which skips them.
        /// </summary>
        private static void SettleOutsideYard(SaveData data, ShopLayout layout, LayoutMigrationReport report)
        {
            RectInt yard = layout.LotStageCells(ShopLayout.FullYardLotStage);
            var outside = data.PlacedObjects.FindAll(item => IsOutside(item, yard));
            if (outside.Count == 0) return;
            data.PlacedObjects.RemoveAll(outside.Contains);

            var homeless = new List<SaveData.PlacedItem>();
            foreach (var item in outside)
            {
                var def = BuildCatalog.Get(item.catalogId);
                if (!IsOccupiedPen(item, def)) { Evict(data, item, def, report); continue; }
                if (TryRelocatePen(data, layout, item, def))
                {
                    data.PlacedObjects.Add(item);
                    report?.Relocated.Add(item);
                }
                else homeless.Add(item);
            }
            foreach (var pen in homeless) SettleHomelessPen(data, layout, pen, report);
        }

        /// <summary>True when <paramref name="item"/> has a known catalogue id and its footprint leaves <paramref name="yard"/>.</summary>
        private static bool IsOutside(SaveData.PlacedItem item, RectInt yard)
        {
            var def = BuildCatalog.Get(item.catalogId);
            return def != null && !Inside(yard, Footprint(item, def));
        }

        /// <summary>Packs <paramref name="item"/> into the saved furniture inventory and records it as evicted.</summary>
        private static void Evict(SaveData data, SaveData.PlacedItem item, PlacedObjectData def, LayoutMigrationReport report)
        {
            ReturnToInventory(data, def);
            report?.Evicted.Add(item);
        }

        /// <summary>The saved cells <paramref name="item"/> (of catalogue entry <paramref name="def"/>) covers.</summary>
        private static RectInt Footprint(SaveData.PlacedItem item, PlacedObjectData def) =>
            new(new Vector2Int(item.cellX, item.cellY), BuildMode.FootprintSize(def, item.footprintRotated));

        /// <summary>True when every cell of <paramref name="footprint"/> lies inside <paramref name="area"/>.</summary>
        private static bool Inside(RectInt area, RectInt footprint) =>
            footprint.xMin >= area.xMin && footprint.yMin >= area.yMin &&
            footprint.xMax <= area.xMax && footprint.yMax <= area.yMax;

        /// <summary>
        /// Adds one <paramref name="def"/> to the saved furniture inventory the way removing it would
        /// (<c>BuildMode.SettleRemoval</c>): a pen counts as packed away, anything else as owned.
        /// </summary>
        private static void ReturnToInventory(SaveData data, PlacedObjectData def)
        {
            data.FurnitureInventory ??= new List<SaveData.StockEntry>();
            Increment(data.FurnitureInventory, def.Id);
            if (def.Type != BuildCatalog.PenType) return;
            data.PackedFurniture ??= new List<SaveData.StockEntry>();
            Increment(data.PackedFurniture, def.Id);
        }

        /// <summary>Raises <paramref name="id"/>'s quantity in <paramref name="entries"/> by one, adding it if absent.</summary>
        private static void Increment(List<SaveData.StockEntry> entries, string id)
        {
            var entry = entries.Find(e => e != null && e.id == id);
            if (entry != null) entry.qty++;
            else entries.Add(new SaveData.StockEntry { id = id, qty = 1 });
        }

        /// <summary>
        /// One-time back-door migration for a save whose room walls were laid before the back door existed: each
        /// plain wall still standing on the back door's ring cells becomes a doorway piece. Any other piece — or a
        /// gap — there is the player's edit and is left alone. Marks the save migrated either way.
        /// </summary>
        /// <returns>How many walls were swapped for doorway pieces.</returns>
        public static int SeedBackDoor(SaveData data, ShopLayout layout)
        {
            if (data == null || layout == null || data.backDoorSeeded || !data.roomWallsSeeded) return 0;
            RectInt back = layout.BackDoorRingCells();
            int swapped = 0;
            foreach (var item in data.PlacedObjects)
            {
                if (item.catalogId != BuildCatalog.Wall || !back.Contains(new Vector2Int(item.cellX, item.cellY))) continue;
                item.catalogId = BuildCatalog.WallDoor;
                swapped++;
            }
            data.backDoorSeeded = true;
            return swapped;
        }
    }
}
