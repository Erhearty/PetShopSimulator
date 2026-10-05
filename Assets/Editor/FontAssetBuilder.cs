#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Generates the Nunito TextMesh Pro SDF font asset that <c>UIFactory.Font()</c> loads from
/// Resources, so the UI font is produced from the checked-in TTF instead of by hand.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod FontAssetBuilder.BuildNunito -quit
///
/// Idempotent: when the asset already exists it is rebuilt in place, keeping its GUID so every
/// reference to it survives. Exits the editor with code 1 on failure in batch mode.
/// </summary>
public static class FontAssetBuilder
{
    /// <summary>Log prefix that build.sh greps for.</summary>
    private const string LogTag = "[FontAssetBuilder]";

    /// <summary>The source font, the variable Nunito TTF from google/fonts (OFL).</summary>
    private const string SourceFontPath = "Assets/Fonts/Nunito-Variable.ttf";

    /// <summary>Folder the font asset is written to; must sit under a Resources folder.</summary>
    private const string OutputFolder = "Assets/Resources/Fonts";

    /// <summary>Asset name; matches <c>UIFactory.FontResourcePath</c> ("Fonts/Nunito SDF").</summary>
    private const string AssetName = "Nunito SDF";

    /// <summary>Full project path of the generated font asset.</summary>
    private const string OutputPath = OutputFolder + "/" + AssetName + ".asset";

    /// <summary>Point size glyphs are rasterised at.</summary>
    private const int SamplingPointSize = 90;

    /// <summary>Padding in pixels around each glyph in the atlas (SDF spread).</summary>
    private const int AtlasPadding = 9;

    /// <summary>Width of each atlas texture in pixels.</summary>
    private const int AtlasWidth = 1024;

    /// <summary>Height of each atlas texture in pixels.</summary>
    private const int AtlasHeight = 1024;

    /// <summary>Allow extra atlas textures once the first fills up.</summary>
    private const bool EnableMultiAtlas = true;

    /// <summary>First printable ASCII code point (space).</summary>
    private const int AsciiFirst = 32;

    /// <summary>Last printable ASCII code point (tilde).</summary>
    private const int AsciiLast = 126;

    /// <summary>First Latin-1 Supplement code point (no-break space).</summary>
    private const int Latin1First = 160;

    /// <summary>Last Latin-1 Supplement code point (ÿ).</summary>
    private const int Latin1Last = 255;

    /// <summary>
    /// Typographic extras the UI uses: euro sign, ellipsis, em dash, en dash, curly single and
    /// double quotes, and bullet (€…—–‘’“”•).
    /// </summary>
    private const string ExtraCharacters = "\u20AC\u2026\u2014\u2013\u2018\u2019\u201C\u201D\u2022";

    /// <summary>
    /// Serialized field behind TMP_FontAsset's internal <c>clearDynamicDataOnBuild</c>. TMP copies
    /// the project-wide TMP Settings value into it, and when true TMP wipes the prepopulated
    /// characters and glyphs on editor quit and before every player build.
    /// </summary>
    private const string ClearDynamicDataProperty = "m_ClearDynamicDataOnBuild";

    /// <summary>Exit code reported to build.sh when the build fails.</summary>
    private const int FailureExitCode = 1;

    /// <summary>
    /// Builds (or rebuilds in place) <c>Assets/Resources/Fonts/Nunito SDF.asset</c> from the Nunito
    /// TTF, prepopulated with ASCII, Latin-1 and <see cref="ExtraCharacters"/>, with its atlas
    /// texture(s) and material stored as sub-assets.
    /// </summary>
    public static void BuildNunito()
    {
        AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (font == null)
        {
            Fail($"Could not load the source font at {SourceFontPath}.");
            return;
        }

        var fresh = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
            GlyphRenderMode.SDFAA, AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, EnableMultiAtlas);
        if (fresh == null)
        {
            Fail("TMP_FontAsset.CreateFontAsset returned null.");
            return;
        }
        fresh.name = AssetName;

        string characters = RequiredCharacters();
        if (!fresh.TryAddCharacters(characters, out string missing))
            Debug.LogWarning($"{LogTag} Font has no glyphs for: '{missing}'.");

