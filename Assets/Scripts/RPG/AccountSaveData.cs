using System;
using UnityEngine;

[Serializable]
public class ItemSaveData
{
    public string displayName;
    public int slot;
    public int rarity;
    public int itemLevel;
    public float attack;
    public float defense;
    public float maxHpBonus;

    public bool IsValid =>
        !string.IsNullOrEmpty(displayName) &&
        displayName != "未知裝備" &&
        itemLevel >= 1 &&
        (attack > 0.01f || defense > 0.01f || maxHpBonus > 0.01f);

    public static ItemSaveData From(ItemInstance item)
    {
        if (item == null) return null;
        if (string.IsNullOrEmpty(item.displayName)) return null;
        if (item.itemLevel < 1) return null;
        // Reject empty serialized placeholders (scene defaults).
        if (item.attack <= 0.01f && item.defense <= 0.01f && item.maxHpBonus <= 0.01f)
            return null;

        return new ItemSaveData
        {
            displayName = item.displayName,
            slot = (int)item.slot,
            rarity = (int)item.rarity,
            itemLevel = item.itemLevel,
            attack = item.attack,
            defense = item.defense,
            maxHpBonus = item.maxHpBonus
        };
    }

    public ItemInstance ToItem()
    {
        if (!IsValid) return null;
        return new ItemInstance
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = displayName,
            slot = (ItemSlot)slot,
            rarity = (ItemRarity)rarity,
            itemLevel = Mathf.Clamp(itemLevel, 1, 40),
            attack = attack,
            defense = defense,
            maxHpBonus = maxHpBonus
        };
    }
}

[Serializable]
public class AccountSaveData
{
    public string accountId;
    public string accountName;
    public string createdAt;
    public string lastPlayedAt;

    public int level = 1;
    public int xp;

    public int currentWorld = 1;
    public int currentMap = 1;
    public int highestWorldUnlocked = 1;
    public int killCount;
    public string[] clearedBossKeys = Array.Empty<string>();

    public float currentHealth = -1f;

    public ItemSaveData weapon;
    public ItemSaveData armor;
    public ItemSaveData[] bag = Array.Empty<ItemSaveData>();
}

[Serializable]
public class AccountIndexEntry
{
    public string accountId;
    public string accountName;
    public string lastPlayedAt;
    public int level = 1;
    public int currentWorld = 1;
}

[Serializable]
public class AccountIndexFile
{
    public AccountIndexEntry[] accounts = Array.Empty<AccountIndexEntry>();
}
