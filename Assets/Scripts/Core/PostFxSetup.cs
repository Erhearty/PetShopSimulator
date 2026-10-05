using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace PetShop.Core
{
    /// <summary>
    /// Runtime post-processing pass (Post Processing Stack v2, Built-in pipeline): a
    /// <see cref="PostProcessLayer"/> on the main camera and one global
    /// <see cref="PostProcessVolume"/> with mild bloom, ACES tonemapping and a soft vignette.
    /// Done in code so MainScene.unity stays untouched. Idempotent: a second call reuses the
    /// existing layer and volume instead of adding new ones.
    /// </summary>
    public static class PostFxSetup
    {
        /// <summary>Name of the GameObject carrying the global volume, so a second pass finds it.</summary>
        public const string VolumeName = "PostFxVolume";

        /// <summary>Layer the volume lives on; the camera's layer mask contains exactly this layer.</summary>
        public const string VolumeLayerName = "Default";

        private const string LogTag = "[PostFx]";

        private const float VolumePriority = 1f;

        private const float BloomIntensity = 1.5f;
        private const float BloomThreshold = 1.1f;
        private const float BloomSoftKnee  = 0.5f;

        private const float VignetteIntensity  = 0.25f;
        private const float VignetteSmoothness = 0.4f;

        private const PostProcessLayer.Antialiasing AntialiasingMode =
            PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        /// <summary>
        /// Applies post FX to <see cref="Camera.main"/>. Returns without doing anything on a
        /// headless (<see cref="GraphicsDeviceType.Null"/>) device or when there is no main camera.
        /// </summary>
        public static void Apply()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log($"{LogTag} action=skip reason=null-graphics-device");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"{LogTag} action=skip reason=no-main-camera");
                return;
            }

            Configure(cam);
        }

        /// <summary>
        /// Gives an extra camera (e.g. the screenshot tour's) the same post FX as the main camera:
        /// enables HDR and adds an initialised <see cref="PostProcessLayer"/> that reads the
        /// existing global volume. Never creates a volume, so there is never a second one; if
        /// <see cref="Apply"/> has not created it yet the layer renders without effects.
        /// Skips on a headless (<see cref="GraphicsDeviceType.Null"/>) device.
        /// </summary>
        /// <returns>True if the layer is attached; false if skipped on a null device.</returns>
        public static bool AttachLayer(Camera cam)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log($"{LogTag} action=attach-skip camera='{cam.name}' reason=null-graphics-device");
                return false;
            }

            cam.allowHDR = true;
            bool layerCreated = EnsureLayer(cam, ResolveVolumeLayer());
            bool volumeFound  = FindVolume() != null;
            if (!volumeFound)
                Debug.LogWarning($"{LogTag} action=attach camera='{cam.name}' result=no-global-volume");
            Debug.Log($"{LogTag} action=attach camera='{cam.name}' layerCreated={layerCreated} " +
                      $"volumeFound={volumeFound}");
            return true;
        }

        /// <summary>
        /// Core of <see cref="Apply"/> without the device/camera guards, so tests can run it
        /// headless. Ensures one layer on <paramref name="cam"/> and one global volume.
        /// </summary>
        /// <returns>The global volume in use.</returns>
        internal static PostProcessVolume Configure(Camera cam)
        {
            int volumeLayer = ResolveVolumeLayer();
            cam.allowHDR = true;
            bool layerCreated  = EnsureLayer(cam, volumeLayer);
            var volume         = FindVolume();
            bool volumeCreated = volume == null;
            if (volumeCreated) volume = CreateVolume(volumeLayer);

            Debug.Log($"{LogTag} action=configure camera='{cam.name}' layerCreated={layerCreated} " +
                      $"volumeCreated={volumeCreated} volumeLayer={LayerMask.LayerToName(volumeLayer)}");
            return volume;
        }

        /// <summary>Index of <see cref="VolumeLayerName"/>, or 0 (Default) if it is not defined.</summary>
        private static int ResolveVolumeLayer()
        {
            int layer = LayerMask.NameToLayer(VolumeLayerName);
            return layer >= 0 ? layer : 0;
        }

        /// <summary>Adds and initialises a layer when the camera has none.</summary>
        /// <returns>True if a layer was added.</returns>
        private static bool EnsureLayer(Camera cam, int volumeLayer)
        {
            var layer   = cam.GetComponent<PostProcessLayer>();
            bool create = layer == null;
            if (create)
            {
                layer = cam.gameObject.AddComponent<PostProcessLayer>();
                var resources = PostFxResourcesHolder.Load();
                if (resources == null)
                    Debug.LogWarning($"{LogTag} action=init-layer result=missing-resources " +
                                     $"path=Resources/{PostFxResourcesHolder.ResourcePath}");
                layer.Init(resources);
            }

            layer.volumeTrigger    = cam.transform;
            layer.volumeLayer      = 1 << volumeLayer;
            layer.antialiasingMode = AntialiasingMode;
            return create;
        }

        /// <summary>The existing global volume: by <see cref="VolumeName"/>, else any global volume.</summary>
        private static PostProcessVolume FindVolume()
        {
            var go = GameObject.Find(VolumeName);
            if (go != null && go.TryGetComponent<PostProcessVolume>(out var named)) return named;
            foreach (var v in Object.FindObjectsByType<PostProcessVolume>(FindObjectsSortMode.None))
                if (v.isGlobal) return v;
            return null;
        }

        private static PostProcessVolume CreateVolume(int volumeLayer)
        {
            var go = new GameObject(VolumeName) { layer = volumeLayer };
            var volume = go.AddComponent<PostProcessVolume>();
            volume.isGlobal      = true;
            volume.priority      = VolumePriority;
            volume.sharedProfile = CreateProfile();
            return volume;
        }

        /// <summary>Builds the runtime profile: bloom, HDR ACES colour grading and vignette.</summary>
        private static PostProcessProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            profile.name = VolumeName + "Profile";

            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            bloom.softKnee.Override(BloomSoftKnee);

            var grading = profile.AddSettings<ColorGrading>();
            grading.enabled.Override(true);
            grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.ACES);

            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);

            return profile;
        }
    }
}
