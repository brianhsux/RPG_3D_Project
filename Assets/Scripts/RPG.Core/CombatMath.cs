/// <summary>
/// Solo and networked combat share these formulas.
/// Random rolls are passed in so call sites keep their existing Random order.
/// </summary>
public static class CombatMath
{
    public const float BlockRemainingFraction = 0.2f;
    public const float MinimumCritBonus = 0.35f;
    public const float ReflectThreshold = 0.5f;

    public static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    public static float OutgoingDamage(
        float attack,
        float critChance,
        float critDamageBonus,
        float critRoll,
        float fire,
        float ice,
        float poison)
    {
        float damage = attack;
        if (critRoll < Clamp01(critChance))
        {
            float bonus = critDamageBonus < MinimumCritBonus ? MinimumCritBonus : critDamageBonus;
            damage *= 1f + bonus;
        }
        return damage + fire + ice + poison;
    }

    /// <summary>Player incoming damage: block, then damage reduction, then defense.</summary>
    public static float MitigatePlayerIncoming(float incoming, float defense, float damageReduction, bool blocked)
    {
        if (incoming < 0f) incoming = 0f;
        if (blocked) incoming *= BlockRemainingFraction;
        incoming *= 1f - Clamp01(damageReduction);
        return Mitigate(incoming, defense);
    }

    public static float Mitigate(float rawDamage, float defense)
    {
        if (defense < 0f) defense = 0f;
        return rawDamage * (100f / (100f + defense));
    }

    public static float LifestealHeal(float dealt, float lifesteal)
    {
        if (lifesteal <= 0f || dealt <= 0f) return 0f;
        return dealt * lifesteal;
    }

    /// <summary>Reflect is raw (no second mitigation) and only when the defender is a player.</summary>
    public static float ReflectDamage(float dealt, float reflectPercent, bool defenderIsPlayer)
    {
        if (!defenderIsPlayer || reflectPercent <= 0f || dealt <= ReflectThreshold) return 0f;
        return dealt * reflectPercent;
    }

    /// <summary>
    /// Any positive hit reduces HP. The old network path discarded hits under 0.5,
    /// which made fast weak attacks (and some reflects) do nothing.
    /// </summary>
    public static float SubtractHp(float currentHp, float amount)
    {
        if (amount <= 0f) return currentHp;
        float next = currentHp - amount;
        return next < 0f ? 0f : next;
    }
}
