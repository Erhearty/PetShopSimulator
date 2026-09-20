#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PetShop.Core;

/// <summary>
/// Spawns key models through the real runtime path and reports the dimensions they actually
/// end up with. This is the substitute for looking at the game: a road tile that comes out
/// 10 m tall instead of 10 m wide, or a character 0.4 m tall, shows up here.
/// </summary>
public static class SpawnVerify
{
    [MenuItem("PetShop/Report/Verify Spawned Models")]
    public static void Verify()
    {
        var cases = new (string path, ModelLibrary.Fit fit, float size, string expect)[]
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

        int bad = 0;
        foreach (var (path, fit, size, expect) in cases)
        {
            if (!ModelLibrary.Has(path))
            {
                Debug.LogWarning($"[Verify] MISSING  {path}");
                bad++;
                continue;
            }

            var go = ModelLibrary.Spawn(path, null, Vector3.zero, 0f, fit, size);
            if (go == null) { Debug.LogWarning($"[Verify] SPAWN FAILED  {path}"); bad++; continue; }

            Bounds b = ModelLibrary.WorldBounds(go);
            string flag = "";

            // Did the requested axis actually come out at the requested size?
            float got = fit switch
            {
                ModelLibrary.Fit.Width  => b.size.x,
                ModelLibrary.Fit.Height => b.size.y,
                ModelLibrary.Fit.Depth  => b.size.z,
                _                       => 0f,
            };
            if (Mathf.Abs(got - size) > size * 0.02f) { flag += "  <<< WRONG SIZE"; bad++; }

            // Anything standing on the ground should have its base at Y = 0.
            if (Mathf.Abs(b.min.y) > 0.05f) { flag += $"  <<< NOT GROUNDED (min.y={b.min.y:F2})"; bad++; }

            Debug.Log($"[Verify] {System.IO.Path.GetFileName(path),-34} " +
                      $"{b.size.x,6:F2} x {b.size.y,6:F2} x {b.size.z,6:F2}   " +
                      $"base.y={b.min.y,6:F2}   expect {expect}{flag}");

            Object.DestroyImmediate(go);
        }

        Debug.Log($"[Verify] {cases.Length} models checked, {bad} problem(s).");
        if (Application.isBatchMode) EditorApplication.Exit(bad == 0 ? 0 : 1);
    }
}
#endif
