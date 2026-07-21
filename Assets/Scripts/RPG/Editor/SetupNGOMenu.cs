using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click NGO wiring: NetworkPlayer prefab under Resources + scene NetHud/NetBootstrap/NetworkManager.
/// </summary>
public static class SetupNGOMenu
{
    const string PrefabDir = "Assets/Resources";
    const string PrefabPath = PrefabDir + "/NetworkPlayer.prefab";

    [InitializeOnLoadMethod]
    static void AutoCreatePrefabWhenMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            try
            {
                EnsurePlayerPrefab();
                Debug.Log("[NGO] Auto-created Resources/NetworkPlayer.prefab");
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
        EnsureSceneObjects();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[NGO] Setup complete: Resources/NetworkPlayer.prefab + NetHud in scene.");
    }

    [MenuItem("RPG/Create NetworkPlayer Prefab Only")]
    public static void CreatePrefabOnly()
    {
        var prefab = EnsurePlayerPrefab();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    static GameObject EnsurePlayerPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            ConfigureNetworkTransform(existing.GetComponent<NetworkTransform>());
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
        ConfigureNetworkTransform(nt);
        root.AddComponent<NetworkPlayer>();
        root.AddComponent<NetworkPlayerMovement>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static void ConfigureNetworkTransform(NetworkTransform nt)
    {
        if (nt == null) return;
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
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

        var bootstrap = hud.GetComponent<NetBootstrap>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (bootstrap != null && prefab != null)
        {
            bootstrap.playerPrefab = prefab;
            EditorUtility.SetDirty(bootstrap);
        }

        // NetworkManager is created at Host/Client time by NetBootstrap — keep it out of
        // the scene so solo Play Mode does not trigger NGO's "add scene to build" dialog.
        var existingNm = Object.FindFirstObjectByType<NetworkManager>();
        if (existingNm != null)
        {
            existingNm.NetworkConfig.EnableSceneManagement = false;
            if (prefab != null)
            {
                existingNm.NetworkConfig.PlayerPrefab = prefab;
                if (!existingNm.NetworkConfig.Prefabs.Contains(prefab))
                    existingNm.AddNetworkPrefab(prefab);
            }
            EditorUtility.SetDirty(existingNm);
        }
    }
}
