using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.UI;

namespace PetShop.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="FeedbackFX"/>. Time is stepped by hand through
    /// <see cref="FeedbackFX.Tick"/>, so nothing depends on the real frame clock.
    /// </summary>
    public class FeedbackFXTests
    {
        private const float Step = 0.05f;
        private const float SaleValue = 12.5f;

        private GameObject _host;
        private FeedbackFX _fx;
        private bool       _previousReduceMotion;

        /// <summary>Builds a fresh FeedbackFX with motion enabled.</summary>
        [SetUp]
        public void SetUp()
        {
            _previousReduceMotion = GameSettings.ReduceMotion;
            GameSettings.ReduceMotion = false;
            _host = new GameObject("FeedbackFXTest");
            _fx   = _host.AddComponent<FeedbackFX>();
            _fx.enabled = false; // only manual Tick() advances time
        }

        /// <summary>Destroys the test objects and restores the reduce-motion setting.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSettings.ReduceMotion = _previousReduceMotion;
        }

        private void StepFor(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) _fx.Tick(Step);
        }

        /// <summary>A sale throws a coin burst (no floating text) and it cleans up.</summary>
        [Test]
        public void Sale_SpawnsCoinBurst_ThenCleansUp()
        {
            _fx.PlaySale(Vector3.zero, SaleValue);
            Assert.AreEqual(FeedbackFX.CoinBurst, _fx.Coins.particleCount);
            Assert.AreEqual(0, _host.GetComponentsInChildren<WorldLabel>(true).Length);

            _fx.Coins.Simulate(FeedbackFX.CoinLifetime + Step, true, false);
            Assert.AreEqual(0, _fx.Coins.particleCount);
        }

        private GameObject Cube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_host.transform);
            return cube;
        }

        private static Vector3 ColliderSize(GameObject go)
        {
            Physics.SyncTransforms();
            return go.GetComponent<Collider>().bounds.size;
        }

        /// <summary>
        /// Placing an object pops a visual clone while the object itself (and its collider, which the
        /// NavMesh bake reads) stays at full size; the original shows again once the pop settles.
        /// </summary>
        [Test]
        public void Placement_PopsCloneThenSettles_ColliderUnscaled()
        {
            var placed = Cube();
            var renderer = placed.GetComponent<Renderer>();
            _fx.PlayPlaced(placed);
            Assert.AreEqual(1, _fx.ActiveFurnitureTweenCount);
            Assert.AreEqual(Vector3.one, placed.transform.localScale);
            Assert.AreEqual(1f, ColliderSize(placed).x, 1e-4f);
            Assert.IsFalse(renderer.enabled, "the clone stands in while popping");
            Assert.AreEqual(FeedbackFX.DustBurst, _fx.Dust.particleCount);

            StepFor(FeedbackFX.PopDuration * 0.5f);
            Assert.AreEqual(1f, ColliderSize(placed).x, 1e-4f);
            var clone = _host.transform.Find(FeedbackFX.CloneName);
            Assert.IsNotNull(clone);
            Assert.AreEqual(0, clone.GetComponentsInChildren<Collider>(true).Length, "clone is visual-only");

            StepFor(FeedbackFX.PopDuration + Step);
            Assert.AreEqual(0, _fx.ActiveFurnitureTweenCount);
            Assert.IsTrue(renderer.enabled);
            Assert.AreEqual(1f, placed.transform.localScale.x, 1e-4f);
        }

        /// <summary>Removal leaves a shrinking clone that outlives the original and cleans up.</summary>
        [Test]
        public void Removal_ShrinksCloneThenCleansUp()
        {
            var removed = Cube();
            _fx.PlayRemoved(removed);
            Object.DestroyImmediate(removed);
            Assert.AreEqual(1, _fx.ActiveFurnitureTweenCount);

            StepFor(FeedbackFX.ShrinkDuration + Step);
            Assert.AreEqual(0, _fx.ActiveFurnitureTweenCount);
        }

        /// <summary>The shrink curve runs 1 → 0 with an ease-in.</summary>
        [Test]
        public void ShrinkScale_Curve()
        {
            Assert.AreEqual(1f, FeedbackFX.ShrinkScale(0f), 1e-4f);
            Assert.Greater(FeedbackFX.ShrinkScale(0.5f), 0.5f);
            Assert.AreEqual(0f, FeedbackFX.ShrinkScale(1f), 1e-4f);
        }

        /// <summary>With reduce motion on, placement and removal start no pop or shrink.</summary>
        [Test]
        public void ReduceMotion_DisablesPop()
        {
            GameSettings.ReduceMotion = true;
            var placed = Cube();
            _fx.PlayPlaced(placed);
            _fx.PlayRemoved(placed);
            Assert.AreEqual(0, _fx.ActiveFurnitureTweenCount);
            Assert.AreEqual(Vector3.one, placed.transform.localScale);
            Assert.IsTrue(placed.GetComponent<Renderer>().enabled);
        }

        /// <summary>The pop curve starts at 0.9, overshoots to 1.05 and lands on 1.</summary>
        [Test]
        public void PopScale_Curve()
        {
            Assert.AreEqual(0.9f, FeedbackFX.PopScale(0f), 1e-4f);
            Assert.AreEqual(1.05f, FeedbackFX.PopScale(0.6f), 1e-4f);
            Assert.AreEqual(1f, FeedbackFX.PopScale(1f), 1e-4f);
        }
    }
}
