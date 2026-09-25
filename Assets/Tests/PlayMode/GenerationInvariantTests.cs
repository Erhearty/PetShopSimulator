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
        /// <summary>Category of tests that report a known fault without failing the run (see README).</summary>
        private const string KnownIssue = "KnownIssue";

        /// <summary>How far below y = 0 a renderer's top may sit before the whole renderer counts as underground.</summary>
        private const float SinkTolerance = 0.05f;
        /// <summary>
        /// Deepest a renderer's base may reach below y = 0. Observed 2026-09-25: pet-pen fence walls
        /// bottomed out at -0.30 and pen posts at -0.36, GatePier at -1.10, Downpipe at -5.20 and
        /// PaddockPost at -0.55. None was deliberately embedded: MeshBuilder.CreateBox lifts a box so
        /// its base sits on its pivot (MeshBuilder.cs:57), and each caller then overwrote that
        /// position with y = 0, standing the piece centred on the ground. Those callers now place
        /// the piece at its mid-height; this footing allowance stays as a margin, not an exemption.
        /// </summary>
        private const float MaxFootingDepth = 0.5f;
        /// <summary>
        /// The 1400 m backdrop ground plane, laid on purpose below the pavements at y = -0.22 so its
        /// top (-0.17) stays under them (StreetGenerator.cs:128-130). Exempt from the underground check.
        /// </summary>
        private const string BackdropGroundName = "Ground";
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
        /// Models the boundary hedges may be spawned from, best first (YardDresser.cs:167). The hedges
        /// are planted inside the lot along its walls (YardDresser.cs:179-181, 188-190) but their
        /// canopies overhang the lot edge, so they are checked by a KnownIssue test instead.
        /// </summary>
        private static readonly string[] YardHedgeModelNames = { "Bush_03", "Bush_01", "plant_bush" };

        /// <summary>
        /// Observed 2026-09-25 ('[Street] Built 344 street objects.'), packs + Kenney installed.
        /// (With packs installed and Kenney absent the same seed built 294.)
        /// </summary>
        private const int   ExpectedStreetObjectsWithPacks = 344;
        /// <summary>
        /// From the procedural fixture's '[Street] Built 519 street objects.' line in
        /// Logs/build/playmode-tests.log (2026-09-25). Whether the Kenney kits were installed for
        /// that run was not recorded; procedural counts depend on them.
        /// </summary>
        private const int   ExpectedStreetObjectsProcedural = 519;
        /// <summary>±15 % band around the expected count.</summary>
        private const float StreetCountBand = 0.15f;

        private const string ErrorShaderName = "Hidden/InternalErrorShader";
        /// <summary>Prefix of every Asset Store pack model path.</summary>
        private const string PacksPathPrefix = "Packs/";

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

        /// <summary>
        /// 2. Nothing under the street, shop or furniture roots is wholly underground or has its base
        /// deeper than <see cref="MaxFootingDepth"/>.
        /// </summary>
        [UnityTest]
        public IEnumerator Renderers_AreNotSunkBelowGround()
        {
            var sunk = new List<string>();
            foreach (var r in AllRootRenderers())
                if (IsSunk(r))
                    sunk.Add($"{r.name} (min y {r.bounds.min.y:F2}, max y {r.bounds.max.y:F2})");

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

        /// <summary>4a. Every shop and furniture renderer lies on the lot, the boundary hedges aside.</summary>
        [UnityTest]
        public IEnumerator ShopRenderers_StayOnTheLot()
        {
            var outside = new List<string>();
            foreach (var r in ShopRenderersExcludingStreet())
                if (!IsYardHedge(r) && !IsOnLot(r.bounds)) outside.Add($"{r.name} at {r.bounds.center}");
            AssertNone(outside, "shop renderers off the lot");
            yield break;
        }

        /// <summary>4a (known issue). The boundary hedges' canopies overhang the lot edge.</summary>
        [UnityTest]
        [Category(KnownIssue)]
        public IEnumerator YardHedges_StayOnTheLot()
        {
            var outside = new List<string>();
            foreach (var r in ShopRenderersExcludingStreet())
                if (IsYardHedge(r) && !IsOnLot(r.bounds)) outside.Add($"{r.name} at {r.bounds.center}");
            AssertNone(outside, "yard hedge renderers off the lot");
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
            int count    = Generator.Street.PropCount;
            int expected = _packs ? ExpectedStreetObjectsWithPacks : ExpectedStreetObjectsProcedural;
            int min = Mathf.FloorToInt(expected * (1f - StreetCountBand));
            int max = Mathf.CeilToInt(expected * (1f + StreetCountBand));
            Assert.That(count, Is.InRange(min, max), $"Street built {count} objects; expected {min}–{max}.");
            yield break;
        }

        /// <summary>
        /// 6. Every renderer, the Asset Store packs' included, has a real, supported material.
        /// </summary>
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

        /// <summary>
        /// 7. Packs mode loads every Asset Store model it asks for (the CC0 Kenney kits are optional);
        /// procedural mode still builds something under each root.
        /// </summary>
        [UnityTest]
        public IEnumerator Models_LoadedOrFallbacksBuilt()
        {
            if (_packs)
            {
                AssertNone(MissingPackPaths(), "missing pack model paths");
                yield break;
            }
            foreach (var root in Roots())
                Assert.Greater(root.GetComponentsInChildren<Renderer>(true).Length, 0,
                               $"No renderers under {root.name}.");
        }

        /// <summary>
        /// 8 (known issue). In packs mode no model lookup fell back from its Asset Store pack model:
        /// <see cref="ModelLibrary.PackFallbacks"/>, recorded while the world booted, is empty.
        /// </summary>
        [UnityTest]
        [Category(KnownIssue)]
        public IEnumerator PackFallbacks_AreEmpty()
        {
            if (!_packs) Assert.Ignore("Pack fallbacks are only checked with the packs loaded.");
            var fallbacks = new List<string>();
            foreach (var pair in ModelLibrary.PackFallbacks) fallbacks.Add($"{pair.Key} -> {pair.Value}");
            AssertNone(fallbacks, "pack models missing, using fallbacks");
            yield break;
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
        /// Wholly underground (top below -<see cref="SinkTolerance"/>, the backdrop ground aside) or
        /// with its base deeper than <see cref="MaxFootingDepth"/>.
        /// </summary>
        private static bool IsSunk(Renderer r)
        {
            Bounds b = r.bounds;
            bool underground = b.max.y < -SinkTolerance && r.name != BackdropGroundName;
            return underground || b.min.y < -MaxFootingDepth;
        }

        private static bool IsOnLot(Bounds b)
        {
            float halfW = Generator.YardWidth * 0.5f + LotEdgeTolerance;
            float halfD = Generator.YardDepth * 0.5f + LotEdgeTolerance;
            bool inX = b.min.x >= -halfW && b.max.x <= halfW;
            bool inZ = b.min.z >= -halfD && b.max.z <= halfD;
            return inX && inZ;
        }

        /// <summary>True when the renderer belongs to a model spawned from one of <see cref="YardHedgeModelNames"/>.</summary>
        private static bool IsYardHedge(Renderer r)
        {
            Transform shopRoot = Generator.ShopRoot;
            for (Transform t = r.transform; t != null && t != shopRoot; t = t.parent)
                if (System.Array.IndexOf(YardHedgeModelNames, t.name) >= 0) return true;
            return false;
        }

        private static List<string> MissingPackPaths()
        {
            var missing = new List<string>();
            foreach (string path in ModelLibrary.MissingPaths)
                if (path.StartsWith(PacksPathPrefix, System.StringComparison.Ordinal)) missing.Add(path);
            return missing;
        }

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
