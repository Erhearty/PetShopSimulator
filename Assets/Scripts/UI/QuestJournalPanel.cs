using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PetShop.Core;
using PetShop.Progression.Quests;

namespace PetShop.UI
{
    /// <summary>
    /// The quest journal (J): a chapter list on the left and, on the right, every quest of the
    /// selected chapter with its state, instruction, progress and reward. Arrow keys move through
    /// the chapters (the page follows the selection), PageUp/PageDown or the wheel scroll the page,
    /// J or Escape closes it.
    /// </summary>
    public class QuestJournalPanel : MonoBehaviour
    {
        /// <summary>The key that opens and closes the journal.</summary>
        public const KeyCode OpenKey = KeyCode.J;

        /// <summary>The label of <see cref="OpenKey"/> for help text.</summary>
        public const string OpenKeyLabel = "J";

        private const string FooterText = "Up/Down: chapter  ·  PageUp/PageDown or wheel: scroll  ·  J or Esc: close";
        private const float HeaderFont = 24f;
        private const float ButtonFont = 15f;
        private const float BodyFont   = 16f;
        private const float HintFont   = 14f;
        private const float BodyMargin = 10f;
        private const float PageStep   = 0.3f;
        private const float WheelSpeed = 30f;

        private const float ColLeft = 0.03f, ColRight = 0.29f, ColTop = 0.86f, ColStep = 0.17f, ColHeight = 0.15f;
        private const float PaneLeft = 0.31f, PaneRight = 0.97f, PaneBottom = 0.10f, PaneTop = 0.86f;

        private static readonly Vector2 Centre        = new(0.5f, 0.5f);
        private static readonly Vector2 PanelHalfSize = new(450f, 330f);
        private static readonly Color   DimColor      = new(0.03f, 0.05f, 0.08f, 0.72f);
        private static readonly Color   PaneColor     = new(0.05f, 0.07f, 0.10f, 1f);
        private static readonly QuestChapter[] Chapters = (QuestChapter[])Enum.GetValues(typeof(QuestChapter));

        private GameObject  _root;
        private GameManager _game;
        private TMP_Text    _body;
        private ScrollRect  _scroll;
        private Button      _closeButton;
        private int         _shown = -1;

        private readonly List<Button>   _chapterButtons = new();
        private readonly List<TMP_Text> _chapterLabels  = new();

        /// <summary>True while the journal is visible.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>Builds the (hidden) journal under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _root = UIFactory.Panel("QuestJournalDim", canvas, Vector2.zero, Vector2.one, DimColor);
            var panel = UIFactory.Panel("QuestJournal", _root.transform, Centre, Centre,
                                        UIFactory.PanelBg, -PanelHalfSize, PanelHalfSize).transform;
            BuildChrome(panel);
            BuildChapterButtons(panel);
            BuildPane(panel);
            WireNavigation();
            _root.SetActive(false);
        }

        /// <summary>When it returns true (the guide is open over the journal) the journal ignores the keyboard.</summary>
        public Func<bool> InputBlocked { get; set; }

        /// <summary>Opens the journal on the current chapter and selects its button.</summary>
        public void Show()
        {
            if (_root == null || IsOpen) return;
            _game?.SetModalOpen(true);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            RefreshChapterLabels();
            var book = Book();
            int current = Array.IndexOf(Chapters, book != null ? book.CurrentChapter : QuestChapter.Tutorial);
            ShowChapter(current);
            EventSystem.current?.SetSelectedGameObject(_chapterButtons[current].gameObject);
        }

        /// <summary>Closes the journal and releases the modal state.</summary>
        public void Hide()
        {
            if (_root == null || !IsOpen) return;
            _root.SetActive(false);
            _game?.SetModalOpen(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        /// <summary>Opens the journal when closed, closes it when open.</summary>
        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        // ── Construction ────────────────────────────────────────────────────────

        private void BuildChrome(Transform panel)
        {
            UIFactory.Panel("Accent", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -4f), Vector2.zero);
            UIFactory.Label("Header", panel, "Quest journal",
                new Vector2(0.03f, 0.90f), new Vector2(0.5f, 0.97f), HeaderFont, UIFactory.Ink);
            _closeButton = UIFactory.Button("Close", panel, $"Close  ({OpenKeyLabel} / Esc)",
                new Vector2(0.74f, 0.905f), new Vector2(0.97f, 0.965f), ButtonFont);
            _closeButton.onClick.AddListener(Hide);
            UIFactory.Label("Footer", panel, FooterText,
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.08f), HintFont, UIFactory.InkMuted);
        }

