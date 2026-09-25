#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-gating visual check: compares the PNGs <see cref="SceneShot"/> rendered against the
/// approved baselines and reports the mean absolute RGB difference of each image.
///
///   Unity -batchmode -nographics -projectPath . -executeMethod ScreenshotDiff.Compare
///         -baseline Tests/Baselines/Screenshots -current Screenshots [-tolerance 2]
///
/// Prints "[ScreenshotDiff] name 1.3% OK|WARN" per image and writes an amplified greyscale
/// diff PNG to Logs/screenshot-diff/. Always exits 0: a WARN is for a human to look at, not a
/// build failure. <see cref="Approve"/> copies the current shots over the baselines.
/// </summary>
public static class ScreenshotDiff
{
    private const string BaselineFlag  = "-baseline";
    private const string CurrentFlag   = "-current";
    private const string ToleranceFlag = "-tolerance";

    private const string DefaultBaselineDir = "Tests/Baselines/Screenshots";
    private const string DefaultCurrentDir  = "Screenshots";
    private const string DiffDir            = "Logs/screenshot-diff";
    private const string PngPattern         = "*.png";

    /// <summary>Mean difference (%) above which an image is reported as WARN.</summary>
    private const float DefaultTolerancePercent = 2f;

    /// <summary>Brightens the diff image so small differences are visible.</summary>
    private const float DiffGain = 4f;

    private const int Channels      = 3;
    private const int MaxChannel    = 255;
    private const int PlaceholderPx = 2;
    private const int ExitCompare   = 0;
    private const int ExitApproved  = 0;
    private const int ExitNothingToApprove = 0;

    /// <summary>Compares every baseline PNG with its current counterpart. Always exits 0.</summary>
    public static void Compare()
    {
        string baseline = Arg(BaselineFlag, DefaultBaselineDir);
        string current  = Arg(CurrentFlag, DefaultCurrentDir);
        float tolerance = ParseTolerance(Arg(ToleranceFlag, null));

        int warnings = 0;
        if (!Directory.Exists(baseline))
        {
            Debug.LogWarning($"[ScreenshotDiff] no baselines in {baseline} WARN — run ./build.sh look-approve");
            warnings++;
        }
        else
        {
            Directory.CreateDirectory(DiffDir);
            foreach (string basePath in Directory.GetFiles(baseline, PngPattern))
                if (!TryCompareOne(basePath, current, tolerance)) warnings++;
            warnings += ReportUnbaselined(baseline, current);
        }

        Debug.Log($"[ScreenshotDiff] {warnings} warning(s) at tolerance {tolerance:F1}%");
        EditorApplication.Exit(ExitCompare);
    }

    /// <summary>Copies every current PNG into the baseline folder, overwriting what is there.</summary>
    public static void Approve()
    {
        string baseline = Arg(BaselineFlag, DefaultBaselineDir);
        string current  = Arg(CurrentFlag, DefaultCurrentDir);
        string[] shots  = Directory.Exists(current) ? Directory.GetFiles(current, PngPattern) : new string[0];
        if (shots.Length == 0)
        {
            Debug.LogWarning($"[ScreenshotDiff] no screenshots in {current} to approve — run ./build.sh look");
            EditorApplication.Exit(ExitNothingToApprove);
            return;
        }

        Directory.CreateDirectory(baseline);
        foreach (string shot in shots)
            File.Copy(shot, Path.Combine(baseline, Path.GetFileName(shot)), true);
        Debug.Log($"[ScreenshotDiff] approved {shots.Length} screenshot(s) into {baseline}");
        EditorApplication.Exit(ExitApproved);
    }

