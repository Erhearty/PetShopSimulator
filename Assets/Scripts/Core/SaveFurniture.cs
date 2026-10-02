using PetShop.Shop;

namespace PetShop.Core
{
    /// <summary>
    /// Converts the <see cref="FurnitureSupply"/> to and from save data. Crates waiting on the
    /// forecourt are saved as arrived-but-uncollected orders and respawned on load, so a crate is
    /// never lost or duplicated by saving.
    /// </summary>
    public static class SaveFurniture
    {
        /// <summary>
        /// Writes the furniture inventory and pending orders into <paramref name="data"/>. An item
        /// held in build mode is first handed back to the inventory (closing build mode), so a save
        /// taken mid-placement never loses that unit.
        /// </summary>
        public static void Capture(SaveData data, FurnitureSupply supply, BuildMode build)
        {
            if (data == null || supply == null) return;
            if (build != null && build.IsHolding) build.ExitBuildMode();
            supply.Capture(data);
        }

        /// <summary>
        /// Restores the inventory and pending orders from <paramref name="data"/>, then re-announces
        /// every arrived-but-uncollected order so its crate is respawned on the forecourt.
        /// </summary>
        public static void Apply(SaveData data, FurnitureSupply supply)
        {
            if (data == null || supply == null) return;
            supply.Restore(data);
            supply.RespawnArrived();
        }
    }
}
