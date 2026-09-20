#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PetShop.Core;

/// <summary>
/// Creates Assets/Scenes/MainScene.unity — an empty scene holding nothing but the
/// Bootstrap object. Everything else is generated at runtime.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod SceneBuilder.BuildMainScene -quit
/// </summary>
public static class SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("PetShop/Build Main Scene")]
    public static void BuildMainScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var bootstrap = new GameObject("Bootstrap");
        var gb = bootstrap.AddComponent<GameBootstrapper>();
        gb.StartBalance    = 1000f;
        gb.StartReputation = 40f;

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(saved
            ? $"[SceneBuilder] Scene saved: {ScenePath}"
            : $"[SceneBuilder] ERROR: scene not saved to {ScenePath}");

        if (Application.isBatchMode) EditorApplication.Exit(saved ? 0 : 1);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var list = new List<EditorBuildSettingsScene>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != scenePath && !string.IsNullOrEmpty(s.path)) list.Add(s);

        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
