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
    /// PlayMode checks on the street authored in MainScene: every car (parked or driving) rests on
    /// the road surface, none stands on the pavement, both lanes carry traffic that moves, and cars
    /// in a lane never overlap one another.
    /// </summary>
    public class TrafficSceneTests
    {
        private const int   Seed            = 4242;
        private const float RealTime        = 1f;
        private const float DayLength       = 600f;
        private const float GroundTolerance = 0.05f;
        private const float RayHeight       = 20f;
        private const float RayRange        = 60f;
        private const float AdvanceSeconds  = 2f;
        private const float SettleSeconds   = 20f;
        private const float MinMovement     = 0.5f;
        private const float OverlapSlack    = 0.05f;
        private const float CrashDepth      = 0.05f;
        private const string CarPrefix      = "Vehicle_Car";
        private const string PavementName   = "Pavement";

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        [UnityTest]
        public IEnumerator Cars_RestOnRoadSurface_AndAvoidPavement()
        {
            yield return PlaytestHarness.Boot(false, Seed, RealTime, DayLength);
            yield return null;

            var cars = FindCarRoots();
            Assert.GreaterOrEqual(cars.Count, 1, "no cars found in the street");
            var pavements = FindPavementBounds();

            foreach (var car in cars)
            {
                var b = CarBounds(car);
                float surface = SurfaceBelow(car, b);
                Assert.AreEqual(surface, b.min.y, GroundTolerance, $"{car.name} is not on the road surface");
                foreach (var p in pavements)
                    Assert.IsFalse(OverlapsXZ(b, p), $"{car.name} overlaps the pavement");
            }
        }

        [UnityTest]
        public IEnumerator BothLanes_HaveCars_ThatAdvance_AndNeverOverlap()
        {
            yield return PlaytestHarness.Boot(false, Seed, RealTime, DayLength);
            yield return null;

            var trafficCars = new List<TrafficCar>(Object.FindObjectsByType<TrafficCar>(FindObjectsSortMode.None));
            var lanes = new HashSet<TrafficLane>();
            foreach (var c in trafficCars) if (c.Lane != null) lanes.Add(c.Lane);
            Assert.GreaterOrEqual(lanes.Count, 2, "both lanes need cars");

            var before = new Dictionary<TrafficCar, Vector3>();
            foreach (var c in trafficCars) before[c] = c.transform.position;
            yield return new WaitForSecondsRealtime(AdvanceSeconds);
            int moved = 0;
            foreach (var c in trafficCars)
                if ((c.transform.position - before[c]).magnitude > MinMovement) moved++;
            Assert.Greater(moved, 0, "no car advanced");

            float until = Time.realtimeSinceStartup + SettleSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                AssertNoLaneOverlap(trafficCars);
                AssertNoCollisions(trafficCars);
                yield return new WaitForSecondsRealtime(1f);
            }
            AssertNoLaneOverlap(trafficCars);
            AssertNoCollisions(trafficCars);
        }

        /// <summary>No two cars' colliders interpenetrate anywhere: lanes, U-turns or the car park.</summary>
        private static void AssertNoCollisions(List<TrafficCar> cars)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < cars.Count; i++)
            for (int j = i + 1; j < cars.Count; j++)
            {
                var a = cars[i].GetComponentInChildren<Collider>();
                var b = cars[j].GetComponentInChildren<Collider>();
                if (a == null || b == null) continue;
                bool hit = Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                                                      b, b.transform.position, b.transform.rotation,
                                                      out _, out float depth);
                Assert.IsFalse(hit && depth > CrashDepth, $"{cars[i].name} and {cars[j].name} collide ({depth:F2} m)");
            }
        }

        private static void AssertNoLaneOverlap(List<TrafficCar> cars)
        {
            foreach (var lane in new HashSet<TrafficLane>(LaneOf(cars)))
            {
                var inLane = cars.FindAll(c => c.Lane == lane);
                inLane.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                for (int i = 1; i < inLane.Count; i++)
                {
                    float gap = inLane[i].Distance - inLane[i - 1].Distance;
                    float need = inLane[i].HalfLength + inLane[i - 1].HalfLength - OverlapSlack;
                    Assert.GreaterOrEqual(gap, need, $"{inLane[i - 1].name} and {inLane[i].name} overlap in {lane.name}");
                }
            }
        }

        private static IEnumerable<TrafficLane> LaneOf(List<TrafficCar> cars)
        {
            foreach (var c in cars) if (c.Lane != null) yield return c.Lane;
        }

        private static List<Transform> FindCarRoots()
        {
            var roots = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith(CarPrefix)) continue;
                if (t.parent != null && t.parent.name.StartsWith(CarPrefix)) continue;
                roots.Add(t);
            }
            return roots;
        }

        private static List<Bounds> FindPavementBounds()
        {
            var list = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.gameObject.name == PavementName) list.Add(r.bounds);
            return list;
        }

        private static Bounds CarBounds(Transform car)
        {
            var renderers = car.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, $"{car.name} has no renderer");
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static float SurfaceBelow(Transform car, Bounds b)
        {
            var origin = new Vector3(b.center.x, b.max.y + RayHeight, b.center.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, RayRange + RayHeight, 1 << GameLayers.Scenery);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(car)) continue;
                if (h.point.y > b.min.y + 1f) continue;   // roofs / props above the car base
                if (h.point.y > best) best = h.point.y;
            }
            Assert.IsFalse(float.IsNegativeInfinity(best), $"no road surface found below {car.name}");
            return best;
        }

        private static bool OverlapsXZ(Bounds a, Bounds b) =>
            a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
    }
}
