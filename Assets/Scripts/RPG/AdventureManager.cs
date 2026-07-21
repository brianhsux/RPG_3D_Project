using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 10 worlds × 4 map tiles in a physical layout per world.
/// Walk between tiles via corridors. Each tile has its own monsters.
/// </summary>
public class AdventureManager : MonoBehaviour
{
    public static AdventureManager Instance { get; private set; }

    public int currentWorld = 1;
    public int currentMap = 1;
    public int highestWorldUnlocked = 1;

    public bool mapBossDefeated;
    public bool worldPortalReady;
    public int killCount;

    readonly HashSet<string> _clearedBossKeys = new HashSet<string>();

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
        LoadWorld(currentWorld, currentMap);
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public void ApplySave(AccountSaveData data)
    {
        if (data == null) data = new AccountSaveData();

        currentWorld = Mathf.Clamp(data.currentWorld, 1, ProgressionFormulas.WorldCount);
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

    public int ContentLevel => ProgressionFormulas.ContentLevel(currentWorld, currentMap);

    public string BossKey(int world, int map) => $"W{world}M{map}";

    public bool IsBossCleared(int world, int map) => _clearedBossKeys.Contains(BossKey(world, map));

    public void LoadWorld(int world, int startMap = 1)
    {
        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        startMap = Mathf.Clamp(startMap, 1, ProgressionFormulas.MapsPerWorld);

        if (world > highestWorldUnlocked)
        {
            Debug.Log("World locked.");
            return;
        }

        currentWorld = world;
        currentMap = startMap;
        RefreshBossFlags();

        if (MapRuntimeBuilder.Instance != null)
            MapRuntimeBuilder.Instance.BuildCurrentWorld();

        PlacePlayerInTile(startMap);
    }

    public void LoadMap(int world, int map, bool forceRebuild = false)
    {
        if (world != currentWorld || forceRebuild)
            LoadWorld(world, map);
        else
            GoToMap(map);
    }

    void RefreshBossFlags()
    {
        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
        worldPortalReady = IsBossCleared(currentWorld, 4) && currentWorld < ProgressionFormulas.WorldCount;
    }

    public void EnterTile(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        if (currentMap == map) return;
        currentMap = map;
        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
    }

    public void GoToMap(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        currentMap = map;
        mapBossDefeated = IsBossCleared(currentWorld, currentMap);
        PlacePlayerInTile(map);
    }

    void PlacePlayerInTile(int map)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || MapRuntimeBuilder.Instance == null) return;

        Vector3 pos = MapRuntimeBuilder.Instance.GetTileSpawn(map);
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
                    ? "World Boss defeated! Portal opened"
                    : $"Map {map} Boss defeated!";
                PlayerInventory.Instance.ShowLoot(msg);
            }

            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
    }

    public void EnterNextWorld()
    {
        if (!worldPortalReady) return;
        int next = currentWorld + 1;
        if (next > ProgressionFormulas.WorldCount) return;
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

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        // Convenience: teleport to a tile (still physically separate rooms)
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

        float panelW = 380f;
        float panelH = 168f;
        Rect panel = new Rect(Screen.width - panelW - 16f, 16f, panelW, panelH);
        RPGUI.DrawPanel(panel, RPGUI.PanelBg);

        float x = panel.x + 14f;
        float y = panel.y + 10f;
        float textW = panelW - 28f;

        RPGUI.DrawText(new Rect(x, y, textW, RPGUI.LabelH),
            ProgressionFormulas.MapName(currentWorld, currentMap), RPGUI.Label);
        y += RPGUI.LabelH;

        RPGUI.DrawText(new Rect(x, y, textW, RPGUI.HintH),
            $"World {currentWorld}/{ProgressionFormulas.WorldCount} · Zone {currentMap}/4 · Power Lv{ContentLevel}",
            RPGUI.Hint);
        y += RPGUI.HintH + 6f;

        RPGUI.DrawMapDots(new Rect(x, y, 160f, 22f), currentMap, 4, IsBossCleared(currentWorld, 4));
        string bossText = currentMap == 4
            ? (mapBossDefeated ? "Boss defeated" : "Boss not defeated")
            : "Normal mobs ×4";
        RPGUI.DrawText(new Rect(x + 170f, y - 2f, textW - 170f, RPGUI.HintH), bossText, RPGUI.Hint);
        y += 28f;

        RPGUI.DrawText(new Rect(x, y, textW, RPGUI.FieldTagH), "1–4 Teleport zones", RPGUI.FieldTag);

        if (worldPortalReady)
        {
            Rect tip = new Rect(Screen.width * 0.5f - 360f, 16f, 720f, RPGUI.BannerH);
            RPGUI.DrawPanel(tip, new Color(0.22f, 0.12f, 0.38f, 0.94f));
            RPGUI.DrawText(tip, "Portal open — walk to the glowing gate for the next world", RPGUI.Banner);
        }

        if (currentWorld == ProgressionFormulas.WorldCount && IsBossCleared(ProgressionFormulas.WorldCount, 4))
        {
            Rect tip = new Rect(Screen.width * 0.5f - 300f, 84f, 600f, RPGUI.BannerH);
            RPGUI.DrawPanel(tip, new Color(0.35f, 0.26f, 0.06f, 0.94f));
            RPGUI.DrawText(tip, $"Congratulations! All {ProgressionFormulas.WorldCount} worlds cleared!", RPGUI.Banner);
        }
    }
}
