using UnityEngine;

public class MapExitTrigger : MonoBehaviour
{
    public int targetMap = 1;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (AdventureManager.Instance == null) return;
        AdventureManager.Instance.GoToMap(targetMap);
    }
}

/// <summary>
/// Covers one map tile square. Walking into it updates the current map index.
/// </summary>
public class MapTileZone : MonoBehaviour
{
    public int mapIndex = 1;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (AdventureManager.Instance == null) return;
        AdventureManager.Instance.EnterTile(mapIndex);
    }
}

public class WorldPortalTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (AdventureManager.Instance == null) return;
        AdventureManager.Instance.EnterNextWorld();
    }
}

/// <summary>Village hub: walk into an unlocked gate to enter that adventure world.</summary>
public class VillageWorldPortalTrigger : MonoBehaviour
{
    public int targetWorld = 1;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (AdventureManager.Instance == null) return;
        AdventureManager.Instance.EnterWorldFromVillage(targetWorld);
    }
}

/// <summary>Adventure hub: walk into the gate to return to the Village.</summary>
public class VillageReturnTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (AdventureManager.Instance == null) return;
        AdventureManager.Instance.ReturnToVillage();
    }
}

/// <summary>World-space name tag for village world portals.</summary>
public class WorldPortalLabel : MonoBehaviour
{
    public int worldId = 1;
    public bool unlocked;

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;
        if (AdventureManager.Instance == null) return;
        // Show in village (world gates) and in adventure hubs (return gate).
        bool inVillage = AdventureManager.Instance.IsInVillage;
        if (!inVillage && worldId != 0) return;
        if (inVillage && worldId == 0) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.6f);
        if (screen.z < 0.5f) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (Vector3.Distance(player.transform.position, transform.position) > 18f) return;

        RPGUI.Ensure();
        float w = 220f;
        float h = 36f;
        Rect r = new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h, w, h);
        Color bg = unlocked
            ? new Color(0.12f, 0.22f, 0.14f, 0.9f)
            : new Color(0.12f, 0.12f, 0.14f, 0.88f);
        RPGUI.DrawPanel(r, bg);
        string text;
        if (worldId == 0)
            text = "Return to Village";
        else
        {
            string name = ProgressionFormulas.WorldThemeName(worldId);
            text = unlocked ? $"W{worldId} {name}" : $"W{worldId} {name} (Locked)";
        }
        RPGUI.DrawText(r, text, RPGUI.Hint);
    }
}
