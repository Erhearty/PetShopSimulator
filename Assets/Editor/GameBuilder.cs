#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless player builds.
///   Unity -batchmode -nographics -projectPath . -executeMethod GameBuilder.BuildLinux -quit
/// The player lands in Build/Linux/PetShopSimulator.
/// </summary>
public static class GameBuilder
{
    [MenuItem("PetShop/Build Player (Linux 64)")]
    public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", "PetShopSimulator.x86_64");

    [MenuItem("PetShop/Build Player (Windows 64)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", "PetShopSimulator.exe");

    private static void Build(BuildTarget target, string folder, string executable)
    {
        if (!File.Exists(SceneBuilder.ScenePath))
        {
            Debug.LogError($"[GameBuilder] {SceneBuilder.ScenePath} does not exist — run SceneBuilder.BuildMainScene first.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        PlayerSettings.companyName = "Pet Shop Studio";
        PlayerSettings.productName = "Pet Shop Simulator";
        PlayerSettings.defaultScreenWidth  = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.runInBackground     = true;

        string outDir  = Path.Combine(Directory.GetCurrentDirectory(), "Build", folder);
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { SceneBuilder.ScenePath },
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
}
#endif
