using System;
using UnityEngine;

public enum PetKind
{
    EmberFox = 0,   // Fast fire bites — attack speed DPS / bond affinity: AtkSpeed
    ShadowWolf = 1, // Heavy hits — high % of player ATK / bond affinity: Attack
    StormHawk = 2   // Longer range elemental strikes / bond affinity: Move + Regen
}

public enum PetBond
{
    Attack = 0,
    AttackSpeed = 1,
    MoveSpeed = 2,
    Regen = 3
}

public struct PetOwnerBuffs
{
    public float attackPercent;
    public float attackSpeed;
    public float moveSpeedPercent;
    /// <summary>Fraction of max HP restored every RegenIntervalSeconds.</summary>
    public float regenPercentPerTick;
}

[Serializable]
public class PetInstance
{
    public string id;
    public PetKind kind;
    public string displayName;
    public int level = 1;
    public int xp;
    /// <summary>Unspent bond points.</summary>
    public int bondPoints;
    /// <summary>Ranks for each PetBond (length PetCatalog.BondCount).</summary>
    public int[] bondRanks;

    public static PetInstance Create(PetKind kind, int level = 1)
    {
        level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel);
        var pet = new PetInstance
        {
            id = Guid.NewGuid().ToString("N"),
            kind = kind,
            displayName = PetCatalog.KindName(kind),
            level = level,
            xp = 0,
            bondPoints = 0,
            bondRanks = new int[PetCatalog.BondCount]
        };
        PetCatalog.EnsureBonds(pet);
        // Starting level grants points (Lv1 = 1 point to spend).
        pet.bondPoints = PetCatalog.TotalBondPointsFromLevel(level);
        return pet;
    }
}

[Serializable]
public class PetSaveData
{
    public string id;
    public int kind;
    public string displayName;
    public int level = 1;
    public int xp;
    public int bondPoints;
    public int[] bondRanks;

    public static PetSaveData From(PetInstance p)
    {
        if (p == null) return null;
        PetCatalog.EnsureBonds(p);
        return new PetSaveData
        {
            id = p.id,
            kind = (int)p.kind,
            displayName = p.displayName,
            level = p.level,
            xp = p.xp,
            bondPoints = p.bondPoints,
            bondRanks = (int[])p.bondRanks.Clone()
        };
    }

    public PetInstance ToPet()
    {
        if (level < 1) return null;
        var petKind = (PetKind)Mathf.Clamp(kind, 0, 2);
        var pet = new PetInstance
        {
            id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id,
            kind = petKind,
            displayName = string.IsNullOrEmpty(displayName) ? PetCatalog.KindName(petKind) : displayName,
            level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel),
            xp = Mathf.Max(0, xp),
            bondPoints = Mathf.Max(0, bondPoints),
            bondRanks = bondRanks != null ? (int[])bondRanks.Clone() : null
        };
        PetCatalog.EnsureBonds(pet);
        PetCatalog.MigrateLegacyBondPoints(pet);
        return pet;
    }
}

public static class PetCatalog
{
    public const int BondCount = 4;
    public const int MaxBondRank = 20;
    public const float RegenIntervalSeconds = 5f;

