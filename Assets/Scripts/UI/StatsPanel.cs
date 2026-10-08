using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The Shop book (Tab): a left-hand nav of pages - Overview, Stock, Animals, Staff, Build
    /// and Guide - each a heading, a one-line purpose and its content. It answers "what should
    /// I do next?" at a glance. Up/Down, Tab/Shift+Tab and the number keys switch pages.
    /// </summary>
    public partial class StatsPanel : MonoBehaviour
    {
        /// <summary>Page index of Overview.</summary>
        public const int OverviewTab = 0;
        /// <summary>Page index of Stock.</summary>
        public const int StockTab = 1;
        /// <summary>Page index of Animals.</summary>
        public const int AnimalsTab = 2;
        /// <summary>Page index of Staff.</summary>
        public const int StaffTab = 3;
        /// <summary>Page index of Build.</summary>
        public const int BuildTab = 4;
        /// <summary>Page index of Guide.</summary>
        public const int GuideTab = 5;

        /// <summary>Key that cycles pages while the book is open (Shift reverses).</summary>
        public const KeyCode CyclePagesKey = KeyCode.Tab;

        /// <summary>Units per wholesale order — one pallet.</summary>
        public const int OrderSize = 12;

        // Layout, in reference pixels and panel fractions.
        private const float PanelHalfWidth  = 560f;
        private const float PanelHalfHeight = 340f;
        private const float NavLeft   = 0.02f, NavRight = 0.2f;
        private const float NavTop    = 0.84f, NavRowHeight = 0.075f, NavRowGap = 0.012f;
        private const float BorderWidth = 1f;
        private static readonly Vector2 ContentMin = new(0.23f, 0.03f);
        private static readonly Vector2 ContentMax = new(0.97f, 0.96f);
        // Within a page (fractions of the content area).
        private const float HeadingBottom = 0.9f, PurposeBottom = 0.84f;
        private const float BodyTop = 0.82f, BodyBottom = 0.2f, CloseLeft = 0.8f;
        private const float NavTextIndent = UIFactory.Gap * 2f;

        /// <summary>One page of the book: its root, body text and nav button.</summary>
        private sealed class BookPage
        {
            public GameObject Root;
            public TMP_Text   Body;
            public Button     Nav;
        }

        private GameObject  _root;
        private Transform   _panel;
        private GameManager _game;
        private ReorderPanel _reorder;
        private readonly List<BookPage> _pages = new();
        private int _current;
        private int _openedFrame = -1;
        private bool _holdsPause;

        /// <summary>True while the book is on screen.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>The page currently showing.</summary>
        public int CurrentTab => _current;

        /// <summary>Number of pages in the book.</summary>
        public int TabCount => _pages.Count;

        /// <summary>
        /// True while the book is open and the toggle key is <see cref="CyclePagesKey"/>: the
        /// key then cycles pages instead of closing the book (Esc closes it).
        /// </summary>
        public bool CyclesWithLedgerKey => IsOpen && InputBindings.Get(GameAction.Ledger) == CyclePagesKey;

        /// <summary>
        /// Builds the (hidden) book under <paramref name="canvas"/>. <paramref name="build"/> is
        /// the build mode the Build page places furniture with.
        /// </summary>
        public void Build(Transform canvas, GameManager game, ReorderPanel reorder = null, BuildMode build = null)
        {
            _game = game;
            _reorder = reorder;

            _root = UIFactory.Panel("StatsDim", canvas, Vector2.zero, Vector2.one, UIFactory.Dim);
            _panel = UIFactory.Card("Stats", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                UIFactory.Surface, new Vector2(-PanelHalfWidth, -PanelHalfHeight),
                new Vector2(PanelHalfWidth, PanelHalfHeight)).transform;

            BuildChrome();
            BuildOverviewPage();
            BuildStockPage();
            BuildAnimalsPage();
            BuildStaffPage();
            BuildBuildPage(build);
            BuildGuidePage();

            _root.SetActive(false);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Rewrites the page bodies in the new language while the book is open.</summary>
        private void OnLanguageChanged()
        {
            if (IsOpen) Refresh();
        }

        /// <summary>Title, divider, nav hint and the Close button.</summary>
        private void BuildChrome()
        {
            UIFactory.LabelKey("Title", _panel, "tabs.title", new Vector2(NavLeft, 0.89f),
                new Vector2(NavRight, 0.97f), UIFactory.TextHeading, UIFactory.Ink);
            UIFactory.Panel("Divider", _panel, new Vector2(NavRight + UIFactory.Gap / (2f * PanelHalfWidth), 0.03f),
                new Vector2(NavRight + UIFactory.Gap / (2f * PanelHalfWidth), 0.97f), UIFactory.Border,
                Vector2.zero, new Vector2(BorderWidth, 0f));
            UIFactory.LabelKey("NavHint", _panel, "tabs.nav_hint",
                new Vector2(NavLeft, 0.03f), new Vector2(NavRight, 0.14f), UIFactory.TextSmall, UIFactory.InkMuted,
                TextAlignmentOptions.BottomLeft);

            var close = UIFactory.ButtonKey("Close", _panel, "common.close_esc",
                new Vector2(ContentMin.x + (ContentMax.x - ContentMin.x) * CloseLeft, 0.9f),
                new Vector2(ContentMax.x, 0.96f), UIFactory.TextSmall, null, "Esc");
            close.onClick.AddListener(Hide);
        }

        /// <summary>
        /// Adds page <paramref name="id"/> (English, used for object names and the <c>tabs.&lt;id&gt;</c>
        /// keys) with its heading, purpose line (<c>tabs.&lt;id&gt;.purpose</c>), body text and nav button.
        /// </summary>
        private BookPage AddPage(string id, float bodySize)
        {
            int index = _pages.Count;
            string key = "tabs." + id.ToLowerInvariant();
            var root = UIFactory.Node($"Page_{id}", _panel, ContentMin, ContentMax);
            UIFactory.LabelKey("Heading", root.transform, key, new Vector2(0f, HeadingBottom),
                new Vector2(CloseLeft, 1f), UIFactory.TextHeading, UIFactory.Ink);
            UIFactory.LabelKey("Purpose", root.transform, key + ".purpose", new Vector2(0f, PurposeBottom),
                new Vector2(1f, HeadingBottom), UIFactory.TextSmall, UIFactory.InkMuted);
            var body = UIFactory.Label("Body", root.transform, "", new Vector2(0f, BodyBottom),
                new Vector2(1f, BodyTop), bodySize, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            body.richText = true;

            var page = new BookPage { Root = root, Body = body, Nav = MakeNav(id, key, index) };
            _pages.Add(page);
            return page;
        }

        private Button MakeNav(string id, string key, int index)
        {
            float top = NavTop - index * (NavRowHeight + NavRowGap);
            var btn = UIFactory.ButtonKey($"Tab_{id}", _panel, key, new Vector2(NavLeft, top - NavRowHeight),
                new Vector2(NavRight, top), UIFactory.TextBody);
            btn.navigation = new Navigation { mode = Navigation.Mode.None };   // the book drives the keys
            var label = btn.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin    = new Vector4(NavTextIndent, 0f, 0f, 0f);
            btn.onClick.AddListener(() => ShowPage(index));
            return btn;
        }

        /// <summary>Select a page by index. Used by the screenshot tour.</summary>
        public void ShowTab(int index)
        {
            Refresh();
            ShowPage(index);
        }

        private void ShowPage(int index)
        {
            if (_pages.Count == 0) return;
            _current = Mathf.Clamp(index, 0, _pages.Count - 1);
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Root.SetActive(i == _current);
                UIFactory.SetSelected(_pages[i].Nav, i == _current);
            }
            ShowBuildPage(_current == BuildTab);
            if (IsOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_pages[_current].Nav.gameObject);
        }

        /// <summary>Opens the book on the Overview page and pauses the clock.</summary>
        public void Show()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
            _openedFrame = Time.frameCount;
            ShowPage(OverviewTab);
            _game?.SetModalOpen(true);
            if (!_holdsPause) { _holdsPause = true; GamePause.Acquire(); }
        }

        /// <summary>Closes the book and restarts the clock.</summary>
        public void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
            if (_holdsPause) { _holdsPause = false; GamePause.Release(); }
        }

        /// <summary>Opens the book when closed, closes it when open.</summary>
        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        /// <summary>Closes the book, then runs <paramref name="open"/> (another panel).</summary>
        private void HandOff(System.Action open)
        {
            Hide();
            open?.Invoke();
        }

        private void Update()
        {
            // The key that opened the book this frame must not also turn its page; the guide
            // open over the book takes the arrow keys.
            if (!IsOpen || Time.frameCount == _openedFrame || GuideIsOpen) return;
            int next = PageFromKeys();
            if (next != _current) ShowPage(next);
        }

        /// <summary>The page the keyboard asks for this frame (the current page if none).</summary>
        private int PageFromKeys()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool cycle = Input.GetKeyDown(CyclePagesKey);
            if (Input.GetKeyDown(KeyCode.DownArrow) || (cycle && !shift)) return Wrap(_current + 1);
            if (Input.GetKeyDown(KeyCode.UpArrow)   || (cycle && shift))  return Wrap(_current - 1);
            for (int i = 0; i < _pages.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
            return _current;
        }

        private int Wrap(int index) => (index % _pages.Count + _pages.Count) % _pages.Count;

        // ── Content ─────────────────────────────────────────────────────────────

        private void Refresh()
        {
            RefreshGuidePage();
            if (_game == null || _game.Shop == null) return;
            RefreshOverview();
            RefreshStock();
            RefreshAnimals();
            RefreshStaff();
        }
    }
}
