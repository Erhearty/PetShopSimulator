using System.Collections.Generic;
using PetShop.Core;
using PetShop.Localization;
using PetShop.Progression;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>One topic of the in-game guide: a title, plain second-person body text and the
    /// bindable actions the text names (so tests can check the current key shows up).</summary>
    public sealed class GuideSection
    {
        /// <summary>Stable lower-case slug; the text keys are <c>guide.&lt;id&gt;.title</c> and <c>.body</c>.</summary>
        public string Id { get; }

        /// <summary>Short sentence-case title shown in the section list.</summary>
        public string Title { get; }

        /// <summary>The explanation, in plain second person, with current key labels filled in.</summary>
        public string Body { get; }

        /// <summary>Bindable actions whose current key the body mentions.</summary>
        public IReadOnlyList<GameAction> Keys { get; }

        /// <summary>The localisation key of the title.</summary>
        public string TitleKey => GuideContent.TitleKey(Id);

        /// <summary>Creates a section.</summary>
        public GuideSection(string id, string title, string body, params GameAction[] keys)
        {
            Id    = id;
            Title = title;
            Body  = body;
            Keys  = keys;
        }
    }

    /// <summary>
    /// The text of the in-game guide, one <see cref="GuideSection"/> per mechanic. Built fresh on
    /// every call, in the current language, from <see cref="InputBindings"/> and the game's own rule
    /// constants, so a rebind, a rule change or a language switch shows up without editing it.
    /// </summary>
    public static class GuideContent
    {
        private static string L(GameAction a) => InputBindings.Label(a);

        /// <summary>The localisation key of section <paramref name="id"/>'s title.</summary>
        public static string TitleKey(string id) => "guide." + id + ".title";

        /// <summary>The localisation key of section <paramref name="id"/>'s body.</summary>
        public static string BodyKey(string id) => "guide." + id + ".body";

        /// <summary>Every guide section, in reading order.</summary>
        public static IReadOnlyList<GuideSection> Sections() => new List<GuideSection>
        {
            Serving(), Restocking(), Deliveries(), Furniture(), Animals(),
            Staff(), Reputation(), Day(), Quests(), Tiers(), Saving(),
        };

        private static GuideSection Make(string id, GameAction[] keys, params object[] args) =>
            new(id, Loc.T(TitleKey(id)), Loc.F(BodyKey(id), args), keys);

        private static GuideSection Serving() => Make("serving",
            new[] { GameAction.Interact },
            L(GameAction.Interact));

        private static GuideSection Restocking() => Make("restocking",
            new[] { GameAction.Interact, GameAction.Ledger },
            L(GameAction.Interact), L(GameAction.Ledger), StatsPanel.OrderSize);

        private static GuideSection Deliveries() => Make("deliveries",
            new[] { GameAction.Interact },
            L(GameAction.Interact));

        private static GuideSection Furniture() => Make("furniture",
            new[] { GameAction.Ledger, GameAction.Interact, GameAction.BuildRotate, GameAction.BuildRemove },
            L(GameAction.Ledger), L(GameAction.Interact), HotkeyHelp.RotateKeys(), L(GameAction.BuildRemove));

        private static GuideSection Animals() => Make("animals",
            new[] { GameAction.Interact },
            L(GameAction.Interact));

        private static GuideSection Staff() => Make("staff",
            new[] { GameAction.Ledger },
            L(GameAction.Ledger));

        private static GuideSection Reputation() => Make("reputation",
            System.Array.Empty<GameAction>());

        private static GuideSection Day() => Make("day",
            new[] { GameAction.EndDay },
            L(GameAction.EndDay));

        private static GuideSection Quests() => Make("quests",
            System.Array.Empty<GameAction>(),
            QuestJournalPanel.OpenKeyLabel);

        private static GuideSection Tiers() => Make("tiers",
            System.Array.Empty<GameAction>(),
            Loc.T("tier.corner_shop"),
            Loc.T("tier.local_favourite"), ProgressionRules.LocalFavouriteReputation,
            Loc.T("tier.trusted_name"), ProgressionRules.TrustedNameReputation,
            Loc.T("tier.town_landmark"), ProgressionRules.TownLandmarkReputation);

        private static GuideSection Saving() => Make("saving",
            new[] { GameAction.QuickSave },
            L(GameAction.QuickSave), SaveSystem.SlotCount);
    }
}
