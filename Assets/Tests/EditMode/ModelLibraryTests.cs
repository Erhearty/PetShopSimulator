using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="ModelLibrary"/>'s handling of a null or empty resource path —
    /// what <see cref="ModelLibrary.FirstAvailable"/> returns when no candidate model is installed —
    /// and for the pack models it records in <see cref="ModelLibrary.PackFallbacks"/>.
    /// </summary>
    public class ModelLibraryTests
    {
        /// <summary>A Resources path no model is ever installed at.</summary>
        private const string AbsentPath = "ModelLibraryTests/NoSuchModel";
        /// <summary>A second absent path, standing in for another spelling of the same model.</summary>
        private const string AbsentAlias = "ModelLibraryTests/NoSuchModel_001";
        /// <summary>An absent path under the Asset Store packs prefix.</summary>
        private const string AbsentPackPath = "Packs/__test__/X";
        /// <summary>A second absent pack path, another spelling of <see cref="AbsentPackPath"/>.</summary>
        private const string AbsentPackAlias = "Packs/__test__/X_001";
        /// <summary>A third absent pack path, a different pack model.</summary>
        private const string OtherAbsentPackPath = "Packs/__test__/Z";
        /// <summary>An absent path under a CC0 kit, standing in for a fallback.</summary>
        private const string AbsentKitPath = "Kenney/__test__/Y";
        /// <summary>What <see cref="ModelLibrary.PackFallbacks"/> records when nothing was chosen.</summary>
        private const string NoneChosen = "(none)";
        private const string ReportPrefix = "[Models] ";

        /// <summary>Any target size; nothing is measured for a missing model.</summary>
        private const float TargetSize = 1f;

        private GameObject _parent;
        private bool _savedForceProcedural;

        /// <summary>Starts from an empty cache (other fixtures may have filled it) and creates a scratch parent.</summary>
        [SetUp]
        public void SetUp()
        {
            _savedForceProcedural = ModelLibrary.ForceProcedural;
            ModelLibrary.ForceProcedural = false;
            ModelLibrary.ResetCache();
            _parent = new GameObject("ModelLibraryTestsParent");
        }

        /// <summary>Destroys the scratch parent, restores ForceProcedural and forgets anything cached.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            ModelLibrary.ForceProcedural = _savedForceProcedural;
            ModelLibrary.ResetCache();
        }

        /// <summary>A null or empty path is simply not installed.</summary>
        [TestCase(null)]
        [TestCase("")]
        public void Prefab_NullOrEmptyPath_ReturnsNull(string path)
        {
            Assert.IsNull(ModelLibrary.Prefab(path));
        }

        /// <summary>A null or empty path is reported as absent.</summary>
        [TestCase(null)]
        [TestCase("")]
        public void Has_NullOrEmptyPath_ReturnsFalse(string path)
        {
            Assert.IsFalse(ModelLibrary.Has(path));
        }

        /// <summary>Spawning a null or empty path returns null and builds nothing.</summary>
        [TestCase(null)]
        [TestCase("")]
        public void Spawn_NullOrEmptyPath_ReturnsNull(string path)
        {
            Assert.IsNull(ModelLibrary.Spawn(path, _parent.transform, Vector3.zero));
            Assert.IsNull(ModelLibrary.SpawnRaw(path, _parent.transform, Vector3.zero));
            Assert.AreEqual(0, _parent.transform.childCount);
        }

        /// <summary>Probing absent spellings (as PetPen.AnimalModelPaths does) records no miss.</summary>
        [Test]
        public void Probes_OfAbsentModels_DoNotRecordMisses()
        {
            Assert.IsNull(ModelLibrary.TryPrefab(AbsentPath));
            Assert.IsFalse(ModelLibrary.Has(AbsentPath));
            Assert.IsNull(ModelLibrary.FirstAvailable(AbsentPath, AbsentAlias));
            Assert.IsNull(ModelLibrary.AnyAvailable(new System.Random(0), AbsentPath, AbsentAlias));
            CollectionAssert.IsEmpty(ModelLibrary.MissingPaths);
        }

        /// <summary>Actually spawning an absent model — even one already probed and cached — records it.</summary>
        [Test]
        public void Spawn_OfAbsentModel_RecordsMiss()
        {
            ModelLibrary.Has(AbsentPath);
            LogAssert.Expect(LogType.Warning, $"[Kenney] model not found: Resources/{AbsentPath}");
            Assert.IsNull(ModelLibrary.Spawn(AbsentPath, _parent.transform, Vector3.zero));
            CollectionAssert.AreEquivalent(new[] { AbsentPath }, ModelLibrary.MissingPaths);
        }

        /// <summary>An absent pack model whose fallback is also absent is recorded as falling back to nothing.</summary>
        [Test]
        public void FirstAvailable_AbsentPackAndFallback_RecordsNone()
        {
            Assert.IsNull(ModelLibrary.FirstAvailable(AbsentPackPath, AbsentKitPath));
            CollectionAssert.AreEquivalent(
                new[] { new KeyValuePair<string, string>(AbsentPackPath, NoneChosen) },
                ModelLibrary.PackFallbacks);
        }

        /// <summary>AnyAvailable records the same fallback as FirstAvailable.</summary>
        [Test]
        public void AnyAvailable_AbsentPackAndFallback_RecordsNone()
        {
            Assert.IsNull(ModelLibrary.AnyAvailable(new System.Random(0), AbsentPackPath, AbsentKitPath));
            CollectionAssert.AreEquivalent(
                new[] { new KeyValuePair<string, string>(AbsentPackPath, NoneChosen) },
                ModelLibrary.PackFallbacks);
        }

        /// <summary>Several absent spellings of one pack model count as one miss, keyed by the first.</summary>
        [Test]
        public void FirstAvailable_AbsentPackAliases_RecordOneEntryKeyedByFirst()
        {
            ModelLibrary.FirstAvailable(AbsentPackPath, AbsentPackAlias, AbsentKitPath);
            Assert.AreEqual(1, ModelLibrary.PackFallbacks.Count);
            Assert.AreEqual(NoneChosen, ModelLibrary.PackFallbacks[AbsentPackPath]);
        }

        /// <summary>A group with no pack candidate is never a pack fallback.</summary>
        [Test]
        public void Probes_WithoutPackCandidates_RecordNoFallback()
        {
            ModelLibrary.FirstAvailable(AbsentPath, AbsentKitPath);
            ModelLibrary.AnyAvailable(new System.Random(0), AbsentPath, AbsentKitPath);
            CollectionAssert.IsEmpty(ModelLibrary.PackFallbacks);
        }

        /// <summary>With ForceProcedural the packs are hidden on purpose, so nothing is recorded.</summary>
        [Test]
        public void Probes_WhenForceProcedural_RecordNoFallback()
        {
            ModelLibrary.ForceProcedural = true;
            ModelLibrary.FirstAvailable(AbsentPackPath, AbsentKitPath);
            ModelLibrary.AnyAvailable(new System.Random(0), AbsentPackPath, AbsentKitPath);
            CollectionAssert.IsEmpty(ModelLibrary.PackFallbacks);
        }

        /// <summary>ResetCache forgets the recorded fallbacks.</summary>
        [Test]
        public void ResetCache_ClearsPackFallbacks()
        {
            ModelLibrary.FirstAvailable(AbsentPackPath, AbsentKitPath);
            ModelLibrary.ResetCache();
            CollectionAssert.IsEmpty(ModelLibrary.PackFallbacks);
        }

        /// <summary>No fallbacks, no report.</summary>
        [Test]
        public void PackFallbackReport_WhenEmpty_ReturnsNull()
        {
            Assert.IsNull(ModelLibrary.PackFallbackReport(10));
        }

        /// <summary>The report counts every fallback and names each as 'pack path -> fallback'.</summary>
        [Test]
        public void PackFallbackReport_ListsEntries()
        {
            ModelLibrary.FirstAvailable(AbsentPackPath, AbsentKitPath);
            Assert.AreEqual(
                $"{ReportPrefix}1 pack model(s) missing, using fallbacks: {AbsentPackPath} -> {NoneChosen}",
                ModelLibrary.PackFallbackReport(10));
        }

        /// <summary>Past the cap the report names only the first entries and counts the rest.</summary>
        [Test]
        public void PackFallbackReport_OverCap_AppendsMoreCount()
        {
            ModelLibrary.FirstAvailable(AbsentPackPath);
            ModelLibrary.FirstAvailable(OtherAbsentPackPath);
            string report = ModelLibrary.PackFallbackReport(1);
            StringAssert.StartsWith($"{ReportPrefix}2 pack model(s) missing, using fallbacks: ", report);
            StringAssert.EndsWith(" (+1 more)", report);
            Assert.AreEqual(1, report.Split(new[] { " -> " }, System.StringSplitOptions.None).Length - 1);
        }

        /// <summary>Measuring a null or empty path does not throw.</summary>
        [TestCase(null)]
        [TestCase("")]
        public void LocalBounds_NullOrEmptyPath_DoesNotThrow(string path)
        {
            Assert.DoesNotThrow(() => ModelLibrary.LocalBounds(path));
            Assert.DoesNotThrow(() => ModelLibrary.ScaleFor(path, ModelLibrary.Fit.Width, TargetSize));
        }
    }
}
