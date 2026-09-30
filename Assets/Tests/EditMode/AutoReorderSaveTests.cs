using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for persisting auto-reorder rules.</summary>
    public class AutoReorderSaveTests
    {
        private const int CustomThreshold = 4;
        private const int CustomUnits     = 30;

        [Test]
        public void RoundTrip_ThroughJson_RestoresRules()
        {
            var src = new AutoReorder();
            var food = src.Rule(ProductCategory.Food);
            food.Enabled = true; food.Threshold = CustomThreshold; food.Units = CustomUnits;

            var data = new SaveData();
            SaveReorder.Capture(data, src);
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var dst = new AutoReorder();
            SaveReorder.Apply(loaded, dst);
            var back = dst.Rule(ProductCategory.Food);
            Assert.IsTrue(back.Enabled);
            Assert.AreEqual(CustomThreshold, back.Threshold);
            Assert.AreEqual(CustomUnits, back.Units);
            Assert.IsFalse(dst.Rule(ProductCategory.Toy).Enabled);
        }

        [Test]
        public void OldSave_WithoutRules_KeepsDefaults()
        {
            var save = JsonUtility.FromJson<SaveData>("{\"Day\":2}");
            Assert.IsNotNull(save.AutoReorder);
            var ar = new AutoReorder();
            SaveReorder.Apply(save, ar);
            foreach (var r in ar.Rules)
            {
                Assert.AreEqual(AutoReorder.DefaultEnabled, r.Enabled);
                Assert.AreEqual(AutoReorder.DefaultThreshold, r.Threshold);
                Assert.AreEqual(AutoReorder.DefaultUnits, r.Units);
            }
        }

        [Test]
        public void ResetToDefaults_UndoesChanges()
        {
            var ar = new AutoReorder();
            ar.Rule(ProductCategory.Food).Enabled = true;
            ar.Rule(ProductCategory.Food).Units   = CustomUnits;
            SaveReorder.ResetToDefaults(ar);
            Assert.IsFalse(ar.Rule(ProductCategory.Food).Enabled);
            Assert.AreEqual(AutoReorder.DefaultUnits, ar.Rule(ProductCategory.Food).Units);
        }

        [Test]
        public void UnknownCategory_IsIgnored()
        {
            var data = new SaveData();
            data.AutoReorder.Add(new ReorderRuleSave { category = "Nope", enabled = true, threshold = 1, units = 1 });
            var ar = new AutoReorder();
            SaveReorder.Apply(data, ar);
            foreach (var r in ar.Rules) Assert.IsFalse(r.Enabled);
        }
    }
}
