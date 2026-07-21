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
    public int gemKind;
    public ItemSaveData[] sockets;

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            if (displayName == "Unknown Item" || displayName == "未知裝備") return false;
            if (itemLevel < 1) return false;
            if (slot == (int)ItemSlot.Gem) return true;
            return attack > 0.01f || defense > 0.01f || maxHpBonus > 0.01f;
        }
    }

    public static ItemSaveData From(ItemInstance item)
    {
        if (item == null) return null;
        if (string.IsNullOrEmpty(item.displayName)) return null;
        if (item.itemLevel < 1) return null;

        if (item.slot == ItemSlot.Gem)
        {
            return new ItemSaveData
            {
                displayName = item.displayName,
                slot = (int)ItemSlot.Gem,
                rarity = (int)item.rarity,
                itemLevel = item.itemLevel,
                gemKind = (int)item.gemKind,
                attack = 0f,
                defense = 0f,
                maxHpBonus = 0f
            };
        }

        if (item.attack <= 0.01f && item.defense <= 0.01f && item.maxHpBonus <= 0.01f)
            return null;

        GemCatalog.EnsureSockets(item);
        ItemSaveData[] sockSave = null;
        if (item.sockets != null && item.socketCount > 0)
        {
            sockSave = new ItemSaveData[GemCatalog.MaxSockets];
            for (int i = 0; i < item.socketCount && i < item.sockets.Length; i++)
                sockSave[i] = From(item.sockets[i]);
        }

        return new ItemSaveData
        {
            displayName = item.displayName,
            slot = (int)item.slot,
            rarity = (int)item.rarity,
            itemLevel = item.itemLevel,
            attack = item.attack,
            defense = item.defense,
            maxHpBonus = item.maxHpBonus,
            sockets = sockSave
        };
    }

    public ItemInstance ToItem()
    {
        if (!IsValid) return null;

        if (slot == (int)ItemSlot.Gem)
        {
            return GemCatalog.CreateGem(
                (GemKind)Mathf.Clamp(gemKind, 0, 11),
                Mathf.Clamp(itemLevel, 1, 40),
                (ItemRarity)Mathf.Clamp(rarity, 0, 4));
        }

        var item = new ItemInstance
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
        GemCatalog.EnsureSockets(item);
        if (sockets != null)
        {
            for (int i = 0; i < item.socketCount && i < sockets.Length && i < item.sockets.Length; i++)
            {
                if (sockets[i] != null)
                    item.sockets[i] = sockets[i].ToItem();
            }
        }
        return item;
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

    /// <summary>0 = Village hub; 1–10 = adventure worlds.</summary>
    public int currentWorld = 0;
    public int currentMap = 1;
    /// <summary>Highest world the player may enter from the village (visited / unlocked).</summary>
    public int highestWorldUnlocked = 1;
    public int killCount;
    public string[] clearedBossKeys = Array.Empty<string>();

    public float currentHealth = -1f;

    public ItemSaveData weapon;
    public ItemSaveData armor;
    public ItemSaveData[] bag = Array.Empty<ItemSaveData>();

    public PetSaveData[] pets = Array.Empty<PetSaveData>();
    public string activePetId;
}

[Serializable]
public class AccountIndexEntry
{
    public string accountId;
    public string accountName;
    public string lastPlayedAt;
    public int level = 1;
    /// <summary>0 = Village; 1–10 = adventure world.</summary>
    public int currentWorld = 0;
}

[Serializable]
public class AccountIndexFile
{
    public AccountIndexEntry[] accounts = Array.Empty<AccountIndexEntry>();
}
