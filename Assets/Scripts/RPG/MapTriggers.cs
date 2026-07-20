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
