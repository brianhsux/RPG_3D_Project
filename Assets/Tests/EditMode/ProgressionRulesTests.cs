using NUnit.Framework;

public class ProgressionRulesTests
{
    [Test]
    public void ContentLevelBandsCoverOneHundredMaps()
    {
        Assert.AreEqual(1, ProgressionRules.ContentLevel(1, 1));
        Assert.AreEqual(4, ProgressionRules.ContentLevel(1, 4));
        Assert.AreEqual(5, ProgressionRules.ContentLevel(2, 1));
        Assert.AreEqual(100, ProgressionRules.ContentLevel(25, 4));
        Assert.AreEqual(1, ProgressionRules.ContentLevel(0, 0));
    }

    [Test]
    public void WorldFromContentLevelStaysInsideTheBand()
    {
        Assert.AreEqual(1, ProgressionRules.WorldFromContentLevel(1));
        Assert.AreEqual(1, ProgressionRules.WorldFromContentLevel(4));
        Assert.AreEqual(2, ProgressionRules.WorldFromContentLevel(5));
        Assert.AreEqual(25, ProgressionRules.WorldFromContentLevel(97));
        Assert.AreEqual(25, ProgressionRules.WorldFromContentLevel(100));
    }

    [Test]
    public void XpCurveLevelsUpAcrossMultipleThresholds()
    {
        Assert.AreEqual(65, ProgressionRules.XpToNextLevel(1));
        var once = ProgressionRules.ApplyXp(1, 0, 65);
        Assert.AreEqual(1, once.LevelsGained);
        Assert.AreEqual(2, once.Level);
        Assert.AreEqual(0, once.Xp);

        int need = ProgressionRules.XpToNextLevel(1) + ProgressionRules.XpToNextLevel(2);
        var twice = ProgressionRules.ApplyXp(1, 10, need - 10);
        Assert.AreEqual(2, twice.LevelsGained);
        Assert.AreEqual(3, twice.Level);
        Assert.AreEqual(0, twice.Xp);
    }

    [Test]
    public void XpSafetyCapStopsRunawayLevels()
    {
        var capped = ProgressionRules.ApplyXp(1, 0, int.MaxValue, safetyCap: 3);
        Assert.AreEqual(3, capped.LevelsGained);
        Assert.AreEqual(4, capped.Level);
        Assert.Greater(capped.Xp, 0);
    }

    [Test]
    public void SynthesisLevelUsesAveragePlusRareBonusAndCaps()
    {
        Assert.AreEqual(13, ProgressionRules.SynthesisItemLevel(10, 11, 12, 0, 0, 0));
        Assert.AreEqual(14, ProgressionRules.SynthesisItemLevel(10, 11, 12, 2, 2, 3));
        Assert.AreEqual(100, ProgressionRules.SynthesisItemLevel(100, 100, 100, 4, 4, 4));
    }

    [Test]
    public void RarityBumpIsStrictlyBelowFifteenPercent()
    {
        Assert.AreEqual(3, ProgressionRules.BumpRarity(2, 0.149f));
        Assert.AreEqual(2, ProgressionRules.BumpRarity(2, 0.15f));
        Assert.AreEqual(4, ProgressionRules.BumpRarity(4, 0f));
    }

    [Test]
    public void WeaponProfileMajorityKeepsFirstWhenAllDiffer()
    {
        Assert.AreEqual(2, ProgressionRules.MajorityWeaponProfile(0, 2, 2));
        Assert.AreEqual(1, ProgressionRules.MajorityWeaponProfile(1, 0, 1));
        Assert.AreEqual(0, ProgressionRules.MajorityWeaponProfile(0, 1, 2));
    }

    [Test]
    public void BossXpIsFiveTimesTrash()
    {
        Assert.AreEqual(ProgressionRules.MobXpReward(10, false) * 5, ProgressionRules.MobXpReward(10, true));
    }
}
