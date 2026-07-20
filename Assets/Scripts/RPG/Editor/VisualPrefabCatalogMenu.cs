using UnityEditor;
using UnityEngine;

public static class VisualPrefabCatalogMenu
{
    const string AssetPath = "Assets/Resources/VisualPrefabCatalog.asset";
    const string SimpleNaturePrefabs = "Assets/SimpleNaturePack/Prefabs";
    const string PolytopeEnv = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs";
    const string PolytopeVillage = "Assets/Polytope Studio/Lowpoly_Village/Prefabs";

    [MenuItem("RPG/Create Visual Prefab Catalog")]
    public static void CreateCatalog()
    {
        EnsureCatalog();
        Ping();
    }

    [MenuItem("RPG/Select Visual Prefab Catalog")]
    public static void SelectCatalog()
    {
        EnsureCatalog();
        Ping();
    }

    [MenuItem("RPG/Apply Polytope Nature to Catalog")]
    public static void ApplyPolytopeNature()
    {
        var catalog = EnsureCatalog();
        if (catalog == null) return;

        if (!AssetDatabase.IsValidFolder(PolytopeEnv))
        {
            EditorUtility.DisplayDialog(
                "Polytope Studio",
                "找不到 Assets/Polytope Studio/Lowpoly_Environments/Prefabs。",
                "OK");
            return;
        }

        Undo.RecordObject(catalog, "Apply Polytope Nature");

        catalog.treeVariants = LoadMany(
            $"{PolytopeEnv}/Trees/PT_Pine_Tree_03_green.prefab",
            $"{PolytopeEnv}/Trees/PT_Fruit_Tree_01_green.prefab",
            $"{PolytopeEnv}/Trees/PT_Fruit_Tree_01_apples.prefab",
            $"{PolytopeEnv}/Trees/PT_Fruit_Tree_01_pears.prefab",
            $"{PolytopeEnv}/Trees/PT_Fruit_Tree_01_plums.prefab");
        catalog.treePrefab = First(catalog.treeVariants);
        catalog.treeScale = 1f;

        catalog.bushVariants = LoadMany(
            $"{PolytopeEnv}/Shrubs/PT_Generic_Shrub_01_green.prefab");
        catalog.bushScale = 1f;

        catalog.rockVariants = LoadMany(
            $"{PolytopeEnv}/Rocks/PT_Generic_Rock_01.prefab",
            $"{PolytopeEnv}/Rocks/PT_River_Rock_Pile_02.prefab",
            $"{PolytopeEnv}/Rocks/PT_Menhir_Rock_02.prefab",
            $"{PolytopeEnv}/Rocks/PT_Ore_Rock_01.prefab",
            $"{PolytopeEnv}/Rocks/PT_Ore_Rock_01_split.prefab");
        catalog.rockScale = 1f;
        catalog.rubblePrefab = First(catalog.rockVariants);
        // Menhir as ruin pillar when available.
        catalog.ruinPillarPrefab = LoadOne($"{PolytopeEnv}/Rocks/PT_Menhir_Rock_02.prefab");
        catalog.ruinScale = 1.2f;

        catalog.mushroomVariants = LoadMany(
            $"{PolytopeEnv}/Mushrooms/PT_Caesars_Mushroom_01.prefab");
        catalog.mushroomScale = 1f;

        catalog.stumpPrefab = LoadOne($"{PolytopeEnv}/Trees/PT_Pine_Tree_03_stump.prefab");
        catalog.stumpScale = 1f;

        catalog.grassVariants = LoadMany(
            $"{PolytopeEnv}/Plants/PT_Grass_02.prefab",
            $"{PolytopeEnv}/Flowers/PT_Poppy_02.prefab");
        catalog.grassScale = 1f;

        catalog.fenceVariants = LoadMany(
            $"{PolytopeVillage}/Modular/Fence/PT_Modular_Fence_Wood_01.prefab",
            $"{PolytopeVillage}/Modular/Fence/PT_Modular_Fence_Wood_02.prefab",
            $"{PolytopeVillage}/Modular/Fence/PT_Modular_Fence_Wood_03.prefab");
        catalog.fenceScale = 1f;
        catalog.gatePrefab = LoadOne($"{PolytopeVillage}/Modular/Fence/PT_Modular_Gate_Wood_01.prefab");
        catalog.gateScale = 1f;
        catalog.bridgePrefab = LoadOne($"{PolytopeVillage}/Modular/Bridge/PT_Wooden_Bridge_02.prefab");
        catalog.bridgeScale = 1f;

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Ping();

        Debug.Log("Polytope Nature applied → trees/rocks/grass + village fences/bridge. Play and visit Village (west) / Forest (east).");
    }

