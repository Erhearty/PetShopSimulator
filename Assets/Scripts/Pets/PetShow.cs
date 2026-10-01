using System;
using System.Collections.Generic;
using System.Linq;
using PetShop.Commerce;

namespace PetShop.Pets
{
    /// <summary>The species and coat the judges want this week.</summary>
    public readonly struct ShowTheme
    {
        public readonly Pet.Species species;
        public readonly string coatName;
        public ShowTheme(Pet.Species species, string coatName) { this.species = species; this.coatName = coatName; }
    }

    /// <summary>Outcome of judging one entry. Placement is 1-6, or 0 when disqualified.</summary>
    public readonly struct ShowResult
    {
        public readonly int placement;
        public readonly float score;
        public readonly bool disqualified;
        public readonly float prize;
        public readonly float reputation;
        public ShowResult(int placement, float score, bool disqualified, float prize, float reputation)
        {
            this.placement = placement; this.score = score; this.disqualified = disqualified;
            this.prize = prize; this.reputation = reputation;
        }
    }

    /// <summary>Weekly pet show: theme, entry, judging, prizes and ribbons.</summary>
    public class PetShow
    {
        public const int   ShowIntervalDays = 7;
        public const float EntryFee         = 20f;
        public const float RibbonBonus      = 0.15f;
        public const int   MaxRibbons       = 3;
        public const int   RivalCount       = 5;

        private const float RivalMin        = 1.5f;
        private const float RivalRange      = 3f;
        private const float IdealTemperament = 0.7f;

        public static readonly float[] PrizeMultipliers = { 2.0f, 1.0f, 0.5f };
        public static readonly float[] ReputationAwards = { 3f, 1.5f, 0.5f };

        private Pet _entry;

        public string EntryPetId => _entry != null ? _entry.id : null;
        public ShowResult? LastResult { get; private set; }
        /// <summary>Day the <see cref="LastResult"/> was judged on (0 when none).</summary>
        public int LastResultDay { get; private set; }

        public static bool IsShowDay(int day) => day > 0 && day % ShowIntervalDays == 0;

        public static int DaysUntilShow(int day)
        {
            int rem = day % ShowIntervalDays;
            return rem == 0 ? 0 : ShowIntervalDays - rem;
        }

        /// <summary>Deterministic theme for a day; species is drawn from the owned pets when any.</summary>
        public static ShowTheme ThemeFor(int day, IEnumerable<Pet.Species> owned)
        {
            var rng = new System.Random(day);
            var pool = owned != null ? owned.Distinct().OrderBy(s => (int)s).ToList() : new List<Pet.Species>();
            if (pool.Count == 0) pool = ((Pet.Species[])Enum.GetValues(typeof(Pet.Species))).ToList();
            var species = pool[rng.Next(pool.Count)];
            string coat = CoatColours.Names[rng.Next(CoatColours.Names.Count)];
            return new ShowTheme(species, coat);
        }

        /// <summary>Scores an entry against the theme and five seeded rivals.</summary>
        public ShowResult Judge(int day, Pet entry, ShowTheme theme)
        {
            if (entry.species != theme.species) return new ShowResult(0, 0f, true, 0f, 0f);
            float score = Score(entry, theme);
            var rng = new System.Random(day);
            int place = 1;
            for (int i = 0; i < RivalCount; i++)
                if (RivalMin + (float)rng.NextDouble() * RivalRange > score) place++;
            return new ShowResult(place, score, false, PrizeFor(place, theme), ReputationFor(place));
        }

        private static float Score(Pet p, ShowTheme theme)
        {
            float s = (int)p.rarity;
            s += p.CoatName == theme.coatName ? 1f : 0f;
            s += (p.health + p.happiness) * 0.5f;
            s += p.friendliness;
            s += 1f - Math.Abs(p.temperament - IdealTemperament);
            return s;
        }

        private static float PrizeFor(int place, ShowTheme theme) =>
            place <= PrizeMultipliers.Length ? PrizeMultipliers[place - 1] * Pet.SpeciesBasePrice(theme.species) : 0f;

        private static float ReputationFor(int place) =>
            place <= ReputationAwards.Length ? ReputationAwards[place - 1] : 0f;

        /// <summary>Pays the fee and registers the pet. False if unaffordable, not adult, or off-theme.</summary>
        public bool Enter(Pet pet, ShopManager shop, ShowTheme theme)
        {
            if (pet == null || shop == null || !pet.IsAdult || pet.species != theme.species) return false;
            if (!shop.ChangeBalance(-EntryFee, "Pet show entry")) return false;
            _entry = pet;
            return true;
        }

        public void Withdraw() => _entry = null;

        /// <summary>Re-registers an already-paid entry (used when loading a save); no fee.</summary>
        public void Restore(Pet pet) => _entry = pet;

        /// <summary>Judges the entered pet, pays prize and reputation, and awards a ribbon for places 1-3.</summary>
        public ShowResult? Resolve(int day, ShowTheme theme, ShopManager shop)
        {
            if (_entry == null) return null;
            var result = Judge(day, _entry, theme);
            if (result.prize > 0f) shop.ChangeBalance(result.prize, "Pet show prize");
            shop.ChangeReputation(result.reputation);
            if (result.placement >= 1 && result.placement <= MaxRibbons) _entry.ribbons++;
            LastResult = result;
            LastResultDay = day;
            _entry = null;
            return result;
        }
    }
}
