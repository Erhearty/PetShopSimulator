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
        public static string PenInShopNotice => PetShop.Localization.Loc.T("build.pen_in_shop");

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
        private static string CantBuildNotice => PetShop.Localization.Loc.T("build.cant_build");

        /// <summary>
        /// True when <paramref name="def"/> is a pen placed by the yard rule
        /// (<see cref="ShopLayout.CanPlacePen"/>) rather than the lot-stage floor rule. Needs a layout.
        /// </summary>
        private bool UsesYardRule(PlacedObjectData def) =>
            def != null && def.Type == PenType && Layout != null;

        /// <summary>
        /// True when <paramref name="def"/> is a building piece (wall, window wall, doorway, fence) placed by the
        /// yard structure rule (<see cref="ShopLayout.CanPlaceStructure"/>) rather than the lot-stage floor rule.
        /// Needs a layout.
        /// </summary>
        private bool UsesStructureRule(PlacedObjectData def) =>
            def != null && Layout != null && BuildCatalog.IsBuildingPiece(def.Id);

        /// <summary>
        /// True when <paramref name="def"/> fits at <paramref name="cell"/>: pens anywhere free in the
        /// yard outside the shop room and doorway, building pieces anywhere free in the yard off the doorway
        /// clearance, everything else on free buildable floor. The room's wall ring takes only building pieces,
        /// and only doorway pieces across the doorway (<see cref="ShopLayout.AllowsOnRoomWallRing"/>). A counter turned
        /// <paramref name="yaw"/> degrees also needs free floor straight behind its back face for its cashier.
        /// </summary>
        public bool CanPlaceItem(PlacedObjectData def, Vector2Int cell, Vector2Int size, float yaw = 0f) =>
            FitsFootprint(def, cell, size) && !BlocksCashier(def, cell, size, yaw);

        /// <summary>
        /// The footprint part of <see cref="CanPlaceItem"/>: the wall-ring, yard, structure and floor rules,
        /// without the counter's room-behind rule.
        /// </summary>
        private bool FitsFootprint(PlacedObjectData def, Vector2Int cell, Vector2Int size)
        {
            if (Layout != null && !Layout.AllowsOnRoomWallRing(def, cell, size)) return false;
            if (UsesYardRule(def))      return Layout.CanPlacePen(GridManager, cell, size);
            if (UsesStructureRule(def)) return Layout.CanPlaceStructure(GridManager, cell, size);
            return GridManager.CanPlace(cell, size);
        }

        /// <summary>
        /// Refuses <paramref name="def"/> at <paramref name="cell"/> when it does not fit, posting the
        /// yard notice for pens, the room-behind notice for a counter that would leave its cashier nowhere to
        /// stand, and the generic notice otherwise; true when refused.
        /// </summary>
        private bool RefusePlacement(PlacedObjectData def, Vector2Int cell, Vector2Int size, float yaw)
        {
            if (CanPlaceItem(def, cell, size, yaw)) return false;
            bool fits = FitsFootprint(def, cell, size);
            OnBuildMessage.Invoke(UsesYardRule(def) ? PenInShopNotice
                                : fits              ? CounterNoRoomNotice : CantBuildNotice);
            return true;
        }

        /// <summary>
        /// Lays floor under a pen's or building piece's yard footprint so <see cref="GridManager.PlaceObject"/>
        /// accepts it; other items keep the lot-stage floor untouched.
        /// </summary>
        private void PrepareFloor(PlacedObjectData def, Vector2Int cell, Vector2Int size)
        {
            if (UsesYardRule(def) || UsesStructureRule(def)) GridManager.EnsureFloor(cell, size);
        }

        /// <summary>
        /// Undoes <see cref="PrepareFloor"/> after a pen or building piece is removed: floor outside the unlocked
        /// lot (and off the wall ring) goes again, so the piece never unlocks ground for other furniture.
        /// </summary>
        private void ReleaseFloor(PlacedObjectData def, Vector2Int root, Vector2Int size)
        {
            if (UsesYardRule(def) || UsesStructureRule(def)) Layout.ReleasePenFloor(GridManager, root, size);
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
