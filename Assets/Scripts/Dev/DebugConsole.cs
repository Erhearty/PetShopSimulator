using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PetShop.Core;
using PetShop.Localization;
using PetShop.UI;

namespace PetShop.Dev
{
    /// <summary>What a console line was understood as.</summary>
    public enum ConsoleOutcome { Empty, AddMoney, Invalid, Unknown }

    /// <summary>The parsed form of one console line.</summary>
    public readonly struct ConsoleCommand
    {
        /// <summary>How the line was understood.</summary>
        public readonly ConsoleOutcome Outcome;

        /// <summary>The money to add (<see cref="ConsoleOutcome.AddMoney"/> only).</summary>
        public readonly int Amount;

        /// <summary>The first word typed (for the unknown-command message).</summary>
        public readonly string Word;

        public ConsoleCommand(ConsoleOutcome outcome, int amount = 0, string word = "")
        {
            Outcome = outcome;
            Amount  = amount;
            Word    = word ?? "";
        }
    }

    /// <summary>
    /// The F2 debug console: a one-line input overlay. Type a command and press Enter; Esc or
    /// F2 closes it. While open it holds the game's modal flag so gameplay keys do nothing.
    /// Commands: <c>greedisgood N</c> adds N money.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        /// <summary>The command that adds money.</summary>
        public const string MoneyCommand = "greedisgood";

        private const float PanelHalfWidth = 360f, PanelHeight = 96f, PanelTop = -24f;
        private const float InputBottom = 0.1f, InputTop = 0.5f, MessageBottom = 0.5f, MessageTop = 0.95f;

        private static int _openCount;
        private static int _closedFrame = -1;

        /// <summary>True while any console is open, or closed on this very frame (so Esc is not also read by the pause menu).</summary>
        public static bool IsAnyOpen => _openCount > 0 || Time.frameCount == _closedFrame;

        private GameManager _game;
        private Func<bool> _canOpen;
        private GameObject _root;
        private TMP_InputField _input;
        private TMP_Text _message;
        private bool _wasModal;

        /// <summary>True while the console is showing.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        // ── Pure parser ─────────────────────────────────────────────────────────

        /// <summary>
        /// Understands one console line. <c>greedisgood N</c> (N a positive whole number) is
        /// <see cref="ConsoleOutcome.AddMoney"/>; the right word with a missing or bad amount is
        /// <see cref="ConsoleOutcome.Invalid"/>; any other word is <see cref="ConsoleOutcome.Unknown"/>.
        /// </summary>
        public static ConsoleCommand Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return new ConsoleCommand(ConsoleOutcome.Empty);
            string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string word = parts[0];
            if (!string.Equals(word, MoneyCommand, StringComparison.OrdinalIgnoreCase))
                return new ConsoleCommand(ConsoleOutcome.Unknown, 0, word);
            if (parts.Length != 2 ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int amount) ||
                amount <= 0)
                return new ConsoleCommand(ConsoleOutcome.Invalid, 0, word);
            return new ConsoleCommand(ConsoleOutcome.AddMoney, amount, word);
        }

        /// <summary>The player-facing message for <paramref name="command"/> once applied (or refused).</summary>
        public static string MessageFor(ConsoleCommand command)
        {
            switch (command.Outcome)
            {
                case ConsoleOutcome.AddMoney: return Loc.F("console.added", command.Amount);
                case ConsoleOutcome.Invalid:  return Loc.T("console.invalid");
                case ConsoleOutcome.Unknown:  return Loc.F("console.unknown", command.Word);
                default:                      return Loc.T("console.hint");
            }
        }

        // ── Wiring ──────────────────────────────────────────────────────────────

        /// <summary>Builds the (hidden) console under <paramref name="canvas"/>.</summary>
        /// <param name="canOpen">Extra gate, e.g. false over the title screen.</param>
        public void Init(GameManager game, Transform canvas, Func<bool> canOpen = null)
        {
            _game    = game;
            _canOpen = canOpen;
            _root = UIFactory.Card("DebugConsole", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                UIFactory.Surface, new Vector2(-PanelHalfWidth, PanelTop - PanelHeight), new Vector2(PanelHalfWidth, PanelTop));

            var panel = _root.transform;
            _message = UIFactory.Label("Message", panel, "", new Vector2(0.03f, MessageBottom),
                new Vector2(0.97f, MessageTop), UIFactory.TextSmall, UIFactory.InkMuted);
            BuildInput(panel);
            _root.SetActive(false);
        }

        private void BuildInput(Transform panel)
        {
            var field = UIFactory.Panel("Input", panel, new Vector2(0.03f, InputBottom),
                new Vector2(0.97f, InputTop), UIFactory.Raised);
            var viewport = UIFactory.Node("Viewport", field.transform, Vector2.zero, Vector2.one,
                new Vector2(UIFactory.Gap, 0f), new Vector2(-UIFactory.Gap, 0f));
            viewport.AddComponent<RectMask2D>();
            var text = UIFactory.Label("Text", viewport.transform, "", Vector2.zero, Vector2.one,
                UIFactory.TextBody, UIFactory.Ink);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholder = UIFactory.LabelKey("Placeholder", viewport.transform, "console.placeholder",
                Vector2.zero, Vector2.one, UIFactory.TextBody, UIFactory.InkMuted);

            _input = field.AddComponent<TMP_InputField>();
            _input.textViewport   = (RectTransform)viewport.transform;
            _input.textComponent  = text;
            _input.placeholder    = placeholder;
            _input.lineType       = TMP_InputField.LineType.SingleLine;
            _input.onSubmit.AddListener(Submit);
        }

        // ── Behaviour ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (_root == null) return;
            if (InputBindings.GetKeyDown(GameAction.DebugConsole))
            {
                if (IsOpen) Hide(); else Show();
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
        }

        /// <summary>Opens the console and focuses its input, unless the game is over or the gate refuses.</summary>
        public void Show()
        {
            if (_root == null || IsOpen) return;
            if (_game == null || _game.IsGameOver) return;
            if (_canOpen != null && !_canOpen()) return;
            _wasModal = _game.IsModalOpen;
            _game.SetModalOpen(true);
            _openCount++;
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
            _message.text = Loc.T("console.hint");
            _input.text = "";
            _input.ActivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_input.gameObject);
        }

        /// <summary>Closes the console and gives the modal flag back as it was.</summary>
        public void Hide()
        {
            if (_root == null || !IsOpen) return;
            _root.SetActive(false);
            _openCount = Mathf.Max(0, _openCount - 1);
            _closedFrame = Time.frameCount;
            _game?.SetModalOpen(_wasModal);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>Runs <paramref name="line"/> and shows the result in the console.</summary>
        public void Submit(string line)
        {
            ConsoleCommand command = Execute(line);
            if (command.Outcome == ConsoleOutcome.AddMoney && _game != null && _game.Shop != null)
                _game.Shop.ChangeBalance(command.Amount, "debug console");
            _message.text = MessageFor(command);
            _input.text = "";
            _input.ActivateInputField();
        }

        private void OnDestroy()
        {
            if (IsOpen) _openCount = Mathf.Max(0, _openCount - 1);
        }
    }
}
