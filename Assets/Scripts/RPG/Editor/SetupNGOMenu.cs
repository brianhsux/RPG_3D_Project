using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click NGO wiring: NetworkPlayer / NetworkEnemy prefabs + scene NetHud.
/// </summary>
public static class SetupNGOMenu
{
    const string PrefabDir = "Assets/Resources";
    const string PlayerPrefabPath = PrefabDir + "/NetworkPlayer.prefab";
    const string EnemyPrefabPath = PrefabDir + "/NetworkEnemy.prefab";

    [InitializeOnLoadMethod]
    static void AutoCreatePrefabWhenMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                {
                    EnsurePlayerPrefab();
                    Debug.Log("[NGO] Auto-created Resources/NetworkPlayer.prefab");
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null)
                {
                    EnsureEnemyPrefab();
                    Debug.Log("[NGO] Auto-created Resources/NetworkEnemy.prefab");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NGO] Prefab auto-create deferred: {e.Message}");
            }
        };
    }

    [MenuItem("RPG/Setup NGO Scene & Prefab")]
    public static void Setup()
    {
        EnsurePlayerPrefab();
        EnsureEnemyPrefab();
        EnsureSceneObjects();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[NGO] Setup complete: NetworkPlayer + NetworkEnemy prefabs + NetHud.");
    }

    [MenuItem("RPG/Create NetworkPlayer Prefab Only")]
    public static void CreatePrefabOnly()
    {
        var prefab = EnsurePlayerPrefab();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    [MenuItem("RPG/Create NetworkEnemy Prefab Only")]
    public static void CreateEnemyPrefabOnly()
    {
        var prefab = EnsureEnemyPrefab();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }

    static GameObject EnsurePlayerPrefab()
    {
        EnsureResourcesFolder();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (existing != null)
        {
            ConfigureOwnerTransform(existing.GetComponent<NetworkTransform>());
            if (existing.GetComponent<NetworkPlayerCombat>() == null)
                existing.AddComponent<NetworkPlayerCombat>();
            PrefabUtility.SavePrefabAsset(existing);
            return existing;
        }

        var root = new GameObject("NetworkPlayer");
        var cc = root.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);

        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        ConfigureOwnerTransform(nt);
        root.AddComponent<NetworkPlayer>();
        root.AddComponent<NetworkPlayerMovement>();
        root.AddComponent<NetworkPlayerCombat>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static GameObject EnsureEnemyPrefab()
    {
        EnsureResourcesFolder();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (existing != null)
        {
            ConfigureServerTransform(existing.GetComponent<NetworkTransform>());
            PrefabUtility.SavePrefabAsset(existing);
            return existing;
        }

        var root = new GameObject("NetworkEnemy");
        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        ConfigureServerTransform(nt);
        root.AddComponent<NetworkEnemy>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static void ConfigureOwnerTransform(NetworkTransform nt)
    {
        if (nt == null) return;
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
    }

    static void ConfigureServerTransform(NetworkTransform nt)
    {
        if (nt == null) return;
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
    }

    static void EnsureSceneObjects()
    {
        var hud = Object.FindFirstObjectByType<NetHud>();
        if (hud == null)
        {
            var hudGo = new GameObject("NetHud");
            hud = hudGo.AddComponent<NetHud>();
            Undo.RegisterCreatedObjectUndo(hudGo, "Create NetHud");
        }

        if (hud.GetComponent<NetBootstrap>() == null)
            Undo.AddComponent<NetBootstrap>(hud.gameObject);
        if (hud.GetComponent<NetCombatSession>() == null)
            Undo.AddComponent<NetCombatSession>(hud.gameObject);

        var bootstrap = hud.GetComponent<NetBootstrap>();
        var session = hud.GetComponent<NetCombatSession>();
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (bootstrap != null && playerPrefab != null)
        {
            bootstrap.playerPrefab = playerPrefab;
            EditorUtility.SetDirty(bootstrap);
        }
        if (session != null && enemyPrefab != null)
        {
            session.enemyPrefab = enemyPrefab;
            EditorUtility.SetDirty(session);
        }

        var existingNm = Object.FindFirstObjectByType<NetworkManager>();
        if (existingNm != null)
        {
            existingNm.NetworkConfig.EnableSceneManagement = false;
            if (playerPrefab != null)
            {
                existingNm.NetworkConfig.PlayerPrefab = playerPrefab;
                if (!existingNm.NetworkConfig.Prefabs.Contains(playerPrefab))
                    existingNm.AddNetworkPrefab(playerPrefab);
            }
            if (enemyPrefab != null && !existingNm.NetworkConfig.Prefabs.Contains(enemyPrefab))
                existingNm.AddNetworkPrefab(enemyPrefab);
            EditorUtility.SetDirty(existingNm);
        }
    }
}
