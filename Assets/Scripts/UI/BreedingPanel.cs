using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// Pick which two adults pair up tonight, instead of leaving it to
    /// <see cref="BreedingSystem.AdvanceDay"/>'s coin flip.
    ///
    /// A locked pairing is guaranteed to produce a baby, which is what makes choosing worth
    /// the clicks; pens left unpaired still breed at random. The panel shows the expected
    /// offspring so the choice is informed rather than a shot in the dark.
    /// </summary>
    public class BreedingPanel : MonoBehaviour
    {
        private GameObject  _root;
        private GameManager _game;

        private TMP_Text _penLabel;
        private TMP_Text _expectedLabel;
        private TMP_Text _statusLabel;
        private Button   _pairButton;
        private Button   _clearButton;

        private readonly List<GameObject> _petRows = new();
        private Transform _listRoot;

        private List<PetPen> _breedablePens = new();
        private int _penIndex;
        private Pet _pickA, _pickB;

        public bool IsOpen => _root != null && _root.activeSelf;

        private PetPen CurrentPen =>
            _penIndex >= 0 && _penIndex < _breedablePens.Count ? _breedablePens[_penIndex] : null;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("BreedDim", canvas, Vector2.zero, Vector2.one,
                                    new Color(0.03f, 0.05f, 0.08f, 0.62f));

            var panel = UIFactory.Panel("Breeding", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-400f, -260f), new Vector2(400f, 260f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            UIFactory.Label("Header", panel.transform, "Breeding",
                new Vector2(0.03f, 0.89f), new Vector2(0.5f, 0.97f), 24f, UIFactory.Ink);

            var close = UIFactory.Button("Close", panel.transform, "Close  (Esc)",
                new Vector2(0.78f, 0.895f), new Vector2(0.97f, 0.965f), 15f);
            close.onClick.AddListener(Hide);

            // Pen selector: one pen at a time keeps the list short enough to read.
            var prev = UIFactory.Button("PrevPen", panel.transform, "<",
                new Vector2(0.03f, 0.79f), new Vector2(0.09f, 0.865f), 18f);
            prev.onClick.AddListener(() => StepPen(-1));

            var next = UIFactory.Button("NextPen", panel.transform, ">",
                new Vector2(0.42f, 0.79f), new Vector2(0.48f, 0.865f), 18f);
            next.onClick.AddListener(() => StepPen(1));

            _penLabel = UIFactory.Label("PenName", panel.transform, "",
                new Vector2(0.10f, 0.79f), new Vector2(0.41f, 0.865f), 17f, UIFactory.Ink,
                TextAlignmentOptions.Center);

            _listRoot = UIFactory.Node("PetList", panel.transform,
                                       new Vector2(0.03f, 0.18f), new Vector2(0.48f, 0.78f)).transform;

            UIFactory.Label("PreviewCaption", panel.transform, "Tonight's pairing",
                new Vector2(0.52f, 0.79f), new Vector2(0.97f, 0.865f), 17f, UIFactory.InkMuted);

            _statusLabel = UIFactory.Label("Status", panel.transform, "",
                new Vector2(0.52f, 0.60f), new Vector2(0.97f, 0.78f), 16f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            _expectedLabel = UIFactory.Label("Expected", panel.transform, "",
                new Vector2(0.52f, 0.26f), new Vector2(0.97f, 0.58f), 15f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);

            _pairButton = UIFactory.Button("Pair", panel.transform, "Pair for tonight",
                new Vector2(0.52f, 0.11f), new Vector2(0.74f, 0.19f), 15f, UIFactory.ButtonOn);
            _pairButton.onClick.AddListener(ConfirmPair);

            _clearButton = UIFactory.Button("ClearPair", panel.transform, "Clear pairing",
                new Vector2(0.75f, 0.11f), new Vector2(0.97f, 0.19f), 15f);
            _clearButton.onClick.AddListener(ClearPair);

            _root.SetActive(false);
        }

        // ── Open / close ────────────────────────────────────────────────────────

        public void Show()
        {
            if (_root == null) return;

            CollectPens();
            _pickA = _pickB = null;
            _penIndex = 0;

            // Reopen on a pen that already has a pairing — that is the one worth looking at.
            for (int i = 0; i < _breedablePens.Count; i++)
                if (_breedablePens[i].HasValidPlan) { _penIndex = i; break; }

            AdoptExistingPlan();
            Refresh();

            _root.SetActive(true);
            _game?.SetModalOpen(true);
        }

        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        // ── State ───────────────────────────────────────────────────────────────

        private void CollectPens()
        {
            _breedablePens = new List<PetPen>();
            if (_game == null) return;

            foreach (var pen in _game.Pens)
                if (pen != null && pen.AdultCount >= 2) _breedablePens.Add(pen);
        }

        private void StepPen(int delta)
        {
            if (_breedablePens.Count == 0) return;

            _penIndex = (_penIndex + delta + _breedablePens.Count) % _breedablePens.Count;
            _pickA = _pickB = null;
            AdoptExistingPlan();
            Refresh();
        }

        /// <summary>Shows the pen's locked pairing as the current selection, if it has one.</summary>
        private void AdoptExistingPlan()
        {
            var pen = CurrentPen;
            if (pen != null && pen.HasValidPlan)
            {
                _pickA = pen.PlannedA;
                _pickB = pen.PlannedB;
            }
        }

        private void Pick(Pet pet)
        {
            if (_pickA == pet)      _pickA = null;
            else if (_pickB == pet) _pickB = null;
            else if (_pickA == null) _pickA = pet;
            else                     _pickB = pet;   // a third pick replaces the second

            Refresh();
        }

        private void ConfirmPair()
        {
            var pen = CurrentPen;
            if (pen == null || !BreedingSystem.CanPair(_pickA, _pickB)) return;

            pen.SetPlannedPair(_pickA, _pickB);
            _game?.Notify($"{_pickA.DisplayName()} and {_pickB.DisplayName()} are paired for tonight.");
            Refresh();
        }

        private void ClearPair()
        {
            CurrentPen?.ClearPlan();
            _pickA = _pickB = null;
            Refresh();
        }

        // ── Drawing ─────────────────────────────────────────────────────────────

        private void Refresh()
        {
            foreach (var row in _petRows) Destroy(row);
            _petRows.Clear();

            var pen = CurrentPen;

            if (pen == null)
            {
                _penLabel.text      = "no pens with two adults";
                _statusLabel.text   = "Breeding needs two fully grown animals in the same pen.\n" +
                                      "Buy another adult at a pen, or wait for a baby to grow up.";
                _expectedLabel.text = string.Empty;
                SetInteractable(_pairButton, false, UIFactory.ButtonOn);
                SetInteractable(_clearButton, false, UIFactory.ButtonBg);
                return;
            }

            _penLabel.text = $"{pen.PenSpecies} pen  ({_penIndex + 1} of {_breedablePens.Count})";

            // One row per adult; babies and juveniles are listed greyed so the player can see
            // what is coming rather than wondering where an animal went.
            var residents = new List<Pet>(pen.Residents);
            float rowHeight = 1f / Mathf.Max(6, residents.Count);

            for (int i = 0; i < residents.Count; i++)
            {
                Pet pet = residents[i];
                float top    = 1f - i * rowHeight;
                float bottom = top - rowHeight * 0.88f;

                if (!pet.IsAdult)
                {
                    var note = UIFactory.Label($"Young_{i}", _listRoot,
                        $"     {pet.petName}   ·   {pet.growthStage.ToString().ToLowerInvariant()}, " +
                        $"grown in {Mathf.Max(0, pet.daysToMature - pet.ageDays)} days",
                        new Vector2(0f, bottom), new Vector2(1f, top), 14f, UIFactory.InkMuted);
                    _petRows.Add(note.gameObject);
                    continue;
                }

                bool picked = pet == _pickA || pet == _pickB;
                string slot = pet == _pickA ? "A" : pet == _pickB ? "B" : "·";

                var btn = UIFactory.Button($"Pet_{i}", _listRoot,
                    $"{slot}   {pet.petName}   ·   {pet.rarity}   ·   {pet.Condition}",
                    new Vector2(0f, bottom), new Vector2(1f, top), 14f,
                    picked ? UIFactory.ButtonOn : UIFactory.ButtonBg);

                Pet captured = pet;
                btn.onClick.AddListener(() => Pick(captured));
                _petRows.Add(btn.gameObject);
            }

            bool canPair = BreedingSystem.CanPair(_pickA, _pickB);
            bool planned = pen.HasValidPlan;

            string a = _pickA != null ? _pickA.DisplayName() : "—";
            string b = _pickB != null ? _pickB.DisplayName() : "—";

            _statusLabel.text = planned && _pickA == pen.PlannedA && _pickB == pen.PlannedB
                ? $"<color=#73DB95>Paired tonight:</color>\n{a}  ×  {b}"
                : $"Selected:\n{a}  ×  {b}";

            if (!pen.HasSpace)
                _statusLabel.text += "\n<color=#F0A07A>The pen is full — no room for a baby.</color>";

            _expectedLabel.text = BreedingSystem.DescribeExpected(_pickA, _pickB);

            SetInteractable(_pairButton, canPair && pen.HasSpace, UIFactory.ButtonOn);
            SetInteractable(_clearButton, planned, UIFactory.ButtonBg);
        }

        private static void SetInteractable(Button button, bool on, Color enabledColour)
        {
            if (button == null) return;
            button.interactable = on;

            var image = button.GetComponent<Image>();
            if (image == null) return;

            // Keep full alpha: scaling the colour scales alpha too, and the button disappears.
            image.color = on ? enabledColour : new Color(0.13f, 0.16f, 0.21f, 0.96f);
        }
    }
}
