#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PetShop.Core;
using PetShop.Shop;
using Object = UnityEngine.Object;

/// <summary>
/// ONE-OFF helpers for the player-placed-furniture plan. Delete this file once both have run.
///   Unity -batchmode -nographics -projectPath . -executeMethod OneOffDecorExtract.Extract
///   Unity -batchmode -nographics -projectPath . -executeMethod OneOffDecorExtract.EmptyShop
/// </summary>
public static class OneOffDecorExtract
{
    private const string ScenePath   = "Assets/Scenes/MainScene.unity";
    private const string DecorFolder = "Assets/Prefabs/Decor";
    private const string CatalogPath = "Assets/Prefabs/Furniture/FurniturePrefabs.asset";
    private const string Tag         = "[DecorExtract]";
    private const string AllSuffix   = "*";

    private const string Dressing     = "Shop/Dressing";
    private const string YardDressing = "Shop/YardDressing";

    /// <summary>
    /// Decor id, the parent path its parts sit under, and the names of the parent's direct
    /// children that make it up. "Name*" takes every direct child called Name; a plain name
    /// takes the first one. Several parts are grouped under one new root.
    /// </summary>
    private static readonly (string Id, string Parent, string[] Parts)[] Sources =
    {
        (BuildCatalog.DecorPlantPot,    YardDressing, new[] { "PlantPot_A" }),
        (BuildCatalog.DecorFlowers,     YardDressing, new[] { "Flowers_01" }),
        (BuildCatalog.DecorBench,       YardDressing, new[] { "Bench_A" }),
        (BuildCatalog.DecorRugRound,    Dressing,     new[] { "rugRound" }),
        (BuildCatalog.DecorBookshelf,   Dressing,     new[] { "BookShelve" }),
        (BuildCatalog.DecorBoxClosed,   Dressing,     new[] { "cardboardBoxClosed" }),
        (BuildCatalog.DecorBoxOpen,     Dressing,     new[] { "cardboardBoxOpen" }),
        (BuildCatalog.DecorDustbin,     Dressing,     new[] { "Props_Dustbin" }),
        (BuildCatalog.DecorPallet,      Dressing,     new[] { "Pallet" }),
        (BuildCatalog.DecorFeedBag,     Dressing,     new[] { "bag" }),
        (BuildCatalog.DecorFishTank,    Dressing,     new[] { "TankStand", "FishTank", "TankWater", "TankGravel" }),
        (BuildCatalog.DecorNoticeBoard, Dressing,     new[] { "NoticeBoard", "Notice*" }),
        (BuildCatalog.DecorLeadRail,    Dressing,     new[] { "LeadRail", "Lead*" }),
    };

    /// <summary>
    /// Groups EmptyShop scans besides every StarterPiece. Only direct children whose name is a
    /// decor part in <see cref="Sources"/> (now orderable) are removed; paths, paddock ground,
    /// planting, floor tiles, walls, street and city stay.
    /// </summary>
    private static readonly string[] InteriorDecorPaths = { Dressing, YardDressing };

