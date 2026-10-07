using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>
    /// The black sky is gone: a static city backdrop rings the world outside the walkable area,
    /// carries no colliders, and the main camera no longer clears to the old near-black colour.
    /// </summary>
    public class CityBackdropPlayTests
    {
        private const int   Seed      = 2718;
        private const float TimeScale = 1f;
        private const float DayLength = 900f;
        /// <summary>The old clear colour's brightest channel was 0.16; anything this dark still reads as black.</summary>
        private const float NearBlackMaxChannel = 0.2f;

        [TearDown]
        public void TearDown() => PlaytestHarness.Teardown();

        [UnityTest]
        public IEnumerator NewGame_BackdropRingsTheWorld_WithoutCollidersOrBlackSky()
        {
            yield return PlaytestHarness.Boot(false, Seed, TimeScale, DayLength, furnish: false);

            var backdrop = GameObject.Find(CityBackdrop.ObjectName);
            Assert.IsNotNull(backdrop, "No city backdrop in the world.");
            Assert.IsEmpty(backdrop.GetComponentsInChildren<Collider>(true), "The backdrop must not have colliders.");

            var renderer = backdrop.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "The backdrop has no renderer.");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode,
                "The backdrop must not cast shadows.");

            var nav = NavMesh.CalculateTriangulation();
            Assert.Greater(nav.vertices.Length, 0, "No NavMesh was baked.");
            var mesh = backdrop.GetComponent<MeshFilter>().sharedMesh;
            Vector3 centre = backdrop.transform.position;
            float radius = float.MaxValue;
            foreach (var v in mesh.vertices)
                radius = Mathf.Min(radius, new Vector2(v.x, v.z).magnitude);
            foreach (var v in nav.vertices)
            {
                float d = new Vector2(v.x - centre.x, v.z - centre.z).magnitude;
                Assert.Less(d, radius, $"Walkable NavMesh at {v} reaches the backdrop (radius {radius:F1}).");
            }

            var cam = Camera.main;
            Assert.IsNotNull(cam, "No main camera.");
            bool skybox = cam.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null;
            Color bg = cam.backgroundColor;
            bool brightSolid = cam.clearFlags == CameraClearFlags.SolidColor && bg.maxColorComponent > NearBlackMaxChannel;
            Assert.IsTrue(skybox || brightSolid,
                $"Main camera still clears to a near-black colour ({cam.clearFlags}, {bg}).");
        }
    }
}
