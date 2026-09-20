#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time project preparation that can run headlessly, so a clone of this repo can go
/// from checkout to a playable build with no manual editor steps.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod ProjectSetup.ImportTMPEssentials
///
/// Note: no -quit. Package import finishes on a later editor tick, so this method exits
/// the editor itself once the import lands (or times out).
/// </summary>
public static class ProjectSetup
{
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const int    TimeoutTicks    = 4000;

    private static int _ticks;

    [MenuItem("PetShop/Setup/Import TMP Essentials")]
    public static void ImportTMPEssentials()
    {
        if (File.Exists(TmpSettingsPath))
        {
            Debug.Log("[ProjectSetup] TMP essential resources already present.");
            Finish(0);
            return;
        }

        Debug.Log("[ProjectSetup] Importing TMP essential resources...");
        AssetDatabase.importPackageCompleted  += OnImported;
        AssetDatabase.importPackageFailed     += OnFailed;
        AssetDatabase.importPackageCancelled  += OnCancelled;

        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);

        if (Application.isBatchMode)
            EditorApplication.update += WaitForImport;
    }

    private static void WaitForImport()
    {
        if (File.Exists(TmpSettingsPath))
        {
            Debug.Log("[ProjectSetup] TMP essential resources imported.");
            Finish(0);
            return;
        }
        if (++_ticks > TimeoutTicks)
        {
            Debug.LogError("[ProjectSetup] Timed out waiting for the TMP import.");
            Finish(1);
        }
    }

    private static void OnImported(string packageName)  => Debug.Log($"[ProjectSetup] Imported '{packageName}'.");
    private static void OnFailed(string p, string err)  => Debug.LogError($"[ProjectSetup] Import of '{p}' failed: {err}");
    private static void OnCancelled(string packageName) => Debug.LogError($"[ProjectSetup] Import of '{packageName}' was cancelled.");

    private static void Finish(int code)
    {
        EditorApplication.update -= WaitForImport;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
#endif
