/// <summary>Bag scoring and craft-count rules used by the inventory.</summary>
public static class GearRules
{
    public const float ScoreEpsilon = 0.01f;
    public const float WeaponSpeedWeight = 18f;
    public const float WeaponRangeWeight = 8f;
    public const float ArmorDefenseWeight = 2.5f;
    public const float ArmorHpWeight = 0.12f;

    public static float WeaponScore(float attack, float attackSpeedBonus, float attackRangeBonus)
    {
        return attack + attackSpeedBonus * WeaponSpeedWeight + attackRangeBonus * WeaponRangeWeight;
    }

    public static float ArmorScore(float defense, float maxHpBonus)
    {
        return defense * ArmorDefenseWeight + maxHpBonus * ArmorHpWeight;
    }

    public static bool IsBetter(
        float candidateScore,
        int candidateRarity,
        int candidateLevel,
        bool hasCurrent,
        float currentScore,
        int currentRarity,
        int currentLevel)
    {
        if (!hasCurrent) return true;
        if (candidateScore > currentScore + ScoreEpsilon) return true;
        if (candidateScore < currentScore - ScoreEpsilon) return false;
        if (candidateRarity != currentRarity) return candidateRarity > currentRarity;
        return candidateLevel > currentLevel;
    }

    /// <summary>How many crafts a pile of identical items eventually produces (3 → 1).</summary>
    public static int CountRecursiveCrafts(int count)
    {
        int total = 0;
        while (count >= 3)
        {
            int batches = count / 3;
            total += batches;
            count = batches + (count % 3);
        }
        return total;
    }
}
