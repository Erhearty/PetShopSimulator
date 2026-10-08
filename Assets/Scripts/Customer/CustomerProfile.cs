using UnityEngine;
using PetShop.Pets;

namespace PetShop.Customer
{
    /// <summary>The kinds of shopper that come through the door.</summary>
    public enum CustomerArchetype { Regular, BargainHunter, RareCollector, ParentWithChild }

    /// <summary>
    /// Relative odds of each archetype arriving. The collector share grows with reputation:
    /// a famous shop attracts people hunting for something special.
    /// </summary>
    public readonly struct ArchetypeWeights
    {
        /// <summary>Weight of <see cref="CustomerArchetype.Regular"/>.</summary>
        public readonly float Regular;
        /// <summary>Weight of <see cref="CustomerArchetype.BargainHunter"/>.</summary>
        public readonly float BargainHunter;
        /// <summary>Weight of <see cref="CustomerArchetype.RareCollector"/> at zero reputation.</summary>
        public readonly float RareCollector;
        /// <summary>Extra collector weight at full (100) reputation, scaled linearly.</summary>
        public readonly float RareCollectorAtFullReputation;
        /// <summary>Weight of <see cref="CustomerArchetype.ParentWithChild"/>.</summary>
        public readonly float ParentWithChild;

        /// <summary>Builds a weight set; negative weights are treated as zero.</summary>
        public ArchetypeWeights(float regular, float bargainHunter, float rareCollector,
                                float rareCollectorAtFullReputation, float parentWithChild)
        {
            Regular                       = Mathf.Max(0f, regular);
            BargainHunter                 = Mathf.Max(0f, bargainHunter);
            RareCollector                 = Mathf.Max(0f, rareCollector);
            RareCollectorAtFullReputation = Mathf.Max(0f, rareCollectorAtFullReputation);
            ParentWithChild               = Mathf.Max(0f, parentWithChild);
        }

        /// <summary>Default mix used when no spawner supplies its own.</summary>
        public static ArchetypeWeights Default => new(
            CustomerProfile.DefaultRegularWeight, CustomerProfile.DefaultBargainHunterWeight,
            CustomerProfile.DefaultRareCollectorWeight, CustomerProfile.DefaultRareCollectorReputationBonus,
            CustomerProfile.DefaultParentWithChildWeight);
    }

    /// <summary>
    /// How one kind of shopper reacts to prices, stock and waiting. Instances are shared and
    /// immutable; look one up with <see cref="For"/> or pick one at random with <see cref="Roll(float)"/>.
    /// </summary>
    public sealed class CustomerProfile
    {
        // ── Default arrival mix ─────────────────────────────────────────────────
        /// <summary>Default weight of regular shoppers.</summary>
        public const float DefaultRegularWeight               = 0.55f;
        /// <summary>Default weight of bargain hunters.</summary>
        public const float DefaultBargainHunterWeight         = 0.20f;
        /// <summary>Default weight of rare-pet collectors at zero reputation.</summary>
        public const float DefaultRareCollectorWeight         = 0.03f;
        /// <summary>Default extra collector weight gained at full reputation.</summary>
        public const float DefaultRareCollectorReputationBonus = 0.17f;
        /// <summary>Default weight of parents with a child in tow.</summary>
        public const float DefaultParentWithChildWeight       = 0.20f;
        private const float FullReputation = 100f;

        // ── Shared values ───────────────────────────────────────────────────────
        private const float StandardMaxDemand   = 1.6f;  // ShopManager's normal demand ceiling
        private const float NormalPatience      = 1f;
        private const float NoPenalty           = 0f;
        private const float NoPetWanted         = 0f;

        // ── Regular: exactly the pre-archetype shopper ─────────────────────────
        private const float RegularSensitivity     = 1.6f;
        private const float RegularItemBudgetMin   = 14f;
        private const float RegularItemBudgetMax   = 60f;
        private const float RegularPetBudgetMin    = 120f;
        private const float RegularPetBudgetMax    = 900f;
        private const float RegularWantsPetChance  = 0.30f;
        private const float RegularEmptyShelfPenalty = 0.6f;

        // ── Bargain hunter: flees markups, floods in on a sale ─────────────────
        private const float BargainSensitivity     = 3.2f;
        private const float BargainMaxDemand       = 2.2f;
        private const float BargainItemBudgetMin   = 8f;
        private const float BargainItemBudgetMax   = 30f;
        private const float BargainPetBudgetMin    = 80f;
        private const float BargainPetBudgetMax    = 350f;
        private const float BargainWantsPetChance  = 0.15f;

