#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Asset Store packages that have already been downloaded through the Package
/// Manager, from Unity's local download cache.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod AssetStoreImporter.ImportAll
///
/// Note: no -quit. AssetDatabase.ImportPackage completes on a later editor tick, so this
/// waits for each package and exits the editor itself.
///
/// Downloading is deliberately NOT automated: Asset Store packages are tied to a Unity
/// account and only the Package Manager's My Assets tab can fetch them.
/// </summary>
public static class AssetStoreImporter
{
    /// <summary>Packages we expect, and the folder each drops into under Assets/.</summary>
    private static readonly (string match, string label)[] Wanted =
    {
        ("100 People",            "animated customer characters"),
        ("SimplePoly City",       "city buildings, vehicles, roads"),
        ("Low Poly Street Pack",  "modular street pieces"),
        ("Furniture Kit",         "stylised shop interior"),
        ("Simple Nature Pack",    "trees and planting"),
    };

    private static string _current;
    private static int    _ticks;

    /// <summary>
    /// Where Package Manager puts downloaded Asset Store packages. Unity lets this be
    /// relocated, so an explicit override wins over the per-platform default:
    ///   ASSETSTORE_CACHE_PATH env var, or -assetCache &lt;path&gt; on the command line.
    /// </summary>
    public static string CacheRoot
    {
        get
        {
            string over = Environment.GetEnvironmentVariable("ASSETSTORE_CACHE_PATH");
            if (string.IsNullOrEmpty(over))
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-assetCache") { over = args[i + 1]; break; }
            }
            if (!string.IsNullOrEmpty(over) && Directory.Exists(over)) return over;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Application.platform switch
            {
                RuntimePlatform.LinuxEditor   => Path.Combine(home, ".local/share/unity3d/Asset Store-5.x"),
                RuntimePlatform.OSXEditor     => Path.Combine(home, "Library/Unity/Asset Store-5.x"),
                _                             => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Unity/Asset Store-5.x"),
            };
        }
    }

    [MenuItem("PetShop/Setup/Report Downloaded Asset Store Packages")]
    public static void Report()
    {
        var packages = FindPackages();
        if (packages.Count == 0)
        {
            Debug.LogWarning($"[AssetStore] Nothing downloaded yet. Looked in:\n  {CacheRoot}\n" +
                             "Open the project in the Unity Editor, sign in to the same Unity ID, then\n" +
                             "Window → Package Manager → My Assets → Download on each package.\n" +
                             "If your cache lives elsewhere, set ASSETSTORE_CACHE_PATH or pass -assetCache <path>.");
        }
        else
        {
            Debug.Log($"[AssetStore] {packages.Count} package(s) in the cache:");
            foreach (string p in packages)
                Debug.Log($"[AssetStore]   {Path.GetFileNameWithoutExtension(p)}  " +
                          $"({new FileInfo(p).Length / (1024 * 1024)} MB)");
        }

        foreach (var (match, label) in Wanted)
            if (!packages.Any(p => p.Contains(match, StringComparison.OrdinalIgnoreCase)))
                Debug.LogWarning($"[AssetStore] still missing: {match}  — {label}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// Imports exactly ONE not-yet-present package per editor run, then exits.
    ///
    /// Importing more than one in a single run does not work: any package containing C#
    /// (the characters pack does) triggers a recompile and domain reload, which wipes the
    /// static state tracking the queue and leaves batch mode hanging forever. build.sh
    /// therefore calls this repeatedly until it reports nothing left.
    /// Exit code 0 = imported one, 2 = nothing left to do, 1 = error.
    /// </summary>
    [MenuItem("PetShop/Setup/Import Next Asset Store Package")]
    public static void ImportAll()
    {
        var pending = FindPackages().Where(p => !AlreadyPresent(p)).ToList();

        if (pending.Count == 0)
        {
            Debug.Log("[AssetStore] nothing left to import.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        _current = pending[0];
        _ticks   = 0;

        AssetDatabase.importPackageCompleted += OnCompleted;
        AssetDatabase.importPackageFailed    += OnFailed;
        AssetDatabase.importPackageCancelled += OnCancelled;
        if (Application.isBatchMode) EditorApplication.update += Pump;

        Debug.Log($"[AssetStore] importing {Path.GetFileNameWithoutExtension(_current)} " +
                  $"({pending.Count} pending)...");
        AssetDatabase.ImportPackage(_current, interactive: false);
    }

    /// <summary>
    /// Has this package already been unpacked? Checked by reading the first Assets/ path
    /// out of the package and seeing whether its top-level folder exists.
    /// </summary>
    private static bool AlreadyPresent(string packagePath)
    {
        string root = TopLevelFolder(packagePath);
        return root != null && Directory.Exists(Path.Combine("Assets", root));
    }

    private static string TopLevelFolder(string packagePath)
    {
        try
        {
            using var stream = File.OpenRead(packagePath);
            using var gzip   = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            var buffer = new byte[512];
            // Walk the tar headers looking for a "pathname" entry and read its payload.
            while (ReadExactly(gzip, buffer, 512))
            {
                string name = System.Text.Encoding.ASCII.GetString(buffer, 0, 100).TrimEnd('\0', ' ');
                if (string.IsNullOrEmpty(name)) break;

                string sizeField = System.Text.Encoding.ASCII.GetString(buffer, 124, 12).Trim('\0', ' ');
                long size = string.IsNullOrEmpty(sizeField) ? 0 : Convert.ToInt64(sizeField, 8);
                long padded = (size + 511) / 512 * 512;

                if (name.EndsWith("pathname"))
                {
                    var payload = new byte[padded];
                    if (!ReadExactly(gzip, payload, (int)padded)) break;
                    string path = System.Text.Encoding.UTF8.GetString(payload, 0, (int)size).Split('\n')[0].Trim();
                    if (path.StartsWith("Assets/"))
                    {
                        var parts = path.Substring("Assets/".Length).Split('/');
                        return parts.Length > 0 ? parts[0] : null;
                    }
                }
                else
                {
                    var skip = new byte[padded];
                    if (padded > 0 && !ReadExactly(gzip, skip, (int)padded)) break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetStore] could not inspect {Path.GetFileName(packagePath)}: {e.Message}");
        }
        return null;
    }

    private static bool ReadExactly(System.IO.Stream stream, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    private static List<string> FindPackages()
    {
        if (!Directory.Exists(CacheRoot)) return new List<string>();
        return Directory.GetFiles(CacheRoot, "*.unitypackage", SearchOption.AllDirectories)
                        .OrderBy(p => p)
                        .ToList();
    }

    // Import completion is asynchronous; this gives each package a generous ceiling so a
    // stuck import cannot hang a CI run forever.
    private static void Pump()
    {
        if (++_ticks > 20000)
        {
            Debug.LogError($"[AssetStore] timed out importing {Path.GetFileName(_current)}");
            Finish(1);
        }
    }

    private static void OnCompleted(string packageName)
    {
        Debug.Log($"[AssetStore] imported '{packageName}'.");
        Finish(0);
    }

    private static void OnFailed(string packageName, string error)
    {
        Debug.LogError($"[AssetStore] '{packageName}' failed: {error}");
        Finish(1);
    }

    private static void OnCancelled(string packageName)
    {
        Debug.LogWarning($"[AssetStore] '{packageName}' was cancelled.");
        Finish(1);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Pump;
        AssetDatabase.importPackageCompleted -= OnCompleted;
        AssetDatabase.importPackageFailed    -= OnFailed;
        AssetDatabase.importPackageCancelled -= OnCancelled;

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();

        Debug.Log("[AssetStore] done.");
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
#endif
