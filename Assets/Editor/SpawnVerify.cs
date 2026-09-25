#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PetShop.Core;

/// <summary>
/// Spawns key models through the real runtime path and reports the dimensions they actually
/// end up with. This is the substitute for looking at the game: a road tile that comes out
/// 10 m tall instead of 10 m wide, or a character 0.4 m tall, shows up here.
///
/// The asset packs are not in the repository, so a clone with none of them installed is
/// normal: that run logs "[Verify] SKIPPED" and exits 0. Some packs present and others
/// missing is a broken install and fails. Pass <c>-requirepacks</c> to fail on any missing
/// model.
/// </summary>
public static class SpawnVerify
{
    /// <summary>Command-line flag that turns every missing model into a failure.</summary>
    private const string RequirePacksFlag = "-requirepacks";

    /// <summary>Allowed size error on the fitted axis, as a fraction of the requested size.</summary>
    private const float SizeTolerance = 0.02f;

    /// <summary>How far (m) a model's base may sit off Y = 0 and still count as grounded.</summary>
    private const float GroundTolerance = 0.05f;

    /// <summary>Batch-mode exit code when every check held (or the run was skipped).</summary>
    private const int ExitOk = 0;

    /// <summary>Batch-mode exit code when any check failed.</summary>
    private const int ExitFailed = 1;

    private static readonly (string path, ModelLibrary.Fit fit, float size, string expect)[] Cases =
    {
        ("Packs/Street/Roads/Streets/Road_Streight",              ModelLibrary.Fit.Width,  10f, "flat: Y smallest"),
        ("Packs/Street/Roads/Streets/Road_Crosswalk",             ModelLibrary.Fit.Width,  10f, "flat: Y smallest"),
        ("Packs/Street/StreetProps/TraficLights/LampPost_A",      ModelLibrary.Fit.Height,  6.5f, "tall: Y biggest"),
        ("Packs/Street/StreetProps/Bench/Bench_A",                ModelLibrary.Fit.Height,  1.0f, "low"),
        ("Packs/Street/Foliage/Trees/Tree_A_V01",                 ModelLibrary.Fit.Height,  6f,  "tall: Y biggest"),
        ("Packs/City/Buildings/Building_Bakery",                  ModelLibrary.Fit.Height,  9f,  "Y=9"),
        ("Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01",
                                                                  ModelLibrary.Fit.Depth,  4.5f, "Z=4.5, Y smallest"),
        ("Packs/People/body_root_1",                              ModelLibrary.Fit.Height, 1.78f, "Y=1.78"),
        ("Packs/Furniture/Cupboards/Cupboards",                   ModelLibrary.Fit.Height, 1.1f, "Y=1.1"),
        ("Packs/Furniture/Book Shelve/BookShelve",                ModelLibrary.Fit.Height, 2.0f, "Y=2.0"),
        ("Packs/Nature/Tree_01",                                  ModelLibrary.Fit.Height, 6f,  "tall"),
        ("Packs/Animals/Fox",                                     ModelLibrary.Fit.Height, 0.7f, "Y=0.7"),
        ("Packs/Animals/Dog_001",                                 ModelLibrary.Fit.Height, 0.7f, "Y=0.7"),
        ("Packs/Animals/Kitty_001",                               ModelLibrary.Fit.Height, 0.5f, "Y=0.5"),
        ("Packs/Animals/Pinguin_001",                             ModelLibrary.Fit.Height, 0.8f, "Y=0.8"),
        ("Packs/Animals/Horse_001",                               ModelLibrary.Fit.Height, 1.6f, "Y=1.6"),
        ("Packs/CityPeople/city/casual_Female_G",                 ModelLibrary.Fit.Height, 1.78f, "Y=1.78"),
        ("Packs/CityPeople/professions/Doctor_Male_B",            ModelLibrary.Fit.Height, 1.78f, "Y=1.78"),
    };

    /// <summary>
    /// Checks every case, then exits the editor in batch mode: 0 when clean or when no pack is
    /// installed at all, 1 on any size/grounding problem, a partial install, or — with
    /// <c>-requirepacks</c> — any missing model.
    /// </summary>
    [MenuItem("PetShop/Report/Verify Spawned Models")]
    public static void Verify()
    {
        int bad = 0, missing = 0;
        foreach (var (path, fit, size, expect) in Cases)
        {
            if (!ModelLibrary.Has(path))
            {
                Debug.LogWarning($"[Verify] MISSING  {path}");
                missing++;
                continue;
            }
            bad += CheckCase(path, fit, size, expect);
        }

        int exitCode = Conclude(bad, missing);
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }

    /// <summary>Spawns one model, logs its measured bounds and returns how many problems it has.</summary>
    private static int CheckCase(string path, ModelLibrary.Fit fit, float size, string expect)
    {
        var go = ModelLibrary.Spawn(path, null, Vector3.zero, 0f, fit, size);
        if (go == null) { Debug.LogWarning($"[Verify] SPAWN FAILED  {path}"); return 1; }

        Bounds b = ModelLibrary.WorldBounds(go);
        string flag = "";
        int bad = 0;

        // Did the requested axis actually come out at the requested size?
        float got = MeasuredAxis(b, fit);
        if (Mathf.Abs(got - size) > size * SizeTolerance) { flag += "  <<< WRONG SIZE"; bad++; }

        // Anything standing on the ground should have its base at Y = 0.
        if (Mathf.Abs(b.min.y) > GroundTolerance) { flag += $"  <<< NOT GROUNDED (min.y={b.min.y:F2})"; bad++; }

        Debug.Log($"[Verify] {System.IO.Path.GetFileName(path),-34} " +
                  $"{b.size.x,6:F2} x {b.size.y,6:F2} x {b.size.z,6:F2}   " +
                  $"base.y={b.min.y,6:F2}   expect {expect}{flag}");

        Object.DestroyImmediate(go);
        return bad;
    }

    /// <summary>The extent of <paramref name="b"/> along the axis <paramref name="fit"/> sizes.</summary>
    private static float MeasuredAxis(Bounds b, ModelLibrary.Fit fit) => fit switch
    {
        ModelLibrary.Fit.Width  => b.size.x,
        ModelLibrary.Fit.Height => b.size.y,
        ModelLibrary.Fit.Depth  => b.size.z,
        _                       => 0f,
    };

    /// <summary>Logs the verdict and returns the exit code for it.</summary>
    private static int Conclude(int bad, int missing)
    {
        bool requirePacks = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), RequirePacksFlag) >= 0;
        if (missing == Cases.Length && !requirePacks)
        {
            Debug.Log($"[Verify] SKIPPED — no asset packs installed ({Cases.Length} models)");
            return ExitOk;
        }

        if (missing > 0)
            Debug.LogWarning($"[Verify] {missing} of {Cases.Length} model(s) missing — " +
                             (requirePacks ? $"{RequirePacksFlag} was given" : "partial pack install"));

        int problems = bad + missing;
        Debug.Log($"[Verify] {Cases.Length} models checked, {problems} problem(s).");
        return problems == 0 ? ExitOk : ExitFailed;
    }
}
#endif
