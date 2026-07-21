using Unity.Netcode;

/// <summary>Shared NGO role checks for adventure / combat gating.</summary>
public static class NetPlay
{
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
