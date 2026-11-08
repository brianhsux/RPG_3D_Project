/// <summary>Timed challenge pacing. Difficulty is 0 Normal, 1 Hard, 2 Extreme.</summary>
public static class ChallengeRules
{
    public const int TotalWaves = 10;

    public struct WaveSpawn
    {
        public int Count;
        public int LevelBonus;
        public bool MiniBoss;
    }

    public static float WaveIntervalSeconds(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 6.5f;
            case 2: return 4.5f;
            default: return 9f;
        }
    }

    public static float StatMultiplier(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 1.25f;
            case 2: return 1.55f;
            default: return 1f;
        }
    }

    public static float EliteChance(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 0.28f;
            case 2: return 0.48f;
            default: return 0.08f;
        }
    }

    public static int ExtraLevel(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 1;
            case 2: return 3;
            default: return 0;
        }
    }

    public static float RewardXpMultiplier(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 1.5f;
            case 2: return 2.2f;
            default: return 1f;
        }
    }

    public static WaveSpawn GetWave(int wave)
    {
        var spawn = new WaveSpawn();
        switch (wave)
        {
            case 1: spawn.Count = 2; break;
            case 2: spawn.Count = 3; break;
            case 3: spawn.Count = 3; spawn.LevelBonus = 1; break;
            case 4: spawn.Count = 4; spawn.LevelBonus = 1; break;
            case 5: spawn.Count = 4; spawn.LevelBonus = 2; break;
            case 6: spawn.Count = 5; spawn.LevelBonus = 2; break;
            case 7: spawn.Count = 5; spawn.LevelBonus = 3; break;
            case 8: spawn.Count = 6; spawn.LevelBonus = 3; break;
            case 9: spawn.Count = 4; spawn.LevelBonus = 4; break;
            default:
                spawn.Count = 1;
                spawn.LevelBonus = 5;
                spawn.MiniBoss = true;
                break;
        }
        return spawn;
    }

    public static int EnemyCount(int wave, int difficulty)
    {
        var spawn = GetWave(wave);
        int count = spawn.Count;
        if (!spawn.MiniBoss && difficulty == 1 && wave >= 4) count += 1;
        if (!spawn.MiniBoss && difficulty == 2 && wave >= 3) count += 2;
        return count;
    }

    public static void ChampionFactors(int difficulty, out float hp, out float attack)
    {
        if (difficulty == 2)
        {
            hp = 2.4f;
            attack = 1.5f;
        }
        else if (difficulty == 1)
        {
            hp = 2.0f;
            attack = 1.35f;
        }
        else
        {
            hp = 1.8f;
            attack = 1.25f;
        }
    }
}
