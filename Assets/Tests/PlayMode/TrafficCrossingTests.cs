using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PetShop.Core;
using PetShop.Traffic;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode checks on the crossing in MainScene: a Character-layer body inside the
    /// <see cref="CrosswalkZone"/> occupies it, approaching cars stop before the stop line, and
    /// they drive on once the zone is empty again.
    /// </summary>
    public class TrafficCrossingTests
    {
        private const int   Seed             = 4242;
        private const float RealTime         = 1f;
        private const float DayLength        = 600f;
        private const int   OccupyFrames     = 5;
        private const int   MaxOccupyFrames  = 120;
        private const float HoldTimeout      = 60f;
        private const float ResumeTimeout    = 20f;
        private const float StoppedSpeed     = 0.05f;
        private const float MovingSpeed      = 0.5f;
        private const float MinAdvance       = 0.5f;
        private const float LineTolerance    = 0.5f;
        private const float StopWindow       = 15f;
        private const float CommittedMargin  = 6f;
        private const float CapsuleHeight    = 1.8f;
        private const float CapsuleRadius    = 0.3f;

        private GameObject _capsule;

        [TearDown]
        public void TearDown()
        {
            if (_capsule != null) Object.DestroyImmediate(_capsule);
            _capsule = null;
            PlaytestHarness.Teardown();
        }

        [UnityTest]
        public IEnumerator Occupant_StopsCars_BeforeStopLine_ThenTheyResume()
        {
            yield return PlaytestHarness.Boot(false, Seed, RealTime, DayLength, furnish: false);
            yield return null;

            CrosswalkZone zone = Object.FindAnyObjectByType<CrosswalkZone>();
            Assert.IsNotNull(zone, "MainScene has no CrosswalkZone");
            Assert.IsFalse(zone.IsOccupied, "the crossing starts occupied");
            var cars = new List<TrafficCar>(TrafficDirector.Cars);
            Assert.GreaterOrEqual(cars.Count, 1, "no traffic cars registered");

            _capsule = MakeCapsule(zone.WorldBounds.center);
            for (int i = 0; i < MaxOccupyFrames && !zone.IsOccupied; i++) yield return null;
            for (int i = 0; i < OccupyFrames; i++) yield return null;
            Assert.IsTrue(zone.IsOccupied, "a Character-layer capsule inside the zone did not occupy it");

            // Cars already close to or past the line when the crossing filled cannot stop in time.
            var committed = new HashSet<TrafficCar>();
            foreach (var c in cars)
                if (AlongToLine(zone, c) < CommittedMargin) committed.Add(c);

            TrafficCar stopped = null;
            float until = Time.realtimeSinceStartup + HoldTimeout;
            while (stopped == null && Time.realtimeSinceStartup < until)
            {
                yield return null;
                foreach (var c in cars)
                {
                    if (committed.Contains(c)) continue;
                    float along = AlongToLine(zone, c);
                    Assert.GreaterOrEqual(along, -LineTolerance,
                        $"{c.name} drove over the stop line while the crossing was occupied (speed {c.Speed}).");
                    if (c.Speed < StoppedSpeed && along <= StopWindow) { stopped = c; break; }
                }
            }
            Assert.IsNotNull(stopped, "no approaching car stopped for the occupied crossing");
            Assert.Less(stopped.Speed, StoppedSpeed, "the stopped car is still moving");

            Object.Destroy(_capsule);
            _capsule = null;
            yield return null;
            yield return null;
            Assert.IsFalse(zone.IsOccupied, "the crossing is still occupied after the capsule left");

            float before = stopped.Distance;
            float resumeUntil = Time.realtimeSinceStartup + ResumeTimeout;
            while ((stopped.Speed < MovingSpeed || Mathf.Abs(stopped.Distance - before) < MinAdvance)
                   && Time.realtimeSinceStartup < resumeUntil)
                yield return null;
            Assert.Greater(stopped.Speed, MovingSpeed, $"{stopped.name} did not resume");
            Assert.Greater(Mathf.Abs(stopped.Distance - before), MinAdvance, $"{stopped.name} did not advance");
        }

        /// <summary>Metres from the car's front to the stop line for its travel direction (negative once past).</summary>
        private static float AlongToLine(CrosswalkZone zone, TrafficCar car)
        {
            Vector3 fwd = car.transform.forward;
            Vector3 toLine = zone.StopLinePosition(fwd) - car.transform.position;
            return Vector3.Dot(toLine, fwd) - car.HalfLength;
        }

        private static GameObject MakeCapsule(Vector3 at)
        {
            var go = new GameObject("TestPedestrian") { layer = GameLayers.Character };
            go.transform.position = at;
            var col = go.AddComponent<CapsuleCollider>();
            col.height = CapsuleHeight;
            col.radius = CapsuleRadius;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            return go;
        }
    }
}
