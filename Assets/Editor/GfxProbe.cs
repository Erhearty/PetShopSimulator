#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class GfxProbe
{
    [MenuItem("PetShop/Report/Graphics Device")]
    public static void Probe()
    {
        Debug.Log($"[Gfx] device={SystemInfo.graphicsDeviceType} name='{SystemInfo.graphicsDeviceName}' " +
                  $"vendor='{SystemInfo.graphicsDeviceVendor}' rt={SystemInfo.supportsAsyncGPUReadback} " +
                  $"nullDevice={SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null}");

        // Can we actually render into a RenderTexture and read it back?
        try
        {
            var rt = new RenderTexture(64, 64, 24);
            var cam = new GameObject("probeCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.green;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            Color c = tex.GetPixel(32, 32);
            Debug.Log($"[Gfx] offscreen render OK — centre pixel {c} (expect green)");
            Object.DestroyImmediate(cam.gameObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Gfx] offscreen render FAILED: {e.Message}");
        }

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
#endif