        // ── Rare-pet collector: shrugs at price, only wants the special ones ───
        private const float CollectorSensitivity    = 0.5f;
        private const float CollectorItemBudgetMin  = 20f;
        private const float CollectorItemBudgetMax  = 80f;
        private const float CollectorPetBudgetMin   = 600f;
        private const float CollectorPetBudgetMax   = 3000f;
        private const float CollectorWantsPetChance = 0.9f;
        private const float CollectorEmptyShelfPenalty = 0.3f;
        private const float CollectorNoMatchPenalty = 1.5f;

        // ── Parent with child: wants a pet, short on patience, hates empty shelves ─
        private const float ParentSensitivity      = 1.2f;
        private const float ParentPetBudgetMin     = 120f;
        private const float ParentPetBudgetMax     = 700f;
        private const float ParentWantsPetChance   = 0.60f;
        private const float ParentPatience         = 0.6f;
        private const float ParentEmptyShelfFactor = 2f;

        /// <summary>Which archetype this profile describes.</summary>
        public CustomerArchetype Archetype { get; private set; }
        /// <summary>Short name shown in the thought bubble.</summary>
        public string Label { get; private set; }
        /// <summary>Exponent applied to 1/markup when working out demand.</summary>
        public float PriceSensitivity { get; private set; }
        /// <summary>Ceiling on demand however deep the discount.</summary>
        public float MaxDemand { get; private set; }
        /// <summary>Lowest per-item budget rolled for a shopper who is not after a pet.</summary>
        public float ItemBudgetMin { get; private set; }
        /// <summary>Highest per-item budget rolled for a shopper who is not after a pet.</summary>
        public float ItemBudgetMax { get; private set; }
        /// <summary>Lowest budget rolled for a shopper after a pet.</summary>
        public float PetBudgetMin { get; private set; }
        /// <summary>Highest budget rolled for a shopper after a pet.</summary>
        public float PetBudgetMax { get; private set; }
        /// <summary>Chance, 0–1, that this shopper came in for an animal.</summary>
        public float WantsPetChance { get; private set; }
        /// <summary>Least rare animal this shopper will consider.</summary>
        public Pet.Rarity MinPetRarity { get; private set; }
        /// <summary>Reputation lost (positive magnitude) when leaving with an empty basket.</summary>
        public float EmptyShelfRepPenalty { get; private set; }
        /// <summary>Reputation lost (positive magnitude) when no acceptable pet was ever on show.</summary>
        public float NoMatchingPetRepPenalty { get; private set; }
        /// <summary>Scales how long this shopper will wait in the checkout queue.</summary>
        public float PatienceMultiplier { get; private set; }

        private CustomerProfile() { }

        private static readonly CustomerProfile RegularProfile = new()
        {
            Archetype = CustomerArchetype.Regular, Label = "Regular",
            PriceSensitivity = RegularSensitivity, MaxDemand = StandardMaxDemand,
            ItemBudgetMin = RegularItemBudgetMin, ItemBudgetMax = RegularItemBudgetMax,
            PetBudgetMin = RegularPetBudgetMin, PetBudgetMax = RegularPetBudgetMax,
            WantsPetChance = RegularWantsPetChance, MinPetRarity = Pet.Rarity.Common,
            EmptyShelfRepPenalty = RegularEmptyShelfPenalty, NoMatchingPetRepPenalty = NoPenalty,
            PatienceMultiplier = NormalPatience,
        };

        private static readonly CustomerProfile BargainHunterProfile = new()
        {
            Archetype = CustomerArchetype.BargainHunter, Label = "Bargain hunter",
            PriceSensitivity = BargainSensitivity, MaxDemand = BargainMaxDemand,
            ItemBudgetMin = BargainItemBudgetMin, ItemBudgetMax = BargainItemBudgetMax,
            PetBudgetMin = BargainPetBudgetMin, PetBudgetMax = BargainPetBudgetMax,
            WantsPetChance = BargainWantsPetChance, MinPetRarity = Pet.Rarity.Common,
            EmptyShelfRepPenalty = RegularEmptyShelfPenalty, NoMatchingPetRepPenalty = NoPenalty,
            PatienceMultiplier = NormalPatience,
        };

