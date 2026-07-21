using System;
using UnityEngine;

public enum PetKind
{
    EmberFox = 0,   // Fast fire bites — attack speed DPS
    ShadowWolf = 1, // Heavy hits — high % of player ATK
    StormHawk = 2   // Longer range elemental strikes
}

[Serializable]
public class PetInstance
{
    public string id;
    public PetKind kind;
    public string displayName;
    public int level = 1;
    public int xp;

    public static PetInstance Create(PetKind kind, int level = 1)
    {
        level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel);
        return new PetInstance
        {
            id = Guid.NewGuid().ToString("N"),
            kind = kind,
            displayName = PetCatalog.KindName(kind),
            level = level,
            xp = 0
        };
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

    public static PetSaveData From(PetInstance p)
    {
        if (p == null) return null;
        return new PetSaveData
        {
            id = p.id,
            kind = (int)p.kind,
            displayName = p.displayName,
            level = p.level,
            xp = p.xp
        };
    }

    public PetInstance ToPet()
    {
        if (level < 1) return null;
        var petKind = (PetKind)Mathf.Clamp(kind, 0, 2);
        return new PetInstance
        {
            id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id,
            kind = petKind,
            displayName = string.IsNullOrEmpty(displayName) ? PetCatalog.KindName(petKind) : displayName,
            level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel),
            xp = Mathf.Max(0, xp)
        };
    }
}

public static class PetCatalog
{
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
                // Fast ticks: lower per-hit, high uptime
                return playerAttack * 0.32f + 5f + lv * 1.4f;
            case PetKind.ShadowWolf:
                // Chunky hits
                return playerAttack * 0.58f + 8f + lv * 2.0f;
            default:
                // Mid damage, safer range
                return playerAttack * 0.42f + 10f + lv * 1.6f;
        }
    }

    public static int XpToNext(int level)
    {
        level = Mathf.Clamp(level, 1, ProgressionFormulas.MaxPetLevel);
        // Scales smoothly toward 100.
        return 18 + level * 10 + (level * level) / 12;
    }

    public static string StatsLine(PetInstance pet, float playerAttack)
    {
        if (pet == null) return "";
        float dmg = ComputeDamage(pet, playerAttack);
        return $"Lv {pet.level}  ·  {RoleLabel(pet.kind)}  ·  Hit ~{dmg:0}  ·  CD {AttackCooldown(pet.kind):0.00}s";
    }
}
