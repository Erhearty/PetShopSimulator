using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.TestTools;
using PetShop.Core;

namespace PetShop.Tests
{
    /// <summary>PlayMode tests for <see cref="PostFxSetup"/>: one layer, one global volume, idempotent.</summary>
    public class PostFxSetupPlayTests
    {
        private GameObject _camGo;
        private Camera     _cam;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyVolumes();
            _camGo = new GameObject("PostFxTestCamera");
            _cam   = _camGo.AddComponent<Camera>();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            DestroyVolumes();
        }

        private static void DestroyVolumes()
        {
            foreach (var v in Object.FindObjectsByType<PostProcessVolume>(FindObjectsSortMode.None))
            {
                if (v.sharedProfile != null) Object.DestroyImmediate(v.sharedProfile);
                Object.DestroyImmediate(v.gameObject);
            }
        }

        private static PostProcessVolume[] GlobalVolumes()
        {
            var all = Object.FindObjectsByType<PostProcessVolume>(FindObjectsSortMode.None);
            return System.Array.FindAll(all, v => v.isGlobal);
        }

        [UnityTest]
        public IEnumerator Configure_AddsOneLayerAndOneGlobalVolumeWithEffects()
        {
            var volume = PostFxSetup.Configure(_cam);
            yield return null;

            Assert.AreEqual(1, _camGo.GetComponents<PostProcessLayer>().Length);
            var layer = _cam.GetComponent<PostProcessLayer>();
            Assert.AreEqual(_cam.transform, layer.volumeTrigger);
            Assert.AreNotEqual(0, layer.volumeLayer.value & (1 << volume.gameObject.layer));
            Assert.IsTrue(_cam.allowHDR);

            Assert.AreEqual(1, GlobalVolumes().Length);
            Assert.AreEqual(PostFxSetup.VolumeName, volume.gameObject.name);
            var profile = volume.sharedProfile;
            Assert.IsTrue(profile.TryGetSettings<Bloom>(out var bloom) && bloom.enabled.value);
            Assert.IsTrue(profile.TryGetSettings<ColorGrading>(out var grading) && grading.enabled.value);
            Assert.AreEqual(Tonemapper.ACES, grading.tonemapper.value);
            Assert.AreEqual(GradingMode.HighDefinitionRange, grading.gradingMode.value);
            Assert.IsTrue(profile.TryGetSettings<Vignette>(out var vignette) && vignette.enabled.value);
        }

        [UnityTest]
        public IEnumerator Configure_Twice_DoesNotDuplicate()
        {
            var first = PostFxSetup.Configure(_cam);
            yield return null;
            var second = PostFxSetup.Configure(_cam);
            yield return null;

            Assert.AreSame(first, second);
            Assert.AreEqual(1, _camGo.GetComponents<PostProcessLayer>().Length);
            Assert.AreEqual(1, GlobalVolumes().Length);
        }

        [Test]
        public void ResourcesHolder_ResolvesPackageResources()
        {
            Assert.IsNotNull(PostFxResourcesHolder.Load(),
                "Resources/" + PostFxResourcesHolder.ResourcePath + " must reference the package's PostProcessResources.");
        }

        [UnityTest]
        public IEnumerator Apply_OnNullDevice_IsHarmless()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                Assert.Ignore("Only meaningful on a headless (-nographics) run.");

            _camGo.tag = "MainCamera";
            Assert.DoesNotThrow(PostFxSetup.Apply);
            yield return null;

            Assert.IsNull(_cam.GetComponent<PostProcessLayer>());
            Assert.AreEqual(0, GlobalVolumes().Length);
        }
    }
}
