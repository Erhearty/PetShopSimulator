using NUnit.Framework;
using PetShop.Localization;

/// <summary>
/// Runs once before every PlayMode test (no namespace, so it covers the whole assembly): forces English for
/// this session without persisting it, so a player's saved language never breaks the English asserts.
/// </summary>
[SetUpFixture]
public class LanguageSetUpFixture
{
    [OneTimeSetUp]
    public void ForceEnglish() => Loc.SetLanguageWithoutSaving(Language.En);
}