    public static string KindName(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.EmberFox: return "Ember Fox";
            case PetKind.ShadowWolf: return "Shadow Wolf";
            default: return "Storm Hawk";
        }
    }

    public static string RoleLabel(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.EmberFox: return "Fast fire DPS";
            case PetKind.ShadowWolf: return "Heavy melee DPS";
            default: return "Ranged storm DPS";
        }
    }

    public static Color KindColor(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.EmberFox: return new Color(1f, 0.45f, 0.15f);
            case PetKind.ShadowWolf: return new Color(0.45f, 0.35f, 0.7f);
            default: return new Color(0.4f, 0.75f, 1f);
        }
    }

    public static float AttackRange(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.StormHawk: return 7f;
            case PetKind.ShadowWolf: return 3.2f;
            default: return 2.8f;
        }
    }

    public static float AttackCooldown(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.EmberFox: return 0.75f;
            case PetKind.ShadowWolf: return 1.25f;
            default: return 1.0f;
        }
    }

    public static float FollowDistance(PetKind kind)
    {
        switch (kind)
        {
            case PetKind.StormHawk: return 2.8f;
            default: return 2.2f;
        }
    }

    /// <summary>Damage scaled from player ATK + pet level. Combat-focused.</summary>
    public static float ComputeDamage(PetInstance pet, float playerAttack)
    {
        if (pet == null) return 0f;
        float lv = pet.level;
        switch (pet.kind)
        {
            case PetKind.EmberFox:
                return playerAttack * 0.32f + 5f + lv * 1.4f;
            case PetKind.ShadowWolf:
                return playerAttack * 0.58f + 8f + lv * 2.0f;
            default:
                return playerAttack * 0.42f + 10f + lv * 1.6f;
        }
    }

    public static int XpToNext(int level)
    {
        level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel);
        return 18 + level * 10 + (level * level) / 12;
    }

    public static string StatsLine(PetInstance pet, float playerAttack)
    {
        if (pet == null) return "";
        float dmg = ComputeDamage(pet, playerAttack);
        EnsureBonds(pet);
        return $"Lv {pet.level}  ·  {RoleLabel(pet.kind)}  ·  Hit ~{dmg:0}  ·  Pts {pet.bondPoints}";
    }

    public static string BondSummaryLine(PetInstance pet)
    {
        if (pet == null) return "";
        EnsureBonds(pet);
        return $"ATK{pet.bondRanks[0]} SPD{pet.bondRanks[1]} MOV{pet.bondRanks[2]} REG{pet.bondRanks[3]}  ·  Pts {pet.bondPoints}";
    }

    // ---- Bonds ----

    public static void EnsureBonds(PetInstance pet)
    {
        if (pet == null) return;
        if (pet.bondRanks == null || pet.bondRanks.Length != BondCount)
        {
            var next = new int[BondCount];
            if (pet.bondRanks != null)
            {
                for (int i = 0; i < Mathf.Min(BondCount, pet.bondRanks.Length); i++)
                    next[i] = Mathf.Clamp(pet.bondRanks[i], 0, MaxBondRank);
            }
            pet.bondRanks = next;
        }
        else
        {
            for (int i = 0; i < BondCount; i++)
                pet.bondRanks[i] = Mathf.Clamp(pet.bondRanks[i], 0, MaxBondRank);
        }
        pet.bondPoints = Mathf.Max(0, pet.bondPoints);
    }

    /// <summary>Old saves: grant level-based points minus already spent ranks.</summary>
    public static void MigrateLegacyBondPoints(PetInstance pet)
    {
        if (pet == null) return;
        EnsureBonds(pet);
        pet.bondPoints = PetBondRules.TopUpUnspent(
            pet.bondPoints, SpentBondPoints(pet), pet.level, ProgressionFormulas.MaxPetLevel);
    }

    public static int TotalBondPointsFromLevel(int level)
    {
        level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel);
        return level; // 1 point per pet level
    }

    public static int SpentBondPoints(PetInstance pet)
    {
        if (pet?.bondRanks == null) return 0;
        int n = 0;
        for (int i = 0; i < pet.bondRanks.Length; i++)
            n += Mathf.Max(0, pet.bondRanks[i]);
        return n;
    }

    public static string BondLabel(PetBond bond)
    {
        switch (bond)
        {
            case PetBond.Attack: return "Attack";
            case PetBond.AttackSpeed: return "Attack Speed";
            case PetBond.MoveSpeed: return "Move Speed";
            case PetBond.Regen: return "Regen";
            default: return "Bond";
        }
    }

    public static string BondBlurb(PetBond bond)
    {
        switch (bond)
        {
            case PetBond.Attack: return "Owner ATK %";
            case PetBond.AttackSpeed: return "Owner attack speed";
            case PetBond.MoveSpeed: return "Owner run speed";
            case PetBond.Regen: return $"Heal % max HP / {RegenIntervalSeconds:0}s";
            default: return "";
        }
    }

    /// <summary>Affinity multiplier for this kind+bond (1.0 = normal, up to ~1.25).</summary>
    public static float AffinityMul(PetKind kind, PetBond bond)
    {
        switch (kind)
        {
            case PetKind.EmberFox:
                return bond == PetBond.AttackSpeed ? 1.25f : (bond == PetBond.Attack ? 1.05f : 1f);
            case PetKind.ShadowWolf:
                return bond == PetBond.Attack ? 1.25f : (bond == PetBond.MoveSpeed ? 1.05f : 1f);
            default: // StormHawk
                if (bond == PetBond.MoveSpeed || bond == PetBond.Regen) return 1.2f;
                return 1f;
        }
    }

    public static float PerRankEffect(PetBond bond)
    {
        switch (bond)
        {
            case PetBond.Attack: return 0.015f;      // +1.5% ATK
            case PetBond.AttackSpeed: return 0.012f; // +1.2% atk speed
            case PetBond.MoveSpeed: return 0.01f;    // +1% move
            case PetBond.Regen: return 0.004f;       // +0.4% max HP / tick
            default: return 0f;
        }
    }

    public static float BondValue(PetInstance pet, PetBond bond)
    {
        if (pet == null) return 0f;
        EnsureBonds(pet);
        int rank = pet.bondRanks[(int)bond];
        return rank * PerRankEffect(bond) * AffinityMul(pet.kind, bond);
    }

    public static string BondEffectLine(PetInstance pet, PetBond bond)
    {
        if (pet == null) return "";
        EnsureBonds(pet);
        int rank = pet.bondRanks[(int)bond];
        float v = BondValue(pet, bond);
        string aff = AffinityMul(pet.kind, bond) > 1.01f ? " ★" : "";
        switch (bond)
        {
            case PetBond.Attack: return $"Lv{rank}/{MaxBondRank}  ATK +{v * 100f:0.#}%{aff}";
            case PetBond.AttackSpeed: return $"Lv{rank}/{MaxBondRank}  SPD +{v * 100f:0.#}%{aff}";
            case PetBond.MoveSpeed: return $"Lv{rank}/{MaxBondRank}  Move +{v * 100f:0.#}%{aff}";
            case PetBond.Regen: return $"Lv{rank}/{MaxBondRank}  +{v * 100f:0.#}% HP / {RegenIntervalSeconds:0}s{aff}";
            default: return "";
        }
    }

    public static PetOwnerBuffs ComputeOwnerBuffs(PetInstance pet)
    {
        var b = new PetOwnerBuffs();
        if (pet == null) return b;
        EnsureBonds(pet);
        b.attackPercent = BondValue(pet, PetBond.Attack);
        b.attackSpeed = BondValue(pet, PetBond.AttackSpeed);
        b.moveSpeedPercent = BondValue(pet, PetBond.MoveSpeed);
        b.regenPercentPerTick = BondValue(pet, PetBond.Regen);
        return b;
    }

    public static string FormatOwnerBuffs(PetOwnerBuffs b)
    {
        var parts = new System.Collections.Generic.List<string>(4);
        if (b.attackPercent > 0.001f) parts.Add($"ATK +{b.attackPercent * 100f:0.#}%");
        if (b.attackSpeed > 0.001f) parts.Add($"AtkSpd +{b.attackSpeed * 100f:0.#}%");
        if (b.moveSpeedPercent > 0.001f) parts.Add($"Move +{b.moveSpeedPercent * 100f:0.#}%");
        if (b.regenPercentPerTick > 0.0005f)
            parts.Add($"Regen {b.regenPercentPerTick * 100f:0.#}%/{RegenIntervalSeconds:0}s");
        return parts.Count == 0 ? "No bond buffs yet" : string.Join("  ·  ", parts);
    }

    public static bool CanTrain(PetInstance pet, PetBond bond)
    {
        if (pet == null) return false;
        EnsureBonds(pet);
        int i = (int)bond;
        if (i < 0 || i >= BondCount) return false;
        return PetBondRules.CanTrain(pet.bondPoints, pet.bondRanks[i], MaxBondRank);
    }

    public static bool TryTrain(PetInstance pet, PetBond bond)
    {
        if (!CanTrain(pet, bond)) return false;
        int i = (int)bond;
        return PetBondRules.TryTrain(ref pet.bondPoints, ref pet.bondRanks[i], MaxBondRank);
    }
}
