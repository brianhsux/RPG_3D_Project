using NUnit.Framework;

public class CombatMathTests
{
    [Test]
    public void CritMultipliesAttackThenAddsElements()
    {
        float damage = CombatMath.OutgoingDamage(10f, 0.5f, 0.5f, critRoll: 0.49f, fire: 4f, ice: 1f, poison: 0f);
        Assert.AreEqual(20f, damage, 0.0001f);
    }

    [Test]
    public void CritRollEqualToChanceDoesNotCrit()
    {
        float damage = CombatMath.OutgoingDamage(10f, 0.5f, 0.5f, critRoll: 0.5f, 0f, 0f, 0f);
        Assert.AreEqual(10f, damage, 0.0001f);
    }

    [Test]
    public void LowCritBonusIsRaisedToMinimum()
    {
        float damage = CombatMath.OutgoingDamage(10f, 1f, 0.1f, critRoll: 0f, 0f, 0f, 0f);
        Assert.AreEqual(13.5f, damage, 0.0001f);
    }

    [Test]
    public void PlayerMitigationOrderIsBlockThenReductionThenDefense()
    {
        float dealt = CombatMath.MitigatePlayerIncoming(100f, defense: 100f, damageReduction: 0.5f, blocked: true);
        Assert.AreEqual(5f, dealt, 0.0001f);
    }

    [Test]
    public void DefenseDoesNotApplyWhenNotBlockedAndReductionIsZero()
    {
        float dealt = CombatMath.MitigatePlayerIncoming(100f, defense: 100f, damageReduction: 0f, blocked: false);
        Assert.AreEqual(50f, dealt, 0.0001f);
    }

    [Test]
    public void ReflectRequiresMoreThanHalfAPointAndAPlayerDefender()
    {
        Assert.AreEqual(0f, CombatMath.ReflectDamage(0.5f, 0.4f, true), 0.0001f);
        Assert.AreEqual(0f, CombatMath.ReflectDamage(10f, 0.4f, false), 0.0001f);
        Assert.AreEqual(4f, CombatMath.ReflectDamage(10f, 0.4f, true), 0.0001f);
    }

    [Test]
    public void LifestealUsesDealtDamage()
    {
        Assert.AreEqual(0f, CombatMath.LifestealHeal(0f, 0.2f), 0.0001f);
        Assert.AreEqual(2f, CombatMath.LifestealHeal(10f, 0.2f), 0.0001f);
    }

    [Test]
    public void SmallHitsStillReduceHp()
    {
        Assert.AreEqual(9.6f, CombatMath.SubtractHp(10f, 0.4f), 0.0001f);
        Assert.AreEqual(10f, CombatMath.SubtractHp(10f, 0f), 0.0001f);
        Assert.AreEqual(0f, CombatMath.SubtractHp(3f, 9f), 0.0001f);
    }
}
