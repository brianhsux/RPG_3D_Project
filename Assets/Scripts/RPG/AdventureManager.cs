using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Village hub + 25 worlds × 4 map tiles (content level 1–100).
/// Login always starts in the Village. World portals unlock after visiting a world.
/// </summary>
public class AdventureManager : MonoBehaviour
{
    public static AdventureManager Instance { get; private set; }

    /// <summary>0 = Village hub; 1–10 = adventure worlds.</summary>
    public int currentWorld = ProgressionFormulas.VillageWorldId;
    public int currentMap = 1;
    public int highestWorldUnlocked = 1;

    public bool mapBossDefeated;
    public bool worldPortalReady;
    public int killCount;

    readonly HashSet<string> _clearedBossKeys = new HashSet<string>();

    /// <summary>True while applying a Host snapshot — skip re-broadcast.</summary>
    bool _networkSyncing;

    public bool IsInVillage => currentWorld == ProgressionFormulas.VillageWorldId;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        DisableLegacyWorld();

        if (AccountManager.Instance == null)
        {
            var go = new GameObject("AccountManager");
            go.AddComponent<AccountManager>();
        }

        // World loads after account select via BeginAfterAccountReady()
    }

    public void BeginAfterAccountReady()
    {
        DisableLegacyWorld();
        // Always land in the Village on login.
        LoadVillage(showBanner: false);
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public void ApplySave(AccountSaveData data)
    {
        if (data == null) data = new AccountSaveData();

        currentWorld = Mathf.Clamp(data.currentWorld, ProgressionFormulas.VillageWorldId, ProgressionFormulas.WorldCount);
        if (IsInVillage)
            currentMap = 1;
        else
            currentMap = Mathf.Clamp(data.currentMap, 1, ProgressionFormulas.MapsPerWorld);
        highestWorldUnlocked = Mathf.Clamp(data.highestWorldUnlocked, 1, ProgressionFormulas.WorldCount);
        killCount = Mathf.Max(0, data.killCount);

        _clearedBossKeys.Clear();
        if (data.clearedBossKeys != null)
        {
            foreach (var key in data.clearedBossKeys)
            {
                if (!string.IsNullOrEmpty(key))
                    _clearedBossKeys.Add(key);
            }
        }

        RefreshBossFlags();
    }

    public void WriteToSave(AccountSaveData data)
    {
        if (data == null) return;
        data.currentWorld = currentWorld;
        data.currentMap = currentMap;
        data.highestWorldUnlocked = highestWorldUnlocked;
        data.killCount = killCount;

        var keys = new string[_clearedBossKeys.Count];
        _clearedBossKeys.CopyTo(keys);
        data.clearedBossKeys = keys;
    }

    void DisableLegacyWorld()
    {
        string[] legacy =
        {
            "Arena", "ExtraEnemies", "Zones",
            "Enemy_Slime", "Enemy_Goblin", "Enemy_Skeleton",
            "Enemy_Slime_2", "Enemy_Slime_3", "Enemy_Slime_4",
            "Enemy_Goblin_2", "Enemy_Goblin_3", "Enemy_Goblin_4",
            "Enemy_Skeleton_2", "Enemy_Skeleton_3", "Enemy_Skeleton_4"
        };
        foreach (var n in legacy)
        {
            var go = GameObject.Find(n);
            if (go != null) go.SetActive(false);
        }
    }

    public int ContentLevel => IsInVillage
        ? 0
        : ProgressionFormulas.ContentLevel(currentWorld, currentMap);

    public string BossKey(int world, int map) => $"W{world}M{map}";

    public bool IsBossCleared(int world, int map) => _clearedBossKeys.Contains(BossKey(world, map));

    /// <summary>World portals open for every world the player has reached (visited / unlocked).</summary>
    public bool HasVisitedWorld(int world)
    {
        return world >= 1 && world <= highestWorldUnlocked;
    }

    public void LoadVillage(bool showBanner = true)
    {
        if (NetPlay.IsClientOnly)
        {
            NetAdventureState.RequestTravel(ProgressionFormulas.VillageWorldId, 1);
            return;
        }

        ApplyLoadVillageLocal(showBanner, fromNetwork: false);
        if (NetPlay.IsServer && !_networkSyncing)
            NetAdventureState.PublishFromServer(teleportPlayers: true);
    }

    public void ReturnToVillage()
    {
        if (IsInVillage) return;
        LoadVillage(showBanner: true);
    }

    public void EnterWorldFromVillage(int world)
    {
        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        if (!NetPlay.IsClientOnly && !HasVisitedWorld(world))
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.ShowLoot($"{ProgressionFormulas.WorldThemeName(world)} portal is sealed");
            return;
        }

        LoadWorld(world, 1);
        if (!NetPlay.IsClientOnly && PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"Entered {ProgressionFormulas.WorldThemeName(world)}");
        if (!NetPlay.IsClientOnly && AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public void LoadWorld(int world, int startMap = 1)
    {
        if (NetPlay.IsClientOnly)
        {
            NetAdventureState.RequestTravel(world, startMap);
            return;
        }

        ApplyLoadWorldLocal(world, startMap, fromNetwork: false);
        if (NetPlay.IsServer && !_networkSyncing)
            NetAdventureState.PublishFromServer(teleportPlayers: true);
    }

    public void LoadMap(int world, int map, bool forceRebuild = false)
    {
        if (world == ProgressionFormulas.VillageWorldId)
        {
            LoadVillage();
            return;
        }

        if (world != currentWorld || forceRebuild || IsInVillage)
            LoadWorld(world, map);
        else
            GoToMap(map);
    }

    /// <summary>Host/offline local village build (no ClientRpc).</summary>
    public void ApplyLoadVillageLocal(bool showBanner, bool fromNetwork)
    {
        currentWorld = ProgressionFormulas.VillageWorldId;
        currentMap = 1;
        mapBossDefeated = false;
        worldPortalReady = false;

        if (MapRuntimeBuilder.Instance != null)
            MapRuntimeBuilder.Instance.BuildVillage();

        if (!fromNetwork)
            PlacePlayerAtVillage();

        if (showBanner && PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot("Welcome to the Village");

        if (!fromNetwork && AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
        else if (fromNetwork && NetPlay.IsServer && AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    /// <summary>Host/offline local world build (no ClientRpc).</summary>
    public void ApplyLoadWorldLocal(int world, int startMap, bool fromNetwork)
    {
        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        startMap = Mathf.Clamp(startMap, 1, ProgressionFormulas.MapsPerWorld);

        if (world > highestWorldUnlocked)
        {
            bool portalAdvance = fromNetwork && world == currentWorld + 1 && worldPortalReady;
            if (!portalAdvance)
            {
                Debug.Log("World locked.");
                return;
            }
        }

        highestWorldUnlocked = Mathf.Max(highestWorldUnlocked, world);
        currentWorld = world;
        currentMap = startMap;
        RefreshBossFlags();

        if (MapRuntimeBuilder.Instance != null)
            MapRuntimeBuilder.Instance.BuildCurrentWorld();

        if (!fromNetwork)
            PlacePlayerInTile(startMap);

        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    /// <summary>Pure client: rebuild geometry + flags from Host snapshot.</summary>
    public void ApplyNetworkSnapshot(int world, int map, int highest, string clearedCsv)
    {
        _networkSyncing = true;
        try
        {
            ImportClearedBossKeysCsv(clearedCsv);
            highestWorldUnlocked = Mathf.Clamp(highest, 1, ProgressionFormulas.WorldCount);

            if (world == ProgressionFormulas.VillageWorldId)
            {
                currentWorld = ProgressionFormulas.VillageWorldId;
                currentMap = 1;
                mapBossDefeated = false;
                worldPortalReady = false;
                if (MapRuntimeBuilder.Instance != null)
                    MapRuntimeBuilder.Instance.BuildVillage();
            }
            else
            {
                currentWorld = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
                currentMap = Mathf.Clamp(map, 1, ProgressionFormulas.MapsPerWorld);
                RefreshBossFlags();
                if (MapRuntimeBuilder.Instance != null)
                    MapRuntimeBuilder.Instance.BuildCurrentWorld();
            }
        }
        finally
        {
            _networkSyncing = false;
        }
    }

    public string ExportClearedBossKeysCsv()
    {
        if (_clearedBossKeys.Count == 0) return "";
        var keys = new string[_clearedBossKeys.Count];
        _clearedBossKeys.CopyTo(keys);
        return string.Join(",", keys);
    }

    public void ImportClearedBossKeysCsv(string csv)
    {
        _clearedBossKeys.Clear();
        if (!string.IsNullOrEmpty(csv))
        {
            var parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var key = parts[i] != null ? parts[i].Trim() : null;
                if (!string.IsNullOrEmpty(key))
                    _clearedBossKeys.Add(key);
            }
        }
        RefreshBossFlags();
    }

    public void RefreshBossFlagsPublic() => RefreshBossFlags();

    void RefreshBossFlags()
    {
        if (IsInVillage)
        {
            mapBossDefeated = false;
            worldPortalReady = false;
            return;
        }

        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
        worldPortalReady = IsBossCleared(currentWorld, 4) && currentWorld < ProgressionFormulas.WorldCount;
    }

    public void EnterTile(int map)
    {
        if (IsInVillage) return;
        map = Mathf.Clamp(map, 1, 4);
        if (currentMap == map) return;
        currentMap = map;
        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
    }

    public void GoToMap(int map)
    {
        if (IsInVillage) return;
        map = Mathf.Clamp(map, 1, 4);
        currentMap = map;
        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
        PlacePlayerInTile(map);
    }

    void PlacePlayerInTile(int map)
    {
        if (MapRuntimeBuilder.Instance == null) return;
        Vector3 pos = MapRuntimeBuilder.Instance.GetTileSpawn(map);
        PlaceLocalPlayer(pos);
    }

    void PlacePlayerAtVillage()
    {
        if (MapRuntimeBuilder.Instance == null) return;
        PlaceLocalPlayer(MapRuntimeBuilder.Instance.GetVillageSpawn());
    }

    void PlaceLocalPlayer(Vector3 pos)
    {
        if (NetPlay.IsNetworking)
        {
            NetAdventureState.TeleportOwnedNetworkPlayer(pos);
            return;
        }

        var player = FindSoloPlayer();
        if (player == null) return;
        PlacePlayerAt(player, pos);
    }

    static GameObject FindSoloPlayer()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            if (players[i].GetComponent<NetworkPlayer>() != null) continue;
            return players[i];
        }
        return players.Length > 0 ? players[0] : null;
    }

    static void PlacePlayerAt(GameObject player, Vector3 pos)
    {
        var stats = player.GetComponent<CharacterStats>();
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = pos;
        if (cc != null) cc.enabled = true;
        if (stats != null && stats.isDead)
            stats.Respawn(pos);
    }

    public void NotifyEnemyDied(CharacterStats stats)
    {
        if (stats == null || stats.isPlayer) return;
        killCount++;

        if (stats.isChallengeEnemy)
        {
            if (ChallengeAltar.Active != null)
                ChallengeAltar.Active.NotifyEnemyKilled(stats);
            return;
        }

        if (stats.isBoss)
        {
            int map = stats.mapIndex > 0 ? stats.mapIndex : currentMap;
            _clearedBossKeys.Add(BossKey(currentWorld, map));
            if (map == currentMap)
                mapBossDefeated = true;

            if (map == 4 && currentWorld < ProgressionFormulas.WorldCount)
            {
                worldPortalReady = true;
                if (MapRuntimeBuilder.Instance != null)
                    MapRuntimeBuilder.Instance.SpawnWorldPortal();
            }

            if (PlayerInventory.Instance != null)
            {
                string msg = map == 4
                    ? "World Boss defeated! Next-world portal opened"
                    : $"Map {map} Boss defeated!";
                PlayerInventory.Instance.ShowLoot(msg);
            }

            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
    }

    public void EnterNextWorld()
    {
        if (IsInVillage) return;
        int next = currentWorld + 1;
        if (next > ProgressionFormulas.WorldCount) return;

        if (NetPlay.IsClientOnly)
        {
            NetAdventureState.RequestTravel(next, 1);
            return;
        }

        if (!worldPortalReady) return;
        highestWorldUnlocked = Mathf.Max(highestWorldUnlocked, next);
        LoadWorld(next, 1);
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"Entered {ProgressionFormulas.WorldThemeName(next)}!");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (IsInVillage) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) GoToMap(1);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) GoToMap(2);
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) GoToMap(3);
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) GoToMap(4);
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        RPGUI.Ensure();

        // Compact location chip — journey details live in I → Info.
        float panelW = 300f;
        float panelH = IsInVillage ? 72f : 88f;
        Rect panel = new Rect(Screen.width - panelW - 16f, 16f, panelW, panelH);
        RPGUI.DrawPanel(panel, RPGUI.PanelBg);

        float x = panel.x + 14f;
        float y = panel.y + 10f;
        float textW = panelW - 28f;

        if (IsInVillage)
        {
            RPGUI.DrawText(new Rect(x, y, textW, RPGUI.LabelH), "Village", RPGUI.Label);
            y += RPGUI.LabelH;
            RPGUI.DrawText(new Rect(x, y, textW, RPGUI.FieldTagH),
                $"Worlds 1–{highestWorldUnlocked}/{ProgressionFormulas.WorldCount}",
                RPGUI.FieldTag);
        }
        else
        {
            RPGUI.DrawText(new Rect(x, y, textW, RPGUI.LabelH),
                ProgressionFormulas.MapName(currentWorld, currentMap), RPGUI.Label);
            y += RPGUI.LabelH - 2f;
            RPGUI.DrawMapDots(new Rect(x, y, 140f, 20f), currentMap, 4, IsBossCleared(currentWorld, 4));
            RPGUI.DrawText(new Rect(x + 150f, y - 2f, textW - 150f, RPGUI.FieldTagH),
                $"W{currentWorld} · Lv{ContentLevel}", RPGUI.FieldTag);
        }

        if (worldPortalReady)
        {
            Rect tip = new Rect(Screen.width * 0.5f - 380f, 16f, 760f, RPGUI.BannerH);
            RPGUI.DrawPanel(tip, new Color(0.22f, 0.12f, 0.38f, 0.94f));
            RPGUI.DrawText(tip, "Boss portal open — walk through to enter the next world", RPGUI.Banner);
        }

        if (currentWorld == ProgressionFormulas.WorldCount && IsBossCleared(ProgressionFormulas.WorldCount, 4))
        {
            Rect tip = new Rect(Screen.width * 0.5f - 300f, 84f, 600f, RPGUI.BannerH);
            RPGUI.DrawPanel(tip, new Color(0.35f, 0.26f, 0.06f, 0.94f));
            RPGUI.DrawText(tip, $"Congratulations! All {ProgressionFormulas.WorldCount} worlds cleared!", RPGUI.Banner);
        }
    }
}