        private static readonly CustomerProfile RareCollectorProfile = new()
        {
            Archetype = CustomerArchetype.RareCollector, Label = "Collector",
            PriceSensitivity = CollectorSensitivity, MaxDemand = StandardMaxDemand,
            ItemBudgetMin = CollectorItemBudgetMin, ItemBudgetMax = CollectorItemBudgetMax,
            PetBudgetMin = CollectorPetBudgetMin, PetBudgetMax = CollectorPetBudgetMax,
            WantsPetChance = CollectorWantsPetChance, MinPetRarity = Pet.Rarity.Rare,
            EmptyShelfRepPenalty = CollectorEmptyShelfPenalty, NoMatchingPetRepPenalty = CollectorNoMatchPenalty,
            PatienceMultiplier = NormalPatience,
        };

        private static readonly CustomerProfile ParentWithChildProfile = new()
        {
            Archetype = CustomerArchetype.ParentWithChild, Label = "Parent",
            PriceSensitivity = ParentSensitivity, MaxDemand = StandardMaxDemand,
            ItemBudgetMin = RegularItemBudgetMin, ItemBudgetMax = RegularItemBudgetMax,
            PetBudgetMin = ParentPetBudgetMin, PetBudgetMax = ParentPetBudgetMax,
            WantsPetChance = ParentWantsPetChance, MinPetRarity = Pet.Rarity.Common,
            EmptyShelfRepPenalty = RegularEmptyShelfPenalty * ParentEmptyShelfFactor,
            NoMatchingPetRepPenalty = NoPenalty, PatienceMultiplier = ParentPatience,
        };

        /// <summary>The shared profile for <paramref name="archetype"/>; Regular for unknown values.</summary>
        public static CustomerProfile For(CustomerArchetype archetype) => archetype switch
        {
            CustomerArchetype.BargainHunter   => BargainHunterProfile,
            CustomerArchetype.RareCollector   => RareCollectorProfile,
            CustomerArchetype.ParentWithChild => ParentWithChildProfile,
            _                                 => RegularProfile,
        };

        /// <summary>Picks an archetype using the default mix at the given shop reputation (0–100).</summary>
        public static CustomerArchetype Roll(float reputation) => Roll(reputation, ArchetypeWeights.Default);

        /// <summary>Picks an archetype using <paramref name="weights"/> at the given reputation (0–100).</summary>
        public static CustomerArchetype Roll(float reputation, ArchetypeWeights weights) =>
            Pick(Random.value, reputation, weights);

        /// <summary>
        /// Pure weighted pick: <paramref name="roll01"/> in [0,1) selects an archetype in proportion
        /// to its weight. Falls back to Regular if every weight is zero.
        /// </summary>
        public static CustomerArchetype Pick(float roll01, float reputation, ArchetypeWeights weights)
        {
            float collector = weights.RareCollector
                            + weights.RareCollectorAtFullReputation * Mathf.Clamp01(reputation / FullReputation);
            float total = weights.Regular + weights.BargainHunter + collector + weights.ParentWithChild;
            if (total <= 0f) return CustomerArchetype.Regular;

            float r = Mathf.Clamp01(roll01) * total;
            if ((r -= weights.Regular)       < 0f) return CustomerArchetype.Regular;
            if ((r -= weights.BargainHunter) < 0f) return CustomerArchetype.BargainHunter;
            if ((r -= collector)             < 0f) return CustomerArchetype.RareCollector;
            return weights.ParentWithChild > 0f ? CustomerArchetype.ParentWithChild : CustomerArchetype.Regular;
        }

        /// <summary>A random per-visit budget for a shopper who is not after a pet.</summary>
        public float RollItemBudget() => Random.Range(ItemBudgetMin, ItemBudgetMax);

        /// <summary>A random budget for a shopper after a pet.</summary>
        public float RollPetBudget() => Random.Range(PetBudgetMin, PetBudgetMax);

        /// <summary>True if this shopper would buy <paramref name="pet"/>: an adult of at least the minimum rarity.</summary>
        public bool Accepts(Pet pet) => pet != null && pet.IsAdult && !pet.Locked && pet.rarity >= MinPetRarity;

        /// <summary>First animal in <paramref name="pen"/> this shopper would buy, or null.</summary>
        public Pet FirstWantedPet(PetPen pen)
        {
            if (pen == null) return null;
            foreach (var pet in pen.Residents)
                if (Accepts(pet)) return pet;
            return null;
        }
    }
}
