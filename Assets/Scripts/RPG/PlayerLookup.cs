using UnityEngine;

/// <summary>
/// The solo hero keeps gear, HP, and save data. Network avatars are also tagged Player,
/// so tag lookups must skip them or a save can read the wrong object.
/// </summary>
public static class PlayerLookup
{
    public static GameObject FindSoloHero()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null) continue;
            if (player.GetComponent<NetworkPlayer>() != null) continue;
            var stats = player.GetComponent<CharacterStats>();
            if (stats != null && stats.isPlayer) return player;
        }
        return null;
    }
}
