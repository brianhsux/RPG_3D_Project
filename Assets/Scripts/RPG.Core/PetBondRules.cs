/// <summary>Pet bond point spend and legacy top-up. Ranks are mutated by the caller.</summary>
public static class PetBondRules
{
    public static bool CanTrain(int bondPoints, int rank, int maxRank)
    {
        return bondPoints > 0 && rank < maxRank;
    }

    public static bool TryTrain(ref int bondPoints, ref int rank, int maxRank)
    {
        if (!CanTrain(bondPoints, rank, maxRank)) return false;
        bondPoints--;
        rank++;
        return true;
    }

    /// <summary>If unspent + spent is below the level grant, top up unspent points.</summary>
    public static int TopUpUnspent(int bondPoints, int spentRanks, int level, int maxLevel)
    {
        if (bondPoints < 0) bondPoints = 0;
        if (spentRanks < 0) spentRanks = 0;
        int earned = level < 1 ? 1 : level;
        if (earned > maxLevel) earned = maxLevel;
        int have = bondPoints + spentRanks;
        if (have < earned) bondPoints += earned - have;
        return bondPoints;
    }
}