        private void BuildChapterButtons(Transform panel)
        {
            for (int i = 0; i < Chapters.Length; i++)
            {
                float top = ColTop - i * ColStep;
                int index = i;
                var button = UIFactory.Button($"Chapter_{Chapters[i]}", panel, "",
                    new Vector2(ColLeft, top - ColHeight), new Vector2(ColRight, top), ButtonFont);
                button.onClick.AddListener(() => ShowChapter(index));
                _chapterButtons.Add(button);
                _chapterLabels.Add(button.GetComponentInChildren<TMP_Text>());
            }
        }

        /// <summary>A masked, scrollable page whose height follows its text.</summary>
        private void BuildPane(Transform panel)
        {
            var viewport = UIFactory.Panel("Page", panel, new Vector2(PaneLeft, PaneBottom),
                                           new Vector2(PaneRight, PaneTop), PaneColor);
            viewport.AddComponent<RectMask2D>();

            _body = UIFactory.Label("PageText", viewport.transform, "", new Vector2(0f, 1f), new Vector2(1f, 1f),
                                    BodyFont, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _body.overflowMode = TextOverflowModes.Overflow;
            _body.margin       = new Vector4(BodyMargin, BodyMargin, BodyMargin, BodyMargin);
            var bodyRect = _body.rectTransform;
            bodyRect.pivot = new Vector2(0.5f, 1f);
            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.AddComponent<ScrollRect>();
            _scroll.viewport          = (RectTransform)viewport.transform;
            _scroll.content           = bodyRect;
            _scroll.horizontal        = false;
            _scroll.movementType      = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = WheelSpeed;
        }

        /// <summary>Up/down through the chapters, then Close.</summary>
        private void WireNavigation()
        {
            var chain = new List<Selectable>(_chapterButtons) { _closeButton };
            for (int i = 0; i < chain.Count; i++)
            {
                chain[i].navigation = new Navigation
                {
                    mode         = Navigation.Mode.Explicit,
                    selectOnUp   = i > 0 ? chain[i - 1] : null,
                    selectOnDown = i < chain.Count - 1 ? chain[i + 1] : null,
                };
            }
        }

        // ── Runtime ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsOpen || (InputBlocked != null && InputBlocked())) return;
            FollowSelection();
            if (Input.GetKeyDown(KeyCode.PageDown)) Scroll(-PageStep);
            if (Input.GetKeyDown(KeyCode.PageUp))   Scroll(PageStep);
        }

        /// <summary>Shows the chapter whose button the keyboard has moved onto.</summary>
        private void FollowSelection()
        {
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < _chapterButtons.Count; i++)
                if (_chapterButtons[i].gameObject == selected && i != _shown) ShowChapter(i);
        }

        private void Scroll(float delta) =>
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + delta);

        private void ShowChapter(int index)
        {
            if (index < 0 || index >= Chapters.Length) return;
            _shown = index;
            _body.text = QuestTextFormatter.ChapterBody(Book(), Chapters[index], QuestTracker.Snapshot(_game));
            _scroll.verticalNormalizedPosition = 1f;
            for (int i = 0; i < _chapterButtons.Count; i++)
                UIFactory.SetSelected(_chapterButtons[i], i == index);
        }

        private void RefreshChapterLabels()
        {
            for (int i = 0; i < _chapterLabels.Count; i++)
                _chapterLabels[i].text = QuestTextFormatter.ChapterSummary(Book(), Chapters[i]);
        }

        private QuestBook Book() => _game != null && _game.Quests != null ? _game.Quests.Book : null;
    }
}
