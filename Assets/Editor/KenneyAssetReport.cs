#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prints what actually came out of the Kenney import: per-kit bounds and whether each
/// model got a real material and texture. Run it after changing the import settings.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod KenneyAssetReport.Report -quit
/// </summary>
public static class KenneyAssetReport
{
    private const string Root = "Assets/Resources/Kenney";

    [MenuItem("PetShop/Report/Kenney Assets")]
    public static void Report()
    {
        if (!Directory.Exists(Root))
        {
            Debug.LogError($"[Kenney] {Root} does not exist.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        int totalModels = 0, missingMaterial = 0, missingTexture = 0;

        foreach (string kitDir in Directory.GetDirectories(Root).OrderBy(d => d))
        {
            string kit   = Path.GetFileName(kitDir);
            var    files = Directory.GetFiles(kitDir, "*.fbx").OrderBy(f => f).ToArray();
            if (files.Length == 0) continue;

            var shaders  = new HashSet<string>();
            var textures = new HashSet<string>();
            Bounds? sample = null;
            string sampleName = "";

            foreach (string file in files)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                totalModels++;
                if (go == null) { Debug.LogWarning($"[Kenney] failed to load {file}"); continue; }

                bool anyMat = false, anyTex = false;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    anyMat = true;
                    shaders.Add(m.shader != null ? m.shader.name : "<null shader>");
                    var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    if (tex != null) { anyTex = true; textures.Add(tex.name); }
                }
                if (!anyMat) missingMaterial++;
                if (!anyTex) missingTexture++;

                if (sample == null)
                {
                    var mf = go.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    { sample = mf.sharedMesh.bounds; sampleName = go.name; }
                }
            }

            string size = sample.HasValue
                ? $"{sample.Value.size.x:F2} x {sample.Value.size.y:F2} x {sample.Value.size.z:F2}"
                : "n/a";

            Debug.Log($"[Kenney] {kit,-11} {files.Length,3} models | shaders: {string.Join(", ", shaders)} " +
                      $"| textures: {(textures.Count == 0 ? "none (vertex/flat colours)" : string.Join(", ", textures))} " +
                      $"| sample '{sampleName}' bounds {size}");
        }

        Debug.Log($"[Kenney] {totalModels} models total; {missingMaterial} without any material, " +
                  $"{missingTexture} without a texture (flat-coloured kits are expected here).");

        if (Application.isBatchMode) EditorApplication.Exit(missingMaterial == 0 ? 0 : 1);
    }
}
#endif
