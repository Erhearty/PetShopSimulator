using System.Collections.Generic;
using PetShop.Pets;

namespace PetShop.Progression
{
    /// <summary>
    /// Pure progression rules: which reputation earns which permanent tier, what each tier
    /// unlocks, and the highest tier this shop has reached. No scene dependencies, so it is
    /// unit-testable.
    /// </summary>
    public class ProgressionRules
    {
        /// <summary>Tier 0, where every shop starts.</summary>
        public const int CornerShopTier = 0;

        /// <summary>Tier 1: the back strip of the lot opens up.</summary>
        public const int LocalFavouriteTier = 1;

        /// <summary>Tier 2: Horse pens.</summary>
        public const int TrustedNameTier = 2;

        /// <summary>Tier 3: Tiger pens and the whole yard.</summary>
        public const int TownLandmarkTier = 3;

        /// <summary>The highest tier a shop can reach.</summary>
        public const int MaxTier = TownLandmarkTier;

        /// <summary>Reputation needed for tier 0.</summary>
        public const float CornerShopReputation = 0f;

        /// <summary>Reputation needed for tier 1.</summary>
        public const float LocalFavouriteReputation = 55f;

        /// <summary>Reputation needed for tier 2.</summary>
        public const float TrustedNameReputation = 70f;

        /// <summary>Reputation needed for tier 3.</summary>
        public const float TownLandmarkReputation = 85f;

        /// <summary>Display name of tier 0.</summary>
        public const string CornerShopName = "Corner Shop";

        /// <summary>Display name of tier 1.</summary>
        public const string LocalFavouriteName = "Local Favourite";

        /// <summary>Display name of tier 2.</summary>
        public const string TrustedNameName = "Trusted Name";

        /// <summary>Display name of tier 3.</summary>
        public const string TownLandmarkName = "Town Landmark";

        /// <summary>First tier at which Horse pens can be placed.</summary>
        public const int HorseTier = TrustedNameTier;

        /// <summary>First tier at which Tiger pens can be placed.</summary>
        public const int TigerTier = TownLandmarkTier;

        /// <summary>Lot stage 0: the shop room and its forecourt.</summary>
        public const int StarterLotStage = 0;

        /// <summary>Lot stage 1: stage 0 plus the strip behind it.</summary>
        public const int BackStripLotStage = 1;

        /// <summary>Lot stage 2: the whole yard.</summary>
        public const int FullYardLotStage = 2;

        private static readonly float[] Thresholds =
        {
            CornerShopReputation, LocalFavouriteReputation, TrustedNameReputation, TownLandmarkReputation,
        };

        private static readonly string[] Names =
        {
            CornerShopName, LocalFavouriteName, TrustedNameName, TownLandmarkName,
        };

        // Only species with real models — the same set the starter layout uses.
        private static readonly Pet.Species[] StarterPenSpecies =
        {
            Pet.Species.Dog, Pet.Species.Cat, Pet.Species.Fox,
            Pet.Species.Chicken, Pet.Species.Penguin, Pet.Species.Deer,
        };

        /// <summary>The highest tier reached. Never falls through <see cref="TryAdvance"/>.</summary>
        public int HighestTier { get; private set; }

        /// <summary>Creates rules starting at <paramref name="highestTier"/> (clamped to 0..MaxTier).</summary>
        public ProgressionRules(int highestTier = CornerShopTier) => SetTier(highestTier);

        /// <summary>Restores a saved tier (clamped to 0..MaxTier). May lower it; used only for loading.</summary>
        public void SetTier(int tier) => HighestTier = ClampTier(tier);

        /// <summary>
        /// Raises <see cref="HighestTier"/> to the tier <paramref name="reputation"/> earns.
        /// Returns true only when it rose; never lowers it.
        /// </summary>
        public bool TryAdvance(float reputation, out int newTier)
        {
            int earned = TierForReputation(reputation);
            bool rose  = earned > HighestTier;
            if (rose) HighestTier = earned;
            newTier = HighestTier;
            return rose;
        }

        /// <summary>The tier a given reputation qualifies for (0..MaxTier).</summary>
        public static int TierForReputation(float reputation)
        {
            int tier = CornerShopTier;
            for (int t = LocalFavouriteTier; t <= MaxTier; t++)
                if (reputation >= Thresholds[t]) tier = t;
            return tier;
        }

        /// <summary>Reputation needed to reach <paramref name="tier"/> (clamped to 0..MaxTier).</summary>
        public static float ThresholdForTier(int tier) => Thresholds[ClampTier(tier)];

        /// <summary>Display name of <paramref name="tier"/> (clamped to 0..MaxTier).</summary>
        public static string TierName(int tier) => Names[ClampTier(tier)];

        /// <summary>The lot stage a tier unlocks: 0 for tier 0, 1 for tiers 1–2, 2 for tier 3.</summary>
        public static int LotStageForTier(int tier)
        {
            if (tier >= TownLandmarkTier)   return FullYardLotStage;
            if (tier >= LocalFavouriteTier) return BackStripLotStage;
            return StarterLotStage;
        }

        /// <summary>Species a player may choose for a new pen at <paramref name="tier"/>.</summary>
        public static IReadOnlyList<Pet.Species> PickableSpecies(int tier)
        {
            var list = new List<Pet.Species>(StarterPenSpecies);
            if (tier >= HorseTier) list.Add(Pet.Species.Horse);
            if (tier >= TigerTier) list.Add(Pet.Species.Tiger);
            return list;
        }

        /// <summary>
        /// <see cref="PickableSpecies"/> as pen variant strings, the form BuildMode and the
        /// furniture factory use.
        /// </summary>
        public static IReadOnlyList<string> PenVariantsForTier(int tier)
        {
            var species = PickableSpecies(tier);
            var names   = new List<string>(species.Count);
            foreach (var s in species) names.Add(s.ToString());
            return names;
        }

        private static int ClampTier(int tier) =>
            tier < CornerShopTier ? CornerShopTier : tier > MaxTier ? MaxTier : tier;
    }
}
