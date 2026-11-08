using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Ensures a NetworkManager + UnityTransport exist and exposes Host/Client/Shutdown.
/// Does not auto-start — call from NetHud or other UI.
/// </summary>
public class NetBootstrap : MonoBehaviour
{
    public static NetBootstrap Instance { get; private set; }

    public const string DefaultAddress = "127.0.0.1";
    public const ushort DefaultPort = 7777;
    public const string PlayerPrefabResource = "NetworkPlayer";

    [Tooltip("Optional. If null, loads Resources/NetworkPlayer or builds a runtime template.")]
    public GameObject playerPrefab;

    public string connectAddress = DefaultAddress;
    public ushort connectPort = DefaultPort;

    /// <summary>Short status / error line shown in NetHud.</summary>
    public string LastMessage { get; private set; } = "";

    NetworkManager _nm;
    bool _soloPlayerSuspended;
    GameObject _runtimePlayerTemplate;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Do NOT create NetworkManager here — that triggers NGO's "add scene to build"
        // dialog on solo play. Only adopt an existing one, or create on Host/Client.
        if (NetworkManager.Singleton != null)
            EnsureNetworkManager();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public NetworkManager NetworkManager => _nm != null ? _nm : NetworkManager.Singleton;

    public bool IsNetworking =>
        NetworkManager != null && (NetworkManager.IsListening || NetworkManager.IsClient);

    public string StatusLabel
    {
        get
        {
            var nm = NetworkManager;
            if (nm == null || (!nm.IsListening && !nm.IsClient)) return "Offline";
            if (nm.IsHost) return $"Host · peers {Mathf.Max(0, nm.ConnectedClientsIds.Count - 1)}";
            if (nm.IsServer) return "Server";
            if (nm.IsConnectedClient) return "Client connected";
            if (nm.IsClient) return "Client connecting…";
            return "Offline";
        }
    }

