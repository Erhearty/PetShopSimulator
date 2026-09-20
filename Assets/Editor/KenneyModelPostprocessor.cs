#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the CC0 Kenney kits under Assets/Resources/Kenney.
/// These are static set dressing: no rigs, no animation, no colliders (the game places
/// its own invisible collision), and materials kept inside the imported asset so a
/// Resources.Load at runtime brings its textures with it.
/// </summary>
public class KenneyModelPostprocessor : AssetPostprocessor
{
    private const string Root = "Assets/Resources/Kenney/";

    private bool IsKenney => assetPath.StartsWith(Root);

    private void OnPreprocessModel()
    {
        if (!IsKenney) return;

        var importer = (ModelImporter)assetImporter;

        importer.globalScale           = 1f;
        importer.useFileScale          = true;
        importer.importAnimation       = false;
        importer.importCameras         = false;
        importer.importLights          = false;
        importer.importVisibility      = false;
        importer.animationType         = ModelImporterAnimationType.None;
        importer.addCollider           = false;
        importer.meshCompression       = ModelImporterMeshCompression.Medium;
        importer.isReadable            = false;
        importer.importNormals         = ModelImporterNormals.Import;
        importer.optimizeMeshPolygons  = true;
        importer.optimizeMeshVertices  = true;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation   = ModelImporterMaterialLocation.InPrefab;
        importer.materialName       = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch     = ModelImporterMaterialSearch.Everywhere;
    }

    private void OnPreprocessTexture()
    {
        if (!IsKenney) return;

        var importer = (TextureImporter)assetImporter;
        // The colormap is a palette atlas — filtering across swatches muddies the colours.
        importer.filterMode      = FilterMode.Point;
        importer.mipmapEnabled   = true;
        importer.wrapMode        = TextureWrapMode.Clamp;
        importer.maxTextureSize  = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
#endif
