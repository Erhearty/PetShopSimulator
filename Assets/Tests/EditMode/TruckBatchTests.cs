using NUnit.Framework;
using PetShop.Core;

namespace PetShop.Tests
{
    public class TruckBatchTests
    {
        [Test]
        public void Flush_SendsAllLoadsOnOneTruck()
        {
            var batch = new TruckBatch();
            int dropped = 0, trucks = 0;
            batch.Add(() => dropped++);
            batch.Add(() => dropped++);
            batch.Add(() => dropped++);

            int sent = batch.Flush(drop => { trucks++; drop(); return true; });

            Assert.AreEqual(3, sent);
            Assert.AreEqual(1, trucks);
            Assert.AreEqual(3, dropped);
            Assert.AreEqual(0, batch.Count);
        }

        [Test]
        public void Flush_WhenEmpty_DispatchesNothing()
        {
            var batch = new TruckBatch();
            int trucks = 0;
            Assert.AreEqual(0, batch.Flush(_ => { trucks++; return true; }));
            Assert.AreEqual(0, trucks);
        }

        [Test]
        public void Flush_WithoutATruck_DropsEverythingAnyway()
        {
            var batch = new TruckBatch();
            int dropped = 0;
            batch.Add(() => dropped++);
            batch.Add(() => dropped++);
            batch.Flush(_ => false);
            Assert.AreEqual(2, dropped);
        }

        [Test]
        public void DropAllNow_LosesNoLoad()
        {
            var batch = new TruckBatch();
            int dropped = 0;
            batch.Add(() => dropped++);
            batch.DropAllNow();
            Assert.AreEqual(1, dropped);
            Assert.AreEqual(0, batch.Count);
        }
    }
}
