namespace PetShop.Core
{
    /// <summary>Which shop layout the save's grid cells were written against.</summary>
    public partial class SaveData
    {
        /// <summary>
        /// Layout version the placed cells belong to (see <see cref="PetShop.Shop.ShopLayout.CurrentLayoutVersion"/>).
        /// Absent in older saves, which read 0: the room stood back from the street, and loading shifts every
        /// saved cell onto the current layout.
        /// </summary>
        public int layoutVersion;

        /// <summary>
        /// True once the room's back row has had its back door: seeded with the walls, or swapped in by
        /// <see cref="SaveLayoutMigration.SeedBackDoor"/>. Older saves read false and get it once on load.
        /// </summary>
        public bool backDoorSeeded;
    }
}
