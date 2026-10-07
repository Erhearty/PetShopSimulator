using NUnit.Framework;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>
    /// Checks the arrival lead time: customers are let in ahead of their planned queue join by
    /// the spawn-to-queue time, converted to game hours using the day length.
    /// </summary>
    public class CustomerCadenceTests
    {
        private const float DefaultDayLength = 540f;
        private const float HalfDayLength    = 270f;
        private const float Tolerance        = 0.0001f;

        /// <summary>A 540 s day runs one game minute per second: 29 s is 29 game minutes.</summary>
        [Test]
        public void LeadGameHours_DefaultDay_MatchesTheWalkTime() =>
            Assert.AreEqual(CustomerSpawner.SpawnToQueueSeconds / 60f,
                            ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, DefaultDayLength),
                            Tolerance);

        /// <summary>Halving the day doubles the lead in game hours: the walk takes as long as before.</summary>
        [Test]
        public void LeadGameHours_HalfDay_Doubles() =>
            Assert.AreEqual(2f * ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, DefaultDayLength),
                            ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, HalfDayLength),
                            Tolerance);

        /// <summary>A zero-length day has no clock to lead.</summary>
        [Test]
        public void LeadGameHours_NoDay_IsZero() =>
            Assert.AreEqual(0f, ArrivalSchedule.LeadGameHours(CustomerSpawner.SpawnToQueueSeconds, 0f), Tolerance);
    }
}
