using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host-authoritative adventure location sync (village / world / map focus).
/// Geometry rebuilds on every peer; enemies still spawn only on the server.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetAdventureState : NetworkBehaviour
{
    public const string PrefabResource = "NetAdventureState";

    public static NetAdventureState Instance { get; private set; }

    static GameObject _runtimeTemplate;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer)
        {
            // Wait a tick so the Host NetworkPlayer exists before teleport.
            CancelInvoke(nameof(PublishInitialServerState));
            Invoke(nameof(PublishInitialServerState), 0.15f);
        }
        else if (IsClient)
        {
            RequestSyncServerRpc();
        }
    }

    void PublishInitialServerState()
    {
        if (!IsServer || !IsSpawned) return;
        PublishInternal(teleportPlayers: true);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void RegisterPrefab(NetworkManager nm)
    {
        if (nm == null) return;
        var prefab = ResolvePrefab();
        if (prefab == null) return;
        if (!nm.NetworkConfig.Prefabs.Contains(prefab))
            nm.AddNetworkPrefab(prefab);
    }

    public static NetAdventureState SpawnOnServer()
    {
        if (!NetPlay.IsServer) return null;
        if (Instance != null && Instance.IsSpawned) return Instance;

        var prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError("[NetAdventure] Prefab missing — cannot sync maps.");
            return null;
        }

        var go = Object.Instantiate(prefab);
        go.name = "NetAdventureState";
        go.SetActive(true);
        var no = go.GetComponent<NetworkObject>();
        no.Spawn(true);
        return go.GetComponent<NetAdventureState>();
    }

    public static void DespawnIfServer()
    {
        if (Instance == null) return;
        var no = Instance.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned && NetPlay.IsServer)
            no.Despawn(true);
        else if (Instance != null)
            Object.Destroy(Instance.gameObject);
    }

    static GameObject ResolvePrefab()
    {
        var prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab != null) return prefab;
        return EnsureRuntimeTemplate();
    }

    static GameObject EnsureRuntimeTemplate()
    {
        if (_runtimeTemplate != null) return _runtimeTemplate;
        var go = new GameObject("NetAdventureState");
        Object.DontDestroyOnLoad(go);
        go.SetActive(false);
        go.AddComponent<NetworkObject>();
        NetPlay.StampRuntimePrefab(go, NetPlay.RuntimeAdventureHash);
        go.AddComponent<NetAdventureState>();
        _runtimeTemplate = go;
        Debug.Log("[NetAdventure] Built runtime NetAdventureState template.");
        return go;
    }

    /// <summary>Client or Host owner requests a shared world change.</summary>
    public static void RequestTravel(int world, int map)
    {
        if (Instance == null || !Instance.IsSpawned)
        {
            Debug.LogWarning("[NetAdventure] Travel ignored — session not ready.");
            return;
        }
        Instance.RequestTravelServerRpc(world, map);
    }

    /// <summary>Host finished a local LoadWorld/LoadVillage — push to peers.</summary>
    public static void PublishFromServer(bool teleportPlayers = true)
    {
        if (Instance == null || !Instance.IsSpawned || !Instance.IsServer) return;
        Instance.PublishInternal(teleportPlayers);
    }

    /// <summary>Push current Host adventure state to one client (late join).</summary>
    public static void SyncClient(ulong clientId)
    {
        if (Instance == null || !Instance.IsSpawned || !Instance.IsServer) return;
        Instance.SendSnapshotToClient(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestTravelServerRpc(int world, int map, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || AdventureManager.Instance == null) return;

        world = Mathf.Clamp(world, ProgressionFormulas.VillageWorldId, ProgressionFormulas.WorldCount);
        map = Mathf.Clamp(map, 1, ProgressionFormulas.MapsPerWorld);

        var adv = AdventureManager.Instance;
        if (world == ProgressionFormulas.VillageWorldId)
        {
            adv.ApplyLoadVillageLocal(showBanner: true, fromNetwork: true);
            PublishInternal(teleportPlayers: true);
            return;
        }

        bool unlocked = AdventureProgress.CanEnterWorld(
            world,
            adv.currentWorld,
            adv.IsInVillage,
            adv.worldPortalReady,
            adv.highestWorldUnlocked,
            ProgressionFormulas.WorldCount);
        if (!unlocked)
        {
            string msg = world == adv.currentWorld + 1
                ? "Defeat the World Boss first"
                : $"{ProgressionFormulas.WorldThemeName(world)} portal is sealed";
            NotifyTravelDeniedClientRpc(msg, Target(rpcParams.Receive.SenderClientId));
            return;
        }

        adv.ApplyLoadWorldLocal(world, map, fromNetwork: true);
        PublishInternal(teleportPlayers: true);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        SendSnapshotToClient(rpcParams.Receive.SenderClientId);
    }

    void PublishInternal(bool teleportPlayers)
    {
        if (!IsServer || AdventureManager.Instance == null) return;

        var adv = AdventureManager.Instance;
        int world = adv.currentWorld;
        int map = adv.currentMap;
        int highest = adv.highestWorldUnlocked;
        string cleared = adv.ExportClearedBossKeysCsv();
        Vector3 spawn = ResolveSpawn(world, map);

        ApplyAdventureClientRpc(world, map, highest, cleared, spawn, teleportPlayers);
    }

    void SendSnapshotToClient(ulong clientId)
    {
        if (!IsServer || AdventureManager.Instance == null) return;
        if (clientId == NetworkManager.ServerClientId && NetworkManager.IsHost)
            return; // Host already has local state

        var adv = AdventureManager.Instance;
        Vector3 spawn = ResolveSpawn(adv.currentWorld, adv.currentMap);
        ApplyAdventureClientRpc(
            adv.currentWorld,
            adv.currentMap,
            adv.highestWorldUnlocked,
            adv.ExportClearedBossKeysCsv(),
            spawn,
            teleportPlayers: true,
            Target(clientId));
    }

    [ClientRpc]
    void ApplyAdventureClientRpc(
        int world,
        int map,
        int highest,
        string clearedCsv,
        Vector3 spawnPos,
        bool teleportPlayers,
        ClientRpcParams rpcParams = default)
    {
        if (AdventureManager.Instance != null)
        {
            // Host already applied geometry/enemies before publishing.
            if (!IsServer)
                AdventureManager.Instance.ApplyNetworkSnapshot(world, map, highest, clearedCsv);
            else
            {
                // Keep Host unlock / boss flags aligned if CSV drifted.
                AdventureManager.Instance.ImportClearedBossKeysCsv(clearedCsv);
                AdventureManager.Instance.highestWorldUnlocked =
                    Mathf.Max(AdventureManager.Instance.highestWorldUnlocked, highest);
                AdventureManager.Instance.RefreshBossFlagsPublic();
                AdventureManager.Instance.CapturePersonalProgress();
            }
        }

        if (teleportPlayers)
            TeleportOwnedNetworkPlayer(spawnPos);
    }

    [ClientRpc]
    void NotifyTravelDeniedClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        if (PlayerInventory.Instance != null && !string.IsNullOrEmpty(message))
            PlayerInventory.Instance.ShowLoot(message);
    }

    static Vector3 ResolveSpawn(int world, int map)
    {
        if (MapRuntimeBuilder.Instance == null)
            return new Vector3(0f, 0.5f, 0f);
        if (world == ProgressionFormulas.VillageWorldId)
            return MapRuntimeBuilder.Instance.GetVillageSpawn();
        return MapRuntimeBuilder.Instance.GetTileSpawn(map);
    }

    public static void TeleportOwnedNetworkPlayer(Vector3 pos)
    {
        var players = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var np = players[i];
            if (np == null || !np.IsSpawned || !np.IsOwner) continue;
            PlaceAt(np.gameObject, pos);
            return;
        }
    }

    static void PlaceAt(GameObject player, Vector3 pos)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    static ClientRpcParams Target(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }
}
