#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Drives <see cref="PetShop.Dev.CameraTour"/> from the editor so the game can be
/// photographed head-lessly.
///
///   Unity -batchmode -projectPath . -executeMethod SceneShot.Capture -tour Screenshots
///
/// Note: **no** -nographics. The editor needs a real graphics device to render, and it gets
/// one from the DRM render node given DISPLAY and XAUTHORITY — even though opening an
/// actual window from a non-session shell does not work. Also no -quit: play mode has to
/// run, so this exits the editor itself once the tour drops its _done marker.
/// </summary>
public static class SceneShot
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";
    private const int    TimeoutTicks = 30000;

    private static string _outputDir = "Screenshots";
    private static int    _ticks;

    public static void Capture()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-tour") _outputDir = args[i + 1];

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError("[SceneShot] No graphics device — run without -nographics.");
            EditorApplication.Exit(1);
            return;
        }

        string marker = Path.Combine(_outputDir, "_done.txt");
        if (File.Exists(marker)) File.Delete(marker);

        // Always photograph a brand-new shop. Left alone, the tour picks up whatever save the
        // last run left behind — which is how a set of "empty pens" screenshots happened:
        // the save was from day 5, by which point customers had bought every animal.
        string save = Path.Combine(Application.persistentDataPath, "petshop_save.json");
        if (File.Exists(save))
        {
            File.Delete(save);
            Debug.Log("[SceneShot] cleared the existing save so the tour shows a fresh shop.");
        }

        Debug.Log($"[SceneShot] device={SystemInfo.graphicsDeviceType}, entering play mode...");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EditorApplication.update += Poll;
        EditorApplication.EnterPlaymode();
    }

    private static void Poll()
    {
        string marker = Path.Combine(_outputDir, "_done.txt");

        if (File.Exists(marker))
        {
            int count = Directory.GetFiles(_outputDir, "*.png").Length;
            Debug.Log($"[SceneShot] tour finished — {count} PNG(s) in {_outputDir}");
            EditorApplication.update -= Poll;
            EditorApplication.Exit(0);
            return;
        }

        if (++_ticks > TimeoutTicks)
        {
            Debug.LogError("[SceneShot] timed out waiting for the tour.");
            EditorApplication.update -= Poll;
            EditorApplication.Exit(1);
        }
    }
}
#endif
