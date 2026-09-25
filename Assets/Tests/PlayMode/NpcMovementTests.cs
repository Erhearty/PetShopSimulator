using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Customer;

namespace PetShop.Tests
{
    /// <summary>
    /// Plays one short trading day at raised timescale while <see cref="NpcMonitor"/> samples
    /// every customer, then asserts on what it saw. The day is run once per fixture (mode) and
    /// shared by every test in it, which keeps the whole file well under three minutes.
    /// </summary>
    [TestFixture(false)]
    [TestFixture(true)]
    public class NpcMovementTests
    {
        private const int   Seed      = 1234;
        private const float TimeScale = 5f;
        private const float DayLength = 40f;
        private const int   MaxNamesInMessage = 10;

        private readonly bool _packs;
        private NpcMonitor _monitor;
        private bool _booted;
        private bool _bootAttempted;

        /// <summary>Creates the fixture for one art mode.</summary>
        /// <param name="packs">True to load the Asset Store packs, false for procedural only.</param>
        public NpcMovementTests(bool packs) => _packs = packs;

        /// <summary>Boots the world and plays the monitored day the first time a test needs it.</summary>
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
                yield return PlaytestHarness.Boot(_packs, Seed, TimeScale, DayLength);
                // Only a Boot that ran to completion counts; a failed one is retried by the next test.
                _booted  = true;
                _monitor = new NpcMonitor();
                _monitor.Subscribe();
                yield return _monitor.Run();
            }
            if (GameManager.Instance == null) Assert.Inconclusive("world did not boot");
        }

        /// <summary>Unhooks the static CustomerAI events before the world is destroyed.</summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _monitor?.Unsubscribe();
            PlaytestHarness.Teardown();
        }

        /// <summary>(a) Every customer leaves, checks out or walks out within a minute of game time.</summary>
        [UnityTest]
        public IEnumerator EveryCustomer_IsResolvedWithinAMinute()
        {
            Assert.Greater(_monitor.Spawned, 0, "No customers spawned during the day.");
            AssertNone(_monitor.Unresolved(), "customers not resolved within the limit");
            yield break;
        }

        /// <summary>(b) Nobody who still has somewhere to walk stands still for long.</summary>
        [UnityTest]
        public IEnumerator NoCustomer_GetsStuck() => Check(_monitor.Stuck, "stuck customers");

        /// <summary>(c) Every customer stays on the NavMesh.</summary>
        [UnityTest]
        public IEnumerator NoCustomer_LeavesTheNavMesh() => Check(_monitor.OffMesh, "customers off the NavMesh");

        /// <summary>(d) Nobody glides along while the animator plays idle. Needs the character pack's animator.</summary>
        [UnityTest]
        public IEnumerator NoCustomer_SkatesWhileIdle() => Check(_monitor.Skating, "customers skating while idle");

        /// <summary>
        /// (e) Every checkout happens at the till, so sales equal customers who physically got there.
        /// </summary>
        // KnownIssue: see the header of Assets/Scripts/Dev/NavProbe.cs. CustomerAI.NavigateTo gives
        // up after a timeout but the shopper stays in the checkout queue regardless, so an assistant
        // still rings up sales for people stranded on the pavement. Expected to fail on HEAD; kept
        // out of the gated run (-testCategory "!KnownIssue") until the gameplay bug is fixed.
        [UnityTest]
        [Category("KnownIssue")]
        public IEnumerator EveryCheckout_HappensAtTheTill()
        {
            var far = _monitor.CheckoutsAwayFromTill();
            Assert.IsEmpty(far,
                $"{far.Count} of {_monitor.Checkouts} checkouts happened more than " +
                $"{NpcMonitor.TillReach} m from the till: {PlaytestHarness.ListNames(far, MaxNamesInMessage)}");
            yield break;
        }

        /// <summary>An empty offender list proves nothing unless the monitor actually sampled a customer.</summary>
        private IEnumerator Check(List<string> offenders, string what)
        {
            if (_monitor.CustomerSamples == 0) Assert.Inconclusive("The monitor recorded no customer samples.");
            AssertNone(offenders, what);
            yield break;
        }

        private static void AssertNone(List<string> offenders, string what)
        {
            if (offenders.Count == 0) return;
            Assert.Fail($"{offenders.Count} {what}: {PlaytestHarness.ListNames(offenders, MaxNamesInMessage)}");
        }
    }

    /// <summary>
    /// Samples every <see cref="CustomerAI"/> every <see cref="SampleInterval"/> seconds of game
    /// time through one trading day, and records resolution, stuck, off-mesh and skating cases.
    /// </summary>
    public sealed class NpcMonitor
    {
        /// <summary>Game seconds between samples.</summary>
        public const float SampleInterval = 0.25f;
        /// <summary>Game seconds a customer has to check out, walk out or be gone.</summary>
        public const float ResolveWithin = 60f;
        /// <summary>Checkouts further than this (metres, XZ) from the till count as stranded.</summary>
        public const float TillReach = 3f;

        private const float StuckWindow    = 6f;
        private const float StuckMinMove   = 0.1f;
        private const float StuckSlack     = 0.2f;
        private const float OffMeshRadius  = 0.5f;
        private const float SpawnGrace     = 0.5f;
        private const float SkateSpeed     = 0.3f;
        private const float SkateDuration  = 0.5f;
        private const int   IdleMove       = 0;
        /// <summary>Hard wall-clock cap on one monitored day, so a hang cannot stall the suite.</summary>
        private const float MaxRunRealSeconds = 45f;

        private sealed class Track
        {
            public string  Name;
            public float   SpawnTime;
            public float   ResolvedTime = -1f;
            public Vector3 LastPos;
            public float   LastTime;
            public Vector3 StuckAnchor;
            public float   StuckSince = -1f;
            public float   SkateSince = -1f;
            public bool    Stuck, OffMesh, Skated;
        }

        private readonly Dictionary<CustomerAI, Track> _tracks = new();
        private readonly List<(string name, float distance)> _checkouts = new();
        private bool _dayEnded;

        /// <summary>"name at position (state)" for each customer stuck for <see cref="StuckWindow"/> s.</summary>
        public List<string> Stuck { get; } = new();
        /// <summary>Customers found more than <see cref="OffMeshRadius"/> m from the NavMesh.</summary>
        public List<string> OffMesh { get; } = new();
        /// <summary>Customers moving while their animator was idle for more than <see cref="SkateDuration"/> s.</summary>
        public List<string> Skating { get; } = new();
        /// <summary>Customers seen during the monitored day.</summary>
        public int Spawned => _tracks.Count;
        /// <summary>Checkouts seen from tracked customers.</summary>
        public int Checkouts => _checkouts.Count;
        /// <summary>Per-customer samples taken (one per customer per <see cref="SampleInterval"/>).</summary>
        public int CustomerSamples { get; private set; }

        /// <summary>Hooks the static CustomerAI outcome events.</summary>
        public void Subscribe()
        {
            CustomerAI.CheckedOut     += OnCheckedOut;
            CustomerAI.WalkedOutEmpty += OnResolved;
            CustomerAI.GaveUp         += OnResolved;
            CustomerAI.Despawned      += OnResolved;
        }

        /// <summary>Unhooks everything <see cref="Subscribe"/> hooked. Safe to call twice.</summary>
        public void Unsubscribe()
        {
            CustomerAI.CheckedOut     -= OnCheckedOut;
            CustomerAI.WalkedOutEmpty -= OnResolved;
            CustomerAI.GaveUp         -= OnResolved;
            CustomerAI.Despawned      -= OnResolved;
        }

        /// <summary>Samples until the day has ended and every customer is resolved or overdue.</summary>
        public IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + MaxRunRealSeconds;
            while (Time.realtimeSinceStartup < deadline && !Finished())
            {
                Sample();
                yield return new WaitForSeconds(SampleInterval);
            }
        }

        /// <summary>Customers never resolved, or resolved later than <see cref="ResolveWithin"/>.</summary>
        public List<string> Unresolved()
        {
            var list = new List<string>();
            foreach (var t in _tracks.Values)
            {
                if (t.ResolvedTime < 0f) list.Add($"{t.Name} (never resolved, last at {t.LastPos})");
                else if (t.ResolvedTime - t.SpawnTime > ResolveWithin)
                    list.Add($"{t.Name} (took {t.ResolvedTime - t.SpawnTime:F0} s)");
            }
            return list;
        }

        /// <summary>"name (distance m)" for each checkout further than <see cref="TillReach"/> from the till.</summary>
        public List<string> CheckoutsAwayFromTill()
        {
            var list = new List<string>();
            foreach (var (name, distance) in _checkouts)
                if (distance > TillReach) list.Add($"{name} ({distance:F1} m)");
            return list;
        }

        private bool Finished()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsDayRunning) _dayEnded = true;
            if (!_dayEnded) return false;

            foreach (var t in _tracks.Values)
                if (t.ResolvedTime < 0f && Time.time - t.SpawnTime < ResolveWithin) return false;
            return true;
        }

        private void Sample()
        {
            float now = Time.time;
            foreach (var ai in Object.FindObjectsByType<CustomerAI>(FindObjectsSortMode.None))
            {
                if (!_tracks.TryGetValue(ai, out var t))
                {
                    // Customers of a following (auto-continued) day are out of scope.
                    if (_dayEnded) continue;
                    t = Register(ai, now);
                }
                SampleOne(ai, t, now);
            }
        }

        private Track Register(CustomerAI ai, float now)
        {
            var t = new Track { Name = ai.name, SpawnTime = now, LastPos = ai.transform.position, LastTime = now };
            _tracks[ai] = t;
            return t;
        }

        private void SampleOne(CustomerAI ai, Track t, float now)
        {
            CustomerSamples++;
            Vector3 pos = ai.transform.position;
            CheckStuck(ai, t, pos, now);
            CheckOffMesh(ai, t, pos, now);
            CheckSkating(ai, t, pos, now);
            t.LastPos  = pos;
            t.LastTime = now;
        }

        private void CheckStuck(CustomerAI ai, Track t, Vector3 pos, float now)
        {
            if (!WantsToWalk(ai)) { t.StuckSince = -1f; return; }
            if (t.StuckSince < 0f || Vector3.Distance(pos, t.StuckAnchor) >= StuckMinMove)
            {
                t.StuckAnchor = pos;
                t.StuckSince  = now;
                return;
            }
            if (t.Stuck || now - t.StuckSince < StuckWindow) return;

            t.Stuck = true;
            Stuck.Add($"{t.Name} at {pos} ({ai.State})");
        }

        /// <summary>Walking state with a path still well beyond the agent's stopping distance.</summary>
        private static bool WantsToWalk(CustomerAI ai)
        {
            bool walkingState = ai.State == CustomerState.Entering || ai.State == CustomerState.Browsing
                             || ai.State == CustomerState.Leaving;
            var agent = ai.GetComponent<NavMeshAgent>();
            if (!walkingState || agent == null || !agent.isOnNavMesh || agent.pathPending) return false;
            return agent.remainingDistance > agent.stoppingDistance + StuckSlack;
        }

        private void CheckOffMesh(CustomerAI ai, Track t, Vector3 pos, float now)
        {
            // The first frame after spawning, before the agent warps onto the mesh, is exempt.
            if (t.OffMesh || now - t.SpawnTime < SpawnGrace) return;
            if (NavMesh.SamplePosition(pos, out _, OffMeshRadius, NavMesh.AllAreas)) return;

            t.OffMesh = true;
            OffMesh.Add($"{t.Name} at {pos} ({ai.State})");
        }

        private void CheckSkating(CustomerAI ai, Track t, Vector3 pos, float now)
        {
            // Procedural bodies have no CharacterVisual (no animator), so there is nothing to mismatch.
            var visual = ai.GetComponent<CharacterVisual>();
            if (visual == null || now <= t.LastTime) return;

            Vector3 step = pos - t.LastPos;
            step.y = 0f;
            bool skating = step.magnitude / (now - t.LastTime) > SkateSpeed && visual.CurrentMove == IdleMove;
            if (!skating) { t.SkateSince = -1f; return; }
            if (t.SkateSince < 0f) { t.SkateSince = t.LastTime; return; }
            if (t.Skated || now - t.SkateSince <= SkateDuration) return;

            t.Skated = true;
            Skating.Add($"{t.Name} at {pos}");
        }

        private void OnCheckedOut(CustomerAI ai)
        {
            if (!_tracks.ContainsKey(ai)) return;
            _checkouts.Add((ai.name, ai.DistanceToTillAtCheckout));
            OnResolved(ai);
        }

        private void OnResolved(CustomerAI ai)
        {
            if (_tracks.TryGetValue(ai, out var t) && t.ResolvedTime < 0f) t.ResolvedTime = Time.time;
        }
    }
}
