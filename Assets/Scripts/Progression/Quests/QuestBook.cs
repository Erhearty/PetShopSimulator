using System.Collections.Generic;

namespace PetShop.Progression.Quests
{
    /// <summary>
    /// Pure quest-book state: the current chapter, the completed quests and the raised one-shot
    /// flags. <see cref="Evaluate"/> checks the unlocked quests against a snapshot.
    ///
    /// Rules: the tutorial runs one step at a time, in catalogue order; quests in later chapters
    /// may complete in any order; the next chapter unlocks only once every quest in the current
    /// one is done. A quest that is already satisfied when it unlocks completes in the SAME
    /// <see cref="Evaluate"/> call, so one call can finish several tutorial steps in a row and
    /// open the next chapter.
    /// </summary>
    public sealed class QuestBook
    {
        private const QuestChapter FirstChapter = QuestChapter.Tutorial;
        private const QuestChapter LastChapter  = QuestChapter.End;

        private readonly IReadOnlyList<QuestDefinition> _quests;
        private readonly HashSet<string> _completed = new();
        private readonly HashSet<string> _flags     = new();

        /// <summary>The chapter being worked on.</summary>
        public QuestChapter CurrentChapter { get; private set; } = FirstChapter;

        /// <summary>Ids of every completed quest.</summary>
        public IReadOnlyCollection<string> Completed => _completed;

        /// <summary>Raised one-shot flags (see <see cref="QuestFlags"/>).</summary>
        public IReadOnlyCollection<string> Flags => _flags;

        /// <summary>The quests this book tracks, in order.</summary>
        public IReadOnlyList<QuestDefinition> Quests => _quests;

        /// <summary>True once every quest in every chapter is done.</summary>
        public bool AllComplete => CurrentChapter == LastChapter && IsChapterComplete(LastChapter);

        /// <summary>The tutorial step waiting to be done, or null once the tutorial is over.</summary>
        public QuestDefinition CurrentTutorialStep =>
            CurrentChapter == QuestChapter.Tutorial ? FirstIncomplete(QuestChapter.Tutorial) : null;

        /// <summary>Creates a book over <paramref name="quests"/>; defaults to <see cref="QuestCatalog.All"/>.</summary>
        public QuestBook(IReadOnlyList<QuestDefinition> quests = null)
        {
            _quests = quests ?? QuestCatalog.All;
            AdvanceChapters();
        }

        /// <summary>True when the quest with <paramref name="id"/> is done.</summary>
        public bool IsComplete(string id) => id != null && _completed.Contains(id);

        /// <summary>True when <paramref name="chapter"/> is unlocked (current or earlier).</summary>
        public bool IsUnlocked(QuestChapter chapter) => chapter <= CurrentChapter;

        /// <summary>True when every quest of <paramref name="chapter"/> is done.</summary>
        public bool IsChapterComplete(QuestChapter chapter)
        {
            foreach (var q in _quests)
                if (q.Chapter == chapter && !_completed.Contains(q.Id)) return false;
            return true;
        }

        /// <summary>
        /// The quests the player is working on now: only the current step during the tutorial,
        /// otherwise every unfinished quest of the current chapter, in order.
        /// </summary>
        public List<QuestDefinition> ActiveQuests()
        {
            var active = new List<QuestDefinition>();
            if (CurrentChapter == QuestChapter.Tutorial)
            {
                var step = CurrentTutorialStep;
                if (step != null) active.Add(step);
                return active;
            }
            foreach (var q in _quests)
                if (q.Chapter == CurrentChapter && !_completed.Contains(q.Id)) active.Add(q);
            return active;
        }

        /// <summary>True when <paramref name="flag"/> has been raised.</summary>
        public bool HasFlag(string flag) => flag != null && _flags.Contains(flag);

        /// <summary>Raises a one-shot flag; it stays raised and is saved.</summary>
        public void RaiseFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        /// <summary>
        /// Completes every unlocked quest whose condition holds in <paramref name="ctx"/>, opening
        /// later steps and chapters as it goes. Returns the newly completed quests in completion
        /// order; empty when nothing changed.
        /// </summary>
        public List<QuestDefinition> Evaluate(QuestContext ctx)
        {
            var done = new List<QuestDefinition>();
            if (ctx == null) return done;

            bool progressed = true;
            while (progressed)
            {
                progressed = CurrentChapter == QuestChapter.Tutorial
                    ? TryCompleteTutorialStep(ctx, done)
                    : CompleteChapterQuests(ctx, done);
                if (AdvanceChapters()) progressed = true;
            }
            return done;
        }

        /// <summary>Marks every tutorial step done (no rewards) and unlocks the chapters after it.</summary>
        public void SkipTutorial()
        {
            foreach (var q in _quests)
                if (q.Chapter == QuestChapter.Tutorial) _completed.Add(q.Id);
            AdvanceChapters();
        }

        /// <summary>The state to save.</summary>
        public QuestProgress ToProgress()
        {
            var progress = new QuestProgress { Chapter = CurrentChapter };
            foreach (var q in _quests) if (_completed.Contains(q.Id)) progress.CompletedIds.Add(q.Id);
            progress.Flags.AddRange(_flags);
            progress.Flags.Sort(System.StringComparer.Ordinal);
            return progress;
        }

        /// <summary>
        /// Replaces this book's state with saved <paramref name="progress"/>. Unknown ids are
        /// ignored. The chapter is re-derived from the completed set (the saved chapter is not
        /// trusted), so a quest added to an earlier chapter after the save is still reached.
        /// Null resets to a fresh book.
        /// </summary>
        public void Restore(QuestProgress progress)
        {
            _completed.Clear();
            _flags.Clear();
            CurrentChapter = FirstChapter;
            if (progress == null) { AdvanceChapters(); return; }

            foreach (var id in progress.CompletedIds ?? new List<string>())
                if (Find(id) != null) _completed.Add(id);
            foreach (var flag in progress.Flags ?? new List<string>()) RaiseFlag(flag);
            AdvanceChapters();
        }

        private bool TryCompleteTutorialStep(QuestContext ctx, List<QuestDefinition> done)
        {
            var step = FirstIncomplete(QuestChapter.Tutorial);
            if (step == null || !step.IsMet(ctx)) return false;
            Complete(step, done);
            return true;
        }

        private bool CompleteChapterQuests(QuestContext ctx, List<QuestDefinition> done)
        {
            bool any = false;
            foreach (var q in _quests)
            {
                if (q.Chapter != CurrentChapter || _completed.Contains(q.Id) || !q.IsMet(ctx)) continue;
                Complete(q, done);
                any = true;
            }
            return any;
        }

        private void Complete(QuestDefinition quest, List<QuestDefinition> done)
        {
            _completed.Add(quest.Id);
            done.Add(quest);
        }

        /// <summary>Moves past every finished chapter. Returns true when the chapter changed.</summary>
        private bool AdvanceChapters()
        {
            bool moved = false;
            while (CurrentChapter < LastChapter && IsChapterComplete(CurrentChapter))
            {
                CurrentChapter++;
                moved = true;
            }
            return moved;
        }

        private QuestDefinition FirstIncomplete(QuestChapter chapter)
        {
            foreach (var q in _quests)
                if (q.Chapter == chapter && !_completed.Contains(q.Id)) return q;
            return null;
        }

        private QuestDefinition Find(string id)
        {
            foreach (var q in _quests) if (q.Id == id) return q;
            return null;
        }

    }
}
