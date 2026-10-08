using NUnit.Framework;
using PetShop.Dev;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the pure <see cref="DebugConsole.Execute"/> parser.</summary>
    public class DebugConsoleTests
    {
        [Test]
        public void Execute_GreedIsGoodWithAmount_AddsMoney()
        {
            var command = DebugConsole.Execute("greedisgood 500");
            Assert.AreEqual(ConsoleOutcome.AddMoney, command.Outcome);
            Assert.AreEqual(500, command.Amount);
        }

        [Test]
        public void Execute_IgnoresCaseAndExtraSpaces()
        {
            var command = DebugConsole.Execute("  GreedIsGood   25  ");
            Assert.AreEqual(ConsoleOutcome.AddMoney, command.Outcome);
            Assert.AreEqual(25, command.Amount);
        }

        [TestCase("greedisgood")]
        [TestCase("greedisgood abc")]
        [TestCase("greedisgood 0")]
        [TestCase("greedisgood -5")]
        [TestCase("greedisgood 1.5")]
        [TestCase("greedisgood 99999999999")]
        [TestCase("greedisgood 5 6")]
        public void Execute_BadAmount_IsInvalid(string line) =>
            Assert.AreEqual(ConsoleOutcome.Invalid, DebugConsole.Execute(line).Outcome);

        [Test]
        public void Execute_UnknownWord_IsUnknownAndKeepsWord()
        {
            var command = DebugConsole.Execute("rosebud 5");
            Assert.AreEqual(ConsoleOutcome.Unknown, command.Outcome);
            Assert.AreEqual("rosebud", command.Word);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Execute_Blank_IsEmpty(string line) =>
            Assert.AreEqual(ConsoleOutcome.Empty, DebugConsole.Execute(line).Outcome);
    }
}
