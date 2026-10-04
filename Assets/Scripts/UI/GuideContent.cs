using System.Collections.Generic;
using PetShop.Core;
using PetShop.Progression;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>One topic of the in-game guide: a title, plain second-person body text and the
    /// bindable actions the text names (so tests can check the current key shows up).</summary>
    public sealed class GuideSection
    {
        /// <summary>Short sentence-case title shown in the section list.</summary>
        public string Title { get; }

        /// <summary>The explanation, in plain second person, with current key labels filled in.</summary>
        public string Body { get; }

        /// <summary>Bindable actions whose current key the body mentions.</summary>
        public IReadOnlyList<GameAction> Keys { get; }

        /// <summary>Creates a section.</summary>
        public GuideSection(string title, string body, params GameAction[] keys)
        {
            Title = title;
            Body  = body;
            Keys  = keys;
        }
    }

    /// <summary>
    /// The text of the in-game guide, one <see cref="GuideSection"/> per mechanic. Built fresh on
    /// every call from <see cref="InputBindings"/> and the game's own rule constants, so a rebind
    /// or a rule change shows up in the guide without editing it.
    /// </summary>
    public static class GuideContent
    {
        private static string L(GameAction a) => InputBindings.Label(a);
        private static string K(UnityEngine.KeyCode key) => BuildToolbar.KeyLabel(key);

        /// <summary>Every guide section, in reading order.</summary>
        public static IReadOnlyList<GuideSection> Sections() => new List<GuideSection>
        {
            Serving(), Restocking(), Deliveries(), Furniture(), Walls(), Animals(),
            Staff(), Reputation(), Day(), Quests(), Tiers(), Saving(),
        };

        private static GuideSection Serving() => new("Serving at the counter",
            "Customers who want to buy something queue at the till. Stand at the counter and press " +
            $"{L(GameAction.Interact)} to serve whoever is at the front of the line. If nobody is waiting, " +
            "you are told so.\n\nEvery customer only waits so long. Serve the queue before they give up " +
            "and walk out, or your reputation suffers. Assistants you hire work the till on their own, " +
            "but you can always serve as well to clear the line faster.",
            GameAction.Interact);

        private static GuideSection Restocking() => new("Restocking shelves",
            $"Walk up to a shelf and press {L(GameAction.Interact)} to fill it. Units come out of the " +
            "stockroom first. If the stockroom runs short, the rest is bought at cash-and-carry prices, " +
            "which cost a lot more.\n\nThe cheap way is to order ahead: open the Shop book with " +
            $"{L(GameAction.Ledger)}, go to Stock and order a pallet of {StatsPanel.OrderSize} units for an aisle. " +
            "Auto-reorder can place those orders for you when the stockroom runs low.",
            GameAction.Interact, GameAction.Ledger);

        private static GuideSection Deliveries() => new("Deliveries on the forecourt",
            "Wholesale orders and furniture arrive by van later in the day and are left on the " +
            $"forecourt in front of the shop. Walk to a pallet and press {L(GameAction.Interact)} to carry " +
            "it into the stockroom, ready to shelve. A furniture crate unpacks into your inventory bar " +
            "instead.\n\nAnything still on a van when you close lands overnight, so nothing you paid for is lost. " +
            "The Stock page shows what is on the van and roughly when it arrives.",
            GameAction.Interact);

        private static GuideSection Furniture() => new("Ordering and placing furniture",
            $"Open the Shop book with {L(GameAction.Ledger)} and go to Build. Furniture is paid for when " +
            "you order it and arrives as a crate on the forecourt; unpack it with " +
            $"{L(GameAction.Interact)}.\n\nPieces you own sit in the inventory bar. Press 1-9 to pick one up, " +
            $"click to place it, and rotate with {HotkeyHelp.RotateKeys()}. Middle-click or " +
            $"{L(GameAction.BuildRemove)} removes a piece, and Esc cancels.",
            GameAction.Ledger, GameAction.Interact, GameAction.BuildRotate, GameAction.BuildRemove);

        private static GuideSection Walls() => new("Walls and structure",
            $"Press {L(GameAction.BuildMode)} for the top-down build view. Its tool strip has " +
            $"Wall ({K(BuildToolbar.WallKey)}), Window wall ({K(BuildToolbar.WindowWallKey)}), " +
            $"Doorway ({K(BuildToolbar.DoorwayKey)}) and Fence ({K(BuildToolbar.FenceKey)}). Walls are not " +
            "delivered: each piece is charged when you place it.\n\nThe Remove tool " +
            $"({K(BuildToolbar.RemoveKey)}) takes pieces away again, including the shop's own room walls, " +
            "so you can open up the floor. Right-drag to orbit the view; Esc leaves it.",
            GameAction.BuildMode);

        private static GuideSection Animals() => new("Pens, buying, feeding and breeding",
            $"Press {L(GameAction.Interact)} at a pen. If the animals need feeding or fresh bedding, that " +
            "comes first and costs a small fee. Otherwise, if there is room, you buy a young animal from " +
            "the breeder.\n\nTwo adults in a pen with space to spare may breed overnight. Plan pairings in " +
            "the breeding planner, follow bloodlines in the family tree and enter animals in the showcase, " +
            "all from the Animals page of the Shop book.",
            GameAction.Interact);

        private static GuideSection Staff() => new("Staff",
            "Assistants work the till on their own, slower than you do. Each one is paid a daily wage, " +
            "added to tonight's bill.\n\nOpen the staff board from the Staff page of the Shop book " +
            $"({L(GameAction.Ledger)}) to hire from the applicants looking for work; the list changes every " +
            "morning. Letting someone go costs you some reputation.",
            GameAction.Ledger);

        private static GuideSection Reputation() => new("Reputation and patience",
            "Reputation runs from 0 to 100. Customers who leave with a full basket raise it. It drops when " +
            "someone gives up waiting in the queue, finds the shelves empty, cannot find the animal they " +
            "wanted, or when you fire staff.\n\nPrices matter too: dearer shelves make shoppers less likely " +
            "to buy. Reputation unlocks new tiers for the shop.");

        private static GuideSection Day() => new("The day and closing early",
            "Each day runs on a clock. Near closing time the doors stop letting new customers in. Press " +
            $"{L(GameAction.EndDay)} to close up early whenever you like.\n\nOvernight, animals may breed, " +
            "deliveries still on the road land, and rent and wages are paid. If the bills take your balance " +
            "below zero, the shop closes for good, so keep an eye on tonight's bill in the Shop book.",
            GameAction.EndDay);

        private static GuideSection Quests() => new("Quests and the journal",
            $"Press {QuestJournalPanel.OpenKeyLabel} to open the quest journal. It lists what you are working " +
            "on and how far along you are. The quest tracker on screen keeps the current goal in view " +
            "while you play.");

        private static GuideSection Tiers() => new("Tiers and unlocks",
            "Your shop climbs tiers as its reputation grows, and a tier once reached is never lost.\n\n" +
            $"{ProgressionRules.CornerShopName}: where every shop starts.\n" +
            $"{ProgressionRules.LocalFavouriteName} (reputation {ProgressionRules.LocalFavouriteReputation:0}): the back strip of the lot opens up.\n" +
            $"{ProgressionRules.TrustedNameName} (reputation {ProgressionRules.TrustedNameReputation:0}): horse pens.\n" +
            $"{ProgressionRules.TownLandmarkName} (reputation {ProgressionRules.TownLandmarkReputation:0}): tiger pens and the whole yard.");

        private static GuideSection Saving() => new("Saving",
            $"Press {L(GameAction.QuickSave)} to save at any time, or use Save game in the pause menu (Esc). " +
            "With Autosave on (in Settings) the game also saves itself each morning.\n\nThere are " +
            $"{SaveSystem.SlotCount} save slots; pick one on the title screen to continue it or start a new shop. " +
            "Save & quit in the pause menu only quits once the save has worked.",
            GameAction.QuickSave);
    }
}
