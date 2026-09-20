#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes Logs/kenney-bounds.txt: the combined renderer bounds of every imported Kenney
/// model. The kits are authored at wildly different scales, so the game sizes models by
/// measured bounds rather than per-kit magic numbers — this is the reference table.
/// </summary>
public static class KenneyBoundsDump
{
    private static readonly string[] Roots = { "Assets/Resources/Kenney", "Assets/Resources/Packs" };

    [MenuItem("PetShop/Report/Dump Kenney Bounds")]
    public static void Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{"pack",-26} {"model",-34} {"width",8} {"height",8} {"depth",8}  pivot");

        var kitDirs = new List<string>();
        foreach (string root in Roots)
        {
            if (!Directory.Exists(root)) continue;
            kitDirs.Add(root);
            kitDirs.AddRange(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
        }

        foreach (string kitDir in kitDirs.OrderBy(d => d))
        {
            string kit = kitDir.Replace("Assets/Resources/", "");
            var models = Directory.GetFiles(kitDir, "*.fbx").Concat(Directory.GetFiles(kitDir, "*.prefab"));
            foreach (string file in models.OrderBy(f => f))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab == null) continue;

                var instance = Object.Instantiate(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                    string pivot = Mathf.Abs(b.min.y) < 0.01f ? "base" :
                                   Mathf.Abs(b.center.y) < 0.01f ? "centre" : $"y{b.min.y:F2}";

                    sb.AppendLine($"{kit,-26} {Path.GetFileNameWithoutExtension(file),-34} " +
                                  $"{b.size.x,8:F2} {b.size.y,8:F2} {b.size.z,8:F2}  {pivot}");
                }
                Object.DestroyImmediate(instance);
            }
        }

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/kenney-bounds.txt", sb.ToString());
        Debug.Log("[Kenney] bounds written to Logs/kenney-bounds.txt");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
#endif
