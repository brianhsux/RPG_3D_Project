using UnityEngine;

[System.Serializable]
public class ItemInstance
{
    public string id;
    public string displayName;
    public ItemSlot slot;
    public ItemRarity rarity;
    public int itemLevel;

    public float attack;
    public float defense;
    public float maxHpBonus;

    /// <summary>Weapon: additive attack-speed bonus (0.15 = +15%).</summary>
    public float attackSpeedBonus;
    /// <summary>Weapon: flat attack-range bonus in meters.</summary>
    public float attackRangeBonus;
    /// <summary>0 Balanced · 1 Swift · 2 Reach</summary>
    public int weaponProfile;

    // Gems
    public GemKind gemKind;
    // Gear sockets (size GemCatalog.MaxSockets; first socketCount are usable)
    public int socketCount;
    public ItemInstance[] sockets;

    public string Summary
    {
        get
        {
            if (slot == ItemSlot.Gem)
                return $"{RarityColorTag}{displayName}</color>  Lv{itemLevel}  {GemCatalog.EffectLabel(gemKind)}";
            if (slot == ItemSlot.Weapon)
            {
                string extra = "";
                if (attackSpeedBonus > 0.01f) extra += $"  SPD +{attackSpeedBonus * 100f:0}%";
                if (attackRangeBonus > 0.01f) extra += $"  RNG +{attackRangeBonus:0.0}";
                return $"{RarityColorTag}{displayName}</color>  Lv{itemLevel}  ATK {attack:0}{extra}";
            }
            return $"{RarityColorTag}{displayName}</color>  Lv{itemLevel}  DEF {defense:0}  HP+{maxHpBonus:0}";
        }
    }

    public string RarityColorTag
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return "<color=#5dca6a>";
                case ItemRarity.Rare: return "<color=#4ea1ff>";
                case ItemRarity.Epic: return "<color=#c26bff>";
                case ItemRarity.Legendary: return "<color=#ffb23e>";
                default: return "<color=#dddddd>";
            }
        }
    }

    public Color RarityColor
    {
        get
        {
            if (slot == ItemSlot.Gem) return GemCatalog.KindColor(gemKind);
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.36f, 0.79f, 0.42f);
                case ItemRarity.Rare: return new Color(0.30f, 0.63f, 1f);
                case ItemRarity.Epic: return new Color(0.76f, 0.42f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.70f, 0.24f);
                default: return Color.white;
            }
        }
    }
}

public static class ItemFactory
{
    public const int ProfileBalanced = 0;
    public const int ProfileSwift = 1;
    public const int ProfileReach = 2;

    static readonly string[] WeaponNames =
    {
        "Wood Sword", "Iron Sword", "Wind Blade", "Bone Blade", "Flame Slash", "Frost Blade", "Thunder Sword", "Void Blade"
    };

    static readonly string[] SwiftNames =
    {
        "Wood Dagger", "Iron Dirk", "Wind Needle", "Bone Knife", "Flame Fang", "Frost Needle", "Thunder Claw", "Void Fang"
    };

    static readonly string[] ReachNames =
    {
        "Wood Spear", "Iron Pike", "Wind Lance", "Bone Halberd", "Flame Spear", "Frost Pike", "Thunder Lance", "Void Glaive"
    };

    static readonly string[] ArmorNames =
    {
        "Cloth", "Leather", "Chainmail", "Steel Plate", "Dragon Scale", "Rune Plate", "Holy Plate", "Apocalypse Plate"
    };

    public static string RarityName(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return "Uncommon";
            case ItemRarity.Rare: return "Rare";
            case ItemRarity.Epic: return "Epic";
            case ItemRarity.Legendary: return "Legendary";
            default: return "Common";
        }
    }

    public static string ProfileLabel(int profile)
    {
        switch (profile)
        {
            case ProfileSwift: return "Swift";
            case ProfileReach: return "Reach";
            default: return "Balanced";
        }
    }

    public static ItemInstance Create(ItemSlot slot, int itemLevel, ItemRarity rarity)
    {
        return Create(slot, itemLevel, rarity, -1);
    }

    /// <param name="forcedProfile">-1 random; 0 Balanced; 1 Swift; 2 Reach (weapons only).</param>
    public static ItemInstance Create(ItemSlot slot, int itemLevel, ItemRarity rarity, int forcedProfile)
    {
        itemLevel = Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel);
        var item = new ItemInstance
        {
            id = System.Guid.NewGuid().ToString("N"),
            slot = slot,
            itemLevel = itemLevel,
            rarity = rarity
        };

        // 8 visual/name tiers across 1–100 (~12–13 levels each)
        int nameIndex = Mathf.Clamp((itemLevel - 1) / 12, 0, 7);
        if (slot == ItemSlot.Weapon)
        {
            int profile = forcedProfile >= 0 && forcedProfile <= 2
                ? forcedProfile
                : (Random.value < 0.34f ? ProfileSwift : (Random.value < 0.5f ? ProfileReach : ProfileBalanced));
            ApplyWeaponProfile(item, profile, nameIndex);
        }
        else if (slot == ItemSlot.Armor)
        {
            item.displayName = $"{RarityName(rarity)}·{ArmorNames[nameIndex]}";
            item.attack = 0f;
            item.defense = ProgressionFormulas.ArmorDefense(itemLevel, rarity);
            item.maxHpBonus = ProgressionFormulas.ArmorMaxHp(itemLevel, rarity);
        }

        GemCatalog.EnsureSockets(item);
        return item;
    }

    static void ApplyWeaponProfile(ItemInstance item, int profile, int nameIndex)
    {
        item.weaponProfile = profile;
        float atk = ProgressionFormulas.WeaponAttack(item.itemLevel, item.rarity);
        // Soft level factor so SPD/RNG stay meaningful at 100 without blowing soft caps alone.
        float lv = Mathf.Sqrt(Mathf.Max(1, item.itemLevel));
        float spd;
        float rng;
        string[] names;
        switch (profile)
        {
            case ProfileSwift:
                names = SwiftNames;
                spd = 0.08f + lv * 0.028f + (int)item.rarity * 0.03f;
                rng = -0.05f + (int)item.rarity * 0.02f;
                atk *= 0.92f;
                break;
            case ProfileReach:
                names = ReachNames;
                spd = -0.03f + (int)item.rarity * 0.01f;
                rng = 0.25f + lv * 0.065f + (int)item.rarity * 0.07f;
                atk *= 0.96f;
                break;
            default:
                names = WeaponNames;
                spd = 0.02f + (int)item.rarity * 0.012f + lv * 0.008f;
                rng = 0.06f + lv * 0.022f + (int)item.rarity * 0.025f;
                break;
        }
        item.displayName = $"{RarityName(item.rarity)}·{names[nameIndex]}";
        item.attack = atk;
        item.attackSpeedBonus = spd;
        item.attackRangeBonus = rng;
        item.defense = 0f;
        item.maxHpBonus = 0f;
    }

    public static ItemInstance RollDrop(int contentLevel, bool fromBoss)
    {
        var slot = Random.value < 0.55f ? ItemSlot.Weapon : ItemSlot.Armor;
        int lv = ProgressionFormulas.RollItemLevel(contentLevel, fromBoss);
        var rarity = ProgressionFormulas.RollRarity(contentLevel, fromBoss);
        return Create(slot, lv, rarity);
    }

    public static ItemInstance StarterWeapon()
    {
        return Create(ItemSlot.Weapon, 1, ItemRarity.Common, ProfileBalanced);
    }

    public static ItemInstance StarterArmor()
    {
        return Create(ItemSlot.Armor, 1, ItemRarity.Common);
    }
}
