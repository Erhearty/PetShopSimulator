using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The whole in-game overlay: floating status cards, toast notifications, build hint and
    /// interact prompt. Builds itself on a canvas at runtime. The furniture inventory lives in
    /// <see cref="InventoryBar"/>.
    /// </summary>
    public class ShopHUD : MonoBehaviour
    {
        // Floating card geometry, in canvas reference pixels.
        private const float Margin     = 16f;
        private const float CardTop    = -Margin;
        private const float CardHeight = 70f;
        private const float CardBottom = CardTop - CardHeight;      // -86
        private const float ChipHeight = 26f;

        private ShopManager _shop;
        private GameManager _game;
        private BuildMode   _build;

        private TMP_Text _balanceLabel;
        private BalanceTicker _balanceTicker;
        private TMP_Text _tickerLabel;
        private TMP_Text _dayLabel;
        private TMP_Text _repLabel;
        private TMP_Text _rentLabel;
        private TMP_Text _clockLabel;
        private Image    _clockFill;
        private TMP_Text _shopperLabel;
        private TMP_Text _queueLabel;
        private GameObject _shopperChip;
        private GameObject _queueChip;
        private TMP_Text _alertLabel;
        private TMP_Text _notification;
        private TMP_Text _prompt;
        private TMP_Text _buildHint;
        private GameObject _crosshair;
        private Image      _repFill;

        /// <summary>Glyphs of the fixed mouse button / key named in the build hint.</summary>
        private const string LeftMouseLabel = "LMB";
        private const string EscapeKeyLabel = "Esc";

        private bool             _buildHintShown;
        private PlacedObjectData _buildHintItem;

        // Cached build hint and the inputs it was built from; rebuilt only when one changes.
        private string           _buildHintText;
        private bool             _buildHintDirty = true;
        private PlacedObjectData _hintHeldItem;
        private float            _hintRotation;
        private bool             _hintHolding;
        private bool             _hintActive;
        private KeyCode          _hintRemoveKey;
        private KeyCode          _hintRotateKey;

        /// <summary>The furniture catalogue; set by GameUI.</summary>
        public FurnitureCatalogPanel Catalogue { get; set; }

        private float _notifyTimer;
        private float _alertTimer;

        public TMP_Text PromptLabel => _prompt;

        /// <summary>The quest tracker under the balance; null until built.</summary>
        public QuestTracker Tracker { get; private set; }
        public void Build(Transform canvas, ShopManager shop, GameManager game, BuildMode build)
        {
            _shop  = shop;
            _game  = game;
            _build = build;

            BuildStatusBar(canvas);
            BuildToast(canvas);
            BuildPrompt(canvas);
            BuildCrosshair(canvas);
            BuildBuildHint(canvas);
            BuildAlerts(canvas);
            (Tracker = gameObject.AddComponent<QuestTracker>()).Build(canvas, game);
            shop.OnBalanceChanged.AddListener(_ => RefreshStatus());
            shop.OnReputationChanged.AddListener(_ => RefreshStatus());
            shop.OnDayAdvanced.AddListener(_ => RefreshStatus());

            game.OnNotification.AddListener(ShowNotification);
            build.OnBuildMessage.AddListener(ShowNotification);
            build.OnBuildModeEntered.AddListener(OnBuildEntered);
            build.OnBuildModeExited.AddListener(OnBuildExited);

            Loc.LanguageChanged += OnLanguageChanged;
            RefreshStatus();
            OnBuildExited();
        }

        private void OnDestroy() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Re-reads every cached line in the new language.</summary>
        private void OnLanguageChanged()
        {
            RefreshStatus();
            _buildHintDirty = true;
            RefreshBuildHint();
            _alertTimer = 0f;
        }

        // ── Construction ────────────────────────────────────────────────────────

        /// <summary>Three floating rounded cards (balance, day/clock, reputation) plus small chips.</summary>
        private void BuildStatusBar(Transform canvas)
        {
            var tl = new Vector2(0f, 1f);
            var tc = new Vector2(0.5f, 1f);
            var tr = new Vector2(1f, 1f);

            // Balance card, top-left.
            var money = UIFactory.Card("BalanceCard", canvas, tl, tl, UIFactory.CardBg,
                                       new Vector2(Margin, CardBottom), new Vector2(Margin + 300f, CardTop));
            _balanceLabel = UIFactory.Label("Balance", money.transform, "€ 0",
                new Vector2(0.06f, 0.40f), new Vector2(0.96f, 0.97f), 30f, UIFactory.Good);
            _balanceLabel.fontStyle = FontStyles.Bold;
            _balanceTicker = _balanceLabel.gameObject.AddComponent<BalanceTicker>();
            // Live running total for the day — the thing players check most often.
            _tickerLabel = UIFactory.Label("Ticker", money.transform, "",
                new Vector2(0.06f, 0.05f), new Vector2(0.96f, 0.42f), 13f, UIFactory.InkMuted);
            UIFactory.AutoFit(_tickerLabel);

            // Day / clock pill, top-centre.
            var day = UIFactory.Card("DayCard", canvas, tc, tc, UIFactory.CardBg,
                                     new Vector2(-250f, CardBottom), new Vector2(250f, CardTop));
            _dayLabel = UIFactory.Label("Day", day.transform, "",
                new Vector2(0.05f, 0.34f), new Vector2(0.32f, 0.96f), 20f, UIFactory.Accent);
            _dayLabel.fontStyle = FontStyles.Bold;
            _clockLabel = UIFactory.Label("Clock", day.transform, "09:00",
                new Vector2(0.32f, 0.30f), new Vector2(0.68f, 0.98f), 30f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            _clockLabel.fontStyle = FontStyles.Bold;
            _rentLabel = UIFactory.Label("Rent", day.transform, "",
                new Vector2(0.60f, 0.34f), new Vector2(0.96f, 0.96f), 14f, UIFactory.InkMuted,
                TextAlignmentOptions.MidlineRight);
            UIFactory.AutoFit(_rentLabel);

            var clockTrack = UIFactory.Card("ClockTrack", day.transform, new Vector2(0.05f, 0.12f),
                                            new Vector2(0.95f, 0.26f), UIFactory.TrackBg);
            _clockFill = UIFactory.Card("ClockFill", clockTrack.transform, Vector2.zero, new Vector2(0f, 1f),
                                        UIFactory.Accent).GetComponent<Image>();

            // Reputation meter, top-right.
            var rep = UIFactory.Card("ReputationCard", canvas, tr, tr, UIFactory.CardBg,
                                     new Vector2(-(Margin + 300f), CardBottom), new Vector2(-Margin, CardTop));
            UIFactory.LabelKey("RepCaption", rep.transform, "hud.reputation",
                new Vector2(0.06f, 0.50f), new Vector2(0.60f, 0.95f), 14f, UIFactory.InkMuted);
            _repLabel = UIFactory.Label("RepValue", rep.transform, "0",
                new Vector2(0.60f, 0.46f), new Vector2(0.94f, 0.97f), 26f, UIFactory.Ink,
                TextAlignmentOptions.MidlineRight);
            _repLabel.fontStyle = FontStyles.Bold;
            var track = UIFactory.Card("RepTrack", rep.transform, new Vector2(0.06f, 0.14f),
                                       new Vector2(0.94f, 0.38f), UIFactory.TrackBg);
            _repFill = UIFactory.Card("RepFill", track.transform, Vector2.zero, new Vector2(1f, 1f),
                                      UIFactory.Accent).GetComponent<Image>();

            // Chips under the day card; each hides itself when it has nothing to say.
            float chipTop = CardBottom - 6f;
            float chipBot = chipTop - ChipHeight;
            _shopperChip = UIFactory.Card("ShoppersChip", canvas, tc, tc, UIFactory.CardBg,
                                          new Vector2(-250f, chipBot), new Vector2(-100f, chipTop));
            _shopperLabel = UIFactory.Label("Shoppers", _shopperChip.transform, "",
                Vector2.zero, Vector2.one, 14f, UIFactory.InkMuted, TextAlignmentOptions.Center);
            _queueChip = UIFactory.Card("QueueChip", canvas, tc, tc, UIFactory.CardBg,
                                        new Vector2(-92f, chipBot), new Vector2(150f, chipTop));
            _queueLabel = UIFactory.Label("Queue", _queueChip.transform, "",
                Vector2.zero, Vector2.one, 14f, UIFactory.Warn, TextAlignmentOptions.Center);
            _shopperChip.SetActive(false);
            _queueChip.SetActive(false);
        }

        private void BuildToast(Transform canvas)
        {
            float top = CardBottom - ChipHeight - 6f - 38f;   // below chips and the alert strip
            var toast = UIFactory.Card("Toast", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                       UIFactory.CardBg,
                                       new Vector2(-400f, top - 40f), new Vector2(400f, top));
            _notification = UIFactory.Label("ToastText", toast.transform, "",
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), UIFactory.TextBody, UIFactory.Ink,
                TextAlignmentOptions.Center);
            toast.SetActive(false);
        }

        /// <summary>A small centre dot — in first person this is also the build aim point.</summary>
        private void BuildCrosshair(Transform canvas)
        {
            var dot = UIFactory.Panel("Crosshair", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      UIFactory.Crosshair,
                                      new Vector2(-2.5f, -2.5f), new Vector2(2.5f, 2.5f));
            dot.GetComponent<Image>().raycastTarget = false;
            _crosshair = dot;
        }

        private void BuildPrompt(Transform canvas)
        {
            var holder = UIFactory.Node("InteractPrompt", canvas,
                                        new Vector2(0.25f, 0.20f), new Vector2(0.75f, 0.26f));
            _prompt = holder.AddComponent<TextMeshProUGUI>();
            var font = UIFactory.Font();
            if (font != null) _prompt.font = font;
            _prompt.fontSize  = 21f;
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.color     = UIFactory.Accent;
            _prompt.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.AutoFit(_prompt);
            _prompt.raycastTarget = false;
            _prompt.enabled   = false;
        }

        /// <summary>The placement hint, anchored just above the inventory bar.</summary>
        private void BuildBuildHint(Transform canvas)
        {
            _buildHint = UIFactory.Label("BuildHint", canvas, "",
                new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), 16f, UIFactory.Accent,
                TextAlignmentOptions.Center);
            var hr = UIFactory.Rect(_buildHint.gameObject);
            hr.offsetMin = new Vector2(0f, InventoryBar.TopEdge + UIFactory.Gap);
            hr.offsetMax = new Vector2(0f, InventoryBar.TopEdge + UIFactory.Gap + 54f);
            _buildHint.enabled = false;
        }

        /// <summary>A standing warning strip under the status chips — empty shelves, low cash.</summary>
        private void BuildAlerts(Transform canvas)
        {
            _alertLabel = UIFactory.Label("Alerts", canvas, "",
                new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), 16f, UIFactory.Warn,
                TextAlignmentOptions.Center);
            var rt = UIFactory.Rect(_alertLabel.gameObject);
            // Directly under the chips; the toast sits below this strip so the two never overlap.
            float top = CardBottom - ChipHeight - 6f - 6f;
            rt.offsetMin = new Vector2(0f, top - 28f);
            rt.offsetMax = new Vector2(0f, top);
            _alertLabel.enabled = false;
        }

        // ── Runtime ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_game != null && _clockLabel != null)
            {
                _clockLabel.text = _game.ClockText;
                if (_clockFill != null)
                {
                    var rt = _clockFill.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = new Vector2(_game.DayProgress, 1f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                }
            }

            RefreshBuildHint();

            if (_crosshair != null && _game != null)
                _crosshair.SetActive(!_game.IsModalOpen && !_game.IsBuildViewActive && !_game.IsGameOver);

            _alertTimer -= Time.unscaledDeltaTime;
            if (_alertTimer <= 0f) { _alertTimer = 1.5f; RefreshAlerts(); }

            if (_shopperLabel != null && _game != null)
            {
                int inShop = _game.CustomersInShop;
                _shopperLabel.text = inShop > 0 ? Loc.F("hud.shoppers", inShop) : "";
                if (_shopperChip != null) _shopperChip.SetActive(inShop > 0);
            }

            if (_queueLabel != null && _game != null && _game.Queue != null)
            {
                int waiting = _game.Queue.Length;
                _queueLabel.text = waiting > 0
                    ? Loc.F("hud.queue", waiting, _game.Queue.WaitingValue)
                    : "";
                if (_queueChip != null) _queueChip.SetActive(waiting > 0);
            }

            if (_notification != null && _notification.transform.parent.gameObject.activeSelf)
            {
                _notifyTimer -= Time.deltaTime;
                if (_notifyTimer <= 0f)
                    _notification.transform.parent.gameObject.SetActive(false);
            }
        }

        public void ShowNotification(string msg)
        {
            if (_notification == null || string.IsNullOrEmpty(msg)) return;
            _notification.text = msg;
            _notification.transform.parent.gameObject.SetActive(true);
            _notifyTimer = 3.5f;
        }

        private void OnBuildEntered(PlacedObjectData item)
        {
            if (_buildHint == null) return;
            // The Remove tool carries no item.
            _buildHintItem     = item;
            _buildHintShown    = true;
            _buildHint.enabled = true;
            _buildHintDirty    = true;
            RefreshBuildHint();
        }

        /// <summary>The hint for the current tool, followed by the live placement line.</summary>
        private string BuildHintText()
        {
            string remove = InputBindings.Label(GameAction.BuildRemove);
            string line = _buildHintItem == null
                ? Loc.F("hud.build_hint.remove", LeftMouseLabel, remove, EscapeKeyLabel)
                : Loc.F("hud.build_hint.place", _buildHintItem.LocalizedName, LeftMouseLabel, remove, EscapeKeyLabel);
            return _build != null ? line + "\n" + _build.PlacementHint : line;
        }

        /// <summary>
        /// Rebuilds the hint only when an input to it changed (tool, held item, angle, key bindings,
        /// language) and assigns the label only when the text actually differs.
        /// </summary>
        private void RefreshBuildHint()
        {
            if (_buildHint == null || !_buildHint.enabled || !_buildHintShown) return;
            if (!BuildHintInputsChanged()) return;
            _buildHintDirty = false;
            string text = BuildHintText();
            if (text == _buildHintText) return;
            _buildHintText  = text;
            _buildHint.text = text;
        }

        /// <summary>Records the current hint inputs; true when any differs from the last build.</summary>
        private bool BuildHintInputsChanged()
        {
            KeyCode removeKey = InputBindings.Get(GameAction.BuildRemove);
            KeyCode rotateKey = InputBindings.Get(GameAction.BuildRotate);
            PlacedObjectData held = _build != null ? _build.CurrentItem : null;
            float rotation = _build != null ? _build.CurrentRotation : 0f;
            bool holding   = _build != null && _build.IsHolding;
            bool active    = _build != null && _build.IsActive;

            bool changed = _buildHintDirty
                || removeKey != _hintRemoveKey || rotateKey != _hintRotateKey
                || held != _hintHeldItem || rotation != _hintRotation
                || holding != _hintHolding || active != _hintActive;

            _hintRemoveKey = removeKey;
            _hintRotateKey = rotateKey;
            _hintHeldItem  = held;
            _hintRotation  = rotation;
            _hintHolding   = holding;
            _hintActive    = active;
            return changed;
        }

        private void OnBuildExited()
        {
            _buildHintShown = false;
            if (_buildHint != null) _buildHint.enabled = false;
        }

        /// <summary>Surfaces the one thing most worth fixing right now.</summary>
        private void RefreshAlerts()
        {
            if (_alertLabel == null || _game == null || _shop == null) return;

            string message = null;
            string e = InputBindings.Label(GameAction.Interact);

            int emptyShelves = 0;
            foreach (var shelf in _game.Shelves)
                if (shelf != null && shelf.IsEmpty) emptyShelves++;

            int emptyPens = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.Count == 0) emptyPens++;

            int crates = UnityEngine.Object.FindObjectsByType<DeliveryCrate>(FindObjectsSortMode.None).Length;

            if (crates > 0)
                message = crates == 1
                    ? Loc.F("hud.alert.delivery", e)
                    : Loc.Plural("hud.alert.deliveries", crates);
            else if (_game.Queue != null && _game.Queue.Length > 0)
                message = _game.Queue.Length == 1
                    ? Loc.F("hud.alert.queue_one", e)
                    : Loc.Plural("hud.alert.queue", _game.Queue.Length);
            else if (_game.PensNeedingService > 0)
                message = _game.PensNeedingService == 1
                    ? Loc.F("hud.alert.feed_one", e)
                    : Loc.Plural("hud.alert.feed", _game.PensNeedingService);
            else if (_shop.Balance < _shop.DailyRent)
                message = Loc.F("hud.alert.rent", _shop.DailyRent, _shop.Balance);
            else if (emptyShelves > 0)
                message = emptyShelves == 1
                    ? Loc.F("hud.alert.shelf_one", e)
                    : Loc.Plural("hud.alert.shelves", emptyShelves, e);
            else if (emptyPens > 0)
                message = Loc.Plural("hud.alert.pens_empty", emptyPens, e);
            else if (_shop.Reputation < 30f)
                message = Loc.T("hud.alert.low_rep");

            _alertLabel.text    = message ?? string.Empty;
            _alertLabel.enabled = message != null;
        }

        private void RefreshStatus()
        {
            if (_shop == null) return;

            if (_balanceLabel != null)
            {
                _balanceTicker.SetTarget(_shop.Balance, _shop.Balance >= _shop.DailyRent ? UIFactory.Good : UIFactory.Bad);
            }
            if (_tickerLabel != null)
            {
                float net = _shop.EarnedToday - _shop.SpentToday;
                string netTag = net >= 0f ? $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Good)}>+€ {net:N0}</color>"
                                          : $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Bad)}>−€ {-net:N0}</color>";
                _tickerLabel.text = Loc.F("hud.today", _shop.EarnedToday, _shop.SpentToday, netTag);
            }
            if (_dayLabel  != null) _dayLabel.text  = Loc.F("common.day", _shop.Day);
            if (_rentLabel != null) _rentLabel.text = Loc.F("hud.rent", _shop.DailyRent);
            if (_repLabel  != null) _repLabel.text  = _shop.Reputation.ToString("0");
            if (_repFill   != null)
            {
                var rt = _repFill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(Mathf.Clamp01(_shop.Reputation / 100f), 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _repFill.color = Color.Lerp(UIFactory.Bad, UIFactory.Good, _shop.Reputation / 100f);
            }
        }
    }
}
