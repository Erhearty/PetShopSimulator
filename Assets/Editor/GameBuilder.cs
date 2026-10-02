#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless player builds.
///   Unity -batchmode -nographics -projectPath . -executeMethod GameBuilder.BuildLinux -quit
/// The player lands in Build/Linux/PetShopSimulator.
///
/// The world is authored in <see cref="ScenePath"/> in the editor; nothing generates it.
/// </summary>
public static class GameBuilder
{
    public const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("PetShop/Build Player (Linux 64)")]
    public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", "PetShopSimulator.x86_64");

    [MenuItem("PetShop/Build Player (Windows 64)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", "PetShopSimulator.exe");

    private static void Build(BuildTarget target, string folder, string executable)
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[GameBuilder] {ScenePath} does not exist — it is authored in the editor and must be checked in.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        AddSceneToBuildSettings(ScenePath);

        PlayerSettings.companyName = "Pet Shop Studio";
        PlayerSettings.productName = "Pet Shop Simulator";
        PlayerSettings.defaultScreenWidth  = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.runInBackground     = true;

        string outDir  = Path.Combine(Directory.GetCurrentDirectory(), "Build", folder);
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { ScenePath },
            locationPathName = Path.Combine(outDir, executable),
            target           = target,
            options          = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[GameBuilder] Build succeeded: {summary.outputPath} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s)");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[GameBuilder] Build {summary.result} with {summary.totalErrors} error(s).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    /// <summary>Keeps MainScene first and enabled in EditorBuildSettings (Play from the editor uses it).</summary>
    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length > 0 && scenes[0].path == scenePath && scenes[0].enabled) return;

        var list = new List<EditorBuildSettingsScene>();
        foreach (var s in scenes)
            if (s.path != scenePath && !string.IsNullOrEmpty(s.path)) list.Add(s);

        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
