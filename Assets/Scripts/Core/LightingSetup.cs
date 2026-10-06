using UnityEngine;
using UnityEngine.Rendering;

namespace PetShop.Core
{
    /// <summary>
    /// Runtime lighting pass for the shop: a warm key light, a soft cool fill and a warm
    /// trilight ambient, all taken from <see cref="MaterialFactory.Palette"/>. Done in code
    /// rather than in scene YAML so the look can be tuned in one place and stays valid
    /// whichever lights the scene happens to author.
    /// </summary>
    public static class LightingSetup
    {
        /// <summary>Name given to the fill light this pass creates, so a second pass reuses it.</summary>
        public const string FillLightName = "FillLight";

        private const float KeyIntensity      = 1.15f;
        private const float KeyShadowStrength = 0.75f;
        private const float FillIntensity     = 0.35f;
        private const float AmbientIntensity  = 1f;
        private static readonly Vector3 KeyEuler  = new(42f, -35f, 0f);
        private static readonly Vector3 FillEuler = new(28f, 150f, 0f);

        /// <summary>
        /// Applies key, fill and ambient lighting, then post FX via <see cref="PostFxSetup.Apply"/>.
        /// Safe to call more than once.
        /// </summary>
        public static void Apply()
        {
            var key = FindKeyLight();
            if (key == null) key = CreateDirectional("KeyLight", KeyEuler);
            ConfigureKey(key);

            var fillGO = GameObject.Find(FillLightName);
            var fill   = fillGO != null ? fillGO.GetComponent<Light>() : CreateDirectional(FillLightName, FillEuler);
            ConfigureFill(fill);

            ApplyAmbient();
            CityBackdrop.Apply();
            PostFxSetup.Apply();
        }

        /// <summary>The scene's sun if it names one, otherwise its first directional light.</summary>
        private static Light FindKeyLight()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.name != FillLightName) return light;
            return null;
        }

        private static Light CreateDirectional(string name, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        private static void ConfigureKey(Light key)
        {
            key.color          = MaterialFactory.Palette.KeyLight;
            key.intensity      = KeyIntensity;
            key.shadows        = LightShadows.Soft;
            key.shadowStrength = KeyShadowStrength;
            RenderSettings.sun = key;
        }

        private static void ConfigureFill(Light fill)
        {
            if (fill == null) return;
            fill.color     = MaterialFactory.Palette.FillLight;
            fill.intensity = FillIntensity;
            fill.shadows   = LightShadows.None;
        }

        private static void ApplyAmbient()
        {
            RenderSettings.ambientMode         = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = MaterialFactory.Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = MaterialFactory.Palette.AmbientEquator;
            RenderSettings.ambientGroundColor  = MaterialFactory.Palette.AmbientGround;
            RenderSettings.ambientIntensity    = AmbientIntensity;
        }
    }
}
