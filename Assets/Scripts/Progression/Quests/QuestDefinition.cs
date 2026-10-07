using System;
using System.Collections.Generic;
using PetShop.Core;
using PetShop.Localization;

namespace PetShop.Progression.Quests
{
    /// <summary>The chapters of the quest book, in the order they unlock.</summary>
    public enum QuestChapter
    {
        /// <summary>Step-by-step: opening the empty shop.</summary>
        Tutorial,
        /// <summary>Corner Shop to Local Favourite.</summary>
        Early,
        /// <summary>Local Favourite to Trusted Name.</summary>
        Mid,
        /// <summary>Trusted Name to Town Landmark.</summary>
        End,
    }

    /// <summary>
    /// One quest: what the player must do, how to do it (with the key to press), the cash
    /// reward, and the rule that decides when it is done. Pure data plus two delegates over a
    /// <see cref="QuestContext"/> snapshot, so it is unit-testable.
    /// </summary>
    public sealed class QuestDefinition
    {
        /// <summary>Stable id, saved in <see cref="QuestProgress"/>. Never rename a shipped id.</summary>
        public string Id { get; }

        /// <summary>The chapter this quest belongs to.</summary>
        public QuestChapter Chapter { get; }

        /// <summary>Short player-facing goal, e.g. "Own 3 shelves".</summary>
        public string Title { get; }

        /// <summary>
        /// The how-to text with key tokens such as <c>{Interact}</c> (see <see cref="KeyToken"/>);
        /// use <see cref="Instruction"/> or <see cref="InstructionText"/> for display.
        /// </summary>
        public string InstructionTemplate { get; }

        /// <summary>Cash paid once when the quest completes.</summary>
        public float Reward { get; }

        /// <summary>True once the quest's goal is met in the given snapshot.</summary>
        public Func<QuestContext, bool> Condition { get; }

        /// <summary>Optional counter (current, target) for goals like "Own 3 shelves"; null when none.</summary>
        public Func<QuestContext, (int current, int target)> Progress { get; }

        /// <summary>
        /// Values for the <c>{0}</c>, <c>{1}</c>… placeholders in the localized title and description
        /// (targets, tier names); evaluated on every read so they follow the language. Null when none.
        /// </summary>
        public Func<object[]> TextArgs { get; }

        /// <summary>The instruction with the player's current key bindings filled in.</summary>
        public string Instruction => InstructionText(InputBindings.Label);

        /// <summary>Table key of the title: <c>quest.&lt;id&gt;.title</c>.</summary>
        public string TitleKey => $"quest.{Id}.title";

        /// <summary>Table key of the description: <c>quest.&lt;id&gt;.desc</c>.</summary>
        public string DescriptionKey => $"quest.{Id}.desc";

        /// <summary>The title in the current language, or <see cref="Title"/> when the table has no entry.</summary>
        public string LocalizedTitle =>
            Loc.Has(TitleKey) ? Loc.F(TitleKey, TextArgs?.Invoke() ?? Array.Empty<object>()) : Title ?? string.Empty;

        /// <summary>
        /// The how-to text in the current language with the player's current key bindings filled in, or
        /// <see cref="InstructionTemplate"/> with its key tokens replaced when the table has no entry.
        /// </summary>
        public string LocalizedDescription => LocalizedInstructionText(InputBindings.Label);

        /// <summary>
        /// The actions whose keys the instruction names, in the order <see cref="InstructionTemplate"/> first
        /// names them. The localized description takes their labels as positional arguments right after
        /// <see cref="TextArgs"/>: with two text arguments, the first key is <c>{2}</c>.
        /// </summary>
        public IReadOnlyList<GameAction> KeyActions => _keyActions ??= KeyActionsIn(InstructionTemplate);

        private IReadOnlyList<GameAction> _keyActions;

        /// <summary>The localized description with the player's current key bindings filled in.</summary>
        public string LocalizedInstruction => LocalizedInstructionText(InputBindings.Label);

