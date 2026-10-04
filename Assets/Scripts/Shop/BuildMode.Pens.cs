using UnityEngine;
using PetShop.Pets;
using PetShop.Progression;

namespace PetShop.Shop
{
    /// <summary>
    /// Pen rules for build mode: pens belong in the yard, never in the shop room, and a freshly
    /// bought per-species pen arrives with its adult breeding pair. A pen packed away and placed
    /// again brings nothing new.
    /// </summary>
    public partial class BuildMode
    {
        /// <summary>Notice shown when a pen is aimed at the shop room.</summary>
        public const string PenInShopNotice = "Pens go in the yard";

        /// <summary>
        /// The scene layout, used to let pens stand anywhere in the yard but never in the shop room
        /// or doorway. Null skips that rule and pens follow the lot-stage floor like other items.
        /// </summary>
        public ShopLayout Layout { get; set; }

        /// <summary>True while the held item was packed away after being placed, not delivered fresh.</summary>
        private bool _heldPacked;

        /// <summary>True for the legacy species-picked <c>pet_pen</c>, the only pen whose species Q cycles.</summary>
        private static bool IsLegacyPen(PlacedObjectData def) =>
            def != null && def.Id == BuildCatalog.PetPen;

        /// <summary>Notice shown when a non-pen item does not fit where it is aimed.</summary>
        private const string CantBuildNotice = "Can't build there.";

        /// <summary>
        /// True when <paramref name="def"/> is a pen placed by the yard rule
        /// (<see cref="ShopLayout.CanPlacePen"/>) rather than the lot-stage floor rule. Needs a layout.
        /// </summary>
        private bool UsesYardRule(PlacedObjectData def) =>
            def != null && def.Type == PenType && Layout != null;

        /// <summary>
        /// True when <paramref name="def"/> fits at <paramref name="cell"/>: pens anywhere free in the
        /// yard outside the shop room and doorway, everything else on free buildable floor.
        /// </summary>
        private bool CanPlaceItem(PlacedObjectData def, Vector2Int cell, Vector2Int size) =>
            UsesYardRule(def) ? Layout.CanPlacePen(GridManager, cell, size) : GridManager.CanPlace(cell, size);

        /// <summary>
        /// Refuses <paramref name="def"/> at <paramref name="cell"/> when it does not fit, posting the
        /// yard notice for pens and the generic notice otherwise; true when refused.
        /// </summary>
        private bool RefusePlacement(PlacedObjectData def, Vector2Int cell, Vector2Int size)
        {
            if (CanPlaceItem(def, cell, size)) return false;
            OnBuildMessage.Invoke(UsesYardRule(def) ? PenInShopNotice : CantBuildNotice);
            return true;
        }

        /// <summary>
        /// Lays floor under a pen's yard footprint so <see cref="GridManager.PlaceObject"/> accepts it;
        /// other items keep the lot-stage floor untouched.
        /// </summary>
        private void PrepareFloor(PlacedObjectData def, Vector2Int cell, Vector2Int size)
        {
            if (UsesYardRule(def)) GridManager.EnsureFloor(cell, size);
        }

        /// <summary>
        /// Undoes <see cref="PrepareFloor"/> after a pen is removed: floor outside the unlocked lot
        /// goes again, so the pen never unlocks ground for other furniture.
        /// </summary>
        private void ReleaseFloor(PlacedObjectData def, Vector2Int root, Vector2Int size)
        {
            if (UsesYardRule(def)) Layout.ReleasePenFloor(GridManager, root, size);
        }

        /// <summary>
        /// Stocks a newly placed per-species pen with its breeding pair of grown adults. Does
        /// nothing for other items or the legacy pen, whose price never included animals.
        /// </summary>
        private static void AddStarterPair(GameObject go, PlacedObjectData def)
        {
            if (go == null || ProgressionRules.PenSpeciesFor(def?.Id) == null) return;
            var pen = go.GetComponent<PetPen>();
            if (pen == null) return;
            for (int i = 0; i < BuildCatalog.PenStarterPairSize; i++)
                pen.AddPet(NewAdult(pen.PenSpecies));
        }

        /// <summary>A random fully grown <paramref name="species"/>.</summary>
        private static Pet NewAdult(Pet.Species species)
        {
            var pet = BreedingSystem.GenerateRandom(species);
            pet.growthStage = Pet.GrowthStage.Adult;
            pet.ageDays     = Mathf.Max(pet.ageDays, pet.daysToMature);
            return pet;
        }
    }
}
