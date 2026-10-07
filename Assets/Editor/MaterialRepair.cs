#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repoints materials that reference a shader this project does not have onto the Built-in
/// pipeline's Standard shader, keeping their textures and colours.
///
/// Several Asset Store packs are authored for the Universal Render Pipeline. This project
/// is Built-in, so URP/Lit (guid 933532a4...) is simply absent and Unity falls back to the
/// magenta error shader — that is the "purple furniture". Switching the shader by hand
/// would lose the maps, because URP and Standard use different property names, so the old
/// values are read straight out of the material's serialised properties (which survive even
/// when the shader is missing) and written back under the Standard names.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod MaterialRepair.Repair -quit
/// </summary>
public static class MaterialRepair
{
    // URP/HDRP name -> Standard name
    private static readonly (string from, string to)[] TextureMap =
    {
        ("_BaseMap",           "_MainTex"),
        ("_AlbedoMap",         "_MainTex"),
        ("_BumpMap",           "_BumpMap"),
        ("_NormalInflow",      "_BumpMap"),
        ("_EmissionMap",       "_EmissionMap"),
        ("_OcclusionMap",      "_OcclusionMap"),
        ("_MetallicGlossMap",  "_MetallicGlossMap"),
    };

    private static readonly (string from, string to)[] ColorMap =
    {
        ("_BaseColor",    "_Color"),
        ("_Tint",         "_Color"),
        ("_EmissionColor","_EmissionColor"),
    };

    [MenuItem("PetShop/Setup/Repair Materials")]
    public static void Repair()
    {
        var standard = Shader.Find("Standard");
        if (standard == null)
        {
            Debug.LogError("[Materials] Standard shader not found.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // Before any material is loaded: loading one whose _MainTex is a cubemap logs
        // "Error assigning CUBE texture to 2D texture property _MainTex".
        _cubeCleaned.Clear();
        ClearCubemapMainTextures();

        var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int repaired = 0, checkedCount = 0;
        var byPack = new Dictionary<string, int>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            checkedCount++;
            // Tracked materials outside the re-extracted packs must never be dirtied and saved.
            if (PackFolders.Any(dir => path.StartsWith(dir + "/")))
                ClearCubemapMainTexture(mat, path);

            if (!NeedsRepair(mat)) continue;

            // Read the old values before the shader swap discards the unknown properties.
            var so       = new SerializedObject(mat);
            var textures = ReadTextures(so);
            var colors   = ReadColors(so);
            var floats   = ReadFloats(so);

            mat.shader = standard;

            foreach (var (from, to) in TextureMap)
                if (textures.TryGetValue(from, out var tex) && tex != null
                    && tex.dimension == UnityEngine.Rendering.TextureDimension.Tex2D && mat.HasProperty(to))
                    mat.SetTexture(to, tex);

            foreach (var (from, to) in ColorMap)
                if (colors.TryGetValue(from, out var c) && mat.HasProperty(to))
                    mat.SetColor(to, c);

            // Default to non-metallic. Left at Standard's default with a high smoothness, a
            // repaired material mirrors the sky and every model comes out sky-blue.
            float metallic = 0f;
            if (floats.TryGetValue("_Metallic", out float m)) metallic = m;
            else if (floats.TryGetValue("_MetMult", out float mm)) metallic = mm * 0.2f;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));

            // URP stores smoothness; Standard calls it glossiness. Roughness is its inverse.
            if (floats.TryGetValue("_Smoothness", out float smooth))
                mat.SetFloat("_Glossiness", Mathf.Clamp01(smooth));
            else if (floats.TryGetValue("_RoughMult", out float rough))
                mat.SetFloat("_Glossiness", Mathf.Clamp01(1f - rough));
            else
                mat.SetFloat("_Glossiness", 0.25f);

            if (textures.TryGetValue("_BumpMap", out var bump) && bump != null ||
                textures.TryGetValue("_NormalInflow", out var bump2) && bump2 != null)
                mat.EnableKeyword("_NORMALMAP");

            // Carry transparency across: URP marks it with _Surface = 1.
            if (floats.TryGetValue("_Surface", out float surface) && surface > 0.5f)
                MakeTransparent(mat);

            EditorUtility.SetDirty(mat);
            repaired++;

            string pack = path.Split('/').Skip(1).FirstOrDefault() ?? "?";
            byPack[pack] = byPack.TryGetValue(pack, out int n) ? n + 1 : 1;
        }

