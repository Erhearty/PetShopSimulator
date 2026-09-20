using System.Collections.Generic;
using UnityEngine;

namespace PetShop.Pets
{
    /// <summary>
    /// Static utility for breeding two adult same-species pets.
    /// Trait inheritance uses blended lerp + mutation, with a small
    /// chance of a rarity upgrade in the offspring.
    /// </summary>
    public static class BreedingSystem
    {
        private const float MutationChance       = 0.10f;
        private const float MutationStrength     = 0.15f;
        private const float RarityUpgradeChance  = 0.05f;
        private const float BreedChancePerNight  = 0.45f;

        // ── Public API ───────────────────────────────────────────────

        /// <summary>Breed two pets. Returns null if prerequisites fail.</summary>
        public static Pet Breed(Pet a, Pet b)
        {
            if (!a.CanBreed || !b.CanBreed)
            {
                Debug.LogWarning("BreedingSystem: one or both parents cannot breed yet.");
                return null;
            }
            if (a.species != b.species)
            {
                Debug.LogWarning("BreedingSystem: cross-species breeding not supported.");
                return null;
            }

            // ScriptableObject.CreateInstance keeps it in memory; 
            // use ResourceSaver or Addressables to persist to disk.
            var offspring = ScriptableObject.CreateInstance<Pet>();
            offspring.species      = a.species;
            offspring.growthStage  = Pet.GrowthStage.Baby;
            offspring.ageDays      = 0;
            offspring.daysToMature = Mathf.RoundToInt((a.daysToMature + b.daysToMature) / 2f);
            offspring.basePrice    = (a.basePrice + b.basePrice) * 0.5f;
            offspring.coat         = BlendColor(a.coat, b.coat);
            offspring.temperament  = Inherit(a.temperament,  b.temperament);
            offspring.energyLevel  = Inherit(a.energyLevel,  b.energyLevel);
            offspring.friendliness = Inherit(a.friendliness, b.friendliness);
            offspring.rarity       = InheritRarity(a.rarity, b.rarity);
            offspring.petName      = RandomName();
            offspring.name         = $"{offspring.species}_{offspring.petName}";
            return offspring;
        }

        /// <summary>
        /// End-of-day tick for every pen: age each resident, then let any pen with two
        /// adults and free space produce one baby. Returns the babies born tonight.
        /// </summary>
        public static List<Pet> AdvanceDay(IEnumerable<PetPen> pens)
        {
            var born = new List<Pet>();
            if (pens == null) return born;

            foreach (var pen in pens)
            {
                if (pen == null) continue;
                pen.AdvanceDay();

                if (!pen.HasSpace || pen.AdultCount < 2) continue;
                if (Random.value > BreedChancePerNight) continue;

                var adults = new List<Pet>();
                foreach (var p in pen.Residents)
                    if (p.IsAdult) adults.Add(p);

                var baby = Breed(adults[0], adults[1]);
                if (baby != null && pen.AddPet(baby)) born.Add(baby);
            }
            return born;
        }

        /// <summary>Generate a random adult pet for starter stock.</summary>
        public static Pet GenerateRandom(Pet.Species species)
        {
            var p = ScriptableObject.CreateInstance<Pet>();
            p.species      = species;
            p.growthStage  = Pet.GrowthStage.Adult;
            p.ageDays      = p.daysToMature;
            p.coat         = RandomCoat(species);
            p.temperament  = Random.value;
            p.energyLevel  = Random.value;
            p.friendliness = Random.value;
            p.rarity       = RandomRarity();
            p.basePrice    = Pet.SpeciesBasePrice(species);
            p.petName      = RandomName();
            p.name         = $"{species}_{p.petName}";
            return p;
        }

        private static readonly string[] Names =
        {
            "Pip", "Biscuit", "Nala", "Mochi", "Rusty", "Clover", "Pepper", "Waffle",
            "Juno", "Miso", "Bramble", "Olive", "Tofu", "Hazel", "Comet", "Pickle",
        };

        public static string RandomName() => Names[Random.Range(0, Names.Length)];

