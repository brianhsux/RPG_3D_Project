using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Simple IMGUI panel for NGO Host / Client / Disconnect.
/// Toggle with N. Independent from the single-player account flow.
/// </summary>
public class NetHud : MonoBehaviour
{
    public static NetHud Instance { get; private set; }

    public bool showPanel = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (FindFirstObjectByType<NetHud>() != null) return;
        var go = new GameObject("NetHud");
        go.AddComponent<NetHud>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<NetBootstrap>() == null && NetBootstrap.Instance == null)
            gameObject.AddComponent<NetBootstrap>();
        if (GetComponent<NetCombatSession>() == null)
            gameObject.AddComponent<NetCombatSession>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame)
            showPanel = !showPanel;
    }

    void OnGUI()
    {
        if (!showPanel) return;
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady)
            return;
        RPGUI.Ensure();

        var boot = NetBootstrap.Instance;
        if (boot == null) return;

        float w = 340f;
        float h = boot.IsNetworking ? 200f : 168f;
        Rect panel = new Rect(16f, Screen.height - h - 16f, w, h);
        RPGUI.DrawPanel(panel, RPGUI.PanelBg);

        float x = panel.x + 12f;
        float y = panel.y + 10f;
        float tw = w - 24f;

        RPGUI.DrawText(new Rect(x, y, tw, RPGUI.HintH), "Multiplayer (NGO)  ·  N hide", RPGUI.Hint);
        y += RPGUI.HintH + 2f;
        RPGUI.DrawText(new Rect(x, y, tw, RPGUI.FieldTagH), boot.StatusLabel, RPGUI.FieldTag);
        y += RPGUI.FieldTagH + 8f;

        bool busy = boot.IsNetworking;
        float btnW = (tw - 8f) * 0.5f;

        if (!busy)
        {
            if (RPGUI.ButtonSm(new Rect(x, y, btnW, RPGUI.ButtonSmH), "Host", true))
                boot.StartHost();
            if (RPGUI.ButtonSm(new Rect(x + btnW + 8f, y, btnW, RPGUI.ButtonSmH), "Client"))
                boot.StartClient();
            y += RPGUI.ButtonSmH + 8f;
            RPGUI.DrawText(new Rect(x, y, tw, RPGUI.FieldTagH),
                $"{boot.connectAddress}:{boot.connectPort}", RPGUI.FieldTag);
        }
        else
        {
            if (RPGUI.ButtonSm(new Rect(x, y, tw, RPGUI.ButtonSmH), "Disconnect", true))
                boot.Shutdown();
            y += RPGUI.ButtonSmH + 6f;

            string hpLine = LocalHpLabel();
            if (!string.IsNullOrEmpty(hpLine))
            {
                RPGUI.DrawText(new Rect(x, y, tw, RPGUI.FieldTagH), hpLine, RPGUI.FieldTag);
                y += RPGUI.FieldTagH + 2f;
            }

            RPGUI.DrawText(new Rect(x, y, tw, RPGUI.FieldTagH),
                "LMB/F · shared map portals · killer loot", RPGUI.FieldTag);
        }
    }

    static string LocalHpLabel()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || (!nm.IsListening && !nm.IsClient)) return null;

        NetworkPlayerCombat local = null;
        var combats = FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            if (combats[i] != null && combats[i].IsOwner)
            {
                local = combats[i];
                break;
            }
        }
        if (local == null || !local.IsSpawned) return null;
        return $"HP {Mathf.CeilToInt(local.CurrentHp.Value)}/{Mathf.CeilToInt(local.MaxHp.Value)}";
    }
}