    [MenuItem("RPG/Apply Simple Nature Pack to Catalog")]
    public static void ApplySimpleNaturePack()
    {
        var catalog = EnsureCatalog();
        if (catalog == null) return;

        if (!AssetDatabase.IsValidFolder(SimpleNaturePrefabs))
        {
            EditorUtility.DisplayDialog(
                "Simple Nature Pack",
                "找不到 Assets/SimpleNaturePack/Prefabs。\n請確認已匯入 Low-Poly Simple Nature Pack。",
                "OK");
            return;
        }

        Undo.RecordObject(catalog, "Apply Simple Nature Pack");

        catalog.treeVariants = LoadMany(
            $"{SimpleNaturePrefabs}/Tree_01.prefab",
            $"{SimpleNaturePrefabs}/Tree_02.prefab",
            $"{SimpleNaturePrefabs}/Tree_03.prefab",
            $"{SimpleNaturePrefabs}/Tree_04.prefab",
            $"{SimpleNaturePrefabs}/Tree_05.prefab");
        catalog.treePrefab = First(catalog.treeVariants);
        catalog.treeScale = 1f;

        catalog.bushVariants = LoadMany(
            $"{SimpleNaturePrefabs}/Bush_01.prefab",
            $"{SimpleNaturePrefabs}/Bush_02.prefab",
            $"{SimpleNaturePrefabs}/Bush_03.prefab");
        catalog.bushScale = 1f;

        catalog.rockVariants = LoadMany(
            $"{SimpleNaturePrefabs}/Rock_01.prefab",
            $"{SimpleNaturePrefabs}/Rock_02.prefab",
            $"{SimpleNaturePrefabs}/Rock_03.prefab",
            $"{SimpleNaturePrefabs}/Rock_04.prefab",
            $"{SimpleNaturePrefabs}/Rock_05.prefab");
        catalog.rockScale = 1f;
        catalog.rubblePrefab = First(catalog.rockVariants);

        catalog.mushroomVariants = LoadMany(
            $"{SimpleNaturePrefabs}/Mushroom_01.prefab",
            $"{SimpleNaturePrefabs}/Mushroom_02.prefab");
        catalog.mushroomScale = 1f;

        catalog.stumpPrefab = LoadOne($"{SimpleNaturePrefabs}/Stump_01.prefab");
        catalog.stumpScale = 1f;

        catalog.grassVariants = LoadMany(
            $"{SimpleNaturePrefabs}/Grass_01.prefab",
            $"{SimpleNaturePrefabs}/Grass_02.prefab",
            $"{SimpleNaturePrefabs}/Flowers_01.prefab",
            $"{SimpleNaturePrefabs}/Flowers_02.prefab");
        catalog.grassScale = 1f;

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Ping();

        int trees = catalog.treeVariants != null ? catalog.treeVariants.Length : 0;
        Debug.Log($"Simple Nature Pack applied → {trees} trees. Enter Play Mode and walk to the Forest zone (east).");
    }

    static VisualPrefabCatalog EnsureCatalog()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var catalog = AssetDatabase.LoadAssetAtPath<VisualPrefabCatalog>(AssetPath);
        if (catalog != null) return catalog;

        catalog = ScriptableObject.CreateInstance<VisualPrefabCatalog>();
        AssetDatabase.CreateAsset(catalog, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created " + AssetPath);
        return catalog;
    }

    static void Ping()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<VisualPrefabCatalog>(AssetPath);
        if (catalog == null) return;
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }

    static GameObject LoadOne(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static GameObject First(GameObject[] arr) =>
        arr != null && arr.Length > 0 ? arr[0] : null;

    static GameObject[] LoadMany(params string[] paths)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < paths.Length; i++)
        {
            var go = LoadOne(paths[i]);
            if (go != null) list.Add(go);
        }
        return list.ToArray();
    }
}
