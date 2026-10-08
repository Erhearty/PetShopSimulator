using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// A pen's info panel, opened by clicking the plaque on the pen (never on hover): species, how many
    /// pets live there, feed and bedding levels, and every resident with a toggle that locks it from
    /// being sold (<see cref="Pet.Locked"/>). Holds <see cref="GamePause"/> while open; Esc (via
    /// <see cref="GameUI.HandleEscape"/>), the interact key or the close button close it.
    /// </summary>
    public class PenInfoPanel : MonoBehaviour
    {
        private const float PanelHalfWidth  = 340f;
        private const float PanelHalfHeight = 280f;
        /// <summary>Most residents listed; a pen holds <see cref="PetShop.Shop.BuildCatalog.PenCapacity"/>.</summary>
        private const int MaxRows = 6;
        private const float RowTop = 0.64f, RowHeight = 0.08f, RowGap = 0.012f;

        private sealed class Row
        {
            public GameObject Root;
            public TMP_Text   Text;
            public UnityEngine.UI.Button Toggle;
            public TMP_Text   ToggleText;
        }

        private GameObject _root;
        private GameManager _game;
        private TMP_Text _title, _summary, _empty;
        private readonly List<Row> _rows = new();
        private PetPen _pen;
        private bool _wasModal;
        private bool _holdsPause;
        private int  _openedFrame = -1;

        /// <summary>True while the panel is on screen.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>The pen shown, or null when closed.</summary>
        public PetPen Pen => IsOpen ? _pen : null;

        /// <summary>Builds the (hidden) panel under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _root = UIFactory.Panel("PenInfoDim", canvas, Vector2.zero, Vector2.one, UIFactory.Dim);
            var panel = UIFactory.ModalPanel("PenInfo", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                UIFactory.Surface, new Vector2(-PanelHalfWidth, -PanelHalfHeight),
                new Vector2(PanelHalfWidth, PanelHalfHeight)).transform;

            _title = UIFactory.Header(panel, "", new Vector2(0.04f, 0.88f), new Vector2(0.72f, 0.98f));
            var close = UIFactory.ButtonKey("Close", panel, "common.close_esc",
                new Vector2(0.76f, 0.89f), new Vector2(0.97f, 0.97f), UIFactory.TextSmall, null, "Esc");
            close.onClick.AddListener(Hide);

            _summary = UIFactory.Label("Summary", panel, "", new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.87f),
                UIFactory.TextBody, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _empty = UIFactory.LabelKey("Empty", panel, "pen.info.empty", new Vector2(0.04f, RowTop - RowHeight),
                new Vector2(0.96f, RowTop), UIFactory.TextBody, UIFactory.InkMuted);
            UIFactory.LabelKey("Hint", panel, "pen.info.hint", new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.1f),
                UIFactory.TextSmall, UIFactory.InkMuted);

            for (int i = 0; i < MaxRows; i++) _rows.Add(MakeRow(panel, i));
            _root.SetActive(false);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private Row MakeRow(Transform panel, int index)
        {
            float top = RowTop - index * (RowHeight + RowGap);
            var row = new Row
            {
                Root = UIFactory.Node($"Row_{index}", panel, new Vector2(0.04f, top - RowHeight), new Vector2(0.96f, top))
            };
            row.Text = UIFactory.Label("Text", row.Root.transform, "", Vector2.zero, new Vector2(0.64f, 1f),
                UIFactory.TextSmall, UIFactory.Ink);
            UIFactory.AutoFit(row.Text);
            row.Toggle = UIFactory.Button("Lock", row.Root.transform, "", new Vector2(0.66f, 0f), Vector2.one,
                UIFactory.TextSmall);
            row.ToggleText = row.Toggle.GetComponentInChildren<TMP_Text>();
            return row;
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            ReleasePause();
        }

        private void OnDisable() => ReleasePause();

        private void OnLanguageChanged()
        {
            if (IsOpen) Refresh();
        }

        /// <summary>Opens the panel for <paramref name="pen"/>, pausing the game.</summary>
        public void Show(PetPen pen)
        {
            if (_root == null || pen == null) return;
            _pen = pen;
            if (!IsOpen)
            {
                _wasModal = _game != null && _game.IsModalOpen;
                _game?.SetModalOpen(true);
                if (!_holdsPause) { GamePause.Acquire(); _holdsPause = true; }
                _openedFrame = Time.frameCount;
            }
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
            Refresh();
        }

        /// <summary>Closes the panel and gives the game back.</summary>
        public void Hide()
        {
            if (_root == null || !_root.activeSelf) return;
            _root.SetActive(false);
            _pen = null;
            _game?.SetModalOpen(_wasModal);
            ReleasePause();
        }

        private void ReleasePause()
        {
            if (!_holdsPause) return;
            _holdsPause = false;
            GamePause.Release();
        }

        private void Update()
        {
            // The key press that opened the panel is still "down" this frame, so it must not close it again.
            if (IsOpen && Time.frameCount != _openedFrame && InputBindings.GetKeyDown(GameAction.Interact)) Hide();
        }

        /// <summary>Redraws the title, summary and resident rows from the pen.</summary>
        public void Refresh()
        {
            if (_pen == null) return;
            _title.text   = TitleText(_pen);
            _summary.text = SummaryText(_pen);

            var residents = _pen.Residents;
            _empty.gameObject.SetActive(residents.Count == 0);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool used = i < residents.Count;
                row.Root.SetActive(used);
                if (!used) continue;

                Pet pet = residents[i];
                row.Text.text = RowText(pet);
                UIFactory.SetSelected(row.Toggle, pet.Locked);
                row.ToggleText.text = LockText(pet);
                row.Toggle.onClick.RemoveAllListeners();
                row.Toggle.onClick.AddListener(() => { ToggleLock(pet); Refresh(); });
            }
        }

        // ── Data model (static, so it can be tested without any UI) ─────────────

        /// <summary>The panel heading: the pen's species.</summary>
        public static string TitleText(PetPen pen) => Loc.F("pen.info.title", LocNames.Species(pen.PenSpecies));

        /// <summary>Pet count against capacity, feed and bedding levels, and what the pen needs.</summary>
        public static string SummaryText(PetPen pen)
        {
            string text = Loc.F("pen.info.summary", pen.Count, pen.Capacity, pen.FoodLevel * 100f, pen.Cleanliness * 100f);
            if (pen.HasValidPlan) text += "\n" + Loc.T("pen.info.paired");
            if (pen.NeedsService) text += "\n" + Loc.F("pen.info.needs_service", pen.ServiceCost);
            else if (pen.HasSpace && pen.AdultCount >= 2) text += "\n" + Loc.T("pen.describe.may_breed").Trim();
            else if (!pen.HasSpace) text += "\n" + Loc.T("pen.describe.full").Trim();
            return text;
        }

        /// <summary>One resident's line: name, condition and sell price.</summary>
        public static string RowText(Pet pet) => Loc.F("pen.info.row", pet.DisplayName(), pet.Condition, pet.SellPrice(), pet.hunger * 100f);

        /// <summary>The lock button's caption for <paramref name="pet"/>'s current state.</summary>
        public static string LockText(Pet pet) => Loc.T(pet.Locked ? "pen.info.locked" : "pen.info.unlocked");

        /// <summary>Flips <see cref="Pet.Locked"/> and returns the new state; a null pet is ignored.</summary>
        public static bool ToggleLock(Pet pet)
        {
            if (pet == null) return false;
            pet.Locked = !pet.Locked;
            return pet.Locked;
        }
    }
}