    public void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            _nm = NetworkManager.Singleton;
            ConfigureNetworkManager(_nm);
            return;
        }

        var go = new GameObject("NetworkManager");
        DontDestroyOnLoad(go);
        _nm = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        _nm.NetworkConfig.NetworkTransport = transport;
        ConfigureNetworkManager(_nm);
    }

    void ConfigureNetworkManager(NetworkManager nm)
    {
        if (nm == null) return;

        // Phase 1: same scene for Host/Client — scene management only causes
        // Editor "add scene to build" prompts and is not needed yet.
        nm.NetworkConfig.EnableSceneManagement = false;

        var transport = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            transport = nm.gameObject.GetComponent<UnityTransport>();
            if (transport == null)
                transport = nm.gameObject.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
        }

        transport.SetConnectionData(
            string.IsNullOrEmpty(connectAddress) ? DefaultAddress : connectAddress,
            connectPort == 0 ? DefaultPort : connectPort);

        AssignPlayerPrefab(nm);
        EnsureCombatSession().RegisterPrefabs(nm);
        NetAdventureState.RegisterPrefab(nm);
        HookCallbacks(nm);
    }

    void AssignPlayerPrefab(NetworkManager nm)
    {
        if (nm == null) return;
        if (playerPrefab == null)
            playerPrefab = Resources.Load<GameObject>(PlayerPrefabResource);
        if (playerPrefab == null)
            playerPrefab = EnsureRuntimePlayerTemplate();

        if (playerPrefab == null)
        {
            Debug.LogWarning(
                "[NetBootstrap] No NetworkPlayer prefab. Run RPG/Setup NGO Scene & Prefab from the menu.");
            return;
        }

        nm.NetworkConfig.PlayerPrefab = playerPrefab;
        if (!nm.NetworkConfig.Prefabs.Contains(playerPrefab))
            nm.AddNetworkPrefab(playerPrefab);
    }

    GameObject EnsureRuntimePlayerTemplate()
    {
        if (_runtimePlayerTemplate != null) return _runtimePlayerTemplate;

        var go = new GameObject("NetworkPlayer");
        DontDestroyOnLoad(go);
        go.SetActive(false);

        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);

        go.AddComponent<NetworkObject>();
        var nt = go.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        go.AddComponent<NetworkPlayer>();
        go.AddComponent<NetworkPlayerMovement>();
        go.AddComponent<NetworkPlayerCombat>();

        // Body visual created by NetworkPlayer.OnNetworkSpawn if missing.
        _runtimePlayerTemplate = go;
        Debug.Log("[NetBootstrap] Built runtime NetworkPlayer template (run RPG/Setup NGO for a Resources prefab).");
        return go;
    }

    void HookCallbacks(NetworkManager nm)
    {
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NGO] Client connected: {clientId} (local={NetworkManager.LocalClientId})");
        if (NetworkManager != null && NetworkManager.IsServer)
        {
            if (clientId == NetworkManager.LocalClientId)
                LastMessage = "Hosting — waiting for clients";
            else
                LastMessage = $"Peer joined · id {clientId}";
            NetAdventureState.SyncClient(clientId);
        }
        else if (NetworkManager != null && NetworkManager.IsClient)
        {
            LastMessage = "Connected to Host";
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NGO] Client disconnected: {clientId}");
        var nm = NetworkManager;
        if (nm != null && nm.IsServer && clientId != nm.LocalClientId)
            LastMessage = $"Peer left · id {clientId}";

        if (nm != null && !nm.IsListening && !nm.IsClient)
        {
            if (string.IsNullOrEmpty(LastMessage) || LastMessage.StartsWith("Connected") || LastMessage.StartsWith("Hosting"))
                LastMessage = "Disconnected";
            RestoreSoloPlayer();
            if (AdventureManager.Instance != null)
                AdventureManager.Instance.RestorePersonalProgress();
        }
    }

    /// <summary>Commit the HUD text only when Host or Client is pressed.</summary>
    public bool TryUseConnectionFields(string addressField, string portField)
    {
        connectAddress = ConnectionDraft.NormalizeAddress(addressField);
        if (!ConnectionDraft.TryParsePort(portField, out int port))
        {
            LastMessage = "Port must be 1-65535";
            return false;
        }

        connectPort = (ushort)port;
        return true;
    }

    public bool StartHost()
    {
        EnsureNetworkManager();
        ConfigureNetworkManager(_nm);
        if (_nm.NetworkConfig.PlayerPrefab == null)
        {
            LastMessage = "Host failed — PlayerPrefab missing";
            Debug.LogError("[NetBootstrap] Cannot Host — PlayerPrefab missing.");
            return false;
        }

        SuspendSoloPlayer();
        bool ok = _nm.StartHost();
        Debug.Log(ok ? "[NGO] StartHost OK" : "[NGO] StartHost failed");
        if (!ok)
        {
            LastMessage = "Host failed — port in use?";
            RestoreSoloPlayer();
            return false;
        }
        LastMessage = "Hosting…";
        EnsureCombatSession().OnHostStarted();
        NetAdventureState.SpawnOnServer();
        return true;
    }

    public bool StartClient()
    {
        EnsureNetworkManager();
        ConfigureNetworkManager(_nm);
        if (_nm.NetworkConfig.PlayerPrefab == null)
        {
            LastMessage = "Client failed — PlayerPrefab missing";
            Debug.LogError("[NetBootstrap] Cannot Client — PlayerPrefab missing.");
            return false;
        }

        SuspendSoloPlayer();
        LastMessage = $"Connecting {connectAddress}:{connectPort}…";
        bool ok = _nm.StartClient();
        Debug.Log(ok
            ? $"[NGO] StartClient → {connectAddress}:{connectPort}"
            : "[NGO] StartClient failed");
        if (!ok)
        {
            LastMessage = "Client failed to start";
            RestoreSoloPlayer();
        }
        return ok;
    }

    public void Shutdown()
    {
        EnsureCombatSession().OnShutdown();
        if (NetPlay.IsServer)
            NetAdventureState.DespawnIfServer();
        var nm = NetworkManager;
        if (nm != null && (nm.IsListening || nm.IsClient))
        {
            nm.Shutdown();
            Debug.Log("[NGO] Shutdown");
        }
        LastMessage = "Disconnected";
        RestoreSoloPlayer();
        if (AdventureManager.Instance != null)
            AdventureManager.Instance.RestorePersonalProgress();
    }

    NetCombatSession EnsureCombatSession()
    {
        var session = GetComponent<NetCombatSession>();
        if (session == null)
            session = gameObject.AddComponent<NetCombatSession>();
        return session;
    }

    void SuspendSoloPlayer()
    {
        if (_soloPlayerSuspended) return;
        var solo = FindSoloHero();
        if (solo == null) return;

        var pc = solo.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        var cc = solo.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Hide solo body so network avatars are clear.
        foreach (var r in solo.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        _soloPlayerSuspended = true;
    }

    void RestoreSoloPlayer()
    {
        if (!_soloPlayerSuspended) return;
        var solo = FindSoloHero();
        if (solo != null)
        {
            var pc = solo.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = true;
            var cc = solo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            foreach (var r in solo.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
        }
        _soloPlayerSuspended = false;
    }

    static GameObject FindSoloHero()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            if (players[i].GetComponent<NetworkPlayer>() != null) continue;
            return players[i];
        }
        return null;
    }
}
