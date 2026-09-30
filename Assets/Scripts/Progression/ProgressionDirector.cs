using System.Collections.Generic;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Pets;
using PetShop.Shop;

namespace PetShop.Progression
{
    /// <summary>
    /// Scene-side progression: holds the shop's permanent reputation tier, applies what each
    /// tier unlocks (lot stage, pen species), and runs the weekly inspection. The rules
    /// themselves live in <see cref="ProgressionRules"/> and <see cref="InspectorGrader"/>.
    /// </summary>
    public class ProgressionDirector : MonoBehaviour
    {
        /// <summary>Sound played when a milestone is reached.</summary>
        public const string MilestoneSfx = "day_start";

        /// <summary>Full marks for health and care when there is nothing to inspect.</summary>
        private const float FullMarks = 1f;

        /// <summary>Empty-shelf fraction to use when there are no shelves.</summary>
        private const float NoEmptyShelves = 0f;

        /// <summary>The highest tier reached. Never falls, even if reputation does.</summary>
        public int Tier => _rules.HighestTier;

        /// <summary>Headlines from the most recent <see cref="CheckMilestones"/> call; empty when nothing changed.</summary>
        public IReadOnlyList<string> LatestMilestones => _latestMilestones;

        private readonly ProgressionRules _rules            = new ProgressionRules();
        private readonly List<string>     _latestMilestones = new();

        private GameManager   _game;
        private ShopGenerator _generator;

        private ShopManager Shop => _game != null ? _game.Shop : null;

        /// <summary>Wires the director to the scene. Called by GameBootstrapper before the game begins.</summary>
        public void Init(GameManager game, ShopGenerator generator, BuildMode build)
        {
            _game      = game;
            _generator = generator;
            if (build != null) build.PenVariantSource = () => ProgressionRules.PenVariantsForTier(Tier);
        }

        /// <summary>Restores a saved tier silently and applies its lot stage. Call before placing saved furniture.</summary>
        public void Restore(int tier)
        {
            _rules.SetTier(tier);
            ApplyLotStage();
        }

        /// <summary>
        /// Raises the tier to whatever the current reputation earns. On a rise, applies the new
        /// lot stage, notifies the player and plays a sound. Headlines go to <see cref="LatestMilestones"/>.
        /// </summary>
        public void CheckMilestones()
        {
            _latestMilestones.Clear();
            if (Shop == null || !_rules.TryAdvance(Shop.Reputation, out int newTier)) return;

            ApplyLotStage();
            string headline = $"Milestone: {ProgressionRules.TierName(newTier)} — {UnlockText(newTier)}";
            Debug.Log($"[Progression] {headline}");
            _latestMilestones.Add(headline);
            _game.Notify(headline);
            _game.Audio?.PlaySfx(MilestoneSfx);
        }

        /// <summary>
        /// On an inspection day, grades the shop and applies the reputation and cash outcome.
        /// Returns the headlines; empty when the inspector is not due.
        /// </summary>
        public List<string> RunInspectionIfDue(int day)
        {
            var headlines = new List<string>();
            if (Shop == null || !InspectorGrader.IsInspectionDay(day)) return headlines;

            var result = InspectorGrader.Grade(AveragePetHealth(), AveragePenCare(), EmptyShelfFraction());
            Shop.ChangeReputation(result.ReputationDelta);
            if (result.CashDelta >= 0f) Shop.ChangeBalance(result.CashDelta, "Inspection grant");
            else                        Shop.ForceCharge(-result.CashDelta, "Inspection fine");

            Debug.Log($"[Progression] Day {day} inspection grade {result.Grade} — " +
                      $"rep {result.ReputationDelta:+0;-0}, cash {result.CashDelta:+0;-0}");
            headlines.Add(result.Summary);
            return headlines;
        }

        private void ApplyLotStage()
        {
            if (_generator != null) _generator.ApplyLotStage(ProgressionRules.LotStageForTier(Tier));
        }

        /// <summary>Mean health of every resident animal; full marks when there are none.</summary>
        private float AveragePetHealth()
        {
            float total = 0f;
            int animals = 0;
            foreach (var pen in _game.Pens)
            {
                if (pen == null) continue;
                foreach (Pet pet in pen.Residents) { total += pet.health; animals++; }
            }
            return animals > 0 ? total / animals : FullMarks;
        }

        /// <summary>Mean of min(FoodLevel, Cleanliness) over occupied pens; full marks when none.</summary>
        private float AveragePenCare()
        {
            float total = 0f;
            int occupied = 0;
            foreach (var pen in _game.Pens)
            {
                if (pen == null || pen.Count == 0) continue;
                total += Mathf.Min(pen.FoodLevel, pen.Cleanliness);
                occupied++;
            }
            return occupied > 0 ? total / occupied : FullMarks;
        }

        /// <summary>Share of shelves that are empty (same test as ShopHUD); 0 when there are none.</summary>
        private float EmptyShelfFraction()
        {
            int shelves = 0, empty = 0;
            foreach (var shelf in _game.Shelves)
            {
                if (shelf == null) continue;
                shelves++;
                if (shelf.IsEmpty) empty++;
            }
            return shelves > 0 ? (float)empty / shelves : NoEmptyShelves;
        }

        private static string UnlockText(int tier)
        {
            if (tier >= ProgressionRules.TigerTier)          return "tiger pens and the whole yard are unlocked!";
            if (tier >= ProgressionRules.HorseTier)          return "horse pens are now available (Q in build mode)!";
            return "the back of the lot is yours to build on!";
        }
    }
}
