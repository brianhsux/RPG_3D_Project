using System;
using System.Collections.Generic;

/// <summary>
/// Personal unlocks survive a host snapshot. The session view may follow the host,
/// but the save file keeps the union so co-op cannot erase solo progress.
/// </summary>
public static class AdventureProgress
{
    public struct Snapshot
    {
        public int HighestWorldUnlocked;
        public string[] ClearedBossKeys;
    }

    public static bool CanEnterWorld(
        int targetWorld,
        int currentWorld,
        bool inVillage,
        bool portalReady,
        int highestUnlocked,
        int worldCount)
    {
        if (targetWorld == ProgressionRules.VillageWorldId) return true;
        if (targetWorld < 1 || targetWorld > worldCount) return false;
        if (targetWorld <= highestUnlocked) return true;
        return targetWorld == currentWorld + 1 && !inVillage && portalReady;
    }

    public static string BossKey(int world, int map) => "W" + world + "M" + map;

    public static Snapshot Merge(Snapshot personal, int sessionHighest, string sessionBossCsv)
    {
        int highest = personal.HighestWorldUnlocked;
        if (sessionHighest > highest) highest = sessionHighest;
        highest = ProgressionRules.Clamp(highest, 1, ProgressionRules.WorldCount);

        var keys = new List<string>();
        AddKeys(keys, personal.ClearedBossKeys);
        AddCsv(keys, sessionBossCsv);
        keys.Sort(StringComparer.Ordinal);
        return new Snapshot
        {
            HighestWorldUnlocked = highest,
            ClearedBossKeys = keys.ToArray()
        };
    }

    public static string[] ParseBossKeyCsv(string csv)
    {
        var keys = new List<string>();
        AddCsv(keys, csv);
        return keys.ToArray();
    }

    static void AddKeys(List<string> into, string[] keys)
    {
        if (keys == null) return;
        for (int i = 0; i < keys.Length; i++)
            AddOne(into, keys[i]);
    }

    static void AddCsv(List<string> into, string csv)
    {
        if (string.IsNullOrEmpty(csv)) return;
        var parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
            AddOne(into, parts[i]);
    }

    static void AddOne(List<string> into, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        key = key.Trim();
        if (key.Length == 0) return;
        for (int i = 0; i < into.Count; i++)
        {
            if (into[i] == key) return;
        }
        into.Add(key);
    }
}
