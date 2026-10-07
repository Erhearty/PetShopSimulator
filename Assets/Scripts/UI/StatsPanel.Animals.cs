using System.Text;
using UnityEngine;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book's Animals page: every pen with its residents and care, and the ways
    /// through to the breeding planner, family tree and showcase.
    /// </summary>
    public partial class StatsPanel
    {
        private const float LinkRowBottom = 0.015f, LinkRowTop = 0.085f;
        private const float LinkWidth = 0.32f, LinkStep = 0.34f;
        private const int   AdultsToBreed = 2;

        private BookPage _animals;

        private void BuildAnimalsPage()
        {
            _animals = AddPage("Animals", "Your pens, how the animals are cared for, and breeding.", UIFactory.TextSmall);
            var page = _animals.Root.transform;
            AddLink(page, 0, "Plan tonight's breeding", true, () => HandOff(() => GetComponent<BreedingPanel>()?.Show()));
            AddLink(page, 1, "Family tree", false, OpenFamilyTree);
            AddLink(page, 2, "Showcase", false, () => HandOff(() => GetComponent<ShowcasePanel>()?.Show()));
        }

        private void AddLink(Transform page, int slot, string text, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            float x = slot * LinkStep;
            var btn = UIFactory.Button($"Link_{text}", page, text, new Vector2(x, LinkRowBottom),
                new Vector2(x + LinkWidth, LinkRowTop), UIFactory.TextSmall,
                primary ? UIFactory.Accent : UIFactory.Raised);
            btn.onClick.AddListener(onClick);
        }

        /// <summary>The family tree of the first animal in the shop; a notice when there is none.</summary>
        private void OpenFamilyTree()
        {
            foreach (var pen in _game.Pens)
                if (pen != null)
                    foreach (var pet in pen.Residents)
                    {
                        HandOff(() => GetComponent<FamilyTreePanel>()?.Show(pet));
                        return;
                    }
            _game.Notify("No animals yet — buy one at a pen first.");
        }

        private static string PenRow(string a, string b, string c, string d, string e) =>
            $"{a}<pos=22%>{b}<pos=36%>{c}<pos=48%>{d}<pos=62%>{e}";

        private void RefreshAnimals()
        {
            var sb = new StringBuilder();
            if (_game.Pens.Count == 0) sb.AppendLine("No pens yet. Order one on the Build page.");
            else
            {
                sb.AppendLine(UIFactory.Tint(PenRow("Pen", "Animals", "Feed", "Bedding", "Note"), UIFactory.InkMuted));
                foreach (var pen in _game.Pens)
                    if (pen != null) sb.AppendLine(PenLine(pen));
            }
            sb.AppendLine();
            sb.AppendLine(UIFactory.Tint("New animals cost", UIFactory.InkMuted));
            foreach (Pet.Species species in System.Enum.GetValues(typeof(Pet.Species)))
                sb.Append($"{species} € {Pet.WholesalePrice(species):N0}    ");
            _animals.Body.text = sb.ToString();
        }

        private string PenLine(PetPen pen)
        {
            string care = UIFactory.Tint($"{pen.FoodLevel * 100f:0}%", pen.NeedsFeeding ? UIFactory.Destructive : UIFactory.Ink);
            return PenRow(pen.PenSpecies.ToString(), $"{pen.Count}/{pen.Capacity}", care,
                          $"{pen.Cleanliness * 100f:0}%", PenNote(pen));
        }

        private static string PenNote(PetPen pen)
        {
            string key = InputBindings.Label(GameAction.Interact);
            if (pen.Count == 0) return UIFactory.Tint($"Empty — press {key} at the pen to buy one", UIFactory.Warning);
            if (pen.NeedsService) return UIFactory.Tint($"Needs care — {key} at the pen, € {pen.ServiceCost:N2}", UIFactory.Destructive);
            if (pen.AdultCount >= AdultsToBreed && pen.HasSpace) return UIFactory.Tint("May breed tonight", UIFactory.Positive);
            if (pen.AdultCount < AdultsToBreed) return UIFactory.Tint("Needs two adults to breed", UIFactory.InkMuted);
            return UIFactory.Tint("Full — no room for young", UIFactory.InkMuted);
        }
    }
}