        int tuned = TuneImportedMaterials();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Materials] cleared a cubemap _MainTex from {_cubeCleaned.Count} material(s)" +
                  (_cubeCleaned.Count > 0 ? ": " + string.Join(", ", _cubeCleaned.Select(Path.GetFileName)) : "."));
        if (tuned > 0) Debug.Log($"[Materials] toned down {tuned} over-shiny pack material(s).");
        foreach (var kv in byPack.OrderByDescending(k => k.Value))
            Debug.Log($"[Materials] repaired {kv.Value,3} in {kv.Key}");
        Debug.Log($"[Materials] {repaired} of {checkedCount} materials repointed to Standard.");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// A material is broken if its shader is missing, is the error shader, targets another
    /// render pipeline, or is present but fails to compile.
    ///
    /// That last case is the subtle one: a pack can ship its own .shader file that only
    /// builds against URP. The asset exists, so the name check passes, but it renders
    /// magenta all the same — the animal pack's ShaderMaster is exactly this.
    /// </summary>
    private static bool NeedsRepair(Material mat)
    {
        if (mat.shader == null) return true;

        string n = mat.shader.name;
        if (n == "Hidden/InternalErrorShader"
            || n.StartsWith("Universal Render Pipeline/")
            || n.StartsWith("HDRP/")
            || n.Contains("Shader Graphs/")) return true;

        if (ShaderUtil.ShaderHasError(mat.shader)) return true;

        return TargetsAnotherPipeline(mat.shader);
    }

    /// <summary>
    /// True when a project-authored shader is written for URP/HDRP.
    ///
    /// Such a shader compiles without error — Unity reports none — but its subshader is
    /// tagged for a pipeline that is not in use, so nothing matches and it renders magenta.
    /// The animal pack's ShaderMaster is exactly this: valid HLSL, tagged
    /// "RenderPipeline" = "UniversalPipeline", magenta in Built-in. The tag is the only
    /// reliable signal, so the source is read to find it.
    /// </summary>
    private static bool TargetsAnotherPipeline(Shader shader)
    {
        string path = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || !path.EndsWith(".shader"))
            return false;

        if (_pipelineCache.TryGetValue(path, out bool cached)) return cached;

        bool foreign = false;
        try
        {
            string source = System.IO.File.ReadAllText(path);
            foreign = source.Contains("\"UniversalPipeline\"")
                   || source.Contains("\"HDRenderPipeline\"")
                   || source.Contains("com.unity.render-pipelines.universal")
                   || source.Contains("com.unity.render-pipelines.high-definition");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Materials] could not read {path}: {e.Message}");
        }

        _pipelineCache[path] = foreign;
        if (foreign) Debug.Log($"[Materials] {System.IO.Path.GetFileName(path)} targets another render pipeline.");
        return foreign;
    }

    private static readonly Dictionary<string, bool> _pipelineCache = new();

    /// <summary>Paths of the materials whose cubemap _MainTex was cleared this run.</summary>
    private static readonly HashSet<string> _cubeCleaned = new();

    /// <summary>
    /// Safety net for a material already in memory with a cubemap _MainTex (the on-disk pass in
    /// <see cref="ClearCubemapMainTextures"/> cannot reach an instance loaded before it ran):
    /// clears it through the serialised properties, which - unlike Material.HasProperty and
    /// friends - do not apply the textures to the shader and so log no error.
    /// </summary>
    private static void ClearCubemapMainTexture(Material mat, string path)
    {
        var so = new SerializedObject(mat);
        var array = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (array == null) return;

        bool changed = false;
        for (int i = 0; i < array.arraySize; i++)
        {
            var entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue != "_MainTex") continue;
            var texProp = entry.FindPropertyRelative("second.m_Texture");
            if (texProp.objectReferenceValue is Texture tex
                && tex.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                texProp.objectReferenceValue = null;
                changed = true;
            }
        }
        if (!changed) return;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mat);
        _cubeCleaned.Add(path);
    }

    /// <summary>
    /// The texture reference under a material's _MainTex entry in m_TexEnvs, in the current
    /// ("- _MainTex:") and the legacy ("- first: name: _MainTex / second:") serialisation.
    /// Group "ref" is the {fileID, guid, type} reference, group "guid" its GUID.
    /// </summary>
    private static readonly Regex MainTexReference = new(
        @"(?<head>(?:-[ \t]+_MainTex:|name:[ \t]+_MainTex[ \t]*\r?\n[ \t]*second:)[ \t]*\r?\n[ \t]*m_Texture:[ \t]*)" +
        @"(?<ref>\{fileID:[ \t]*-?\d+,[ \t]*guid:[ \t]*(?<guid>[0-9a-fA-F]{32}),[ \t]*type:[ \t]*\d+\})");

    /// <summary>
    /// Clears the _MainTex entry of every text-serialised .mat under Assets that points at a
    /// cubemap (a texture imported with the Cube shape, or a .cubemap asset), by editing the YAML
    /// on disk so the material is never loaded with it. Pack folders are re-extracted by
    /// ./build.sh assets, so this has to happen here rather than in the assets themselves.
    /// Returns the number of materials changed.
    /// </summary>
    private static int ClearCubemapMainTextures()
    {
        var cubeByGuid = new Dictionary<string, bool>();
        bool IsCubemap(string guid)
        {
            if (cubeByGuid.TryGetValue(guid, out bool known)) return known;
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            bool cube = !string.IsNullOrEmpty(texPath)
                     && typeof(Cubemap).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(texPath));
            cubeByGuid[guid] = cube;
            return cube;
        }

        // Only the re-extracted pack folders: tracked materials elsewhere (e.g. the baked scene's)
        // must never be rewritten, or they lose textures that are 2D in a fresh checkout.
        var changed = new List<string>();
        var packFiles = PackFolders.Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.mat", SearchOption.AllDirectories));
        foreach (string file in packFiles)
        {
            string path = file.Replace('\\', '/');

            string yaml;
            try { yaml = File.ReadAllText(path); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Materials] could not read {path}: {e.Message}");
                continue;
            }
            if (!yaml.StartsWith("%YAML")) continue; // binary-serialised; nothing to edit

            string fixedYaml = MainTexReference.Replace(yaml, m =>
                IsCubemap(m.Groups["guid"].Value.ToLowerInvariant())
                    ? m.Groups["head"].Value + "{fileID: 0}"
                    : m.Value);
            if (fixedYaml == yaml) continue;

            try { File.WriteAllText(path, fixedYaml); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Materials] could not write {path}: {e.Message}");
                continue;
            }
            changed.Add(path);
            _cubeCleaned.Add(path);
        }

        foreach (string path in changed)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return changed.Count;
    }

    /// <summary>
    /// Imported packs are authored for physically-based pipelines and often arrive metallic
    /// and glossy. Under Built-in with a sky ambient that makes models mirror the sky and
    /// come out uniformly blue, which reads as a texture fault rather than a material one.
    /// Flatten anything that is not deliberately metallic.
    /// </summary>
    private static readonly string[] PackFolders =
    {
        "Assets/CuteMagic_CubeAnimals_Free", "Assets/DenysAlmaral",
        "Assets/SimplePoly City - Low Poly Assets", "Assets/LowpolyStreetPack",
        "Assets/Low-Poly Furniture Kit - Stylized Wooden Set", "Assets/SimpleNaturePack",
        "Assets/100 People - Animated Characters Pack", "Assets/Resources/Packs",
    };

    private static int TuneImportedMaterials()
    {
        var existing = PackFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (existing.Length == 0) return 0;

        int tuned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", existing))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            ClearCubemapMainTexture(mat, path);
            if (mat.shader == null || mat.shader.name != "Standard") continue;

            bool changed = false;
            if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > 0.05f)
            { mat.SetFloat("_Metallic", 0f); changed = true; }

            if (mat.HasProperty("_Glossiness") && mat.GetFloat("_Glossiness") > 0.3f)
            { mat.SetFloat("_Glossiness", 0.2f); changed = true; }

            if (!changed) continue;
            EditorUtility.SetDirty(mat);
            tuned++;
        }
        return tuned;
    }

    private static Dictionary<string, Texture> ReadTextures(SerializedObject so)
    {
        var result = new Dictionary<string, Texture>();
        var array  = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (array == null) return result;

        for (int i = 0; i < array.arraySize; i++)
        {
            var entry = array.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("first").stringValue;
            var tex = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (!string.IsNullOrEmpty(key)) result[key] = tex;
        }
        return result;
    }

    private static Dictionary<string, Color> ReadColors(SerializedObject so)
    {
        var result = new Dictionary<string, Color>();
        var array  = so.FindProperty("m_SavedProperties.m_Colors");
        if (array == null) return result;

        for (int i = 0; i < array.arraySize; i++)
        {
            var entry = array.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("first").stringValue;
            if (!string.IsNullOrEmpty(key))
                result[key] = entry.FindPropertyRelative("second").colorValue;
        }
        return result;
    }

    private static Dictionary<string, float> ReadFloats(SerializedObject so)
    {
        var result = new Dictionary<string, float>();
        var array  = so.FindProperty("m_SavedProperties.m_Floats");
        if (array == null) return result;

        for (int i = 0; i < array.arraySize; i++)
        {
            var entry = array.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("first").stringValue;
            if (!string.IsNullOrEmpty(key))
                result[key] = entry.FindPropertyRelative("second").floatValue;
        }
        return result;
    }

    private static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
#endif
