using System.Reflection;
using Unity.Netcode;
using UnityEngine;

/// <summary>Shared NGO role checks for adventure / combat gating.</summary>
public static class NetPlay
{
    public const uint RuntimeAdventureHash = 0x52504701;
    public const uint RuntimeEnemyHash = 0x52504702;
    public const uint RuntimePlayerHash = 0x52504703;

    /// <summary>
    /// Runtime templates are not assets, so NGO leaves their prefab hash at 0.
    /// Every such object would collide, and a joining client could not spawn them.
    /// </summary>
    public static void StampRuntimePrefab(GameObject prefab, uint hash)
    {
        if (prefab == null || hash == 0)
            return;
        var networkObject = prefab.GetComponent<NetworkObject>();
        if (networkObject == null)
            return;
        var field = typeof(NetworkObject).GetField(
            "GlobalObjectIdHash",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            return;
        if (field.GetValue(networkObject) is uint current && current != 0)
            return;
        field.SetValue(networkObject, hash);
    }

    public static bool IsNetworking =>
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient);

    public static bool IsServer =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    /// <summary>Pure client (not host/server) — should skip local enemy spawn.</summary>
    public static bool IsClientOnly =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsClient &&
        !NetworkManager.Singleton.IsServer;
}
