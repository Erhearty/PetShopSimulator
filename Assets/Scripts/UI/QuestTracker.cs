using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Progression.Quests;

namespace PetShop.UI
{
    /// <summary>
    /// Compact quest tracker at the top-left, directly below the status bar: the active tutorial step or up
    /// to three chapter quests. Flashes green when a quest completes (the reward toast comes from
    /// the game's notification, which the HUD already shows). Hidden only when the player turns
    /// it off in Settings; it sits clear of the build hint, so build mode never needs to hide it.
    /// </summary>
    public class QuestTracker : MonoBehaviour
    {
        /// <summary>Real seconds between text refreshes.</summary>
        public const float RefreshSeconds = 0.5f;

        /// <summary>Real seconds the completion flash lasts.</summary>
        public const float HighlightSeconds = 1.5f;

        private const float FontMax = 14f;
        private const float FontMin = 10f;

        private static readonly Vector2 TopLeft     = new(0f, 1f);
        /// <summary>Height of the full-width status bar built by ShopHUD.BuildStatusBar (floating cards, bottom edge at y -86).</summary>
        private const float StatusBarHeight = 88f;
        private const float BelowBarMargin  = 6f;
        private const float PanelHeight     = 128f;
        private const float PanelTop        = -(StatusBarHeight + BelowBarMargin);

        private static readonly Vector2 PanelMin    = new(12f, PanelTop - PanelHeight);
        private static readonly Vector2 PanelMax    = new(340f, PanelTop);
        private static readonly Vector2 TextMin     = new(0.03f, 0.04f);
        private static readonly Vector2 TextMax     = new(0.97f, 0.96f);
        private static readonly Color   HighlightBg = UIFactory.PositiveFill;

        private GameManager   _game;
        private GameObject    _root;
        private Image         _background;
        private TMP_Text      _text;
        private QuestDirector _subscribed;
        private float         _sinceRefresh;
        private float         _highlightLeft;

        /// <summary>A snapshot for progress counters, or null when there is no quest director or shop.</summary>
        public static QuestContext Snapshot(GameManager game)
        {
            if (game == null || game.Quests == null || game.Shop == null) return null;
            return game.Quests.BuildContext();
        }

        /// <summary>Builds the (initially hidden) tracker under <paramref name="canvas"/>.</summary>
        public void Build(Transform canvas, GameManager game)
        {
            _game = game;
            _root = UIFactory.Panel("QuestTracker", canvas, TopLeft, TopLeft, UIFactory.BarBg, PanelMin, PanelMax);
            _background = _root.GetComponent<Image>();
            _background.raycastTarget = false;
            _text = UIFactory.Label("QuestTrackerText", _root.transform, "", TextMin, TextMax,
                                    FontMax, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _text.enableAutoSizing = true;
            _text.fontSizeMin      = FontMin;
            _text.fontSizeMax      = FontMax;
            _root.SetActive(false);
            Refresh();
        }

        /// <summary>Re-reads the setting and the quest state now.</summary>
        public void Refresh()
        {
            _sinceRefresh = 0f;
            if (_root == null) return;
            Subscribe();
            string text = GameSettings.ShowQuestTracker ? CurrentText() : string.Empty;
            _root.SetActive(text.Length > 0);
            if (text.Length > 0 && _text.text != text) _text.text = text;
        }

        private void Update()
        {
            if (_root == null) return;
            TickHighlight();
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= RefreshSeconds) Refresh();
        }

        private void OnDestroy()
        {
            if (_subscribed != null) _subscribed.QuestCompleted -= OnQuestCompleted;
        }

        private string CurrentText()
        {
            if (_game == null || _game.Quests == null) return string.Empty;
            return QuestTextFormatter.TrackerText(_game.Quests.Book, Snapshot(_game));
        }

        /// <summary>Follows the game's quest director, which may be created after the HUD.</summary>
        private void Subscribe()
        {
            var quests = _game != null ? _game.Quests : null;
            if (quests == _subscribed) return;
            if (_subscribed != null) _subscribed.QuestCompleted -= OnQuestCompleted;
            _subscribed = quests;
            if (quests != null) quests.QuestCompleted += OnQuestCompleted;
        }

        private void OnQuestCompleted(QuestDefinition quest)
        {
            _highlightLeft = HighlightSeconds;
            _background.color = HighlightBg;
            Refresh();
        }

        private void TickHighlight()
        {
            if (_highlightLeft <= 0f) return;
            _highlightLeft -= Time.unscaledDeltaTime;
            if (_highlightLeft <= 0f) _background.color = UIFactory.BarBg;
        }
    }
}