        /// <summary>Creates a quest. <paramref name="progress"/> and <paramref name="textArgs"/> may be null.</summary>
        public QuestDefinition(string id, QuestChapter chapter, string title, string instruction, float reward,
                               Func<QuestContext, bool> condition,
                               Func<QuestContext, (int current, int target)> progress = null,
                               Func<object[]> textArgs = null)
        {
            Id                  = id;
            Chapter             = chapter;
            Title               = title;
            InstructionTemplate = instruction;
            Reward              = reward;
            Condition           = condition;
            Progress            = progress;
            TextArgs            = textArgs;
        }

        /// <summary>The token an instruction uses for <paramref name="action"/>'s key, e.g. "{Interact}".</summary>
        public static string KeyToken(GameAction action) => "{" + action + "}";

        /// <summary>The factory key label for <paramref name="action"/>, ignoring any rebind.</summary>
        public static string DefaultKeyLabel(GameAction action) =>
            InputBindings.KeyLabel(InputBindings.Default(action));

        /// <summary>The instruction with every key token replaced by <paramref name="keyLabel"/>.</summary>
        public string InstructionText(Func<GameAction, string> keyLabel) => ReplaceKeyTokens(InstructionTemplate, keyLabel);

        /// <summary>
        /// The localized description formatted with <see cref="TextArgs"/> followed by <paramref name="keyLabel"/>
        /// of each of <see cref="KeyActions"/>; the English instruction when the table has no entry. A broken
        /// pattern is logged and returned as is (see <see cref="Loc.F"/>), never swapped for English.
        /// </summary>
        public string LocalizedInstructionText(Func<GameAction, string> keyLabel)
        {
            if (!Loc.Has(DescriptionKey)) return InstructionText(keyLabel);
            return Loc.F(DescriptionKey, DescriptionArgs(keyLabel));
        }

        /// <summary><see cref="TextArgs"/>, then the label of each of <see cref="KeyActions"/>.</summary>
        private object[] DescriptionArgs(Func<GameAction, string> keyLabel)
        {
            object[] text = TextArgs?.Invoke() ?? Array.Empty<object>();
            var keys = KeyActions;
            var all = new object[text.Length + keys.Count];
            Array.Copy(text, all, text.Length);
            for (int i = 0; i < keys.Count; i++) all[text.Length + i] = keyLabel(keys[i]);
            return all;
        }

        /// <summary>The actions whose tokens appear in <paramref name="template"/>, by first appearance.</summary>
        private static IReadOnlyList<GameAction> KeyActionsIn(string template)
        {
            var found = new List<(int index, GameAction action)>();
            if (!string.IsNullOrEmpty(template))
                foreach (var action in InputBindings.AllActions)
                {
                    int at = template.IndexOf(KeyToken(action), StringComparison.Ordinal);
                    if (at >= 0) found.Add((at, action));
                }
            found.Sort((a, b) => a.index.CompareTo(b.index));
            return found.ConvertAll(f => f.action);
        }

        private static string ReplaceKeyTokens(string template, Func<GameAction, string> keyLabel)
        {
            string text = template ?? string.Empty;
            foreach (var action in InputBindings.AllActions)
                text = text.Replace(KeyToken(action), keyLabel(action));
            return text;
        }

        /// <summary>True when <see cref="Condition"/> holds for <paramref name="ctx"/>.</summary>
        public bool IsMet(QuestContext ctx) => Condition != null && ctx != null && Condition(ctx);

        /// <summary>
        /// The title, with " (current/target)" appended when the quest has a counter, e.g.
        /// "Own 3 shelves (1/3)". The current value is clamped to 0..target.
        /// </summary>
        public string ProgressText(QuestContext ctx)
        {
            if (Progress == null || ctx == null) return LocalizedTitle;
            var (current, target) = Progress(ctx);
            int shown = Math.Max(0, Math.Min(current, target));
            return $"{LocalizedTitle} ({shown}/{target})";
        }
    }
}
