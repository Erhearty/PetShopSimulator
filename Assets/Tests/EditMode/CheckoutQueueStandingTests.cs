using NUnit.Framework;
using UnityEngine;
using PetShop.Commerce;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="CheckoutQueue.StandingPosition"/>: the line forms on the customer
    /// side of the counter (its -Z, where the cashier used to stand) for any counter rotation, never on
    /// the cashier's side (+Z).
    /// </summary>
    public class CheckoutQueueStandingTests
    {
        private GameObject _go;
        private GameObject _till;
        private CheckoutQueue _queue;

        [SetUp]
        public void SetUp()
        {
            _go    = new GameObject("Queue");
            _till  = new GameObject("Till");
            _queue = _go.AddComponent<CheckoutQueue>();
            _queue.TillPoint = _till.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_till);
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        [TestCase(333f)]
        public void StandingPositions_LieOnCustomerSide_ForAnyCounterRotation(float yaw)
        {
            var counterPos = new Vector3(3f, 0f, -2f);
            var counter    = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = counter * Vector3.back;

            _till.transform.position = counterPos + forward * 0.5f;
            _queue.QueueDirection    = forward;

            for (int place = 0; place < 4; place++)
            {
                Vector3 stand = _queue.StandingPosition(place);
                Assert.Greater(Vector3.Dot(stand - counterPos, forward), 0.5f,
                    $"place {place} is not in front of the counter at yaw {yaw}");
                Assert.AreEqual(0f, Vector3.Dot(stand - counterPos, Vector3.Cross(Vector3.up, forward)), 1e-3f,
                    "the line runs straight out from the counter");
            }
        }

        [Test]
        public void StandingPositions_AreSpacedAlongTheQueueDirection()
        {
            _queue.QueueDirection = Vector3.right * 3f;
            Vector3 first  = _queue.StandingPosition(0);
            Vector3 second = _queue.StandingPosition(1);
            Assert.AreEqual(_queue.Spacing, Vector3.Distance(first, second), 1e-4f);
            Assert.Greater(second.x, first.x);
        }
    }
}
