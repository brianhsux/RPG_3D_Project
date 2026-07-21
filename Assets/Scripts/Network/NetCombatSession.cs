using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Host-only: register NetworkEnemy prefab and spawn adventure / sandbox enemies.
/// </summary>
public class NetCombatSession : MonoBehaviour
{
    public const string EnemyPrefabResource = "NetworkEnemy";

    public static NetCombatSession Instance { get; private set; }

    [Tooltip("Optional. If null, loads Resources/NetworkEnemy or builds a runtime template.")]
    public GameObject enemyPrefab;

    GameObject _runtimeEnemyTemplate;
    readonly List<NetworkObject> _spawned = new List<NetworkObject>();

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
        // Convert any already-built local adventure mobs into network spawns.
        CancelInvoke(nameof(ConvertLocalWorldEnemies));
        Invoke(nameof(ConvertLocalWorldEnemies), 0.2f);
    }

    public void OnShutdown()
    {
        CancelInvoke(nameof(ConvertLocalWorldEnemies));
        DespawnAllAdventureEnemies();
    }

    public void DespawnAllAdventureEnemies()
    {
        if (!NetPlay.IsServer) return;

        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var no = _spawned[i];
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }
        _spawned.Clear();

        // Also catch any NetworkEnemy not tracked (e.g. after domain reload).
        var enemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var no = enemies[i] != null ? enemies[i].GetComponent<NetworkObject>() : null;
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }
    }

    /// <summary>Destroy local (non-network) EnemyAI and re-run zone enemy spawns as NetworkEnemy.</summary>
    public void ConvertLocalWorldEnemies()
    {
        if (!NetPlay.IsServer) return;
        if (MapRuntimeBuilder.Instance == null) return;

        DespawnAllAdventureEnemies();
        DestroyLocalOnlyEnemies();
        MapRuntimeBuilder.Instance.RespawnZoneEnemiesForHost();
    }

    public static void DestroyLocalOnlyEnemies()
    {
        var ais = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < ais.Length; i++)
        {
            var ai = ais[i];
            if (ai == null) continue;
            if (ai.GetComponent<NetworkObject>() != null) continue;
            Object.Destroy(ai.gameObject);
        }
    }

    public GameObject SpawnAdventureEnemy(
        string name,
        Vector3 pos,
        Vector3 zoneCenter,
        int level,
        bool isBoss,
        EnemyKind kind,
        int mapIndex,
        float moveSpeed,
        float leashRadius)
    {
        if (!NetPlay.IsServer) return null;

        var nm = NetworkManager.Singleton;
        if (nm == null) return null;
        RegisterPrefabs(nm);

        var prefab = ResolveEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogError("[NetCombat] Cannot spawn adventure enemy — prefab missing.");
            return null;
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.name = name;
        go.SetActive(true);
        // Do NOT SetParent before Spawn — NGO forbids re-parenting unspawned NetworkObjects.

        var enemy = go.GetComponent<NetworkEnemy>();
        if (enemy == null)
        {
            Debug.LogError("[NetCombat] Prefab missing NetworkEnemy.");
            Destroy(go);
            return null;
        }

        enemy.ConfigureAdventureBeforeSpawn(kind, level, isBoss, mapIndex, zoneCenter, moveSpeed, leashRadius);

        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Destroy(go);
            return null;
        }

        no.Spawn(true);
        // Keep enemies unparented: parenting a NetworkObject to a plain Transform is fragile,
        // and map rebuild already despawns via NetCombatSession tracking.
        _spawned.Add(no);
        return go;
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
}
