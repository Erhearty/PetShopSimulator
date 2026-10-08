using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;

namespace PetShop.Tests
{
    public class StaffRoleBehaviorTests
    {
        [Test]
        public void OnlyCashierStaysAtTill()
        {
            Assert.IsTrue(StaffRoleBehavior.StaysAtTill(StaffRole.Cashier));
            Assert.IsFalse(StaffRoleBehavior.StaysAtTill(StaffRole.Restocker));
            Assert.IsFalse(StaffRoleBehavior.StaysAtTill(StaffRole.Feeder));
        }

        [Test]
        public void CashierIdleTargetIsTheTill()
        {
            var home = new Vector3(3f, 0f, 4f);
            Assert.AreEqual(home, StaffRoleBehavior.IdleTarget(StaffRole.Cashier, home, 0.3f, 0.9f));
        }

        [Test]
        public void RoamersStayWithinTheirRadius()
        {
            foreach (var role in new[] { StaffRole.Restocker, StaffRole.Feeder })
            {
                var p = StaffRoleBehavior.IdleTarget(role, Vector3.zero, 0.37f, 1f);
                Assert.LessOrEqual(p.magnitude, StaffRoleBehavior.RoamRadius(role) + 0.001f);
                Assert.AreEqual(0f, p.y);
            }
        }

        [Test]
        public void RollsMoveTheTarget()
        {
            var a = StaffRoleBehavior.IdleTarget(StaffRole.Restocker, Vector3.zero, 0.1f, 0.8f);
            var b = StaffRoleBehavior.IdleTarget(StaffRole.Restocker, Vector3.zero, 0.6f, 0.8f);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void RolesBehaveDifferently()
        {
            Assert.AreNotEqual(StaffRoleBehavior.RoamRadius(StaffRole.Restocker), StaffRoleBehavior.RoamRadius(StaffRole.Feeder));
            Assert.Less(StaffRoleBehavior.PauseSeconds(StaffRole.Restocker, 1f), StaffRoleBehavior.PauseSeconds(StaffRole.Feeder, 0f) + 0.01f);
        }

        [Test]
        public void PauseStaysInRange()
        {
            var r = StaffRoleBehavior.PauseRange(StaffRole.Feeder);
            Assert.AreEqual(r.x, StaffRoleBehavior.PauseSeconds(StaffRole.Feeder, -5f));
            Assert.AreEqual(r.y, StaffRoleBehavior.PauseSeconds(StaffRole.Feeder, 5f));
        }
    }
}
