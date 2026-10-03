using NUnit.Framework;
using PetShop.Traffic;
using UnityEngine;

namespace PetShop.Tests
{
    /// <summary>EditMode tests for the pure street-traffic maths in <see cref="TrafficMath"/>.</summary>
    public class TrafficTests
    {
        private const float Tolerance = 1e-4f;
        private const float Cruise = 8f;
        private const float MinGap = 2f;
        private const float Accel = 3f;
        private const float Brake = 8f;
        private const float Dt = 0.1f;
        private const float FarGap = 100f;

        private static readonly Vector3[] Bend =
        {
            new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10),
        };

        [Test]
        public void Length_SumsSegments() => Assert.AreEqual(20f, TrafficMath.Length(Bend), Tolerance);

        [Test]
        public void PositionAt_WithinFirstSegment()
        {
            Assert.AreEqual(new Vector3(4, 0, 0), TrafficMath.PositionAt(Bend, 4f));
        }

        [Test]
        public void PositionAt_CrossesIntoSecondSegment()
        {
            Vector3 p = TrafficMath.PositionAt(Bend, 15f);
            Assert.AreEqual(10f, p.x, Tolerance);
            Assert.AreEqual(5f, p.z, Tolerance);
        }

        [Test]
        public void PositionAt_ClampsAtEnds()
        {
            Assert.AreEqual(Bend[0], TrafficMath.PositionAt(Bend, -5f));
            Assert.AreEqual(Bend[2], TrafficMath.PositionAt(Bend, 999f));
        }

        [Test]
        public void TangentAt_FollowsSegmentAndClamps()
        {
            Assert.AreEqual(Vector3.right, TrafficMath.TangentAt(Bend, 3f));
            Assert.AreEqual(Vector3.forward, TrafficMath.TangentAt(Bend, 15f));
            Assert.AreEqual(Vector3.right, TrafficMath.TangentAt(Bend, -1f));
            Assert.AreEqual(Vector3.forward, TrafficMath.TangentAt(Bend, 999f));
        }

        [Test]
        public void TargetSpeed_FreeRoad_AcceleratesTowardCruise()
        {
            float v = TrafficMath.TargetSpeed(0f, Cruise, FarGap, MinGap, Accel, Brake, Dt);
            Assert.AreEqual(Accel * Dt, v, Tolerance);
            Assert.AreEqual(Cruise, TrafficMath.TargetSpeed(Cruise, Cruise, FarGap, MinGap, Accel, Brake, Dt), Tolerance);
        }

        [Test]
        public void TargetSpeed_AtMinGap_BrakesToZero()
        {
            float v = TrafficMath.TargetSpeed(Cruise, Cruise, MinGap, MinGap, Accel, Brake, Dt);
            Assert.AreEqual(Cruise - Brake * Dt, v, Tolerance);
            Assert.AreEqual(0f, TrafficMath.TargetSpeed(0.1f, Cruise, 0f, MinGap, Accel, Brake, 1f), Tolerance);
        }

        [Test]
        public void StopLine_BrakesOnlyWhenOccupiedAndShortOfLine()
        {
            Assert.IsTrue(TrafficMath.ShouldBrakeForStopLine(2f, true, Cruise, Brake));
            Assert.IsFalse(TrafficMath.ShouldBrakeForStopLine(2f, false, Cruise, Brake));
            Assert.IsFalse(TrafficMath.ShouldBrakeForStopLine(-1f, true, Cruise, Brake));
            Assert.IsFalse(TrafficMath.ShouldBrakeForStopLine(FarGap, true, Cruise, Brake));
        }

        [Test]
        public void CanWrap_OnlyWhenStartIsClear()
        {
            Assert.IsTrue(TrafficMath.CanWrap(float.PositiveInfinity, MinGap));
            Assert.IsTrue(TrafficMath.CanWrap(MinGap, MinGap));
            Assert.IsFalse(TrafficMath.CanWrap(MinGap - 0.5f, MinGap));
        }
    }
}
