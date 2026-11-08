using System;

/// <summary>Balance sheet shared by gameplay code. No UnityEngine dependency.</summary>
public static class ProgressionRules
{
    public const int WorldCount = 25;
    public const int MapsPerWorld = 4;
    public const int MaxContentLevel = WorldCount * MapsPerWorld;
    public const int MaxItemLevel = 100;
    public const int MaxPetLevel = 100;
    public const int VillageWorldId = 0;
    public const int SoftLevelMilestone = 60;

    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static int RoundToInt(float value)
    {
        return (int)Math.Round(value, MidpointRounding.ToEven);
    }

    public static int ContentLevel(int world, int map)
    {
        world = Clamp(world, 1, WorldCount);
        map = Clamp(map, 1, MapsPerWorld);
        return (world - 1) * MapsPerWorld + map;
    }

    /// <summary>Content band for a world, ignoring bonus levels added by challenges.</summary>
    public static int WorldFromContentLevel(int contentLevel)
    {
        contentLevel = Clamp(contentLevel, 1, MaxContentLevel);
        return (contentLevel - 1) / MapsPerWorld + 1;
    }

    public static int ContentToItemLevel(int contentLevel)
    {
        return Clamp(contentLevel, 1, MaxItemLevel);
    }

    public static float RarityMultiplier(int rarity)
    {
        switch (rarity)
        {
            case 0: return 1.00f;
            case 1: return 1.15f;
            case 2: return 1.35f;
            case 3: return 1.60f;
            case 4: return 2.00f;
            default: return 1f;
        }
    }

    public static int XpToNextLevel(int level)
    {
        level = level < 1 ? 1 : level;
        float linear = level * 28f;
        float quad = level * level * 1.8f;
        float late = level > SoftLevelMilestone
            ? (level - SoftLevelMilestone) * (level - SoftLevelMilestone) * 0.35f
            : 0f;
        int value = RoundToInt(35f + linear + quad + late);
        return value < 40 ? 40 : value;
    }

    public struct XpProgress
    {
        public int Level;
        public int Xp;
        public int LevelsGained;
    }

    public static XpProgress ApplyXp(int level, int xp, int amount, int safetyCap = 1000)
    {
        var result = new XpProgress
        {
            Level = level < 1 ? 1 : level,
            Xp = xp < 0 ? 0 : xp,
            LevelsGained = 0
        };
        if (amount <= 0) return result;

        result.Xp += amount;
        int safety = 0;
        while (result.Xp >= XpToNextLevel(result.Level) && safety++ < safetyCap)
        {
            result.Xp -= XpToNextLevel(result.Level);
            result.Level++;
            result.LevelsGained++;
        }
        return result;
    }

    public static int MobXpReward(int contentLevel, bool isBoss)
    {
        int xp = 12 + contentLevel * 8 + contentLevel * contentLevel / 4;
        return isBoss ? xp * 5 : xp;
    }

    public static int SynthesisItemLevel(int levelA, int levelB, int levelC, int rarityA, int rarityB, int rarityC)
    {
        int avg = RoundToInt((levelA + levelB + levelC) / 3f);
        bool allRare = rarityA >= 2 && rarityB >= 2 && rarityC >= 2;
        int bonus = allRare ? 3 : 2;
        return Clamp(avg + bonus, 1, MaxItemLevel);
    }

    public static int BumpRarity(int bestRarity, float roll)
    {
        if (roll < 0.15f && bestRarity < 4) return bestRarity + 1;
        return bestRarity;
    }

    /// <summary>Majority weapon profile. All different keeps the first item.</summary>
    public static int MajorityWeaponProfile(int a, int b, int c)
    {
        if (b == c) return b;
        if (a == c) return a;
        if (a == b) return a;
        return a;
    }

    public static float AverageWeaponBonus(float a, float b, float c)
    {
        return (a + b + c) / 3f * 1.08f;
    }

    public static bool SameGemKind(int kindA, int kindB, int kindC)
    {
        return kindA == kindB && kindB == kindC;
    }
}