        /// <summary>
        /// A plausible coat for the species. Sampling pure random RGB gave magenta rabbits,
        /// which looked like a rendering fault rather than a design choice.
        /// </summary>
        public static Color RandomCoat(Pet.Species species)
        {
            Color[] palette = species switch
            {
                Pet.Species.Cat     => new[] { new Color(0.85f,0.66f,0.42f), new Color(0.32f,0.29f,0.28f),
                                               new Color(0.93f,0.92f,0.89f), new Color(0.58f,0.44f,0.30f) },
                Pet.Species.Dog     => new[] { new Color(0.72f,0.56f,0.36f), new Color(0.40f,0.31f,0.24f),
                                               new Color(0.90f,0.87f,0.80f), new Color(0.25f,0.23f,0.22f) },
                Pet.Species.Rabbit  => new[] { new Color(0.94f,0.92f,0.89f), new Color(0.66f,0.55f,0.45f),
                                               new Color(0.42f,0.36f,0.32f), new Color(0.82f,0.74f,0.64f) },
                Pet.Species.Hamster => new[] { new Color(0.90f,0.74f,0.48f), new Color(0.80f,0.62f,0.40f),
                                               new Color(0.95f,0.90f,0.84f) },
                Pet.Species.Parrot  => new[] { new Color(0.30f,0.72f,0.35f), new Color(0.85f,0.30f,0.25f),
                                               new Color(0.25f,0.50f,0.85f), new Color(0.95f,0.78f,0.20f) },
                Pet.Species.Fish    => new[] { new Color(0.95f,0.60f,0.20f), new Color(0.30f,0.60f,0.90f),
                                               new Color(0.90f,0.85f,0.35f) },
                Pet.Species.Fox     => new[] { new Color(0.88f,0.45f,0.18f), new Color(0.80f,0.38f,0.15f) },
                Pet.Species.Chicken => new[] { new Color(0.95f,0.92f,0.86f), new Color(0.72f,0.50f,0.30f) },
                Pet.Species.Penguin => new[] { new Color(0.20f,0.22f,0.26f) },
                Pet.Species.Deer    => new[] { new Color(0.72f,0.55f,0.36f), new Color(0.62f,0.46f,0.30f) },
                Pet.Species.Horse   => new[] { new Color(0.50f,0.36f,0.24f), new Color(0.25f,0.21f,0.19f),
                                               new Color(0.85f,0.80f,0.74f) },
                Pet.Species.Tiger   => new[] { new Color(0.92f,0.58f,0.18f) },
                _                   => new[] { new Color(0.75f,0.68f,0.58f) },
            };

            Color baseColour = palette[Random.Range(0, palette.Length)];
            Color.RGBToHSV(baseColour, out float h, out float sat, out float v);
            return Color.HSVToRGB(h,
                                  Mathf.Clamp01(sat + Random.Range(-0.08f, 0.08f)),
                                  Mathf.Clamp01(v   + Random.Range(-0.10f, 0.10f)));
        }

        // ── Trait helpers ────────────────────────────────────────────

        private static float Inherit(float a, float b)
        {
            float v = Mathf.Lerp(a, b, Random.value);
            if (Random.value < MutationChance)
                v = Mathf.Clamp01(v + Random.Range(-MutationStrength, MutationStrength));
            return v;
        }

        private static Color BlendColor(Color a, Color b)
        {
            Color c = Color.Lerp(a, b, Random.value);
            if (Random.value < MutationChance)
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                h = (h + Random.Range(-0.08f, 0.08f) + 1f) % 1f;
                s = Mathf.Clamp01(s + Random.Range(-0.1f, 0.1f));
                v = Mathf.Clamp(v + Random.Range(-0.1f, 0.1f), 0.2f, 1f);
                c = Color.HSVToRGB(h, s, v);
            }
            return c;
        }

        private static Pet.Rarity InheritRarity(Pet.Rarity a, Pet.Rarity b)
        {
            int baseTier = Mathf.Min((int)a, (int)b);
            if (Random.value < RarityUpgradeChance && baseTier < (int)Pet.Rarity.Legendary)
                baseTier++;
            return (Pet.Rarity)baseTier;
        }

        private static Pet.Rarity RandomRarity()
        {
            float r = Random.value;
            if (r < 0.55f) return Pet.Rarity.Common;
            if (r < 0.80f) return Pet.Rarity.Uncommon;
            if (r < 0.95f) return Pet.Rarity.Rare;
            return Pet.Rarity.Legendary;
        }
    }
}