    /// <summary>
    /// <see cref="CompareOne"/> for the baseline's counterpart in <paramref name="current"/>, but an
    /// exception only turns that image into an ERROR WARN line, so Compare always reaches its exit.
    /// </summary>
    private static bool TryCompareOne(string basePath, string current, float tolerance)
    {
        string name = Path.GetFileName(basePath);
        try
        {
            return CompareOne(basePath, Path.Combine(current, name), tolerance);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ScreenshotDiff] {name} ERROR WARN ({e.GetType().Name}: {e.Message})");
            return false;
        }
    }

    /// <summary>Compares one image pair, logs the verdict and returns true when it is OK.</summary>
    private static bool CompareOne(string basePath, string currentPath, float tolerance)
    {
        string name = Path.GetFileName(basePath);
        if (!File.Exists(currentPath)) return Warn(name, "missing from current screenshots");

        Texture2D a = Load(basePath), b = Load(currentPath);
        try
        {
            if (a == null || b == null) return Warn(name, "unreadable PNG");
            if (a.width != b.width || a.height != b.height)
                return Warn(name, $"size mismatch {a.width}x{a.height} vs {b.width}x{b.height}");

            float percent = MeanDiffPercent(a, b, Path.Combine(DiffDir, name));
            bool ok = percent <= tolerance;
            Debug.Log($"[ScreenshotDiff] {name} {percent:F1}% {(ok ? "OK" : "WARN")}");
            return ok;
        }
        finally
        {
            Release(a);
            Release(b);
        }
    }

    /// <summary>
    /// Mean absolute RGB difference of two same-sized images, as a percentage of full scale.
    /// Writes the per-pixel difference as a greyscale PNG to <paramref name="diffPath"/>.
    /// </summary>
    private static float MeanDiffPercent(Texture2D a, Texture2D b, string diffPath)
    {
        Color32[] pa = a.GetPixels32(), pb = b.GetPixels32();
        var pd = new Color32[pa.Length];
        double total = 0;
        for (int i = 0; i < pa.Length; i++)
        {
            int d = Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
            total += d;
            byte v = (byte)Mathf.Min(MaxChannel, d * DiffGain / Channels);
            pd[i] = new Color32(v, v, v, (byte)MaxChannel);
        }

        var diff = new Texture2D(a.width, a.height, TextureFormat.RGBA32, false);
        diff.SetPixels32(pd);
        diff.Apply();
        File.WriteAllBytes(diffPath, diff.EncodeToPNG());
        Release(diff);
        return pa.Length == 0 ? 0f : (float)(100.0 * total / ((double)pa.Length * Channels * MaxChannel));
    }

    /// <summary>Warns about current PNGs that have no baseline yet; returns how many.</summary>
    private static int ReportUnbaselined(string baseline, string current)
    {
        if (!Directory.Exists(current)) return 0;
        int count = 0;
        foreach (string shot in Directory.GetFiles(current, PngPattern))
        {
            string name = Path.GetFileName(shot);
            if (File.Exists(Path.Combine(baseline, name))) continue;
            Warn(name, "has no baseline");
            count++;
        }
        return count;
    }

    /// <summary>Logs a WARN line for <paramref name="name"/> and returns false.</summary>
    private static bool Warn(string name, string reason)
    {
        Debug.LogWarning($"[ScreenshotDiff] {name} n/a WARN ({reason})");
        return false;
    }

    /// <summary>Loads a PNG into a readable texture, or returns null when it cannot be decoded.</summary>
    private static Texture2D Load(string path)
    {
        var tex = new Texture2D(PlaceholderPx, PlaceholderPx, TextureFormat.RGBA32, false);
        if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
        Release(tex);
        return null;
    }

    /// <summary>Destroys a texture created by this class, if there is one.</summary>
    private static void Release(Texture2D tex)
    {
        if (tex != null) Object.DestroyImmediate(tex);
    }

    /// <summary>The value following <paramref name="flag"/> on the command line, or <paramref name="fallback"/>.</summary>
    private static string Arg(string flag, string fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    /// <summary>Parses the -tolerance value, falling back to the default when absent or invalid.</summary>
    private static float ParseTolerance(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct) && pct >= 0f
            ? pct
            : DefaultTolerancePercent;
    }
}
#endif
