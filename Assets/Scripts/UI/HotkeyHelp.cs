using PetShop.Core;
using PetShop.Shop;

namespace PetShop.UI
{
    /// <summary>
    /// The controls cheat-sheet text shown on the title screen, built
    /// from the current key bindings so a rebind shows up everywhere.
    /// </summary>
    public static class HotkeyHelp
    {
        private static string L(GameAction a) => InputBindings.Label(a);
        private static string K(UnityEngine.KeyCode key) => BuildToolbar.KeyLabel(key);

        /// <summary>The rotate keys while placing: "R / Shift+R / wheel" with the bound key.</summary>
        public static string RotateKeys()
        {
            string r = L(GameAction.BuildRotate);
            return $"{r} / Shift+{r} / wheel";
        }

        /// <summary>The HUD controls cheat-sheet.</summary>
        public static string Controls() =>
            "<b>Controls</b>\n" +
            $"{InputBindings.MoveLabel()}  move  ·  {L(GameAction.Jump)}  jump\n" +
            "Mouse  look\n" +
            $"{L(GameAction.Interact)}  interact / restock / unpack\n" +
            $"{L(GameAction.BuildMode)}  build view\n" +
            "1-9  place from the inventory bar\n" +
            "LMB place  ·  Esc cancel\n" +
            $"{RotateKeys()}  rotate\n" +
            $"Middle-click / {L(GameAction.BuildRemove)}  remove\n" +
            $"Build view: {K(BuildToolbar.WallKey)} wall · {K(BuildToolbar.WindowWallKey)} window wall · " +
            $"{K(BuildToolbar.DoorwayKey)} doorway · {K(BuildToolbar.FenceKey)} fence · " +
            $"{K(BuildToolbar.RemoveKey)} remove tool · RMB orbit\n" +
            $"{L(GameAction.EndDay)}  close up early\n" +
            $"{L(GameAction.Interact)} at the counter  serve the queue\n" +
            $"{L(GameAction.Ledger)}  Shop book\n" +
            $"{QuestJournalPanel.OpenKeyLabel}  quest journal\n" +
            $"{L(GameAction.Guide)}  guide\n" +
            $"Esc  pause  ·  {L(GameAction.QuickSave)} save";

        /// <summary>The one-line controls hint at the foot of the title screen.</summary>
        public static string TitleHint() =>
            $"{InputBindings.MoveLabel()} move  ·  RMB orbit  ·  {L(GameAction.Interact)} interact  ·  " +
            $"{L(GameAction.Ledger)} Shop book  ·  {L(GameAction.BuildMode)} build view  ·  {RotateKeys()} rotate  ·  {QuestJournalPanel.OpenKeyLabel} journal";
    }
}
