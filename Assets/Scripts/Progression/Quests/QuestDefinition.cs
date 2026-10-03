using System;
using PetShop.Core;

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

        /// <summary>The instruction with the player's current key bindings filled in.</summary>
        public string Instruction => InstructionText(InputBindings.Label);

        /// <summary>Creates a quest. <paramref name="progress"/> may be null.</summary>
        public QuestDefinition(string id, QuestChapter chapter, string title, string instruction, float reward,
                               Func<QuestContext, bool> condition,
                               Func<QuestContext, (int current, int target)> progress = null)
        {
            Id                  = id;
            Chapter             = chapter;
            Title               = title;
            InstructionTemplate = instruction;
            Reward              = reward;
            Condition           = condition;
            Progress            = progress;
        }

        /// <summary>The token an instruction uses for <paramref name="action"/>'s key, e.g. "{Interact}".</summary>
        public static string KeyToken(GameAction action) => "{" + action + "}";

        /// <summary>The factory key label for <paramref name="action"/>, ignoring any rebind.</summary>
        public static string DefaultKeyLabel(GameAction action) =>
            InputBindings.KeyLabel(InputBindings.Default(action));

        /// <summary>The instruction with every key token replaced by <paramref name="keyLabel"/>.</summary>
        public string InstructionText(Func<GameAction, string> keyLabel)
        {
            string text = InstructionTemplate ?? string.Empty;
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
            if (Progress == null || ctx == null) return Title;
            var (current, target) = Progress(ctx);
            int shown = Math.Max(0, Math.Min(current, target));
            return $"{Title} ({shown}/{target})";
        }
    }
}
