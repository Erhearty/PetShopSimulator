using NUnit.Framework;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="HotkeyHelp"/>: the cheat-sheets name the catalogue (build) key
    /// and the R / Shift+R / wheel rotate keys, using the current bindings.
    /// </summary>
    public class HotkeyHelpTests
    {
        private static string BuildKey  => InputBindings.Label(GameAction.BuildMode);
        private static string RotateKey => InputBindings.Label(GameAction.BuildRotate);

        [Test]
        public void Controls_MentionsCatalogueKeyAndRotateKeys()
        {
            string text = HotkeyHelp.Controls();

            StringAssert.Contains($"{BuildKey}  furniture catalogue", text);
            StringAssert.Contains($"{RotateKey} / Shift+{RotateKey} / wheel", text);
        }

        [Test]
        public void TitleHint_MentionsCatalogueKeyAndRotateKeys()
        {
            string text = HotkeyHelp.TitleHint();

            StringAssert.Contains($"{BuildKey} furniture catalogue", text);
            StringAssert.Contains($"{RotateKey} / Shift+{RotateKey} / wheel", text);
        }
    }
}
