using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Pets;

namespace PetShop.UI
{
    /// <summary>
    /// Three-generation family tree for one pet: the pet, its parents and its grandparents,
    /// drawn from <see cref="LineageRegistry"/> so animals that were sold still appear.
    /// Clicking an ancestor re-centres the tree on it; Back returns to the previous centre.
    ///
    /// Opened from the breeding panel, which already holds the modal flag, so this panel
    /// does not touch it.
    /// </summary>
    public class FamilyTreePanel : MonoBehaviour
    {
        private const float BoxW = 0.22f;

        private static readonly float[] GrandX  = { 0.03f, 0.27f, 0.51f, 0.75f };
        private static readonly float[] ParentX = { 0.15f, 0.63f };
        private static readonly Color FocusBg   = UIFactory.FocusFill;

        private GameObject  _root;
        private GameManager _game;
        private TMP_Text    _title;
        private Transform   _content;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly Stack<string> _history = new Stack<string>();
        private string _focusId;
        private LineageEntry _fallback;   // the opened pet, when it was never registered

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, GameManager game)
        {
            _game = game;

            _root = UIFactory.Panel("TreeDim", canvas, Vector2.zero, Vector2.one,
                                    UIFactory.Dim);

            var panel = UIFactory.ModalPanel("FamilyTree", _root.transform,
                                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        UIFactory.PanelBg,
                                        new Vector2(-430f, -260f), new Vector2(430f, 260f));

            UIFactory.Panel("Accent", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);

            _title = UIFactory.Label("Header", panel.transform, Loc.T("family.title"),
                new Vector2(0.03f, 0.89f), new Vector2(0.52f, 0.97f), 24f, UIFactory.Ink);

            var back = UIFactory.ButtonKey("Back", panel.transform, "family.back",
                new Vector2(0.55f, 0.895f), new Vector2(0.75f, 0.965f), 15f);
            back.onClick.AddListener(GoBack);

            var close = UIFactory.ButtonKey("Close", panel.transform, "common.close",
                new Vector2(0.78f, 0.895f), new Vector2(0.97f, 0.965f), 15f);
            close.onClick.AddListener(Hide);

            UIFactory.LabelKey("Hint", panel.transform, "family.hint",
                new Vector2(0.64f, 0.06f), new Vector2(0.97f, 0.30f), 13f, UIFactory.InkMuted);

            _content = UIFactory.Node("Content", panel.transform, Vector2.zero, Vector2.one).transform;
            _root.SetActive(false);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Redraws the tree in the new language while the panel is open.</summary>
        private void OnLanguageChanged()
        {
            if (IsOpen) Redraw();
        }

        // ── Open / close ────────────────────────────────────────────────────────

        public void Show(Pet pet)
        {
            if (_root == null || pet == null) return;

            _history.Clear();
            _focusId  = pet.id;
            _fallback = LineageRegistry.Get(pet.id) == null ? FromPet(pet) : null;

            Redraw();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void GoBack()
        {
            if (_history.Count == 0) { Hide(); return; }
            _focusId = _history.Pop();
            Redraw();
        }

        private void Navigate(string id)
        {
            _history.Push(_focusId);
            _focusId = id;
            Redraw();
        }

        // ── Lookup ──────────────────────────────────────────────────────────────

        private static LineageEntry FromPet(Pet p) => new LineageEntry
        {
            id = p.id, petName = p.petName, species = p.species, rarity = p.rarity, coat = p.coat,
            parentAId = p.parentAId, parentBId = p.parentBId, generation = p.generation,
        };

        private LineageEntry Resolve(string id)
        {
            if (_fallback != null && id == _fallback.id) return _fallback;
            return LineageRegistry.Get(id);
        }

        private LineageEntry ParentOf(LineageEntry e, bool first)
        {
            if (e == null) return null;
            return Resolve(first ? e.parentAId : e.parentBId);
        }

        private bool IsInPen(string id)
        {
            if (_game == null || string.IsNullOrEmpty(id)) return false;

            foreach (var pen in _game.Pens)
            {
                if (pen == null) continue;
                foreach (var pet in pen.Residents)
                    if (pet != null && pet.id == id) return true;
            }
            return false;
        }

        // ── Drawing ─────────────────────────────────────────────────────────────

        private void Redraw()
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();

            var focus = Resolve(_focusId);
            _title.text = focus != null && !string.IsNullOrEmpty(focus.petName)
                ? Loc.F("family.title_named", focus.petName) : Loc.T("family.title");

            DrawBox(focus, 0.39f, 0.06f, 0.30f, true);
            for (int i = 0; i < 2; i++)
            {
                var parent = ParentOf(focus, i == 0);
                DrawBox(parent, ParentX[i], 0.33f, 0.57f, false);
                DrawBox(ParentOf(parent, true),  GrandX[i * 2],     0.60f, 0.84f, false);
                DrawBox(ParentOf(parent, false), GrandX[i * 2 + 1], 0.60f, 0.84f, false);
            }
        }

        private void DrawBox(LineageEntry e, float x, float y0, float y1, bool focus)
        {
            var min = new Vector2(x, y0);
            var max = new Vector2(x + BoxW, y1);

            if (e == null)
            {
                var none = UIFactory.Label("Unknown", _content, Loc.T("family.unknown"), min, max, 14f,
                                           UIFactory.InkMuted, TextAlignmentOptions.Center);
                _spawned.Add(none.gameObject);
                return;
            }

            var box = UIFactory.Panel($"Box_{e.id}", _content, min, max,
                                      focus ? FocusBg : UIFactory.ButtonBg);
            _spawned.Add(box);
            AddLabels(box.transform, e);
            if (!focus) MakeClickable(box, e.id);
        }

        private void AddLabels(Transform box, LineageEntry e)
        {
            UIFactory.Label("Name", box, e.petName,
                new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.95f), 16f, UIFactory.Ink);

            string sold = IsInPen(e.id) ? "" : $"  ·  {Loc.T("family.sold")}";
            string info = LocNames.Rarity(e.rarity) + "  ·  " + Loc.F("family.generation", e.generation) + sold;
            UIFactory.AutoFit(UIFactory.Label("Info", box, info,
                new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.64f), 13f, UIFactory.InkMuted));

            var coat   = UIFactory.WithAlpha(e.coat, 1f);
            var swatch = UIFactory.Panel("Swatch", box,
                new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.36f), coat);
            UIFactory.Label("CoatName", swatch.transform, CoatColours.DisplayName(CoatColours.Classify(e.coat)),
                Vector2.zero, Vector2.one, 13f, SwatchTextColour(coat), TextAlignmentOptions.Center);
        }

        private void MakeClickable(GameObject box, string id)
        {
            var btn = box.AddComponent<Button>();
            btn.targetGraphic = box.GetComponent<Image>();
            btn.onClick.AddListener(() => Navigate(id));
        }

        // ── Contrast ────────────────────────────────────────────────────────────

        /// <summary>Black or white, whichever contrasts more with <paramref name="bg"/> (always ≥ 4.5:1).</summary>
        internal static Color SwatchTextColour(Color bg)
        {
            float l = Luminance(bg);
            float withWhite = 1.05f / (l + 0.05f);
            float withBlack = (l + 0.05f) / 0.05f;
            return withBlack >= withWhite ? UIFactory.SwatchInkDark : UIFactory.SwatchInkLight;
        }

        private static float Luminance(Color c) =>
            0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        private static float Linear(float v) =>
            v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }
}
