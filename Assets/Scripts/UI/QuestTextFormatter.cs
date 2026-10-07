using System;
using System.Text;
using UnityEngine;
using PetShop.Localization;
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
        public static string AllDoneText => Loc.T("tracker.all_done");

        /// <summary>Journal text when there is no quest book.</summary>
        public static string UnavailableText => Loc.T("journal.unavailable");

        /// <summary>Journal note above a chapter that has not unlocked.</summary>
        public static string LockedNotice => Loc.T("journal.locked_notice");

        /// <summary>The player-facing name of <paramref name="chapter"/> in the current language.</summary>
        public static string ChapterName(QuestChapter chapter) => chapter switch
        {
            QuestChapter.Tutorial => Loc.T("journal.chapter.tutorial"),
            QuestChapter.Early    => Loc.T("journal.chapter.early"),
            QuestChapter.Mid      => Loc.T("journal.chapter.mid"),
            QuestChapter.End      => Loc.T("journal.chapter.end"),
            _                     => chapter.ToString(),
        };

        /// <summary>The lower-case word shown for <paramref name="state"/>, in the current language.</summary>
        public static string StateLabel(QuestJournalState state) => state switch
        {
            QuestJournalState.Locked   => Loc.T("journal.state.locked"),
            QuestJournalState.Upcoming => Loc.T("journal.state.upcoming"),
            QuestJournalState.Active   => Loc.T("journal.state.active"),
            _                          => Loc.T("journal.state.done"),
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
            return $"<b>{Loc.F("tracker.tutorial", number, steps.Count, step.LocalizedTitle)}</b>\n{step.LocalizedInstruction}";
        }

        private static string ChapterText(QuestBook book, QuestContext ctx)
        {
            var active = book.ActiveQuests();
            var sb = new StringBuilder($"<b>{ChapterName(book.CurrentChapter)}</b>");
            int shown = Math.Min(active.Count, MaxTrackedQuests);
            for (int i = 0; i < shown; i++) sb.Append("\n- ").Append(active[i].ProgressText(ctx));
            if (active.Count > shown)
                sb.Append('\n').Append(Loc.F("tracker.more", active.Count - shown, QuestJournalPanel.OpenKeyLabel));
            return sb.ToString();
        }

        private static string Entry(QuestBook book, QuestDefinition quest, QuestContext ctx)
        {
            var state = StateOf(book, quest);
            string titleHex = ColorUtility.ToHtmlStringRGB(StateColor(state));
            string bodyHex  = ColorUtility.ToHtmlStringRGB(state == QuestJournalState.Locked ? UIFactory.InkMuted : UIFactory.Ink);
            return $"<color=#{titleHex}><b>[{StateLabel(state)}] {quest.LocalizedTitle}</b></color>\n" +
                   $"<color=#{bodyHex}>{quest.LocalizedInstruction}\n{ProgressLine(state, quest, ctx)}\n" +
                   $"{Loc.F("journal.reward", quest.Reward)}</color>\n";
        }

        private static string ProgressLine(QuestJournalState state, QuestDefinition quest, QuestContext ctx)
        {
            if (state == QuestJournalState.Done) return Loc.T("journal.progress.complete");
            if (state == QuestJournalState.Locked) return Loc.T("journal.progress.locked");
            string counter = CounterText(quest, ctx);
            return counter != null ? Loc.F("journal.progress.counter", counter) : Loc.T("journal.progress.pending");
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
