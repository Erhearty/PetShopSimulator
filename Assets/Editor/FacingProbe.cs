#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using PetShop.Core;

/// <summary>
/// Works out which way a building model faces, without anyone having to look at it.
///
/// Shopfront buildings put their glazing on the front, so the centroid of the triangles
/// using a window/glass material, measured against the building's own centre, points at
/// the front. The answer feeds StreetGenerator.BuildingFacing.
/// </summary>
public static class FacingProbe
{
    [MenuItem("PetShop/Report/Probe Building Facing")]
    public static void Probe()
    {
        string[] models =
        {
            "Packs/City/Buildings/Building_Bakery",
            "Packs/City/Buildings/Building_Books Shop",
            "Packs/City/Buildings/Building_Coffee Shop",
            "Packs/City/Buildings/Building_Gift Shop",
            "Packs/City/Buildings/Building_Pizza",
            "Packs/City/Buildings/Building_Clothing",
        };

        foreach (string path in models)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[Facing] missing {path}"); continue; }

            var instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;

            Bounds whole = ModelLibrary.WorldBounds(instance);
            Vector3 glassSum = Vector3.zero;
            int glassCount = 0;
            string matched = "";

            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var rend = filter.GetComponent<MeshRenderer>();
                if (mesh == null || rend == null) continue;

                var verts = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount && sub < rend.sharedMaterials.Length; sub++)
                {
                    var mat = rend.sharedMaterials[sub];
                    if (mat == null) continue;
                    string n = mat.name.ToLowerInvariant();
                    if (!(n.Contains("window") || n.Contains("glass") || n.Contains("door"))) continue;

                    matched = mat.name;
                    foreach (int idx in mesh.GetTriangles(sub))
                    {
                        glassSum += filter.transform.TransformPoint(verts[idx]);
                        glassCount++;
                    }
                }
            }

            if (glassCount == 0)
            {
                Debug.Log($"[Facing] {System.IO.Path.GetFileName(path),-28} no window material found " +
                          $"(materials: {string.Join(", ", instance.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().Take(6))})");
            }
            else
            {
                Vector3 offset = glassSum / glassCount - whole.center;
                string dir = Mathf.Abs(offset.z) > Mathf.Abs(offset.x)
                    ? (offset.z > 0 ? "+Z (front faces +Z, use yaw 0)" : "-Z (front faces -Z, use yaw 180)")
                    : (offset.x > 0 ? "+X (use yaw 270)" : "-X (use yaw 90)");
                Debug.Log($"[Facing] {System.IO.Path.GetFileName(path),-28} glass '{matched}' " +
                          $"offset ({offset.x,6:F2},{offset.y,6:F2},{offset.z,6:F2}) -> {dir}");
            }

            Object.DestroyImmediate(instance);
        }

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
#endif
