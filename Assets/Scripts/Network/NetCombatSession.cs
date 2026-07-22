using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Host-only: register NetworkEnemy prefab, spawn adventure enemies, schedule respawns.
/// </summary>
public class NetCombatSession : MonoBehaviour
{
    public const string EnemyPrefabResource = "NetworkEnemy";
    public const float DefaultRespawnDelay = 6f;

    public static NetCombatSession Instance { get; private set; }

    [Tooltip("Optional. If null, loads Resources/NetworkEnemy or builds a runtime template.")]
    public GameObject enemyPrefab;

    public float enemyRespawnDelay = DefaultRespawnDelay;

    GameObject _runtimeEnemyTemplate;
    readonly List<NetworkObject> _spawned = new List<NetworkObject>();
    readonly List<Coroutine> _respawnJobs = new List<Coroutine>();

    public struct EnemySpawnSpec
    {
        public string name;
        public Vector3 pos;
        public Vector3 zoneCenter;
        public int level;
        public bool isBoss;
        public EnemyKind kind;
        public int mapIndex;
        public float moveSpeed;
        public float leashRadius;
        public bool allowRespawn;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        if (GameManager.Instance != null)
            enemyRespawnDelay = GameManager.Instance.enemyRespawnDelay;
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
        CancelInvoke(nameof(ConvertLocalWorldEnemies));
        Invoke(nameof(ConvertLocalWorldEnemies), 0.2f);
    }

    public void OnShutdown()
    {
        CancelInvoke(nameof(ConvertLocalWorldEnemies));
        CancelAllRespawns();
        DespawnAllAdventureEnemies();
    }

    public void CancelAllRespawns()
    {
        for (int i = 0; i < _respawnJobs.Count; i++)
        {
            if (_respawnJobs[i] != null)
                StopCoroutine(_respawnJobs[i]);
        }
        _respawnJobs.Clear();
    }

    public void DespawnAllAdventureEnemies()
    {
        if (!NetPlay.IsServer) return;
        CancelAllRespawns();

        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            var no = _spawned[i];
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }
        _spawned.Clear();

        var enemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var no = enemies[i] != null ? enemies[i].GetComponent<NetworkObject>() : null;
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }
    }

    public void ConvertLocalWorldEnemies()
    {
        if (!NetPlay.IsServer) return;
        if (MapRuntimeBuilder.Instance == null) return;
        if (AdventureManager.Instance != null && AdventureManager.Instance.IsInVillage)
        {
            DespawnAllAdventureEnemies();
            DestroyLocalOnlyEnemies();
            return;
        }

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

    public void ScheduleRespawn(EnemySpawnSpec spec, float delaySeconds = -1f)
    {
        if (!NetPlay.IsServer) return;
        if (!spec.allowRespawn) return;
        float delay = delaySeconds > 0f ? delaySeconds : enemyRespawnDelay;
        var job = StartCoroutine(RespawnAfterDelay(spec, delay));
        _respawnJobs.Add(job);
    }

    IEnumerator RespawnAfterDelay(EnemySpawnSpec spec, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, delay));
        if (!NetPlay.IsServer) yield break;
        SpawnAdventureEnemy(
            spec.name, spec.pos, spec.zoneCenter, spec.level, spec.isBoss,
            spec.kind, spec.mapIndex, spec.moveSpeed, spec.leashRadius, spec.allowRespawn);
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
        float leashRadius,
        bool allowRespawn = true)
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

        var enemy = go.GetComponent<NetworkEnemy>();
        if (enemy == null)
        {
            Debug.LogError("[NetCombat] Prefab missing NetworkEnemy.");
            Destroy(go);
            return null;
        }

        enemy.ConfigureAdventureBeforeSpawn(
            kind, level, isBoss, mapIndex, zoneCenter, moveSpeed, leashRadius, pos, name, allowRespawn);

        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Destroy(go);
            return null;
        }

        no.Spawn(true);
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
