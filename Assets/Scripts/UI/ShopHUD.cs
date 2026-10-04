using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
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

        private string   _buildHintBase = string.Empty;

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

            RefreshStatus();
            OnBuildExited();
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
            // Live running total for the day — the thing players check most often.
            _tickerLabel = UIFactory.Label("Ticker", money.transform, "today  +€ 0  /  −€ 0",
                new Vector2(0.06f, 0.05f), new Vector2(0.96f, 0.42f), 13f, UIFactory.InkMuted);

            // Day / clock pill, top-centre.
            var day = UIFactory.Card("DayCard", canvas, tc, tc, UIFactory.CardBg,
                                     new Vector2(-250f, CardBottom), new Vector2(250f, CardTop));
            _dayLabel = UIFactory.Label("Day", day.transform, "Day 1",
                new Vector2(0.05f, 0.34f), new Vector2(0.32f, 0.96f), 20f, UIFactory.Accent);
            _dayLabel.fontStyle = FontStyles.Bold;
            _clockLabel = UIFactory.Label("Clock", day.transform, "09:00",
                new Vector2(0.32f, 0.30f), new Vector2(0.68f, 0.98f), 30f, UIFactory.Ink,
                TextAlignmentOptions.Center);
            _clockLabel.fontStyle = FontStyles.Bold;
            _rentLabel = UIFactory.Label("Rent", day.transform, "Rent € 0",
                new Vector2(0.60f, 0.34f), new Vector2(0.96f, 0.96f), 14f, UIFactory.InkMuted,
                TextAlignmentOptions.MidlineRight);

            var clockTrack = UIFactory.Card("ClockTrack", day.transform, new Vector2(0.05f, 0.12f),
                                            new Vector2(0.95f, 0.26f), UIFactory.TrackBg);
            _clockFill = UIFactory.Card("ClockFill", clockTrack.transform, Vector2.zero, new Vector2(0f, 1f),
                                        UIFactory.Accent).GetComponent<Image>();

            // Reputation meter, top-right.
            var rep = UIFactory.Card("ReputationCard", canvas, tr, tr, UIFactory.CardBg,
                                     new Vector2(-(Margin + 300f), CardBottom), new Vector2(-Margin, CardTop));
            UIFactory.Label("RepCaption", rep.transform, "Reputation",
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
                                       new Color(0.10f, 0.14f, 0.21f, 0.94f),
                                       new Vector2(-400f, top - 40f), new Vector2(400f, top));
            _notification = UIFactory.Label("ToastText", toast.transform, "",
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), 17f, new Color(1f, 0.94f, 0.72f),
                TextAlignmentOptions.Center);
            toast.SetActive(false);
        }

        /// <summary>A small centre dot — in first person this is also the build aim point.</summary>
        private void BuildCrosshair(Transform canvas)
        {
            var dot = UIFactory.Panel("Crosshair", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Color(1f, 1f, 1f, 0.55f),
                                      new Vector2(-2.5f, -2.5f), new Vector2(2.5f, 2.5f));
            dot.GetComponent<Image>().raycastTarget = false;
            _crosshair = dot;
        }

        private void BuildPrompt(Transform canvas)
        {
            var holder = UIFactory.Node("InteractPrompt", canvas,
                                        new Vector2(0.25f, 0.20f), new Vector2(0.75f, 0.26f));
            _prompt = holder.AddComponent<TextMeshProUGUI>();
            _prompt.fontSize  = 21f;
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.color     = new Color(1f, 0.95f, 0.65f);
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
                _shopperLabel.text = inShop > 0 ? $"shoppers  {inShop}" : "";
                if (_shopperChip != null) _shopperChip.SetActive(inShop > 0);
            }

            if (_queueLabel != null && _game != null && _game.Queue != null)
            {
                int waiting = _game.Queue.Length;
                _queueLabel.text = waiting > 0
                    ? $"till: {waiting} waiting  €{_game.Queue.WaitingValue:N0}"
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
            if (_buildHint != null && item == null)
            {
                // The Remove tool carries no item.
                _buildHintBase     = $"Remove tool — LMB / middle-click / {InputBindings.Label(GameAction.BuildRemove)} remove · Esc cancel";
                _buildHint.text    = _buildHintBase;
                _buildHint.enabled = true;
            }
            else if (_buildHint != null)
            {
                _buildHintBase     = $"Placing {item.DisplayName} — LMB place · " +
                                     $"middle-click / {InputBindings.Label(GameAction.BuildRemove)} remove · Esc cancel";
                _buildHint.text    = $"{_buildHintBase}\n{_build.PlacementHint}";
                _buildHint.enabled = true;
            }
        }

        /// <summary>The angle changes as the player rotates, so the placement hint is rebuilt each frame.</summary>
        private void RefreshBuildHint()
        {
            if (_buildHint != null && _buildHint.enabled && _build != null)
                _buildHint.text = $"{_buildHintBase}\n{_build.PlacementHint}";
        }

        private void OnBuildExited()
        {
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
                    ? $"A delivery is waiting on the forecourt — press {e} at it to take it in."
                    : $"{crates} deliveries are waiting on the forecourt.";
            else if (_game.Queue != null && _game.Queue.Length > 0)
                message = _game.Queue.Length == 1
                    ? $"Someone is waiting at the till — press {e} behind the counter to serve them."
                    : $"{_game.Queue.Length} people are waiting at the till.";
            else if (_game.PensNeedingService > 0)
                message = _game.PensNeedingService == 1
                    ? $"A pen needs feeding — press {e} at it."
                    : $"{_game.PensNeedingService} pens need feeding and mucking out.";
            else if (_shop.Balance < _shop.DailyRent)
                message = $"Rent tonight is € {_shop.DailyRent:N0} and you have € {_shop.Balance:N0} — sell something.";
            else if (emptyShelves > 0)
                message = emptyShelves == 1
                    ? $"A shelf is empty — walk up to it and press {e} to restock."
                    : $"{emptyShelves} shelves are empty — press {e} at each one to restock.";
            else if (emptyPens > 0)
                message = $"{emptyPens} pen(s) are empty — press {e} at a pen to buy from the breeder.";
            else if (_shop.Reputation < 30f)
                message = "Reputation is low; keep the shelves stocked to bring customers back.";

            _alertLabel.text    = message ?? string.Empty;
            _alertLabel.enabled = message != null;
        }

        private void RefreshStatus()
        {
            if (_shop == null) return;

            if (_balanceLabel != null)
            {
                _balanceLabel.text  = $"€ {_shop.Balance:N0}";
                _balanceLabel.color = _shop.Balance >= _shop.DailyRent ? UIFactory.Good : UIFactory.Bad;
            }
            if (_tickerLabel != null)
            {
                float net = _shop.EarnedToday - _shop.SpentToday;
                string netTag = net >= 0f ? $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Good)}>+€ {net:N0}</color>"
                                          : $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Bad)}>−€ {-net:N0}</color>";
                _tickerLabel.text = $"today  +€ {_shop.EarnedToday:N0}  /  −€ {_shop.SpentToday:N0}   =  {netTag}";
            }
            if (_dayLabel  != null) _dayLabel.text  = $"Day {_shop.Day}";
            if (_rentLabel != null) _rentLabel.text = $"Rent tonight  € {_shop.DailyRent:N0}";
            if (_repLabel  != null) _repLabel.text  = $"{_shop.Reputation:0}";
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
