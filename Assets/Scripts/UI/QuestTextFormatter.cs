using System;
using System.Text;
using UnityEngine;
using PetShop.Progression.Quests;

namespace PetShop.UI
{
    /// <summary>How a quest reads in the journal.</summary>
    public enum QuestJournalState
    {
        /// <summary>Its chapter has not unlocked yet.</summary>
        Locked,
        /// <summary>Its chapter is open but this tutorial step is not the current one.</summary>
        Upcoming,
        /// <summary>The player is working on it now.</summary>
        Active,
        /// <summary>Completed.</summary>
        Done,
    }

    /// <summary>
    /// Pure text for the HUD quest tracker and the quest journal, so the wording is unit-testable
    /// without a scene. Colours follow the shared UI palette (all well above 4.5:1 on the panels).
    /// </summary>
    public static class QuestTextFormatter
    {
        /// <summary>Most quests the HUD tracker lists at once.</summary>
        public const int MaxTrackedQuests = 3;

        /// <summary>Tracker text once every quest is done.</summary>
        public const string AllDoneText = "All quests complete";

        /// <summary>Journal text when there is no quest book.</summary>
        public const string UnavailableText = "Quests are not available.";

        /// <summary>Journal note above a chapter that has not unlocked.</summary>
        public const string LockedNotice = "Locked: finish the previous chapter to unlock this one.";

        /// <summary>The player-facing name of <paramref name="chapter"/>.</summary>
        public static string ChapterName(QuestChapter chapter) => chapter switch
        {
            QuestChapter.Tutorial => "Tutorial",
            QuestChapter.Early    => "Early game",
            QuestChapter.Mid      => "Mid game",
            QuestChapter.End      => "End game",
            _                     => chapter.ToString(),
        };

        /// <summary>The lower-case word shown for <paramref name="state"/>.</summary>
        public static string StateLabel(QuestJournalState state) => state switch
        {
            QuestJournalState.Locked   => "locked",
            QuestJournalState.Upcoming => "up next",
            QuestJournalState.Active   => "active",
            _                          => "done",
        };

        /// <summary>Where <paramref name="quest"/> stands in <paramref name="book"/>.</summary>
        public static QuestJournalState StateOf(QuestBook book, QuestDefinition quest)
        {
            if (book.IsComplete(quest.Id)) return QuestJournalState.Done;
            if (!book.IsUnlocked(quest.Chapter)) return QuestJournalState.Locked;
            foreach (var active in book.ActiveQuests())
                if (active.Id == quest.Id) return QuestJournalState.Active;
            return QuestJournalState.Upcoming;
        }

        /// <summary>"current/target" for a counter quest (current clamped to 0..target), else null.</summary>
        public static string CounterText(QuestDefinition quest, QuestContext ctx)
        {
            if (quest == null || quest.Progress == null || ctx == null) return null;
            var (current, target) = quest.Progress(ctx);
            return $"{Math.Max(0, Math.Min(current, target))}/{target}";
        }

        /// <summary>The HUD tracker text: the tutorial step's instruction, or up to three active quests.</summary>
        public static string TrackerText(QuestBook book, QuestContext ctx)
        {
            if (book == null) return string.Empty;
            if (book.AllComplete) return AllDoneText;
            var step = book.CurrentTutorialStep;
            return step != null ? TutorialText(book, step) : ChapterText(book, ctx);
        }

        /// <summary>Two-line chapter button text: the name, then its state and done/total count.</summary>
        public static string ChapterSummary(QuestBook book, QuestChapter chapter)
        {
            string name = ChapterName(chapter);
            if (book == null) return name;
            var quests = QuestCatalog.InChapter(book.Quests, chapter);
            int done = quests.FindAll(q => book.IsComplete(q.Id)).Count;
            var state = book.IsChapterComplete(chapter) ? QuestJournalState.Done
                      : book.IsUnlocked(chapter) ? QuestJournalState.Active : QuestJournalState.Locked;
            return $"{name}\n{StateLabel(state)}  {done}/{quests.Count}";
        }

        /// <summary>The journal page for <paramref name="chapter"/>: every quest with state, instruction, progress, reward.</summary>
        public static string ChapterBody(QuestBook book, QuestChapter chapter, QuestContext ctx)
        {
            if (book == null) return UnavailableText;
            var sb = new StringBuilder();
            if (!book.IsUnlocked(chapter)) sb.Append(LockedNotice).Append("\n\n");
            foreach (var quest in QuestCatalog.InChapter(book.Quests, chapter))
                sb.Append(Entry(book, quest, ctx)).Append('\n');
            return sb.ToString();
        }

        private static string TutorialText(QuestBook book, QuestDefinition step)
        {
            var steps = QuestCatalog.InChapter(book.Quests, QuestChapter.Tutorial);
            int number = steps.FindIndex(q => q.Id == step.Id) + 1;
            return $"<b>Tutorial {number}/{steps.Count}: {step.Title}</b>\n{step.Instruction}";
        }

        private static string ChapterText(QuestBook book, QuestContext ctx)
        {
            var active = book.ActiveQuests();
            var sb = new StringBuilder($"<b>{ChapterName(book.CurrentChapter)}</b>");
            int shown = Math.Min(active.Count, MaxTrackedQuests);
            for (int i = 0; i < shown; i++) sb.Append("\n- ").Append(active[i].ProgressText(ctx));
            if (active.Count > shown)
                sb.Append($"\n+{active.Count - shown} more ({QuestJournalPanel.OpenKeyLabel})");
            return sb.ToString();
        }

        private static string Entry(QuestBook book, QuestDefinition quest, QuestContext ctx)
        {
            var state = StateOf(book, quest);
            string titleHex = ColorUtility.ToHtmlStringRGB(StateColor(state));
            string bodyHex  = ColorUtility.ToHtmlStringRGB(state == QuestJournalState.Locked ? UIFactory.InkMuted : UIFactory.Ink);
            return $"<color=#{titleHex}><b>[{StateLabel(state)}] {quest.Title}</b></color>\n" +
                   $"<color=#{bodyHex}>{quest.Instruction}\n{ProgressLine(state, quest, ctx)}\n" +
                   $"Reward: €{quest.Reward:N0}</color>\n";
        }

        private static string ProgressLine(QuestJournalState state, QuestDefinition quest, QuestContext ctx)
        {
            if (state == QuestJournalState.Done) return "Progress: complete";
            if (state == QuestJournalState.Locked) return "Progress: locked";
            string counter = CounterText(quest, ctx);
            return counter != null ? $"Progress: {counter}" : "Progress: not done yet";
        }

        private static Color StateColor(QuestJournalState state) => state switch
        {
            QuestJournalState.Done   => UIFactory.Good,
            QuestJournalState.Active => UIFactory.Accent,
            QuestJournalState.Locked => UIFactory.InkMuted,
            _                        => UIFactory.Ink,
        };
    }
}
