using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PetShop.Localization;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// The "Aim for" selector on the breeding panel plus the odds text built from it.
    /// State lives for the session only; it is never saved.
    /// </summary>
    public class BreedingTargetView
    {
        private const int TierOptions = 4;   // Any, Uncommon, Rare, Legendary

        private int _coatIndex;   // 0 = Any, n = CoatColours.Names[n - 1]
        private int _tierIndex;   // 0 = Any, n = (Pet.Rarity)n
        private TMP_Text _coatLabel;
        private TMP_Text _tierLabel;
        private Action _changed;

        public string Coat => _coatIndex == 0 ? null : CoatColours.Names[_coatIndex - 1];

        public Pet.Rarity? MinRarity => _tierIndex == 0 ? (Pet.Rarity?)null : (Pet.Rarity)_tierIndex;

        public bool HasTarget => _coatIndex != 0 || _tierIndex != 0;

        // ── Selector row ────────────────────────────────────────────────────────

        public void Build(Transform panel, Action changed)
        {
            _changed = changed;

            Stepper(panel, "CoatPrev", "<", 0.03f, 0.07f, () => StepCoat(-1));
            _coatLabel = UIFactory.Label("AimFor", panel, "",
                new Vector2(0.075f, 0.02f), new Vector2(0.295f, 0.10f), 14f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            Stepper(panel, "CoatNext", ">", 0.30f, 0.34f, () => StepCoat(1));

            Stepper(panel, "TierPrev", "<", 0.38f, 0.42f, () => StepTier(-1));
            _tierLabel = UIFactory.Label("MinRarity", panel, "",
                new Vector2(0.425f, 0.02f), new Vector2(0.695f, 0.10f), 14f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            Stepper(panel, "TierNext", ">", 0.70f, 0.74f, () => StepTier(1));

            // The producers re-run on a language change; RefreshLabels covers selection changes.
            LocalizedText.Bind(_coatLabel, CoatText);
            LocalizedText.Bind(_tierLabel, TierText);
        }

        private static void Stepper(Transform panel, string name, string text,
                                    float x0, float x1, Action onClick)
        {
            var b = UIFactory.Button(name, panel, text,
                new Vector2(x0, 0.02f), new Vector2(x1, 0.10f), 16f);
            b.onClick.AddListener(() => onClick());
        }

        private void StepCoat(int delta)
        {
            int count = CoatColours.Names.Count + 1;
            _coatIndex = (_coatIndex + delta + count) % count;
            Changed();
        }

        private void StepTier(int delta)
        {
            _tierIndex = (_tierIndex + delta + TierOptions) % TierOptions;
            Changed();
        }

        private void Changed()
        {
            RefreshLabels();
            _changed?.Invoke();
        }

        private void RefreshLabels()
        {
            _coatLabel.text = CoatText();
            _tierLabel.text = TierText();
        }

        private string CoatText() =>
            Loc.F("breeding.aim_for", Coat != null ? CoatColours.DisplayName(Coat) : Loc.T("breeding.any_coat"));

        private string TierText() =>
            Loc.F("breeding.min_rarity", MinRarity.HasValue ? LocNames.Rarity(MinRarity.Value) : Loc.T("breeding.any_rarity"));

        // ── Odds ────────────────────────────────────────────────────────────────

        /// <summary>Chance that one baby from this pair matches the target.</summary>
        public float Chance(Pet a, Pet b)
        {
            var odds = BreedingSystem.EstimateOdds(a, b);
            return CoatChance(odds) * RarityChance(odds);
        }

        private float CoatChance(PairingOdds odds)
        {
            if (Coat == null) return 1f;
            return odds.Coats.TryGetValue(Coat, out float p) ? p : 0f;
        }

        private float RarityChance(PairingOdds odds)
        {
            var min = MinRarity;
            if (!min.HasValue) return 1f;

            float sum = 0f;
            foreach (var kv in odds.Rarities)
                if (kv.Key >= min.Value) sum += kv.Value;
            return sum;
        }

        private static string Percent(float p) => $"~{Mathf.RoundToInt(p * 100f)}%";

        /// <summary>The expected-offspring text with the target chance appended for a valid pair.</summary>
        public string ExpectedWithChance(string expected, Pet a, Pet b)
        {
            if (!HasTarget || !BreedingSystem.CanPair(a, b)) return expected;
            return $"{expected}\n\n{Loc.F("breeding.target_chance", Percent(Chance(a, b)))}";
        }

        /// <summary>"  ·  best ~NN%" for the strongest partner of <paramref name="pet"/>, or empty.</summary>
        public string BestPartnerSuffix(Pet pet, IEnumerable<Pet> residents)
        {
            if (!HasTarget) return string.Empty;

            float best = -1f;
            foreach (var other in residents)
                if (BreedingSystem.CanPair(pet, other)) best = Mathf.Max(best, Chance(pet, other));

            return best < 0f ? string.Empty : $"   ·   {Loc.F("breeding.best", Percent(best))}";
        }
    }
}
