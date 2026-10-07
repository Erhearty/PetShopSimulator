using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Localization;

namespace PetShop.UI
{
    /// <summary>
    /// The in-game guide: the <see cref="GuideContent"/> sections listed on the left, the chosen
    /// section's text on the right, split into pages. Opened from the Shop book, the pause menu,
    /// the title screen or <see cref="GameAction.Guide"/>; Esc closes it via
    /// <see cref="GameUI.HandleEscape"/>. Up/Down pick a section, Left/Right turn pages.
    /// </summary>
    public class GuidePanel : MonoBehaviour
    {
        private const float PanelHalfWidth  = 520f;
        private const float PanelHalfHeight = 320f;
        private const float ListLeft = 0.02f, ListRight = 0.3f, ListTop = 0.86f;
        private const float RowHeight = 0.058f, RowGap = 0.008f;
        private const float TextLeft = 0.33f, TextRight = 0.97f;
        private const float TitleBottom = 0.86f, TitleTop = 0.96f;
        private const float BodyBottom = 0.14f, BodyTop = 0.84f;
        private const float PagerBottom = 0.03f, PagerTop = 0.1f, PagerButtonWidth = 0.12f;
        private const float CloseLeft = 0.8f;
        private const float RowTextIndent = UIFactory.Gap * 1.5f;

        private GameObject _root;
        private GameManager _game;
        private TMP_Text _title, _body, _pageLabel;
        private Button _prev, _next;
        private static readonly Navigation NoNavigation = new() { mode = Navigation.Mode.None };

        private readonly List<Button> _rows = new();
        private IReadOnlyList<GuideSection> _sections;
        private bool _wasModal;
        private GameObject _previousSelection;

        /// <summary>True while the guide is on screen.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>Index of the section showing.</summary>
        public int CurrentSection { get; private set; }

        /// <summary>The page of the current section showing (1-based).</summary>
        public int CurrentPage { get; private set; } = 1;

        /// <summary>How many pages the current section's text fills.</summary>
        public int PageCount => _body != null ? Mathf.Max(1, _body.textInfo.pageCount) : 1;

        /// <summary>Builds the (hidden) guide under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _sections = GuideContent.Sections();
            _root = UIFactory.Panel("GuideDim", canvas, Vector2.zero, Vector2.one, UIFactory.Dim);
            var panel = UIFactory.Card("Guide", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                UIFactory.Surface, new Vector2(-PanelHalfWidth, -PanelHalfHeight),
                new Vector2(PanelHalfWidth, PanelHalfHeight)).transform;

            UIFactory.LabelKey("Heading", panel, "guide.heading", new Vector2(ListLeft, TitleBottom),
                new Vector2(ListRight, TitleTop), UIFactory.TextHeading, UIFactory.Ink);
            for (int i = 0; i < _sections.Count; i++) _rows.Add(MakeRow(panel, i));
            BuildText(panel);
            BuildPager(panel);
            _root.SetActive(false);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Rebuilds the section text in the new language and redraws the open section.</summary>
        private void OnLanguageChanged()
        {
            _sections = GuideContent.Sections();
            if (IsOpen) ShowSection(CurrentSection);
        }

        private Button MakeRow(Transform panel, int index)
        {
            float top = ListTop - index * (RowHeight + RowGap);
            var btn = UIFactory.ButtonKey($"Section_{index}", panel, _sections[index].TitleKey,
                new Vector2(ListLeft, top - RowHeight), new Vector2(ListRight, top), UIFactory.TextSmall);
            btn.navigation = NoNavigation;
            var label = btn.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin    = new Vector4(RowTextIndent, 0f, 0f, 0f);
            btn.onClick.AddListener(() => ShowSection(index));
            return btn;
        }

