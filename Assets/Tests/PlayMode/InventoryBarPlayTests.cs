using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Shop;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>PlayMode tests for <see cref="InventoryBar"/>: slots follow the furniture supply.</summary>
    public class InventoryBarPlayTests
    {
        private GameObject    _canvas;
        private InventoryBar  _bar;
        private FurnitureSupply _supply;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _supply = new FurnitureSupply();
            _bar    = _canvas.AddComponent<InventoryBar>();
            _bar.Build(_canvas.transform, _supply, null, null);
            yield return null;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_canvas);

        [UnityTest]
        public IEnumerator EmptySupply_ShowsNoSlots()
        {
            Assert.AreEqual(0, _bar.SlotCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OwnedFurniture_PopulatesSlotsWithNameAndCount()
        {
            _supply.AddOwned(BuildCatalog.Counter, 2);
            _supply.AddOwned(BuildCatalog.ShelfSmall);
            yield return null;

            Assert.AreEqual(2, _bar.SlotCount);
            int counterSlot = _bar.SlotIds[0] == BuildCatalog.Counter ? 0 : 1;
            Assert.AreEqual(BuildCatalog.Get(BuildCatalog.Counter).DisplayName, _bar.NameText(counterSlot));
            StringAssert.Contains("2", _bar.CountText(counterSlot));
        }

        [UnityTest]
        public IEnumerator TakingTheLastOne_RemovesTheSlot()
        {
            _supply.AddOwned(BuildCatalog.Counter);
            Assert.AreEqual(1, _bar.SlotCount);

            _supply.TakeOwned(BuildCatalog.Counter);
            yield return null;

            Assert.AreEqual(0, _bar.SlotCount);
        }

        [UnityTest]
        public IEnumerator Activate_WithoutBuildMode_ReturnsFalse()
        {
            _supply.AddOwned(BuildCatalog.Counter);
            yield return null;
            Assert.IsFalse(_bar.Activate(0));
        }
    }
}
