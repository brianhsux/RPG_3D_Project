using System;
using UnityEngine;

/// <summary>
/// Socket gems only — must NOT nest ItemSaveData (JsonUtility recursion depth limit).
/// </summary>
[Serializable]
public class GemSaveData
{
    public string displayName;
    public int rarity;
    public int itemLevel;
    public int gemKind;

    public static GemSaveData From(ItemInstance gem)
    {
        if (gem == null || gem.slot != ItemSlot.Gem) return null;
        if (string.IsNullOrEmpty(gem.displayName) || gem.itemLevel < 1) return null;
        return new GemSaveData
        {
            displayName = gem.displayName,
            rarity = (int)gem.rarity,
            itemLevel = gem.itemLevel,
            gemKind = (int)gem.gemKind
        };
    }

    public ItemInstance ToItem()
    {
        if (string.IsNullOrEmpty(displayName) || itemLevel < 1) return null;
        return GemCatalog.CreateGem(
            (GemKind)Mathf.Clamp(gemKind, 0, GemCatalog.KindCount - 1),
            Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel),
            (ItemRarity)Mathf.Clamp(rarity, 0, 4));
    }
}

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
    public float attackSpeedBonus;
    public float attackRangeBonus;
    public int weaponProfile;
    public int gemKind;
    public GemSaveData[] sockets;

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
        GemSaveData[] sockSave = null;
        if (item.sockets != null && item.socketCount > 0)
        {
            sockSave = new GemSaveData[GemCatalog.MaxSockets];
            for (int i = 0; i < item.socketCount && i < item.sockets.Length; i++)
                sockSave[i] = GemSaveData.From(item.sockets[i]);
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
            attackSpeedBonus = item.attackSpeedBonus,
            attackRangeBonus = item.attackRangeBonus,
            weaponProfile = item.weaponProfile,
            sockets = sockSave
        };
    }

    public ItemInstance ToItem()
    {
        if (!IsValid) return null;

        if (slot == (int)ItemSlot.Gem)
        {
            return GemCatalog.CreateGem(
                (GemKind)Mathf.Clamp(gemKind, 0, GemCatalog.KindCount - 1),
                Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel),
                (ItemRarity)Mathf.Clamp(rarity, 0, 4));
        }

        var item = new ItemInstance
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = displayName,
            slot = (ItemSlot)slot,
            rarity = (ItemRarity)rarity,
            itemLevel = Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel),
            attack = attack,
            defense = defense,
            maxHpBonus = maxHpBonus,
            attackSpeedBonus = attackSpeedBonus,
            attackRangeBonus = attackRangeBonus,
            weaponProfile = Mathf.Clamp(weaponProfile, 0, 2)
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