        if (!EnsureFolder(OutputFolder))
        {
            Fail($"Could not create folder {OutputFolder}.");
            return;
        }

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        TMP_FontAsset saved;
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, OutputPath);
            saved = fresh;
            Debug.Log($"{LogTag} Creating {OutputPath}.");
        }
        else
        {
            RemoveSubAssets(existing);
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = AssetName;
            saved = existing;
            Debug.Log($"{LogTag} Updating {OutputPath} in place (GUID {AssetDatabase.AssetPathToGUID(OutputPath)}).");
        }

        AddSubAssets(saved);
        if (!KeepDynamicData(saved))
        {
            Fail($"Could not find {ClearDynamicDataProperty} on TMP_FontAsset.");
            return;
        }
        saved.ReadFontAssetDefinition();
        EditorUtility.SetDirty(saved);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);

        Verify(characters);
    }

    /// <summary>ASCII 32–126, Latin-1 160–255 and <see cref="ExtraCharacters"/>, as one string.</summary>
    private static string RequiredCharacters()
    {
        var sb = new StringBuilder();
        for (int c = AsciiFirst; c <= AsciiLast; c++) sb.Append((char)c);
        for (int c = Latin1First; c <= Latin1Last; c++) sb.Append((char)c);
        sb.Append(ExtraCharacters);
        return sb.ToString();
    }

    /// <summary>Creates <paramref name="folder"/> and any missing parents; true when it exists afterwards.</summary>
    private static bool EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return true;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent) || !EnsureFolder(parent)) return false;
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        return AssetDatabase.IsValidFolder(folder);
    }

    /// <summary>Removes and destroys every sub-asset (old atlases and material) stored with <paramref name="main"/>.</summary>
    private static void RemoveSubAssets(TMP_FontAsset main)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(OutputPath))
        {
            if (obj == null || obj == main) continue;
            AssetDatabase.RemoveObjectFromAsset(obj);
            Object.DestroyImmediate(obj, true);
        }
    }

    /// <summary>Stores the atlas texture(s) and material of <paramref name="asset"/> inside its asset file.</summary>
    private static void AddSubAssets(TMP_FontAsset asset)
    {
        var added = new HashSet<Object>();
        var atlases = asset.atlasTextures;
        for (int i = 0; atlases != null && i < atlases.Length; i++)
        {
            var atlas = atlases[i];
            if (atlas == null || !added.Add(atlas) || AssetDatabase.Contains(atlas)) continue;
            atlas.name = i == 0 ? $"{AssetName} Atlas" : $"{AssetName} Atlas {i}";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }

        var material = asset.material;
        if (material != null && !AssetDatabase.Contains(material))
        {
            material.name = $"{AssetName} Material";
            AssetDatabase.AddObjectToAsset(material, asset);
        }
    }

    /// <summary>
    /// Turns off clearing of dynamic data on <paramref name="asset"/> so the prepopulated glyphs
    /// survive editor quit and player builds; false when the field does not exist.
    /// </summary>
    private static bool KeepDynamicData(TMP_FontAsset asset)
    {
        var so = new SerializedObject(asset);
        var prop = so.FindProperty(ClearDynamicDataProperty);
        if (prop == null) return false;
        prop.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    /// <summary>Reloads the saved asset and checks every required character made it in.</summary>
    private static void Verify(string characters)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        if (asset == null)
        {
            Fail($"{OutputPath} was not written.");
            return;
        }

        var absent = new StringBuilder();
        foreach (char c in characters)
            if (!asset.HasCharacter(c)) absent.Append(c);
        if (absent.Length > 0)
        {
            Fail($"{OutputPath} is missing characters: '{absent}'.");
            return;
        }

        int atlasCount = asset.atlasTextures?.Length ?? 0;
        Debug.Log($"{LogTag} Wrote {OutputPath}: {asset.characterTable.Count} characters, " +
                  $"{atlasCount} atlas texture(s), GUID {AssetDatabase.AssetPathToGUID(OutputPath)}.");
    }

    /// <summary>Logs <paramref name="message"/> as an error and, in batch mode, exits with a failure code.</summary>
    private static void Fail(string message)
    {
        Debug.LogError($"{LogTag} {message}");
        if (Application.isBatchMode) EditorApplication.Exit(FailureExitCode);
    }
}
#endif