    /// <summary>Bake one prefab per decor id from MainScene and register it in FurniturePrefabs.asset.</summary>
    public static void Extract()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureFolder();
            var catalog = AssetDatabase.LoadAssetAtPath<FurniturePrefabs>(CatalogPath)
                          ?? throw new InvalidOperationException($"No FurniturePrefabs at {CatalogPath}");
            int count = 0;
            foreach (var src in Sources)
            {
                Register(catalog, src.Id, BakePrefab(src.Id, src.Parent, src.Parts));
                count++;
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // discard, never save
            Debug.Log($"{Tag} done {count}");
            Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} failed: {e}");
            Exit(1);
        }
    }

    /// <summary>Removes every direct child of <paramref name="path"/> named like an orderable decor part.</summary>
    private static int RemoveOrderableDecor(Scene scene, string path)
    {
        var parent = FindPath(scene, path) ?? throw new InvalidOperationException($"Missing {path}");
        var names = new HashSet<string>(Sources.SelectMany(s => s.Parts).Select(p => p.TrimEnd('*')));
        var doomed = parent.Cast<Transform>().Where(c => names.Contains(c.name)).ToList();
        foreach (var child in doomed)
        {
            Debug.Log($"{Tag} removed {path}/{child.name}");
            Object.DestroyImmediate(child.gameObject);
        }
        return doomed.Count;
    }

    /// <summary>Remove starter furniture and interior decor from MainScene and save it.</summary>
    public static void EmptyShop()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int starters = RemoveStarterPieces();
            int decor = 0;
            foreach (string path in InteriorDecorPaths)
                decor += RemoveOrderableDecor(scene, path);
            LogRemainingShopChildren(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("SaveScene failed");
            Debug.Log($"{Tag} emptied: {starters} starter pieces, {decor} decor roots");
            Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} failed: {e}");
            Exit(1);
        }
    }

    private static GameObject BakePrefab(string id, string parentPath, string[] parts)
    {
        var parent = FindPath(SceneManager.GetActiveScene(), parentPath)
                     ?? throw new InvalidOperationException($"{id}: missing {parentPath}");
        var sources = ResolveParts(parent, parts, id);
        GameObject copy = sources.Count == 1 ? CopySingle(sources[0]) : CopyGroup(id, sources);
        copy.name = id;
        MeshBuilder.SetLayerRecursive(copy, FurnitureFactory.DecorationLayer);
        AssertAssetReferences(copy, id);

        string path = $"{DecorFolder}/{id}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path, out bool ok);
        Object.DestroyImmediate(copy);
        if (!ok || prefab == null) throw new InvalidOperationException($"{id}: SaveAsPrefabAsset failed");
        Debug.Log($"{Tag} {id} <- {string.Join(" + ", sources.Select(PathOf).Distinct())}");
        return prefab;
    }

    private static List<Transform> ResolveParts(Transform parent, string[] parts, string id)
    {
        var found = new List<Transform>();
        foreach (string part in parts)
        {
            bool all = part.EndsWith(AllSuffix);
            string name = all ? part.Substring(0, part.Length - AllSuffix.Length) : part;
            var matches = parent.Cast<Transform>().Where(c => c.name == name).ToList();
            if (matches.Count == 0)
                throw new InvalidOperationException($"{id}: no '{name}' under {PathOf(parent)}");
            found.AddRange(all ? matches : matches.Take(1));
        }
        return found;
    }

    /// <summary>Duplicate one object with its root reset to origin/identity; children keep their local transforms.</summary>
    private static GameObject CopySingle(Transform source)
    {
        var copy = Object.Instantiate(source.gameObject);
        copy.transform.SetParent(null, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        return copy;
    }

    /// <summary>Group several siblings under a new root at the first part, then move that root to origin.</summary>
    private static GameObject CopyGroup(string id, List<Transform> sources)
    {
        var root = new GameObject(id);
        root.transform.position = sources[0].position;
        foreach (var src in sources)
        {
            var part = Object.Instantiate(src.gameObject, src.position, src.rotation);
            part.name = src.name;
            part.transform.localScale = src.lossyScale;
            part.transform.SetParent(root.transform, true);
        }
        root.transform.position = Vector3.zero;
        return root;
    }

    private static void AssertAssetReferences(GameObject go, string id)
    {
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh))
                throw new InvalidOperationException($"{id}: {mf.name} uses non-asset mesh '{mf.sharedMesh.name}'");
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null && !EditorUtility.IsPersistent(m))
                    throw new InvalidOperationException($"{id}: {r.name} uses non-asset material '{m.name}'");
    }

    private static void Register(FurniturePrefabs catalog, string id, GameObject prefab)
    {
        var entry = new FurniturePrefabs.Entry { Id = id, Prefab = prefab };
        int index = catalog.Entries.FindIndex(e => e.Id == id);
        if (index >= 0) catalog.Entries[index] = entry;
        else catalog.Entries.Add(entry);
    }

    private static int RemoveStarterPieces()
    {
        var pieces = Object.FindObjectsByType<StarterPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;
        foreach (var piece in pieces)
        {
            if (piece == null) continue; // already gone with a removed ancestor
            Debug.Log($"{Tag} removed starter {PathOf(piece.transform)} ({piece.CatalogId})");
            Object.DestroyImmediate(piece.gameObject);
            removed++;
        }
        return removed;
    }

    /// <summary>Audit trail: what is left directly under Shop, grouped by name.</summary>
    private static void LogRemainingShopChildren(Scene scene)
    {
        var shop = FindPath(scene, "Shop");
        if (shop == null) return;
        foreach (var group in shop.Cast<Transform>().GroupBy(c => c.name))
            Debug.Log($"{Tag} kept Shop/{group.Key} x{group.Count()}");
    }

    private static Transform FindPath(Scene scene, string path)
    {
        string[] names = path.Split('/');
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == names[0]);
        Transform t = root != null ? root.transform : null;
        for (int i = 1; i < names.Length && t != null; i++)
            t = t.Find(names[i]);
        return t;
    }

    private static string PathOf(Transform t) =>
        t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(DecorFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Decor");
    }

    private static void Exit(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
#endif
