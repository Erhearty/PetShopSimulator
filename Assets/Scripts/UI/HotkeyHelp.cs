using PetShop.Core;
using PetShop.Localization;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The controls cheat-sheet text shown on the title screen, built
    /// from the current key bindings so a rebind shows up everywhere.
    /// </summary>
    public static class HotkeyHelp
    {
        /// <summary>Fixed mouse / key glyphs that are not rebindable.</summary>
        private const string Lmb = "LMB";
        private const string Rmb = "RMB";
        private const string Esc = "Esc";
        private const string SlotKeys = "1-9";

        private static string L(GameAction a) => InputBindings.Label(a);
        private static string K(UnityEngine.KeyCode key) => BuildToolbar.KeyLabel(key);

        /// <summary>The rotate keys while placing: "R / Shift+R / wheel" with the bound key.</summary>
        public static string RotateKeys() => Loc.F("hotkey.rotate_keys", L(GameAction.BuildRotate));

        /// <summary>The HUD controls cheat-sheet.</summary>
        public static string Controls() =>
            Loc.T("hotkey.title") + "\n" +
            Loc.F("hotkey.move", InputBindings.MoveLabel(), L(GameAction.Jump)) + "\n" +
            Loc.T("hotkey.look") + "\n" +
            Loc.F("hotkey.interact", L(GameAction.Interact)) + "\n" +
            Loc.F("hotkey.build_view", L(GameAction.BuildMode)) + "\n" +
            Loc.F("hotkey.place_slot", SlotKeys) + "\n" +
            Loc.F("hotkey.place", Lmb, Esc) + "\n" +
            Loc.F("hotkey.rotate", RotateKeys()) + "\n" +
            Loc.F("hotkey.remove", L(GameAction.BuildRemove)) + "\n" +
            Loc.F("hotkey.build_tools", K(BuildToolbar.WallKey), K(BuildToolbar.WindowWallKey),
                  K(BuildToolbar.DoorwayKey), K(BuildToolbar.FenceKey), K(BuildToolbar.RemoveKey), Rmb) + "\n" +
            Loc.F("hotkey.end_day", L(GameAction.EndDay)) + "\n" +
            Loc.F("hotkey.serve", L(GameAction.Interact)) + "\n" +
            Loc.F("hotkey.ledger", L(GameAction.Ledger)) + "\n" +
            Loc.F("hotkey.journal", QuestJournalPanel.OpenKeyLabel) + "\n" +
            Loc.F("hotkey.guide", L(GameAction.Guide)) + "\n" +
            Loc.F("hotkey.pause", Esc, L(GameAction.QuickSave));

        /// <summary>The one-line controls hint at the foot of the title screen.</summary>
        public static string TitleHint() =>
            Loc.F("hotkey.title_hint", InputBindings.MoveLabel(), Rmb, L(GameAction.Interact),
                  L(GameAction.Ledger), L(GameAction.BuildMode), RotateKeys(), QuestJournalPanel.OpenKeyLabel);
    }
}