        private void BuildText(Transform panel)
        {
            _title = UIFactory.Label("Title", panel, "", new Vector2(TextLeft, TitleBottom),
                new Vector2(CloseLeft, TitleTop), UIFactory.TextHeading, UIFactory.Ink);
            _body = UIFactory.Label("Body", panel, "", new Vector2(TextLeft, BodyBottom),
                new Vector2(TextRight, BodyTop), UIFactory.TextBody, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _body.overflowMode = TextOverflowModes.Page;

            var close = UIFactory.ButtonKey("Close", panel, "common.close_esc", new Vector2(CloseLeft, TitleBottom + RowGap),
                new Vector2(TextRight, TitleTop), UIFactory.TextSmall, null, "Esc");
            close.navigation = NoNavigation;
            close.onClick.AddListener(Hide);
        }

        private void BuildPager(Transform panel)
        {
            _prev = UIFactory.ButtonKey("Prev", panel, "guide.prev", new Vector2(TextLeft, PagerBottom),
                new Vector2(TextLeft + PagerButtonWidth, PagerTop), UIFactory.TextSmall);
            _prev.navigation = NoNavigation;
            _prev.onClick.AddListener(() => TurnPage(-1));
            _next = UIFactory.ButtonKey("Next", panel, "guide.next", new Vector2(TextRight - PagerButtonWidth, PagerBottom),
                new Vector2(TextRight, PagerTop), UIFactory.TextSmall);
            _next.navigation = NoNavigation;
            _next.onClick.AddListener(() => TurnPage(1));
            _pageLabel = UIFactory.Label("Page", panel, "", new Vector2(TextLeft + PagerButtonWidth, PagerBottom),
                new Vector2(TextRight - PagerButtonWidth, PagerTop), UIFactory.TextSmall, UIFactory.InkMuted,
                TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Opens the guide over everything else (title screen included), pauses the clock and takes
        /// keyboard focus so Up/Down/Enter cannot drive the menu behind it.
        /// </summary>
        public void Show()
        {
            if (_root == null) return;
            _sections = GuideContent.Sections();   // rebuilt so rebinds show
            _root.transform.SetAsLastSibling();
            if (!IsOpen) TakeFocus();
            _root.SetActive(true);
            _game?.SetModalOpen(true);
            ShowSection(CurrentSection);
        }

        /// <summary>
        /// Closes the guide, leaving the clock paused only if something else had paused it, and hands
        /// keyboard focus back to whatever had it before (title screen, pause menu, journal).
        /// </summary>
        public void Hide()
        {
            if (_root == null || !_root.activeSelf) return;
            _root.SetActive(false);
            _game?.SetModalOpen(_wasModal);
            RestoreFocus();
        }

        /// <summary>Remembers the modal state and the selected control, then clears the selection.</summary>
        private void TakeFocus()
        {
            _wasModal = _game != null && _game.IsModalOpen;
            var events = EventSystem.current;
            _previousSelection = events != null ? events.currentSelectedGameObject : null;
            if (events != null) events.SetSelectedGameObject(null);
        }

        /// <summary>Reselects the control that had focus before <see cref="Show"/>, if it is still on screen.</summary>
        private void RestoreFocus()
        {
            var events = EventSystem.current;
            var previous = _previousSelection;
            _previousSelection = null;
            if (events == null) return;
            events.SetSelectedGameObject(previous != null && previous.activeInHierarchy ? previous : null);
        }

        /// <summary>Opens the guide when closed, closes it when open.</summary>
        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        /// <summary>Shows section <paramref name="index"/> (clamped) from its first page.</summary>
        public void ShowSection(int index)
        {
            if (_sections == null || _sections.Count == 0) return;
            CurrentSection = Mathf.Clamp(index, 0, _sections.Count - 1);
            for (int i = 0; i < _rows.Count; i++) UIFactory.SetSelected(_rows[i], i == CurrentSection);
            _title.text = _sections[CurrentSection].Title;
            _body.text  = _sections[CurrentSection].Body;
            _body.ForceMeshUpdate();
            SetPage(1);
        }

        /// <summary>Moves <paramref name="delta"/> pages through the current section (clamped).</summary>
        public void TurnPage(int delta) => SetPage(CurrentPage + delta);

        private void SetPage(int page)
        {
            CurrentPage = Mathf.Clamp(page, 1, PageCount);
            _body.pageToDisplay = CurrentPage;
            _pageLabel.text = Loc.F("guide.page", CurrentPage, PageCount);
            _prev.interactable = CurrentPage > 1;
            _next.interactable = CurrentPage < PageCount;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.DownArrow)) ShowSection((CurrentSection + 1) % _sections.Count);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) ShowSection((CurrentSection - 1 + _sections.Count) % _sections.Count);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) TurnPage(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) TurnPage(-1);
        }
    }
}
