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
        string prefix = isBoss ? "Boss·" : "";
        string name;
        switch (kind)
        {
            case EnemyKind.Slime: name = "史萊姆"; break;
            case EnemyKind.Goblin: name = "哥布林"; break;
            case EnemyKind.Skeleton: name = "骷髏兵"; break;
            case EnemyKind.Wolf: name = "野狼"; break;
            case EnemyKind.Spider: name = "毒蛛"; break;
            case EnemyKind.Bat: name = "血蝠"; break;
            case EnemyKind.Mushroom: name = "蘑菇怪"; break;
            case EnemyKind.Golem: name = "石魔像"; break;
            case EnemyKind.Ghost: name = "遊魂"; break;
            case EnemyKind.Imp: name = "小惡魔"; break;
            case EnemyKind.Beetle: name = "甲蟲"; break;
            case EnemyKind.Treant: name = "樹人"; break;
            case EnemyKind.LavaSpawn: name = "熔岩怪"; break;
            case EnemyKind.Crystal: name = "水晶魔"; break;
            case EnemyKind.Wraith: name = "死靈"; break;
            case EnemyKind.Demon: name = "惡魔"; break;
            case EnemyKind.Yeti: name = "雪人"; break;
            case EnemyKind.IceElemental: name = "冰元素"; break;
            case EnemyKind.Scorpion: name = "沙蠍"; break;
            case EnemyKind.Mummy: name = "木乃伊"; break;
            case EnemyKind.SandWorm: name = "沙蟲"; break;
            case EnemyKind.Toad: name = "毒蟾"; break;
            case EnemyKind.Leech: name = "血蛭"; break;
            case EnemyKind.Mosquito: name = "沼蚊"; break;
            case EnemyKind.Harpy: name = "鷹身女妖"; break;
            case EnemyKind.StormSprite: name = "風暴精靈"; break;
            case EnemyKind.Griffon: name = "獅鷲"; break;
            case EnemyKind.ShadowHound: name = "影犬"; break;
            case EnemyKind.VoidEye: name = "虛空之眼"; break;
            case EnemyKind.Tentacle: name = "深淵觸手"; break;
            case EnemyKind.WorldBoss:
            {
                int w = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
                return ProgressionFormulas.WorldThemeName(w) + "首領 Lv" + contentLevel;
            }
            default: name = "怪物"; break;
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
            case 1: return "草原世界";
            case 2: return "森林世界";
            case 3: return "遺跡世界";
            case 4: return "火山世界";
            case 5: return "終焉世界";
            case 6: return "冰霜世界";
            case 7: return "沙漠世界";
            case 8: return "沼澤世界";
            case 9: return "天空世界";
            case 10: return "深淵世界";
            default: return "未知世界";
        }
    }

    public static string MapName(int world, int map)
    {
        map = Mathf.Clamp(map, 1, MapsPerWorld);
        if (map == 4) return WorldThemeName(world) + " · 首領殿堂";

        string zone;
        switch (world)
        {
            case 1: zone = map == 1 ? "牧草地" : map == 2 ? "丘陵" : "蟲巢"; break;
            case 2: zone = map == 1 ? "樹屋村" : map == 2 ? "密林" : "蛛網谷"; break;
            case 3: zone = map == 1 ? "廢墟門廊" : map == 2 ? "墓園" : "死靈迴廊"; break;
            case 4: zone = map == 1 ? "熔岩村" : map == 2 ? "火口" : "岩魔窟"; break;
            case 5: zone = map == 1 ? "扭曲平原" : map == 2 ? "晶核林" : "魔樹海"; break;
            case 6: zone = map == 1 ? "雪原營地" : map == 2 ? "凍湖" : "冰晶洞"; break;
            case 7: zone = map == 1 ? "綠洲" : map == 2 ? "沙丘" : "蝎穴"; break;
            case 8: zone = map == 1 ? "泥屋" : map == 2 ? "毒沼" : "腐根林"; break;
            case 9: zone = map == 1 ? "浮島村" : map == 2 ? "雲橋" : "風暴巢"; break;
            case 10: zone = map == 1 ? "深淵碼頭" : map == 2 ? "虛空裂縫" : "魔王前廳"; break;
            default: zone = "地圖 " + map; break;
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
