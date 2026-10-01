using System;
using System.Collections.Generic;
using System.Linq;
using PetShop.Commerce;
using PetShop.Pets;

namespace PetShop.Core
{
    /// <summary>Judges the pet show entry at closing time on show days.</summary>
    public static class ShowJudging
    {
        /// <summary>Runs the show for the manager's current day, using its pens.</summary>
        public static void Run(GameManager game)
        {
            Run(game.Show, game.Shop, game.Shop.Day, OwnedPets(game), game.Notify);
        }

        /// <summary>Every pet currently in any pen.</summary>
        public static List<Pet> OwnedPets(GameManager game)
        {
            var pets = new List<Pet>();
            foreach (var pen in game.Pens)
                if (pen != null) pets.AddRange(pen.Residents);
            return pets;
        }

        /// <summary>The theme judging will use on <paramref name="day"/>, given the pets owned now.</summary>
        public static ShowTheme CurrentTheme(GameManager game, int day) => ThemeOf(day, OwnedPets(game));

        /// <summary>Single source of truth for the theme: species drawn from the given pets.</summary>
        public static ShowTheme ThemeOf(int day, IEnumerable<Pet> pets) =>
            PetShow.ThemeFor(day, pets.Where(p => p != null).Select(p => p.species));

        /// <summary>
        /// On a show day, judges the entered pet if it is still owned (pays prize, reputation,
        /// ribbon for places 1-3); voids the entry without refund if it is gone. Clears the entry.
        /// </summary>
        public static void Run(PetShow show, ShopManager shop, int day, IEnumerable<Pet> owned, Action<string> notify)
        {
            if (!PetShow.IsShowDay(day) || show.EntryPetId == null) return;
            var pets = owned.ToList();
            if (!pets.Any(p => p != null && p.id == show.EntryPetId))
            {
                show.Withdraw();
                notify?.Invoke("Your show entry is void - the pet is no longer in the shop.");
                return;
            }
            var theme = ThemeOf(day, pets);
            var result = show.Resolve(day, theme, shop);
            if (result.HasValue) notify?.Invoke(Describe(result.Value));
        }

        private static string Describe(ShowResult r)
        {
            if (r.disqualified) return "Pet show: your entry was disqualified.";
            string prize = r.prize > 0f ? $" Prize €{r.prize:N0}." : "";
            return $"Pet show: your pet placed #{r.placement}.{prize}";
        }
    }
}
