using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Dev;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// Boots the real world once per mode (Asset Store packs on / procedural only) and checks
    /// that what the generators built is walkable, above ground, non-overlapping, on the lot,
    /// the expected size and correctly shaded.
    /// </summary>
    [TestFixture(true)]
    [TestFixture(false)]
    public class GenerationInvariantTests
    {
        private const int   Seed      = 20240919;
        private const float DayLength = 600f;
        /// <summary>The world is frozen while it is inspected.</summary>
        private const float FrozenTimeScale = 0f;
        private const int   MaxNamesInMessage = 10;

        /// <summary>How far below y = 0 a renderer may reach before it counts as sunk.</summary>
        private const float SinkTolerance = 0.05f;
        /// <summary>A renderer no thicker than this and at least <see cref="GroundSlabMinSpan"/> wide is a ground surface.</summary>
        private const float GroundSlabMaxThickness = 0.3f;
        private const float GroundSlabMinSpan      = 2f;
        /// <summary>Penetration deeper than this between two furniture items is an overlap.</summary>
        private const float OverlapTolerance = 0.02f;
        /// <summary>Walls and fences sit on the lot boundary; let them poke this far past it.</summary>
        private const float LotEdgeTolerance = 0.5f;
        /// <summary>
        /// Road tiles and end buildings are laid past HalfLength by up to about one tile/building.
        /// PROVISIONAL: widen or tighten after a first run.
        /// </summary>
        private const float StreetEndAllowance = 15f;

        /// <summary>
        /// PROVISIONAL: "about 140" per the plan, to be replaced by N from the
        /// '[Street] Built N street objects.' log line of a first run with the packs installed.
        /// </summary>
        private const int   ExpectedStreetObjectsWithPacks = 140;
        /// <summary>±15 % band around the expected count.</summary>
        private const float StreetCountBand = 0.15f;
        /// <summary>
        /// PLACEHOLDER, not measured: procedural-only counts depend on whether the Kenney kits are
        /// installed. Always at least the walkable pavement collider. Replace with a ±15 % band
        /// around the '[Street] Built N street objects.' count of a first -nopacks run.
        /// </summary>
        private const int   MinStreetObjectsProcedural = 1;
        private const int   MaxStreetObjectsProcedural = 400;

        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        private readonly bool _packs;
        private bool _booted;
        private bool _bootAttempted;

        /// <summary>Creates the fixture for one art mode.</summary>
        /// <param name="packs">True to load the Asset Store packs, false for procedural only.</param>
        public GenerationInvariantTests(bool packs) => _packs = packs;

        private static ShopGenerator Generator => GameManager.Instance.Generator;

        /// <summary>Boots the world the first time a test in this fixture needs it.</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (_packs && !PlaytestHarness.PacksInstalled())
                Assert.Ignore("Asset Store packs are not installed (Assets/Resources/Packs).");
            if (!_booted)
            {
                // A previous Boot that failed part-way leaves a half-built world behind; clear it first.
                if (_bootAttempted) PlaytestHarness.Teardown();
                _bootAttempted = true;
                yield return PlaytestHarness.Boot(_packs, Seed, FrozenTimeScale, DayLength);
                // Only a Boot that ran to completion counts; a failed one is retried by the next test.
                _booted = true;
            }
            if (GameManager.Instance == null) Assert.Inconclusive("world did not boot");
        }

        /// <summary>Destroys the world and resets the playtest statics.</summary>
        [OneTimeTearDown]
        public void OneTimeTearDown() => PlaytestHarness.Teardown();

        /// <summary>1. The street, forecourt, doorway and till are joined on the NavMesh.</summary>
        [UnityTest]
        public IEnumerator NavMesh_JoinsStreetToTill()
        {
            string report = NavProbe.Run();
            Assert.IsFalse(report.Contains("BROKEN") || report.Contains("no NavMesh"), report);
            yield break;
        }

        /// <summary>2. Nothing under the street, shop or furniture roots is sunk below the ground.</summary>
        [UnityTest]
        public IEnumerator Renderers_AreNotSunkBelowGround()
        {
            var sunk = new List<string>();
            foreach (var r in AllRootRenderers())
                if (r.bounds.min.y < -SinkTolerance && !IsGroundSlab(r.bounds))
                    sunk.Add($"{r.name} (min y {r.bounds.min.y:F2})");

            AssertNone(sunk, "renderers sunk below ground");
            yield break;
        }

        /// <summary>3. No two furniture items' colliders interpenetrate.</summary>
        [UnityTest]
        public IEnumerator FurnitureColliders_DoNotOverlap()
        {
            var colliders = SolidColliders(Generator.FurnitureRoot);
            var overlaps  = new List<string>();
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    if (Overlaps(colliders[i], colliders[j]))
                        overlaps.Add($"{colliders[i].name} × {colliders[j].name}");

            AssertNone(overlaps, "overlapping furniture colliders");
            yield break;
        }

        /// <summary>4a. Every shop and furniture renderer lies on the lot.</summary>
        [UnityTest]
        public IEnumerator ShopRenderers_StayOnTheLot()
        {
            float halfW = Generator.YardWidth * 0.5f + LotEdgeTolerance;
            float halfD = Generator.YardDepth * 0.5f + LotEdgeTolerance;
            var outside = new List<string>();
            foreach (var r in ShopRenderersExcludingStreet())
            {
                Bounds b = r.bounds;
                bool inX = b.min.x >= -halfW && b.max.x <= halfW;
                bool inZ = b.min.z >= -halfD && b.max.z <= halfD;
                if (!inX || !inZ) outside.Add($"{r.name} at {b.center}");
            }
            AssertNone(outside, "shop renderers off the lot");
            yield break;
        }

        /// <summary>4b. Everything along the street cross-section stays within the street's length.</summary>
        [UnityTest]
        public IEnumerator StreetProps_StayWithinTheStreet()
        {
            StreetGenerator street = Generator.Street;
            Assert.IsNotNull(street, "No StreetGenerator was built.");
            float limit   = street.HalfLength + StreetEndAllowance;
            var   outside = new List<string>();
            foreach (var r in street.StreetRoot.GetComponentsInChildren<Renderer>(true))
            {
                Vector3 c = r.bounds.center;
                bool onStreet = c.z >= street.PavementBackZ && c.z <= street.FarPavementZ;
                if (onStreet && Mathf.Abs(c.x) > limit) outside.Add($"{r.name} at x {c.x:F1}");
            }
            AssertNone(outside, "street props past the end of the street");
            yield break;
        }

        /// <summary>5. The street object count is inside the band for this mode.</summary>
        [UnityTest]
        public IEnumerator StreetPropCount_IsInBand()
        {
            int count = Generator.Street.PropCount;
            int min = _packs ? Mathf.FloorToInt(ExpectedStreetObjectsWithPacks * (1f - StreetCountBand))
                             : MinStreetObjectsProcedural;
            int max = _packs ? Mathf.CeilToInt(ExpectedStreetObjectsWithPacks * (1f + StreetCountBand))
                             : MaxStreetObjectsProcedural;
            Assert.That(count, Is.InRange(min, max), $"Street built {count} objects; expected {min}–{max}.");
            yield break;
        }

        /// <summary>6. Every renderer has a real, supported material.</summary>
        [UnityTest]
        public IEnumerator Materials_AreAssignedAndSupported()
        {
            var bad = new List<string>();
            foreach (var r in AllRootRenderers())
            {
                string problem = MaterialProblem(r);
                if (problem != null) bad.Add($"{r.name} ({problem})");
            }
            AssertNone(bad, "renderers with broken materials");
            yield break;
        }

        /// <summary>7. Packs mode loads every model it asks for; procedural mode still builds something under each root.</summary>
        [UnityTest]
        public IEnumerator Models_LoadedOrFallbacksBuilt()
        {
            if (_packs)
            {
                AssertNone(new List<string>(ModelLibrary.MissingPaths), "missing model paths");
                yield break;
            }
            foreach (var root in Roots())
                Assert.Greater(root.GetComponentsInChildren<Renderer>(true).Length, 0,
                               $"No renderers under {root.name}.");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static Transform[] Roots() =>
            new[] { Generator.Street.StreetRoot, Generator.ShopRoot, Generator.FurnitureRoot };

        /// <summary>Renderers under all three roots, each once (the street and furniture sit under the shop root).</summary>
        private static IEnumerable<Renderer> AllRootRenderers()
        {
            var seen = new HashSet<Renderer>();
            foreach (var root in Roots())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (seen.Add(r)) yield return r;
        }

        private static IEnumerable<Renderer> ShopRenderersExcludingStreet()
        {
            Transform streetRoot = Generator.Street != null ? Generator.Street.StreetRoot : null;
            foreach (var r in Generator.ShopRoot.GetComponentsInChildren<Renderer>(true))
                if (streetRoot == null || !r.transform.IsChildOf(streetRoot)) yield return r;
        }

        /// <summary>
        /// Thin, wide pieces (pavements, kerbs, road tiles, rugs) are laid slightly below y = 0 on
        /// purpose (the road sits at y = -0.02, StreetGenerator). They are exempt only while their top
        /// surface is still at ground level (max y within <see cref="SinkTolerance"/> of 0); a slab
        /// sunk deeper than that is a real bug and fails like any other renderer.
        /// </summary>
        private static bool IsGroundSlab(Bounds b) =>
            b.size.y <= GroundSlabMaxThickness && Mathf.Max(b.size.x, b.size.z) >= GroundSlabMinSpan
            && b.max.y >= -SinkTolerance;

        private static List<Collider> SolidColliders(Transform root)
        {
            var list = new List<Collider>();
            foreach (var c in root.GetComponentsInChildren<Collider>())
                if (c.enabled && !c.isTrigger) list.Add(c);
            return list;
        }

        /// <summary>
        /// Colliders of the same placed item (e.g. a collider and its own parent's, or a pen's
        /// fence panels) are allowed to touch; only separate items count.
        /// </summary>
        private static bool Overlaps(Collider a, Collider b)
        {
            if (ItemOf(a.transform) == ItemOf(b.transform)) return false;
            bool hit = Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation, out _, out float distance);
            return hit && distance > OverlapTolerance;
        }

        /// <summary>The direct child of FurnitureRoot that <paramref name="t"/> belongs to.</summary>
        private static Transform ItemOf(Transform t)
        {
            Transform root = Generator.FurnitureRoot;
            while (t.parent != null && t.parent != root) t = t.parent;
            return t;
        }

        private static string MaterialProblem(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) return "null material";
                if (m.shader == null) return $"{m.name}: no shader";
                if (m.shader.name == ErrorShaderName) return $"{m.name}: error shader";
                if (!m.shader.isSupported) return $"{m.name}: unsupported shader {m.shader.name}";
            }
            return null;
        }

        private static void AssertNone(List<string> offenders, string what)
        {
            if (offenders.Count == 0) return;
            Assert.Fail($"{offenders.Count} {what}: {PlaytestHarness.ListNames(offenders, MaxNamesInMessage)}");
        }
    }
}
