using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for SaveMigrator: current saves pass through untouched, older saves are
    /// upgraded with sane defaults, and newer or malformed saves are rejected.
    /// </summary>
    public class SaveMigratorTests
    {
        private const float Tolerance = 1e-5f;

        /// <summary>A current-version save with every migrated field off its default.</summary>
        private const string V2Json =
            "{\"Version\":2,\"Balance\":1234.5,\"Reputation\":67.25,\"Day\":9,\"Staff\":3," +
            "\"PriceMultiplier\":1.25,\"SavedAt\":\"2024-01-01T00:00:00Z\"," +
            "\"Stock\":[{\"id\":\"kibble\",\"qty\":12}],\"Warehouse\":[{\"id\":\"toys\",\"qty\":4}]," +
            "\"PlacedObjects\":[{\"catalogId\":\"pen_small\",\"cellX\":4,\"cellY\":7,\"variant\":\"\",\"rotation\":90.0," +
            "\"shelfStock\":[],\"pets\":[{\"species\":\"Cat\",\"petName\":\"Pip\",\"ageDays\":2}]}]}";

        /// <summary>A version 1 save with invalid staff/multiplier and a placed item lacking lists.</summary>
        private const string V1Json =
            "{\"Version\":1,\"Balance\":50.0,\"Day\":3,\"Staff\":0,\"PriceMultiplier\":0.0," +
            "\"PlacedObjects\":[{\"catalogId\":\"shelf_basic\",\"cellX\":1,\"cellY\":2}]}";

        /// <summary>A pre-versioning save: no Version field at all.</summary>
        private const string LegacyJson =
            "{\"Balance\":10.0,\"Day\":1,\"Staff\":0,\"PriceMultiplier\":-2.0," +
            "\"PlacedObjects\":[{\"catalogId\":\"pen_small\",\"cellX\":0,\"cellY\":0}]}";

        private const string MalformedJson = "{ this is not json";

        /// <summary>A version 3 save carrying a named staff list.</summary>
        private const string V3Json =
            "{\"Version\":3,\"Balance\":10.0,\"Day\":2,\"Staff\":1,\"PriceMultiplier\":1.0," +
            "\"StaffList\":[{\"name\":\"Ada\",\"role\":\"Feeder\",\"skill\":4,\"wage\":72.5,\"serviceSeconds\":3.25}]}";

        [Test]
        public void Migrate_V3_KeepsStaffListExactly()
        {
            var data = SaveMigrator.Migrate(V3Json);

            Assert.IsNotNull(data);
            Assert.AreEqual(3, data.Version);
            Assert.AreEqual(1, data.StaffList.Count);
            var s = data.StaffList[0];
            Assert.AreEqual("Ada", s.name);
            Assert.AreEqual("Feeder", s.role);
            Assert.AreEqual(4, s.skill);
            Assert.AreEqual(72.5f, s.wage, Tolerance);
            Assert.AreEqual(3.25f, s.serviceSeconds, Tolerance);
        }

        [Test]
        public void Migrate_V2_LeavesStaffListEmptyAndHeadCountIntact()
        {
            var data = SaveMigrator.Migrate(V2Json);

            Assert.AreEqual(0, data.StaffList.Count);
            Assert.AreEqual(3, data.Staff);
        }

        [Test]
        public void Migrate_CurrentVersion_RoundTripsUnchanged()
        {
            var data = SaveMigrator.Migrate(V2Json);

            Assert.IsNotNull(data);
            // Version 2 is still accepted: it upgrades to current with every field intact.
            Assert.AreEqual(SaveMigrator.CurrentVersion, data.Version);
            Assert.IsNotNull(data.StaffList);
            Assert.AreEqual(1234.5f, data.Balance, Tolerance);
            Assert.AreEqual(67.25f, data.Reputation, Tolerance);
            Assert.AreEqual(9, data.Day);
            Assert.AreEqual(3, data.Staff);
            Assert.AreEqual(1.25f, data.PriceMultiplier, Tolerance);
            Assert.AreEqual("2024-01-01T00:00:00Z", data.SavedAt);
            Assert.AreEqual(1, data.Stock.Count);
            Assert.AreEqual("kibble", data.Stock[0].id);
            Assert.AreEqual(12, data.Stock[0].qty);
            Assert.AreEqual(1, data.Warehouse.Count);
            Assert.AreEqual(4, data.Warehouse[0].qty);
            Assert.AreEqual(1, data.PlacedObjects.Count);
            Assert.AreEqual("pen_small", data.PlacedObjects[0].catalogId);
            Assert.AreEqual(90f, data.PlacedObjects[0].rotation, Tolerance);
            Assert.AreEqual(1, data.PlacedObjects[0].pets.Count);
            Assert.AreEqual("Pip", data.PlacedObjects[0].pets[0].petName);
        }

        [Test]
        public void Migrate_Version1_UpgradesToCurrentWithDefaults()
        {
            var data = SaveMigrator.Migrate(V1Json);

            AssertMigratedWithDefaults(data);
            Assert.AreEqual(50f, data.Balance, Tolerance);
            Assert.AreEqual(3, data.Day);
        }

        [Test]
        public void Migrate_MissingVersion_UpgradesToCurrentWithDefaults()
        {
            var data = SaveMigrator.Migrate(LegacyJson);

            AssertMigratedWithDefaults(data);
            Assert.AreEqual(10f, data.Balance, Tolerance);
        }

        [Test]
        public void Migrate_NewerVersion_ReturnsNullWithWarning()
        {
            string json = $"{{\"Version\":{SaveMigrator.CurrentVersion + 1},\"Balance\":1.0}}";
            LogAssert.Expect(LogType.Warning, new Regex("newer build"));

            Assert.IsNull(SaveMigrator.Migrate(json));
        }

        [Test]
        public void Migrate_MalformedJson_ReturnsNullWithError()
        {
            LogAssert.Expect(LogType.Error, new Regex("malformed"));

            Assert.IsNull(SaveMigrator.Migrate(MalformedJson));
        }

        /// <summary>Checks a migrated save is current, has normalised defaults and no null lists.</summary>
        private static void AssertMigratedWithDefaults(SaveData data)
        {
            Assert.IsNotNull(data);
            Assert.AreEqual(SaveMigrator.CurrentVersion, data.Version);
            Assert.AreEqual(1, data.Staff);
            Assert.AreEqual(1f, data.PriceMultiplier, Tolerance);
            Assert.IsNotNull(data.Stock);
            Assert.IsNotNull(data.Warehouse);
            Assert.IsNotNull(data.PlacedObjects);
            Assert.AreEqual(1, data.PlacedObjects.Count);
            Assert.IsNotNull(data.PlacedObjects[0].shelfStock);
            Assert.IsNotNull(data.PlacedObjects[0].pets);
        }
    }
}
