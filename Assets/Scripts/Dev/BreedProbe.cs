using System.Collections.Generic;
using UnityEngine;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.Dev
{
    /// <summary>
    /// Exercises the one gameplay path a screenshot cannot show: that a pairing locked in
    /// through the breeding panel actually produces a baby overnight, and that a stale plan
    /// (one parent sold since) is ignored rather than breeding a ghost.
    ///
    /// Run at the end of the tour, after every frame has been captured — it ages animals and
    /// adds residents, so it must not touch the world before the photographs are taken.
    /// </summary>
    public static class BreedProbe
    {
        public static string Run()
        {
            var game = GameManager.Instance;
            if (game == null) return "[BreedProbe] no GameManager";

            PetPen pen = null;
            foreach (var candidate in game.Pens)
                if (candidate != null && candidate.AdultCount >= 2 && candidate.HasSpace)
                {
                    pen = candidate;
                    break;
                }

            if (pen == null) return "[BreedProbe] no pen with two adults and room — skipped";

            var adults = new List<Pet>();
            foreach (var p in pen.Residents)
                if (p.IsAdult) adults.Add(p);

            // 1. A locked pairing must breed, not roll the 45% dice.
            pen.SetPlannedPair(adults[0], adults[1]);
            bool planValid = pen.HasValidPlan;

            int before = pen.Count;
            BreedingSystem.AdvanceDay(new[] { pen });
            int after = pen.Count;

            bool bred    = after > before;
            bool cleared = !pen.HasValidPlan;

            // 2. A plan naming an animal that is no longer resident must be ignored.
            var stranger = BreedingSystem.GenerateRandom(pen.PenSpecies);
            pen.SetPlannedPair(adults[0], stranger);
            bool staleRejected = !pen.HasValidPlan;
            pen.ClearPlan();

            return $"[BreedProbe] plan valid: {planValid}, bred on the night: {bred} " +
                   $"({before} → {after}), plan cleared after use: {cleared}, " +
                   $"stale plan rejected: {staleRejected}";
        }
    }
}
