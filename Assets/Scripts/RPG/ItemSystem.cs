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

    public string Summary
    {
        get
        {
            if (slot == ItemSlot.Weapon)
                return $"{RarityColorTag}{displayName}</color>  Lv{itemLevel}  ATK {attack:0}";
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
    static readonly string[] WeaponNames =
    {
        "Wood Sword", "Iron Sword", "Wind Blade", "Bone Blade", "Flame Slash", "Frost Blade", "Thunder Sword", "Void Blade"
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

    public static ItemInstance Create(ItemSlot slot, int itemLevel, ItemRarity rarity)
    {
        itemLevel = Mathf.Clamp(itemLevel, 1, 40);
        var item = new ItemInstance
        {
            id = System.Guid.NewGuid().ToString("N"),
            slot = slot,
            itemLevel = itemLevel,
            rarity = rarity
        };

        int nameIndex = Mathf.Clamp((itemLevel - 1) / 5, 0, 7);
        if (slot == ItemSlot.Weapon)
        {
            item.displayName = $"{RarityName(rarity)}·{WeaponNames[nameIndex]}";
            item.attack = ProgressionFormulas.WeaponAttack(itemLevel, rarity);
            item.defense = 0f;
            item.maxHpBonus = 0f;
        }
        else
        {
            item.displayName = $"{RarityName(rarity)}·{ArmorNames[nameIndex]}";
            item.attack = 0f;
            item.defense = ProgressionFormulas.ArmorDefense(itemLevel, rarity);
            item.maxHpBonus = ProgressionFormulas.ArmorMaxHp(itemLevel, rarity);
        }

        return item;
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
        return Create(ItemSlot.Weapon, 1, ItemRarity.Common);
    }

    public static ItemInstance StarterArmor()
    {
        return Create(ItemSlot.Armor, 1, ItemRarity.Common);
    }
}
