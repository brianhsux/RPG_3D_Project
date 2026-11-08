using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Drives the real RPGBattle hero. Scripted hits call CharacterStats.DealAttack, which is
/// the same method PlayerController uses. A follow-up lets that controller fire on its own.
/// </summary>
public class SoloCombatPlayModeTests
{
    const int KillSeed = 424242;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        return PlayModeWorld.Begin(PlayModeWorld.SoloAccount);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        return PlayModeWorld.End();
    }

    [UnityTest]
    [Timeout(60000)]
    public IEnumerator BattleScene_AttackCritDropAndXp_MatchSoloRules()
    {
        var player = PlayModeWorld.RequireSoloHero();
        var stats = player.GetComponent<CharacterStats>();
        var inventory = player.GetComponent<PlayerInventory>();
        var progression = PlayerProgression.Instance;
        var controller = player.GetComponent<PlayerController>();
        Assert.IsNotNull(controller);
        Assert.IsFalse(controller.enabled, "Auto-attack must stay off while scripted hits run.");

        var enemy = PlayModeWorld.SpawnSoloEnemy(4, false, player.transform.position + new Vector3(40f, 0f, 0f));
        enemy.currentHealth = 100000f;
        enemy.maxHealth = 100000f;

        stats.critChance = 0f;
        stats.critDamageBonus = 0.5f;
        stats.fireBonus = 3f;
        stats.iceBonus = 2f;
        stats.poisonBonus = 1f;
        stats.lifesteal = 0f;
        stats.blockChance = 0f;
        stats.reflectPercent = 0f;

        float enemyBefore = enemy.currentHealth;
        Random.InitState(7);
        float dealt = stats.DealAttack(enemy);
        float expected = CombatMath.OutgoingDamage(
            stats.attackDamage, 0f, 0.5f, 0.5f, 3f, 2f, 1f);
        Assert.AreEqual(expected, dealt, 0.02f, "Non-crit hit should be attack plus elements.");
        Assert.AreEqual(enemyBefore - dealt, enemy.currentHealth, 0.02f);
        Assert.Greater(dealt, 0.5f);

        stats.critChance = 1f;
        stats.critDamageBonus = 0.8f;
        stats.lifesteal = 0.25f;
        stats.currentHealth = stats.maxHealth * 0.4f;
        float heroBefore = stats.currentHealth;
        Random.InitState(11);
        float critDealt = stats.DealAttack(enemy);
        float expectedCrit = CombatMath.OutgoingDamage(
            stats.attackDamage, 1f, 0.8f, 0f, 3f, 2f, 1f);
        Assert.AreEqual(expectedCrit, critDealt, 0.05f, "Guaranteed crit should match CombatMath.");
        Assert.Greater(expectedCrit, expected, "Crit hit should exceed the non-crit hit.");
        float heal = CombatMath.LifestealHeal(critDealt, 0.25f);
        Assert.AreEqual(
            Mathf.Min(stats.maxHealth, heroBefore + heal),
            stats.currentHealth,
            0.05f,
            "Lifesteal should return a fraction of the dealt hit.");

        stats.defense = 50f;
        stats.damageReduction = 0.2f;
        stats.blockChance = 0f;
        stats.reflectPercent = 0.5f;
        stats.currentHealth = stats.maxHealth;
        float foeBefore = enemy.currentHealth;
        float taken = stats.TakeDamage(80f, enemy);
        float expectedTaken = CombatMath.MitigatePlayerIncoming(80f, 50f, 0.2f, blocked: false);
        Assert.AreEqual(expectedTaken, taken, 0.05f, "Player mitigation order drifted from CombatMath.");
        float reflected = CombatMath.ReflectDamage(taken, 0.5f, defenderIsPlayer: true);
        Assert.Greater(reflected, 0.5f);
        Assert.AreEqual(foeBefore - reflected, enemy.currentHealth, 0.05f, "Reflect should hit the attacker.");

        var sponge = PlayModeWorld.SpawnSoloEnemy(1, false, player.transform.position + new Vector3(50f, 0f, 0f));
        sponge.currentHealth = 10f;
        sponge.maxHealth = 10f;
        stats.attackDamage = 0.2f;
        stats.critChance = 0f;
        stats.fireBonus = 0f;
        stats.iceBonus = 0f;
        stats.poisonBonus = 0f;
        stats.lifesteal = 0f;
        stats.reflectPercent = 0f;
        float tiny = stats.DealAttack(sponge);
        Assert.Greater(tiny, 0f);
        Assert.Less(tiny, 0.5f);
        Assert.AreEqual(10f - tiny, sponge.currentHealth, 0.0001f, "Sub-0.5 hits must still reduce HP.");

        inventory.ApplyEquipmentStats(true);
        stats.critChance = 0f;
        stats.fireBonus = 0f;
        stats.iceBonus = 0f;
        stats.poisonBonus = 0f;
        stats.lifesteal = 0f;
        stats.blockChance = 0f;
        stats.reflectPercent = 0f;

        var victim = PlayModeWorld.SpawnSoloEnemy(6, false, player.transform.position + new Vector3(60f, 0f, 0f));
        victim.currentHealth = 1f;
        int levelBefore = progression.level;
        int xpBefore = progression.xp;
        int bagBefore = inventory.CountGearInBag() + inventory.CountGemsInBag();
        bool expectDrop = ReplayTrashDrop(KillSeed);

        Random.InitState(KillSeed);
        stats.DealAttack(victim);

        Assert.IsTrue(victim.isDead, "A killing blow should mark the enemy dead and roll loot.");
        int reward = ProgressionFormulas.MobXpReward(6, false);
        var gained = ProgressionRules.ApplyXp(levelBefore, xpBefore, reward);
        Assert.AreEqual(gained.Level, progression.level);
        Assert.AreEqual(gained.Xp, progression.xp);
        int bagAfter = inventory.CountGearInBag() + inventory.CountGemsInBag();
        if (expectDrop)
            Assert.Greater(bagAfter, bagBefore, "Seeded trash roll should have dropped gear.");
        else
            Assert.AreEqual(bagBefore, bagAfter, "Seeded trash roll should have skipped the drop.");

        stats.critChance = 1f;
        stats.critDamageBonus = 0.8f;
        stats.fireBonus = 4f;
        stats.iceBonus = 0f;
        stats.poisonBonus = 0f;
        stats.lifesteal = 0f;
        float autoHit = CombatMath.OutgoingDamage(stats.attackDamage, 1f, 0.8f, 0f, 4f, 0f, 0f);

        var dummy = PlayModeWorld.SpawnSoloEnemy(2, false, player.transform.position + Vector3.forward * 1.4f);
        dummy.currentHealth = 100000f;
        dummy.maxHealth = 100000f;
        var body = player.GetComponent<CharacterController>();
        if (body != null)
            body.enabled = false;
        player.transform.position = new Vector3(120f, 1f, 120f);
        dummy.transform.position = player.transform.position + Vector3.forward * 1.4f;
        if (body != null)
            body.enabled = true;
        controller.gravity = 0f;
        controller.enabled = true;

        float autoBefore = dummy.currentHealth;
        yield return PlayModeWorld.WaitUntil(
            () => dummy.currentHealth < autoBefore - autoHit * 0.5f,
            3f,
            "PlayerController did not auto-attack the enemy standing in range.");

        controller.enabled = false;
        float lost = autoBefore - dummy.currentHealth;
        float hits = Mathf.Round(lost / autoHit);
        Assert.GreaterOrEqual(hits, 1f);
        Assert.AreEqual(autoHit * hits, lost, 0.25f, "Live auto-attack damage should be whole crit hits.");
    }

    /// <summary>
    /// DealAttack consumes one roll, then a popup consumes two ranges, then the drop roll.
    /// Keep this aligned with CharacterStats.DealAttack / TakeDamage / DamageNumbers.Spawn.
    /// </summary>
    static bool ReplayTrashDrop(int seed)
    {
        Random.InitState(seed);
        _ = Random.value;
        Random.Range(-42f, 42f);
        Random.Range(-6f, 10f);
        return Random.value < 0.55f;
    }
}
