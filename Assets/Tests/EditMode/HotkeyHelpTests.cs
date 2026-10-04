using NUnit.Framework;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="HotkeyHelp"/>: the cheat-sheets name the ledger &amp; build key,
    /// the build view key and the R / Shift+R / wheel rotate keys, using the current bindings.
    /// </summary>
    public class HotkeyHelpTests
    {
        private static string BuildKey  => InputBindings.Label(GameAction.BuildMode);
        private static string LedgerKey => InputBindings.Label(GameAction.Ledger);
        private static string RotateKey => InputBindings.Label(GameAction.BuildRotate);

        [Test]
        public void Controls_MentionsLedgerBuildViewAndRotateKeys()
        {
            string text = HotkeyHelp.Controls();

            StringAssert.Contains($"{LedgerKey}  ledger & build", text);
            StringAssert.Contains($"{BuildKey}  build view", text);
            StringAssert.DoesNotContain("furniture catalogue", text);
            StringAssert.Contains($"{RotateKey} / Shift+{RotateKey} / wheel", text);
        }

        [Test]
        public void TitleHint_MentionsLedgerBuildViewAndRotateKeys()
        {
            string text = HotkeyHelp.TitleHint();

            StringAssert.Contains($"{LedgerKey} ledger & build", text);
            StringAssert.Contains($"{BuildKey} build view", text);
            StringAssert.DoesNotContain("furniture catalogue", text);
            StringAssert.Contains($"{RotateKey} / Shift+{RotateKey} / wheel", text);
        }
    }
}
