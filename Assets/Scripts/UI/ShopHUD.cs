using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetShop.Commerce;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The whole in-game overlay: status bar, toast notifications, build palette,
    /// interact prompt and the controls cheat-sheet. Builds itself on a canvas at runtime.
    /// </summary>
    public class ShopHUD : MonoBehaviour
    {
        private ShopManager _shop;
        private GameManager _game;
        private BuildMode   _build;

        private TMP_Text _balanceLabel;
        private TMP_Text _dayLabel;
        private TMP_Text _repLabel;
        private TMP_Text _rentLabel;
        private TMP_Text _clockLabel;
        private Image    _clockFill;
        private TMP_Text _shopperLabel;
        private TMP_Text _queueLabel;
        private TMP_Text _alertLabel;
        private TMP_Text _notification;
        private TMP_Text _prompt;
        private TMP_Text _buildHint;
        private GameObject _helpPanel;
        private GameObject _crosshair;
        private Image      _repFill;

        private readonly Dictionary<string, Image> _buildButtons = new();
        private float _notifyTimer;
        private float _alertTimer;

        public TMP_Text PromptLabel => _prompt;

        public void Build(Transform canvas, ShopManager shop, GameManager game, BuildMode build)
        {
            _shop  = shop;
            _game  = game;
            _build = build;

            BuildStatusBar(canvas);
            BuildToast(canvas);
            BuildPrompt(canvas);
            BuildCrosshair(canvas);
            BuildBuildBar(canvas);
            BuildAlerts(canvas);
            BuildHelp(canvas);

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

        private void BuildStatusBar(Transform canvas)
        {
            var bar = UIFactory.Panel("StatusBar", canvas, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                      UIFactory.BarBg, new Vector2(0f, -54f), Vector2.zero);

            _balanceLabel = UIFactory.Label("Balance", bar.transform, "€ 0",
                new Vector2(0.012f, 0f), new Vector2(0.22f, 1f), 24f, UIFactory.Good);
            _dayLabel = UIFactory.Label("Day", bar.transform, "Day 1",
                new Vector2(0.23f, 0f), new Vector2(0.36f, 1f), 21f);
            _rentLabel = UIFactory.Label("Rent", bar.transform, "Rent € 0",
                new Vector2(0.36f, 0f), new Vector2(0.55f, 1f), 16f, UIFactory.InkMuted);

            _clockLabel = UIFactory.Label("Clock", bar.transform, "09:00",
                new Vector2(0.555f, 0.30f), new Vector2(0.665f, 1f), 22f, UIFactory.Ink,
                TextAlignmentOptions.Center);

            var clockTrack = UIFactory.Panel("ClockTrack", bar.transform, new Vector2(0.555f, 0.12f),
                                             new Vector2(0.665f, 0.26f), new Color(1f, 1f, 1f, 0.13f));
            _clockFill = UIFactory.Panel("ClockFill", clockTrack.transform, Vector2.zero, new Vector2(0f, 1f),
                                         UIFactory.InkMuted).GetComponent<Image>();

            UIFactory.Label("RepCaption", bar.transform, "Reputation",
                new Vector2(0.70f, 0.48f), new Vector2(0.88f, 0.96f), 14f, UIFactory.InkMuted);

            var track = UIFactory.Panel("RepTrack", bar.transform, new Vector2(0.70f, 0.16f),
                                        new Vector2(0.94f, 0.46f), new Color(1f, 1f, 1f, 0.13f));
            var fill = UIFactory.Panel("RepFill", track.transform, Vector2.zero, new Vector2(1f, 1f),
                                       UIFactory.Accent);
            _repFill = fill.GetComponent<Image>();

            _repLabel = UIFactory.Label("RepValue", bar.transform, "0",
                new Vector2(0.945f, 0.16f), new Vector2(0.99f, 0.96f), 18f, UIFactory.Ink,
                TextAlignmentOptions.Right);

            _shopperLabel = UIFactory.Label("Shoppers", bar.transform, "",
                new Vector2(0.665f, 0f), new Vector2(0.695f, 1f), 16f, UIFactory.InkMuted,
                TextAlignmentOptions.Center);

            _queueLabel = UIFactory.Label("Queue", bar.transform, "",
                new Vector2(0.555f, 0f), new Vector2(0.665f, 0.45f), 15f, new Color(0.98f, 0.78f, 0.35f),
                TextAlignmentOptions.Center);
        }

        private void BuildToast(Transform canvas)
        {
            var toast = UIFactory.Panel("Toast", canvas, new Vector2(0.22f, 1f), new Vector2(0.78f, 1f),
                                        new Color(0.10f, 0.13f, 0.19f, 0.92f),
                                        new Vector2(0f, -96f), new Vector2(0f, -60f));
            _notification = UIFactory.Label("ToastText", toast.transform, "",
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), 17f, new Color(1f, 0.94f, 0.72f),
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

        private void BuildBuildBar(Transform canvas)
        {
            var ids = BuildCatalog.HotkeyOrder;
            float halfWidth = Mathf.Max(330f, ids.Length * 84f * 0.5f);

            var bar = UIFactory.Panel("BuildBar", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      UIFactory.BarBg,
                                      new Vector2(-halfWidth, 12f), new Vector2(halfWidth, 92f));

            float pad  = 0.008f;
            float slot = (1f - pad * (ids.Length + 1)) / ids.Length;

            for (int i = 0; i < ids.Length; i++)
            {
                var def = BuildCatalog.Get(ids[i]);
                float x0 = pad + i * (slot + pad);

                int index = i;
                var btn = UIFactory.Button($"Build_{def.Id}", bar.transform, "",
                    new Vector2(x0, 0.30f), new Vector2(x0 + slot, 0.94f));
                btn.onClick.AddListener(() => _game.Build.EnterBuildMode(BuildCatalog.Get(BuildCatalog.HotkeyOrder[index])));

                UIFactory.Label("Name", btn.transform, def.DisplayName,
                    new Vector2(0.03f, 0.40f), new Vector2(0.97f, 0.95f), 13f, UIFactory.Ink,
                    TextAlignmentOptions.Center);
                UIFactory.Label("Cost", btn.transform, $"€{def.Cost:N0}",
                    new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.42f), 14f, UIFactory.InkMuted,
                    TextAlignmentOptions.Center);

                _buildButtons[def.Id] = btn.GetComponent<Image>();

                UIFactory.Label("Key", bar.transform, $"{i + 1}",
                    new Vector2(x0, 0.03f), new Vector2(x0 + slot, 0.28f), 13f, UIFactory.InkMuted,
                    TextAlignmentOptions.Center);
            }

            _buildHint = UIFactory.Label("BuildHint", canvas, "",
                new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), 16f, UIFactory.Accent,
                TextAlignmentOptions.Center);
            var hr = UIFactory.Rect(_buildHint.gameObject);
            hr.offsetMin = new Vector2(0f, 96f);
            hr.offsetMax = new Vector2(0f, 126f);
            _buildHint.enabled = false;
        }

        /// <summary>A standing warning strip under the status bar — empty shelves, low cash.</summary>
        private void BuildAlerts(Transform canvas)
        {
            _alertLabel = UIFactory.Label("Alerts", canvas, "",
                new Vector2(0f, 1f), new Vector2(1f, 1f), 15f, new Color(0.95f, 0.72f, 0.42f),
                TextAlignmentOptions.Center);
            var rt = UIFactory.Rect(_alertLabel.gameObject);
            rt.offsetMin = new Vector2(0f, -80f);
            rt.offsetMax = new Vector2(0f, -56f);
            _alertLabel.enabled = false;
        }

        private void BuildHelp(Transform canvas)
        {
            _helpPanel = UIFactory.Panel("Help", canvas, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                         new Color(0.07f, 0.09f, 0.13f, 0.80f),
                                         new Vector2(12f, 12f), new Vector2(268f, 244f));

            UIFactory.Label("HelpText", _helpPanel.transform,
                "<b>Controls</b>\n" +
                "WASD  move  ·  Space  jump\n" +
                "Mouse  look\n" +
                
                
                "E  interact / restock\n" +
                "1-4 or B  build mode\n" +
                "LMB place · R rotate\n" +
                "Middle-click  remove\n" +
                "Enter  close up early\n" +
                "E at the counter  serve the queue\n" +
                "Tab  the ledger\n" +
                "Esc  pause  ·  F5 save\n" +
                "H  hide this panel",
                new Vector2(0.06f, 0.04f), new Vector2(0.97f, 0.96f), 14.5f, UIFactory.InkMuted,
                TextAlignmentOptions.TopLeft);
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

            if (Input.GetKeyDown(KeyCode.H) && _helpPanel != null)
                _helpPanel.SetActive(!_helpPanel.activeSelf);

            if (_crosshair != null && _game != null)
                _crosshair.SetActive(!_game.IsModalOpen && !_game.IsGameOver);

            _alertTimer -= Time.unscaledDeltaTime;
            if (_alertTimer <= 0f) { _alertTimer = 1.5f; RefreshAlerts(); }

            if (_shopperLabel != null && _game != null)
            {
                int inShop = _game.CustomersInShop;
                _shopperLabel.text = inShop > 0 ? $"\U0001F464 {inShop}" : "";
            }

            if (_queueLabel != null && _game != null && _game.Queue != null)
            {
                int waiting = _game.Queue.Length;
                _queueLabel.text = waiting > 0
                    ? $"till: {waiting} waiting  €{_game.Queue.WaitingValue:N0}"
                    : "";
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
            foreach (var kvp in _buildButtons)
                kvp.Value.color = kvp.Key == item.Id ? UIFactory.ButtonOn : UIFactory.ButtonBg;

            if (_buildHint != null)
            {
                _buildHint.text    = $"Placing {item.DisplayName} — LMB place · R rotate · middle-click remove · Esc cancel";
                _buildHint.enabled = true;
            }
        }

        private void OnBuildExited()
        {
            foreach (var img in _buildButtons.Values)
                img.color = UIFactory.ButtonBg;
            if (_buildHint != null) _buildHint.enabled = false;
        }

        /// <summary>Surfaces the one thing most worth fixing right now.</summary>
        private void RefreshAlerts()
        {
            if (_alertLabel == null || _game == null || _shop == null) return;

            string message = null;

            int emptyShelves = 0;
            foreach (var shelf in _game.Shelves)
                if (shelf != null && shelf.IsEmpty) emptyShelves++;

            int emptyPens = 0;
            foreach (var pen in _game.Pens)
                if (pen != null && pen.Count == 0) emptyPens++;

            if (_game.Queue != null && _game.Queue.Length > 0)
                message = _game.Queue.Length == 1
                    ? "Someone is waiting at the till — press E behind the counter to serve them."
                    : $"{_game.Queue.Length} people are waiting at the till.";
            else if (_game.PensNeedingService > 0)
                message = _game.PensNeedingService == 1
                    ? "A pen needs feeding — press E at it."
                    : $"{_game.PensNeedingService} pens need feeding and mucking out.";
            else if (_shop.Balance < _shop.DailyRent)
                message = $"Rent tonight is € {_shop.DailyRent:N0} and you have € {_shop.Balance:N0} — sell something.";
            else if (emptyShelves > 0)
                message = emptyShelves == 1
                    ? "A shelf is empty — walk up to it and press E to restock."
                    : $"{emptyShelves} shelves are empty — press E at each one to restock.";
            else if (emptyPens > 0)
                message = $"{emptyPens} pen(s) are empty — press E at a pen to buy from the breeder.";
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
