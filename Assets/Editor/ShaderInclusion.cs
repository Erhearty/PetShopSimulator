#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Every material in this game is created at runtime with Shader.Find, so the shaders it
/// asks for have no asset referencing them and get stripped from player builds. Adding
/// them to "Always Included Shaders" keeps them in the build.
/// </summary>
public static class ShaderInclusion
{
    private static readonly string[] Required =
    {
        "Standard",
        "Skybox/Procedural",
        "Legacy Shaders/Diffuse",
        "Sprites/Default",
        "UI/Default",
        "TextMeshPro/Distance Field",
        "TextMeshPro/Mobile/Distance Field",
    };

    [MenuItem("PetShop/Setup/Include Runtime Shaders")]
    public static void EnsureIncluded()
    {
        var graphics = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        if (graphics == null)
        {
            Debug.LogError("[ShaderInclusion] Could not open GraphicsSettings.asset.");
            Finish(1);
            return;
        }

        var so    = new SerializedObject(graphics);
        var array = so.FindProperty("m_AlwaysIncludedShaders");

        var existing = new HashSet<Shader>();
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) existing.Add(s);

        int added = 0;
        foreach (string name in Required)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[ShaderInclusion] Shader not found in project: {name}");
                continue;
            }
            if (!existing.Add(shader)) continue;

            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
            added++;
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[ShaderInclusion] Always-included shaders: {existing.Count} total, {added} added " +
                  $"({string.Join(", ", existing.Where(s => s != null).Select(s => s.name))}).");
        Finish(0);
    }

    private static void Finish(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
#endif
