using System.Collections.Generic;

namespace PetShop.Core
{
    /// <summary>
    /// What a layout migration (<see cref="SaveLayoutMigration"/>) did to a save's placed pieces, so the loader
    /// can settle the packed pieces' contents and tell the player in one notice.
    /// </summary>
    internal sealed class LayoutMigrationReport
    {
        /// <summary>Opening words of <see cref="Notice"/>.</summary>
        public const string NoticePrefix = "Your shop was rebuilt at the front of the yard";

        /// <summary>Opening words of <see cref="RefundNotice"/>.</summary>
        public const string RefundNoticePrefix = "No room was left for a pen pushed off the yard, so its pets were sold back";

        /// <summary>One pet sold back because its pen had no valid spot and no other pen had room.</summary>
        public sealed class RefundedPet
        {
            /// <summary>The pet's id.</summary>
            public string Id;
            /// <summary>Its name, or its species when it has none.</summary>
            public string Name;
            /// <summary>What it sold back for.</summary>
            public float Value;
        }

        /// <summary>Pieces packed into the furniture inventory instead of placed.</summary>
        public readonly List<SaveData.PlacedItem> Evicted = new();

        /// <summary>Pens with pets moved to a free spot in the yard.</summary>
        public readonly List<SaveData.PlacedItem> Relocated = new();

        /// <summary>Pens with pets that found no spot and no room elsewhere, left at their original unshifted cells.</summary>
        public readonly List<SaveData.PlacedItem> KeptUnshifted = new();

        /// <summary>Pets moved into another saved pen of their species because their own pen had no spot.</summary>
        public int RehomedPets;

        /// <summary>Pets sold back for their value because no pen could hold them; their pen was packed.</summary>
        public readonly List<RefundedPet> RefundedPets = new();

        /// <summary>Total credited to the save's balance for <see cref="RefundedPets"/>.</summary>
        public float RefundTotal;

        /// <summary>True when the migration moved, packed or emptied anything the player should hear about.</summary>
        public bool HasChanges =>
            Evicted.Count > 0 || Relocated.Count > 0 || KeptUnshifted.Count > 0 || RehomedPets > 0 ||
            RefundedPets.Count > 0;

        /// <summary>
        /// The one in-game notice for the whole load, e.g. "Your shop was rebuilt at the front of the yard:
        /// 2 items moved to your furniture inventory, 1 pen moved".
        /// </summary>
        public string Notice()
        {
            var parts = new List<string>();
            if (Evicted.Count > 0)       parts.Add($"{Count(Evicted.Count, "item")} moved to your furniture inventory");
            if (Relocated.Count > 0)     parts.Add($"{Count(Relocated.Count, "pen")} moved");
            if (RehomedPets > 0)         parts.Add($"{Count(RehomedPets, "pet")} moved to another pen");
            if (KeptUnshifted.Count > 0) parts.Add($"{Count(KeptUnshifted.Count, "pen")} left where it stood");
            return $"{NoticePrefix}: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// The notice naming the pets sold back and the refund, e.g. "No room was left for a pen pushed off the
        /// yard, so its pets were sold back: Bun, Rabbit for €123.40". Empty when none were.
        /// </summary>
        public string RefundNotice()
        {
            if (RefundedPets.Count == 0) return string.Empty;
            var names = RefundedPets.ConvertAll(p => p.Name);
            return $"{RefundNoticePrefix}: {string.Join(", ", names)} for €{RefundTotal:N2}";
        }

        /// <summary>"1 pen" / "2 pens".</summary>
        private static string Count(int n, string noun) => n == 1 ? $"{n} {noun}" : $"{n} {noun}s";
    }
}
