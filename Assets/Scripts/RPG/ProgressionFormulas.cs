using UnityEngine;

/// <summary>
/// Central balance sheet for the adventure.
/// contentLevel = (world-1)*4 + map  →  range 1..(WorldCount*MapsPerWorld)
/// </summary>
public static class ProgressionFormulas
{
    public const int WorldCount = 10;
    public const int MapsPerWorld = 4;
    public const int MaxContentLevel = WorldCount * MapsPerWorld;

    public static int ContentLevel(int world, int map)
    {
        world = Mathf.Clamp(world, 1, WorldCount);
        map = Mathf.Clamp(map, 1, MapsPerWorld);
        return (world - 1) * MapsPerWorld + map;
    }

    public static float RarityMult(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 1.00f;
            case ItemRarity.Uncommon: return 1.15f;
            case ItemRarity.Rare: return 1.35f;
            case ItemRarity.Epic: return 1.60f;
            case ItemRarity.Legendary: return 2.00f;
            default: return 1f;
        }
    }

    // ---- Monster scaling ----
    public static float MobMaxHp(int contentLevel, bool isBoss)
    {
        float hp = 45f + contentLevel * 28f + Mathf.Pow(contentLevel, 1.35f) * 6f;
        return isBoss ? hp * 4.5f : hp;
    }

    public static float MobAttack(int contentLevel, bool isBoss)
    {
        float atk = 7f + contentLevel * 3.2f + contentLevel * contentLevel * 0.08f;
        return isBoss ? atk * 1.8f : atk;
    }

    public static float MobMoveSpeed(int contentLevel, bool isBoss)
    {
        float spd = 2.2f + contentLevel * 0.06f;
        return isBoss ? spd * 0.9f : spd;
    }

    public static string MobDisplayName(int contentLevel, bool isBoss, EnemyKind kind)
    {
        string prefix = isBoss ? "Boss " : "";
        string name;
        switch (kind)
        {
            case EnemyKind.Slime: name = "Slime"; break;
            case EnemyKind.Goblin: name = "Goblin"; break;
            case EnemyKind.Skeleton: name = "Skeleton"; break;
            case EnemyKind.Wolf: name = "Wolf"; break;
            case EnemyKind.Spider: name = "Spider"; break;
            case EnemyKind.Bat: name = "Bat"; break;
            case EnemyKind.Mushroom: name = "Mushroom"; break;
            case EnemyKind.Golem: name = "Golem"; break;
            case EnemyKind.Ghost: name = "Ghost"; break;
            case EnemyKind.Imp: name = "Imp"; break;
            case EnemyKind.Beetle: name = "Beetle"; break;
            case EnemyKind.Treant: name = "Treant"; break;
            case EnemyKind.LavaSpawn: name = "Lava Spawn"; break;
            case EnemyKind.Crystal: name = "Crystal Fiend"; break;
            case EnemyKind.Wraith: name = "Wraith"; break;
            case EnemyKind.Demon: name = "Demon"; break;
            case EnemyKind.Yeti: name = "Yeti"; break;
            case EnemyKind.IceElemental: name = "Ice Elemental"; break;
            case EnemyKind.Scorpion: name = "Scorpion"; break;
            case EnemyKind.Mummy: name = "Mummy"; break;
            case EnemyKind.SandWorm: name = "Sand Worm"; break;
            case EnemyKind.Toad: name = "Toad"; break;
            case EnemyKind.Leech: name = "Leech"; break;
            case EnemyKind.Mosquito: name = "Mosquito"; break;
            case EnemyKind.Harpy: name = "Harpy"; break;
            case EnemyKind.StormSprite: name = "Storm Sprite"; break;
            case EnemyKind.Griffon: name = "Griffon"; break;
            case EnemyKind.ShadowHound: name = "Shadow Hound"; break;
            case EnemyKind.VoidEye: name = "Void Eye"; break;
            case EnemyKind.Tentacle: name = "Abyss Tentacle"; break;
            case EnemyKind.WorldBoss:
            {
                int w = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
                return ProgressionFormulas.WorldThemeName(w) + " Boss Lv" + contentLevel;
            }
            default: name = "Monster"; break;
        }
        return prefix + name + " Lv" + contentLevel;
    }

    // ---- Item stats ----
    public static int RollItemLevel(int contentLevel, bool fromBoss)
    {
        int baseLv = contentLevel;
        int roll = Random.Range(-1, 3); // -1..2
        if (fromBoss) roll += 2;
        return Mathf.Clamp(baseLv + roll, 1, MaxContentLevel);
    }

    public static ItemRarity RollRarity(int contentLevel, bool fromBoss)
    {
        float t = contentLevel / (float)MaxContentLevel;
        float r = Random.value;
        if (fromBoss) r -= 0.25f;

        if (r < 0.02f + t * 0.03f) return ItemRarity.Legendary;
        if (r < 0.08f + t * 0.08f) return ItemRarity.Epic;
        if (r < 0.22f + t * 0.12f) return ItemRarity.Rare;
        if (r < 0.50f + t * 0.10f) return ItemRarity.Uncommon;
        return ItemRarity.Common;
    }

    public static float WeaponAttack(int itemLevel, ItemRarity rarity)
    {
        return (6f + itemLevel * 2.6f + itemLevel * itemLevel * 0.04f) * RarityMult(rarity);
    }

    public static float ArmorDefense(int itemLevel, ItemRarity rarity)
    {
        return (3f + itemLevel * 1.8f + itemLevel * 0.05f) * RarityMult(rarity);
    }

    public static float ArmorMaxHp(int itemLevel, ItemRarity rarity)
    {
        return (12f + itemLevel * 9f + itemLevel * itemLevel * 0.15f) * RarityMult(rarity);
    }

    /// <summary>Incoming damage after defense. Soft diminishing returns.</summary>
    public static float MitigateDamage(float rawDamage, float defense)
    {
        return rawDamage * (100f / (100f + Mathf.Max(0f, defense)));
    }

    // ---- Crafting ----
    /// <summary>
    /// 3 items same slot → 1 upgraded item.
    /// Level = average + 2 (or +3 if all Rare+). Keeps best rarity among inputs, 15% chance to bump.
    /// </summary>
    public static ItemInstance Synthesize(ItemInstance a, ItemInstance b, ItemInstance c)
    {
        if (a == null || b == null || c == null) return null;
        if (a.slot != b.slot || b.slot != c.slot) return null;

        int avg = Mathf.RoundToInt((a.itemLevel + b.itemLevel + c.itemLevel) / 3f);
        int bonus = 2;
        if (a.rarity >= ItemRarity.Rare && b.rarity >= ItemRarity.Rare && c.rarity >= ItemRarity.Rare)
            bonus = 3;

        ItemRarity best = a.rarity;
        if (b.rarity > best) best = b.rarity;
        if (c.rarity > best) best = c.rarity;
        if (Random.value < 0.15f && best < ItemRarity.Legendary)
            best = (ItemRarity)((int)best + 1);

        int newLevel = Mathf.Clamp(avg + bonus, 1, MaxContentLevel);
        return ItemFactory.Create(a.slot, newLevel, best);
    }

    public static string WorldThemeName(int world)
    {
        switch (world)
        {
            case 1: return "Plains";
            case 2: return "Forest";
            case 3: return "Ruins";
            case 4: return "Volcano";
            case 5: return "Apocalypse";
            case 6: return "Frost";
            case 7: return "Desert";
            case 8: return "Swamp";
            case 9: return "Sky";
            case 10: return "Abyss";
            default: return "Unknown World";
        }
    }

    public static string MapName(int world, int map)
    {
        map = Mathf.Clamp(map, 1, MapsPerWorld);
        if (map == 4) return WorldThemeName(world) + " · Boss Sanctum";

        string zone;
        switch (world)
        {
            case 1: zone = map == 1 ? "Meadow" : map == 2 ? "Hills" : "Bug Nest"; break;
            case 2: zone = map == 1 ? "Treehouse Village" : map == 2 ? "Deep Woods" : "Web Valley"; break;
            case 3: zone = map == 1 ? "Ruined Hall" : map == 2 ? "Graveyard" : "Wraith Corridor"; break;
            case 4: zone = map == 1 ? "Lava Village" : map == 2 ? "Crater" : "Rock Golem Den"; break;
            case 5: zone = map == 1 ? "Twisted Plains" : map == 2 ? "Crystal Grove" : "Demon Tree Sea"; break;
            case 6: zone = map == 1 ? "Snow Camp" : map == 2 ? "Frozen Lake" : "Ice Crystal Cave"; break;
            case 7: zone = map == 1 ? "Oasis" : map == 2 ? "Dunes" : "Scorpion Den"; break;
            case 8: zone = map == 1 ? "Mud Huts" : map == 2 ? "Poison Swamp" : "Rotroot Forest"; break;
            case 9: zone = map == 1 ? "Sky Village" : map == 2 ? "Cloud Bridge" : "Storm Nest"; break;
            case 10: zone = map == 1 ? "Abyss Dock" : map == 2 ? "Void Rift" : "Demon Lord Antechamber"; break;
            default: zone = "Map " + map; break;
        }
        return WorldThemeName(world) + " · " + zone;
    }

    // ---- Player XP / Level ----
    public const int MaxPlayerLevel = 60;

    public static int XpToNextLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxPlayerLevel);
        return Mathf.RoundToInt(35f + level * 28f + level * level * 1.8f);
    }

    public static int MobXpReward(int contentLevel, bool isBoss)
    {
        int xp = 12 + contentLevel * 8 + contentLevel * contentLevel / 4;
        return isBoss ? xp * 5 : xp;
    }

    public static float PlayerLevelBonusAtk(int level) => (level - 1) * 2.2f;
    public static float PlayerLevelBonusDef(int level) => (level - 1) * 1.4f;
    public static float PlayerLevelBonusHp(int level) => (level - 1) * 18f;
}

public enum EnemyKind
{
    // Worlds 1-5
    Slime,
    Goblin,
    Skeleton,
    Wolf,
    Spider,
    Bat,
    Mushroom,
    Golem,
    Ghost,
    Imp,
    Beetle,
    Treant,
    LavaSpawn,
    Crystal,
    Wraith,
    Demon,
    // Worlds 6-10 (unique — never reused across worlds)
    Yeti,
    IceElemental,
    Scorpion,
    Mummy,
    SandWorm,
    Toad,
    Leech,
    Mosquito,
    Harpy,
    StormSprite,
    Griffon,
    ShadowHound,
    VoidEye,
    Tentacle,
    WorldBoss
}

public enum ItemSlot
{
    Weapon,
    Armor
}

public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}
