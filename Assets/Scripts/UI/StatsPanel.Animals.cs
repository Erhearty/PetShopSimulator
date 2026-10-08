using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
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

        private const float PetRowPx = 30f;

        private BookPage  _animals;
        private Transform _petHost;
        private TMP_Text  _animalsText;

        private void BuildAnimalsPage()
        {
            _animals = AddPage("Animals", UIFactory.TextSmall);
            var page = _animals.Root.transform;
            _animals.Body.gameObject.SetActive(false);
            var content = BuildScrollContent(page);
            _animalsText = UIFactory.Label("Pens", content, "", Vector2.zero, Vector2.one,
                UIFactory.TextSmall, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _animalsText.richText     = true;
            _animalsText.overflowMode = TextOverflowModes.Overflow;
            _petHost = AddColumn(content, "Pets");
            AddLink(page, 0, "Plan tonight's breeding", "animals.link.breeding", true, () => HandOff(() => GetComponent<BreedingPanel>()?.Show()));
            AddLink(page, 1, "Family tree", "animals.link.family_tree", false, OpenFamilyTree);
            AddLink(page, 2, "Showcase", "animals.link.showcase", false, () => HandOff(() => GetComponent<ShowcasePanel>()?.Show()));
        }

        private void AddLink(Transform page, int slot, string id, string key, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            float x = slot * LinkStep;
            var btn = UIFactory.ButtonKey($"Link_{id}", page, key, new Vector2(x, LinkRowBottom),
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
            _game.Notify(Loc.T("animals.no_animals"));
        }

        private static string PenRow(string a, string b, string c, string d, string e) =>
            $"{a}<pos=22%>{b}<pos=36%>{c}<pos=48%>{d}<pos=62%>{e}";

        private void RefreshAnimals()
        {
            var sb = new StringBuilder();
            if (_game.Pens.Count == 0) sb.AppendLine(Loc.T("animals.no_pens"));
            else
            {
                sb.AppendLine(UIFactory.Tint(PenRow(Loc.T("animals.col.pen"), Loc.T("animals.col.animals"),
                    Loc.T("animals.col.feed"), Loc.T("animals.col.bedding"), Loc.T("animals.col.note")), UIFactory.InkMuted));
                foreach (var pen in _game.Pens)
                    if (pen != null) sb.AppendLine(PenLine(pen));
            }
            sb.AppendLine();
            sb.AppendLine(UIFactory.Tint(Loc.T("animals.prices"), UIFactory.InkMuted));
            foreach (Pet.Species species in System.Enum.GetValues(typeof(Pet.Species)))
                sb.Append($"{LocNames.Species(species)} € {Pet.WholesalePrice(species):N0}    ");
            sb.AppendLine();
            sb.AppendLine(UIFactory.Tint(Loc.T("animals.lock_hint"), UIFactory.InkMuted));
            _animalsText.text = sb.ToString();
            RebuildPetRows();
        }

        /// <summary>One row per animal with a Lock / Unlock toggle; a locked animal is never sold.</summary>
        private void RebuildPetRows()
        {
            ClearChildren(_petHost);
            foreach (var pen in _game.Pens)
            {
                if (pen == null) continue;
                foreach (var pet in pen.Residents)
                {
                    var row = AddRow(_petHost, "Pet", PetRowPx).transform;
                    string text = pet.DisplayName() + (pet.Locked ? " " + Loc.T("animals.locked_tag") : "");
                    UIFactory.Label("Name", row, text, Vector2.zero, new Vector2(0.74f, 1f), UIFactory.TextSmall,
                        pet.Locked ? UIFactory.Accent : UIFactory.Ink);
                    var toggle = UIFactory.ButtonKey("Lock", row, pet.Locked ? "animals.unlock" : "animals.lock",
                        new Vector2(0.76f, 0.08f), new Vector2(1f, 0.92f), UIFactory.TextSmall);
                    var captured = pet;
                    toggle.onClick.AddListener(() => { captured.Locked = !captured.Locked; Refresh(); });
                }
            }
        }

        private string PenLine(PetPen pen)
        {
            string care = UIFactory.Tint($"{pen.FoodLevel * 100f:0}%", pen.NeedsFeeding ? UIFactory.Destructive : UIFactory.Ink);
            return PenRow(LocNames.Species(pen.PenSpecies), $"{pen.Count}/{pen.Capacity}", care,
                          $"{pen.Cleanliness * 100f:0}%", PenNote(pen));
        }

        private static string PenNote(PetPen pen)
        {
            string key = InputBindings.Label(GameAction.Interact);
            if (pen.Count == 0) return UIFactory.Tint(Loc.F("animals.note.empty", key), UIFactory.Warning);
            if (pen.NeedsService) return UIFactory.Tint(Loc.F("animals.note.needs_care", key, pen.ServiceCost), UIFactory.Destructive);
            if (pen.AdultCount >= AdultsToBreed && pen.HasSpace) return UIFactory.Tint(Loc.T("animals.note.may_breed"), UIFactory.Positive);
            if (pen.AdultCount < AdultsToBreed) return UIFactory.Tint(Loc.T("animals.note.needs_adults"), UIFactory.InkMuted);
            return UIFactory.Tint(Loc.T("animals.note.full"), UIFactory.InkMuted);
        }
    }
}
