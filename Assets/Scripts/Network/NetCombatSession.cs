using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Host-only combat arena helpers: register enemy prefab and spawn test mobs.
/// </summary>
public class NetCombatSession : MonoBehaviour
{
    public const string EnemyPrefabResource = "NetworkEnemy";
    public const int TestEnemyCount = 3;

    public static NetCombatSession Instance { get; private set; }

    [Tooltip("Optional. If null, loads Resources/NetworkEnemy or builds a runtime template.")]
    public GameObject enemyPrefab;

    GameObject _runtimeEnemyTemplate;
    bool _spawnedTestEnemies;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterPrefabs(NetworkManager nm)
    {
        if (nm == null) return;
        var prefab = ResolveEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[NetCombat] NetworkEnemy prefab missing.");
            return;
        }
        if (!nm.NetworkConfig.Prefabs.Contains(prefab))
            nm.AddNetworkPrefab(prefab);
    }

    public void OnHostStarted()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        RegisterPrefabs(nm);
        // Delay one frame so the host player object exists for positioning.
        CancelInvoke(nameof(SpawnTestEnemies));
        Invoke(nameof(SpawnTestEnemies), 0.15f);
    }

    public void OnShutdown()
    {
        CancelInvoke(nameof(SpawnTestEnemies));
        _spawnedTestEnemies = false;
        // NetworkManager.Shutdown despawns network objects; nothing else required.
    }

    public void SpawnTestEnemies()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (_spawnedTestEnemies) return;

        var prefab = ResolveEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogError("[NetCombat] Cannot spawn — NetworkEnemy prefab missing.");
            return;
        }

        Vector3 origin = FindHostSpawnOrigin();
        Vector3[] offsets =
        {
            new Vector3(4f, 0f, 3f),
            new Vector3(-4f, 0f, 4f),
            new Vector3(2f, 0f, -5f),
        };

        int count = Mathf.Min(TestEnemyCount, offsets.Length);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, origin + offsets[i], Quaternion.identity);
            go.name = $"NetworkEnemy_{i}";
            go.SetActive(true);
            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[NetCombat] Prefab missing NetworkObject.");
                Destroy(go);
                continue;
            }
            no.Spawn(true);
        }

        _spawnedTestEnemies = true;
        Debug.Log($"[NetCombat] Spawned {count} test enemies near {origin}");
    }

    GameObject ResolveEnemyPrefab()
    {
        if (enemyPrefab != null) return enemyPrefab;
        enemyPrefab = Resources.Load<GameObject>(EnemyPrefabResource);
        if (enemyPrefab != null) return enemyPrefab;
        enemyPrefab = EnsureRuntimeEnemyTemplate();
        return enemyPrefab;
    }

    GameObject EnsureRuntimeEnemyTemplate()
    {
        if (_runtimeEnemyTemplate != null) return _runtimeEnemyTemplate;

        var go = new GameObject("NetworkEnemy");
        DontDestroyOnLoad(go);
        go.SetActive(false);

        go.AddComponent<NetworkObject>();
        var nt = go.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        go.AddComponent<NetworkEnemy>();

        _runtimeEnemyTemplate = go;
        Debug.Log("[NetCombat] Built runtime NetworkEnemy template.");
        return go;
    }

    static Vector3 FindHostSpawnOrigin()
    {
        var local = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
            : null;
        if (local != null)
            return local.transform.position;

        var players = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        if (players != null && players.Length > 0 && players[0] != null)
            return players[0].transform.position;

        var solo = GameObject.FindGameObjectWithTag("Player");
        if (solo != null) return solo.transform.position;

        return Vector3.zero;
    }
}
