using UnityEngine;

namespace PetShop.Pets
{
    /// <summary>
    /// A single pet — species, traits, growth stage, and pricing.
    /// Use ScriptableObject so pets can be saved as assets and
    /// duplicated at runtime via Instantiate().
    /// </summary>
    [CreateAssetMenu(fileName = "NewPet", menuName = "PetShop/Pet")]
    public class Pet : ScriptableObject
    {
        public enum Species      { Cat, Dog, Fox, Chicken, Penguin, Deer, Horse, Tiger, Rabbit, Hamster, Parrot, Fish }
        public enum GrowthStage  { Baby, Juvenile, Adult }
        public enum Rarity       { Common, Uncommon, Rare, Legendary }

        [Header("Identity")]
        public Species      species      = Species.Cat;
        public string       petName      = "";
        public GrowthStage  growthStage  = GrowthStage.Baby;
        public Rarity       rarity       = Rarity.Common;

        [Header("Age")]
        public int ageDays      = 0;
        public int daysToMature = 7;

        [Header("Traits  (0 – 1)")]
        public Color coat       = Color.white;
        [Range(0f,1f)] public float temperament  = 0.5f;  // 0=shy, 1=bold
        [Range(0f,1f)] public float energyLevel  = 0.5f;  // 0=lazy, 1=hyper
        [Range(0f,1f)] public float friendliness = 0.5f;  // 0=aloof, 1=cuddly

        [Header("Wellbeing  (0 – 1)")]
        [Range(0f,1f)] public float health    = 1f;    // falls when hungry or kept in filth
        [Range(0f,1f)] public float hunger    = 0f;    // 1 = starving
        [Range(0f,1f)] public float happiness = 1f;

        [Header("Commerce")]
        public float basePrice = 50f;

        // ── Queries ──────────────────────────────────────────────────

        public bool IsAdult   => growthStage == GrowthStage.Adult;
        public bool CanBreed  => IsAdult;

        public float SellPrice()
        {
            float m = rarity switch
            {
                Rarity.Uncommon  => 1.5f,
                Rarity.Rare      => 2.5f,
                Rarity.Legendary => 5.0f,
                _                => 1.0f
            };
            if (IsAdult) m *= 1.2f;
            m += friendliness * 0.1f + (1f - Mathf.Abs(temperament - 0.7f)) * 0.05f;

            // A neglected animal is worth far less, and a miserable one barely sells.
            m *= Mathf.Lerp(0.35f, 1.1f, health);
            m *= Mathf.Lerp(0.75f, 1.05f, happiness);

            return Mathf.Round(basePrice * m * 100f) / 100f;
        }

        /// <summary>Short word for the pen panel: how this animal is doing.</summary>
        public string Condition =>
            health > 0.85f && happiness > 0.7f ? "thriving"
          : health > 0.6f                      ? "well"
          : health > 0.35f                     ? "poorly"
          :                                      "suffering";

        public bool NeedsAttention => hunger > 0.55f || health < 0.6f;

        public string DisplayName()
        {
            string r = rarity.ToString().ToUpper();
            string g = growthStage.ToString();
            string s = SpeciesLabel(species);
            string n = petName.Length > 0 ? $" ({petName})" : "";
            return $"[{r}] {g} {s}{n}";
        }

        // ── Aging ────────────────────────────────────────────────────

        /// <summary>
        /// One night of care (or neglect). <paramref name="fed"/> and <paramref name="clean"/>
        /// describe the pen this animal spent the night in.
        /// </summary>
        public void AdvanceDay(bool fed, bool clean)
        {
            hunger = fed ? Mathf.Max(0f, hunger - 0.6f) : Mathf.Min(1f, hunger + 0.45f);

            float drift = 0f;
            if (hunger > 0.6f) drift -= 0.18f;
            if (!clean)        drift -= 0.12f;
            if (fed && clean)  drift += 0.22f;

            health    = Mathf.Clamp01(health + drift);
            happiness = Mathf.Clamp01(happiness + (fed && clean ? 0.2f : -0.15f)
                                                + (friendliness - 0.5f) * 0.05f);

            ageDays++;
            growthStage = growthStage switch
            {
                GrowthStage.Baby     when ageDays >= 3            => GrowthStage.Juvenile,
                GrowthStage.Juvenile when ageDays >= daysToMature => GrowthStage.Adult,
                _ => growthStage
            };
        }

        // ── Static helpers ───────────────────────────────────────────

        public static string SpeciesLabel(Species s) => s.ToString();

        /// <summary>What the breeder charges you for a young pet of this species.</summary>
        public static float WholesalePrice(Species s) => Mathf.Round(SpeciesBasePrice(s) * 0.55f);

        public static float SpeciesBasePrice(Species s) => s switch
        {
            Species.Cat     => 80f,
            Species.Dog     => 120f,
            Species.Fox     => 220f,
            Species.Chicken => 25f,
            Species.Penguin => 320f,
            Species.Deer    => 260f,
            Species.Horse   => 480f,
            Species.Tiger   => 900f,   // the licence is your problem
            Species.Rabbit  => 40f,
            Species.Hamster => 15f,
            Species.Parrot  => 150f,
            Species.Fish    => 10f,
            _               => 50f
        };
    }
}
