using System.Collections.Generic;
using UnityEngine;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Core
{
    /// <summary>
    /// Layout migration for pens with pets in them that the shift pushed off the yard. In-game such a pen cannot
    /// be packed (the Remove tool refuses), so the migration moves it to the nearest free valid yard spot instead,
    /// searched in save-data space before anything is placed. With no spot left, its pets move into other saved
    /// pens of their species with room to spare and the emptied pen is packed; any pets still without a stall stay
    /// in their pen, which goes back to its original unshifted cells. No pet is ever dropped.
    /// </summary>
    internal static partial class SaveLayoutMigration
    {
        /// <summary>True when <paramref name="item"/> is a pen (per <paramref name="def"/>) with at least one pet in it.</summary>
        private static bool IsOccupiedPen(SaveData.PlacedItem item, PlacedObjectData def) =>
            def != null && def.Type == BuildCatalog.PenType && item.pets != null && item.pets.Count > 0;

        /// <summary>
        /// Moves <paramref name="pen"/> to the free valid yard spot nearest its shifted cell, searching outward ring
        /// by ring (as far as the yard's longer side) and taking the closest fitting cell of the first ring with one.
        /// </summary>
        /// <returns>True when a spot was found and the pen's cells now point at it.</returns>
        private static bool TryRelocatePen(SaveData data, ShopLayout layout, SaveData.PlacedItem pen, PlacedObjectData def)
        {
            RectInt yard    = layout.LotStageCells(ShopLayout.FullYardLotStage);
            Vector2Int size = BuildMode.FootprintSize(def, pen.footprintRotated);
            var from        = new Vector2Int(pen.cellX, pen.cellY);
            int reach       = Mathf.Max(yard.width, yard.height);
            for (int ring = 0; ring <= reach; ring++)
            {
                Vector2Int? best = null;
                int bestDistance = int.MaxValue;
                for (int dx = -ring; dx <= ring; dx++)
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    int distance = dx * dx + dy * dy;
                    var cell = from + new Vector2Int(dx, dy);
                    if (distance >= bestDistance || !PenFits(data, layout, pen, new RectInt(cell, size))) continue;
                    best = cell;
                    bestDistance = distance;
                }
                if (!best.HasValue) continue;
                pen.cellX = best.Value.x;
                pen.cellY = best.Value.y;
                Debug.Log($"[SaveLoad] layout migration: action=relocate-pen id='{pen.catalogId}' " +
                          $"from=({from.x},{from.y}) to=({pen.cellX},{pen.cellY}) pets={pen.pets.Count}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// The pen rule (<see cref="ShopLayout.CanPlacePen"/>) applied to save data: <paramref name="footprint"/>
        /// lies wholly in the full yard, off the room and its wall ring, off both doorways, and overlaps no saved
        /// placed piece other than <paramref name="self"/>.
        /// </summary>
        private static bool PenFits(SaveData data, ShopLayout layout, SaveData.PlacedItem self, RectInt footprint)
        {
            if (!Inside(layout.LotStageCells(ShopLayout.FullYardLotStage), footprint)) return false;
            if (layout.IsInsideShop(footprint.position, footprint.size)) return false;
            if (layout.RoomWallCells().Overlaps(footprint) || layout.OverlapsDoorway(footprint)) return false;
            foreach (var other in data.PlacedObjects)
            {
                if (other == self) continue;
                var otherDef = BuildCatalog.Get(other.catalogId);
                if (otherDef != null && Footprint(other, otherDef).Overlaps(footprint)) return false;
            }
            return true;
        }

        /// <summary>
        /// Settles an occupied pen with no free yard spot: its pets move into other saved pens of their species
        /// that have room; an emptied pen is packed, while a pen with pets still left keeps them and goes back to
        /// its original unshifted cells. When those cells are not a valid pen spot either, the pen is kept there
        /// all the same (its pets must not be dropped) and a warning reports it.
        /// </summary>
        private static void SettleHomelessPen(SaveData data, ShopLayout layout, SaveData.PlacedItem pen, LayoutMigrationReport report)
        {
            var def = BuildCatalog.Get(pen.catalogId);
            int moved = RehomePets(data, pen);
            if (report != null) report.RehomedPets += moved;
            if (pen.pets.Count == 0) { Evict(data, pen, def, report); return; }

            Vector2Int shift = ShopLayout.LegacyLayoutCellShift;
            pen.cellX -= shift.x;
            pen.cellY -= shift.y;
            data.PlacedObjects.Add(pen);
            report?.KeptUnshifted.Add(pen);
            bool valid = PenFits(data, layout, pen, Footprint(pen, def));
            string message = $"[SaveLoad] layout migration: action=keep-pen-unshifted id='{pen.catalogId}' " +
                             $"cell=({pen.cellX},{pen.cellY}) pets={pen.pets.Count} rehomed={moved} valid={valid}";
            if (valid) Debug.Log(message);
            else Debug.LogWarning(message);
        }

        /// <summary>
        /// Moves as many of <paramref name="pen"/>'s pets as fit into the other saved pens holding their species,
        /// up to <see cref="BuildCatalog.PenCapacity"/> each.
        /// </summary>
        /// <returns>How many pets moved.</returns>
        private static int RehomePets(SaveData data, SaveData.PlacedItem pen)
        {
            int moved = 0;
            foreach (var pet in new List<SaveData.PetSaveData>(pen.pets))
            {
                var target = data.PlacedObjects.Find(other => other != pen && HasStallFor(other, pet));
                if (target == null) continue;
                pen.pets.Remove(pet);
                target.pets.Add(pet);
                moved++;
            }
            return moved;
        }

        /// <summary>True when <paramref name="item"/> is a pen of <paramref name="pet"/>'s species with a free stall.</summary>
        private static bool HasStallFor(SaveData.PlacedItem item, SaveData.PetSaveData pet)
        {
            var def = BuildCatalog.Get(item.catalogId);
            if (def == null || def.Type != BuildCatalog.PenType) return false;
            item.pets ??= new List<SaveData.PetSaveData>();
            if (item.pets.Count >= BuildCatalog.PenCapacity) return false;
            return System.Enum.TryParse(pet.species, out Pet.Species species) &&
                   species == BuildCatalog.PenSpecies(item.catalogId, item.variant);
        }
    }
}
