using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// Pet show entry: the coming theme, the fee, eligible adults with an estimated placing,
    /// and the last show's result. The theme comes from <see cref="ShowJudging"/> so the
    /// entry is checked against exactly what the judges will use.
    ///
    /// Opened from the breeding panel, which already holds the modal flag, so this panel
    /// does not touch it.
    /// </summary>
    public class ShowcasePanel : MonoBehaviour
    {
        private const int MaxRows = 8;

        private GameObject  _root;
        private GameManager _game;
        private TMP_Text    _info;
        private TMP_Text    _last;
        private Transform   _list;
        private UnityEngine.UI.Button _withdraw;
        private readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _root = UIFactory.Panel("ShowDim", canvas, Vector2.zero, Vector2.one,
                                    UIFactory.Dim);
            var panel = UIFactory.ModalPanel("Showcase", _root.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), UIFactory.PanelBg,
                new Vector2(-400f, -260f), new Vector2(400f, 260f));
            UIFactory.Header(panel.transform, "Pet show", new Vector2(0.03f, 0.89f), new Vector2(0.5f, 0.97f));
            UIFactory.Button("Close", panel.transform, "Close",
                new Vector2(0.78f, 0.895f), new Vector2(0.97f, 0.965f), 15f).onClick.AddListener(Hide);
            _withdraw = UIFactory.Button("Withdraw", panel.transform, "Withdraw",
                new Vector2(0.55f, 0.895f), new Vector2(0.75f, 0.965f), 15f);
            _withdraw.onClick.AddListener(Withdraw);
            BuildBody(panel.transform);
            _root.SetActive(false);
        }

        private void BuildBody(Transform panel)
        {
            _info = UIFactory.Label("Info", panel, "",
                new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.88f), 16f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);
            _list = UIFactory.Node("Entrants", panel,
                new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.61f)).transform;
            _last = UIFactory.Label("Last", panel, "",
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.15f), 15f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);
        }

        public void Show()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private int ShowDay(int today) => today + PetShow.DaysUntilShow(today);

        private void Withdraw()
        {
            _game.Show.Withdraw();
            Refresh();
        }

        private void Enter(Pet pet, ShowTheme theme)
        {
            if (!_game.Show.Enter(pet, _game.Shop, theme))
                _game.Notify($"Cannot enter the show (fee €{PetShow.EntryFee:N0}).");
            Refresh();
        }

        private void Refresh()
        {
            foreach (var row in _rows) Destroy(row);
            _rows.Clear();

            int today = _game.Shop.Day;
            int day   = ShowDay(today);
            var theme = ShowJudging.CurrentTheme(_game, day);
            var pets  = ShowJudging.OwnedPets(_game);

            _info.text = InfoText(today, day, theme, pets);
            _withdraw.interactable = _game.Show.EntryPetId != null;
            DrawRows(day, theme, pets);
            _last.text = LastText();
        }

        private string InfoText(int today, int day, ShowTheme theme, List<Pet> pets)
        {
            int left = PetShow.DaysUntilShow(today);
            string when = left == 0 ? "tonight" : $"in {left} day(s)";
            var entry = pets.Find(p => p != null && p.id == _game.Show.EntryPetId);
            string mine = entry != null ? $"<color=#73DB95>{entry.DisplayName()}</color>" : "none";
            return $"Next show: day {day} ({when})\n" +
                   $"Judges want: <b>{theme.species}</b> with a <b>{theme.coatName}</b> coat\n" +
                   $"Entry fee: €{PetShow.EntryFee:N0} (not refunded if you withdraw)\n" +
                   $"Your entry: {mine}\n" +
                   "The theme species is drawn from the pets you own at closing time.";
        }

        private void DrawRows(int day, ShowTheme theme, List<Pet> pets)
        {
            var eligible = pets.FindAll(p => p != null && p.IsAdult && p.species == theme.species);
            if (eligible.Count == 0)
            {
                AddRow(UIFactory.Label("None", _list, $"No adult {theme.species} to enter.",
                    Vector2.zero, new Vector2(1f, 0.12f), 15f, UIFactory.InkMuted).gameObject);
                return;
            }
            float h = 1f / MaxRows;
            for (int i = 0; i < Mathf.Min(MaxRows, eligible.Count); i++)
                DrawRow(eligible[i], i, h, day, theme);
        }

        private void DrawRow(Pet pet, int i, float h, int day, ShowTheme theme)
        {
            float top = 1f - i * h, bottom = top - h * 0.88f;
            var r = _game.Show.Judge(day, pet, theme);
            AddRow(UIFactory.Label($"Pet_{i}", _list,
                $"{pet.DisplayName()}  ·  {pet.rarity}  ·  {pet.CoatName}  ·  est. #{r.placement}, prize €{r.prize:N0}",
                new Vector2(0f, bottom), new Vector2(0.78f, top), 14f).gameObject);
            var btn = UIFactory.Button($"Enter_{i}", _list, "Enter",
                new Vector2(0.80f, bottom), new Vector2(1f, top), 14f, UIFactory.ButtonOn);
            btn.interactable = _game.Show.EntryPetId == null;
            Pet captured = pet;
            btn.onClick.AddListener(() => Enter(captured, theme));
            AddRow(btn.gameObject);
        }

        private void AddRow(GameObject go) => _rows.Add(go);

        private string LastText()
        {
            var r = _game.Show.LastResult;
            if (!r.HasValue) return "Last show: no result yet.";
            if (r.Value.disqualified) return "Last show: disqualified.";
            return $"Last show: placed #{r.Value.placement}, prize €{r.Value.prize:N0}.";
        }
    }
}
