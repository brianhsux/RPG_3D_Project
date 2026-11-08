using NUnit.Framework;

public class AdventureGearPetTests
{
    [Test]
    public void TravelAllowsVisitedWorldsAndTheNextPortalOnly()
    {
        Assert.IsTrue(AdventureProgress.CanEnterWorld(0, 4, false, false, 4, 25));
        Assert.IsTrue(AdventureProgress.CanEnterWorld(2, 4, true, false, 4, 25));
        Assert.IsTrue(AdventureProgress.CanEnterWorld(5, 4, false, true, 4, 25));
        Assert.IsFalse(AdventureProgress.CanEnterWorld(5, 4, true, true, 4, 25));
        Assert.IsFalse(AdventureProgress.CanEnterWorld(6, 4, false, true, 4, 25));
        Assert.IsFalse(AdventureProgress.CanEnterWorld(26, 4, false, true, 25, 25));
    }

    [Test]
    public void HostSnapshotDoesNotErasePersonalUnlocks()
    {
        var personal = new AdventureProgress.Snapshot
        {
            HighestWorldUnlocked = 10,
            ClearedBossKeys = new[] { "W10M4", "W3M4" }
        };

        var merged = AdventureProgress.Merge(personal, sessionHighest: 2, sessionBossCsv: "W2M4, W3M4, ");

        Assert.AreEqual(10, merged.HighestWorldUnlocked);
        CollectionAssert.AreEqual(new[] { "W10M4", "W2M4", "W3M4" }, merged.ClearedBossKeys);
    }

    [Test]
    public void MergeCanRaiseHighestAndParseEmptyCsv()
    {
        var personal = new AdventureProgress.Snapshot
        {
            HighestWorldUnlocked = 1,
            ClearedBossKeys = null
        };
        var merged = AdventureProgress.Merge(personal, 8, "");
        Assert.AreEqual(8, merged.HighestWorldUnlocked);
        Assert.AreEqual(0, merged.ClearedBossKeys.Length);
        Assert.AreEqual("W4M2", AdventureProgress.BossKey(4, 2));
    }

    [Test]
    public void GearTieBreaksByRarityThenLevel()
    {
        Assert.IsTrue(GearRules.IsBetter(10f, 1, 1, true, 10f, 0, 50));
        Assert.IsFalse(GearRules.IsBetter(10f, 1, 4, true, 10.02f, 1, 1));
        Assert.IsTrue(GearRules.IsBetter(10.02f, 0, 1, true, 10f, 4, 9));
        Assert.IsTrue(GearRules.IsBetter(1f, 0, 1, false, 0f, 0, 0));
        Assert.IsFalse(GearRules.IsBetter(5f, 2, 3, true, 5f, 2, 3));
        Assert.IsTrue(GearRules.IsBetter(5f, 2, 4, true, 5f, 2, 3));
    }

    [Test]
    public void WeaponAndArmorScoresMatchInventoryWeights()
    {
        Assert.AreEqual(10f + 0.1f * 18f + 0.5f * 8f, GearRules.WeaponScore(10f, 0.1f, 0.5f), 0.0001f);
        Assert.AreEqual(8f * 2.5f + 40f * 0.12f, GearRules.ArmorScore(8f, 40f), 0.0001f);
    }

    [Test]
    public void RecursiveCraftsConsumeProducts()
    {
        Assert.AreEqual(0, GearRules.CountRecursiveCrafts(2));
        Assert.AreEqual(1, GearRules.CountRecursiveCrafts(4));
        Assert.AreEqual(2, GearRules.CountRecursiveCrafts(5));
        Assert.AreEqual(3, GearRules.CountRecursiveCrafts(8));
    }

    [Test]
    public void BondTrainingStopsAtZeroPointsAndMaxRank()
    {
        int points = 1;
        int rank = 20;
        Assert.IsFalse(PetBondRules.TryTrain(ref points, ref rank, 20));
        Assert.AreEqual(1, points);

        rank = 19;
        Assert.IsTrue(PetBondRules.TryTrain(ref points, ref rank, 20));
        Assert.AreEqual(0, points);
        Assert.AreEqual(20, rank);
        Assert.IsFalse(PetBondRules.TryTrain(ref points, ref rank, 20));
    }

    [Test]
    public void LegacyBondTopUpFillsMissingPointsWithoutRemovingExtras()
    {
        Assert.AreEqual(5, PetBondRules.TopUpUnspent(1, 2, level: 7, maxLevel: 100));
        Assert.AreEqual(5, PetBondRules.TopUpUnspent(5, 4, level: 7, maxLevel: 100));
    }

    [Test]
    public void PortDraftDoesNotCoercePartialOrInvalidText()
    {
        Assert.IsFalse(ConnectionDraft.TryParsePort("", out _));
        Assert.IsFalse(ConnectionDraft.TryParsePort("0", out _));
        Assert.IsFalse(ConnectionDraft.TryParsePort("7777a", out _));
        Assert.IsFalse(ConnectionDraft.TryParsePort("65536", out _));
        Assert.IsTrue(ConnectionDraft.TryParsePort(" 25565 ", out int port));
        Assert.AreEqual(25565, port);
        Assert.AreEqual("10.0.0.8", ConnectionDraft.NormalizeAddress(" 10.0.0.8 "));
        Assert.AreEqual(ConnectionDraft.DefaultAddress, ConnectionDraft.NormalizeAddress("   "));
    }

    [Test]
    public void ChallengeWaveTenIsTheChampionAndHardAddsBodies()
    {
        var champ = ChallengeRules.GetWave(10);
        Assert.IsTrue(champ.MiniBoss);
        Assert.AreEqual(1, ChallengeRules.EnemyCount(10, 2));
        Assert.AreEqual(5, ChallengeRules.EnemyCount(4, 1));
        Assert.AreEqual(5, ChallengeRules.EnemyCount(3, 2));
        Assert.AreEqual(3, ChallengeRules.EnemyCount(3, 0));
        Assert.AreEqual(9f, ChallengeRules.WaveIntervalSeconds(0), 0.0001f);
        Assert.AreEqual(2.2f, ChallengeRules.RewardXpMultiplier(2), 0.0001f);
        ChallengeRules.ChampionFactors(2, out float hp, out float atk);
        Assert.AreEqual(2.4f, hp, 0.0001f);
        Assert.AreEqual(1.5f, atk, 0.0001f);
    }
}
