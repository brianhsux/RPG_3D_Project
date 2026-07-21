using UnityEngine;

public enum GemKind
{
    Ruby = 0,       // Crit chance + crit damage
    Sapphire,       // Damage reduction %
    Emerald,        // Poison bonus damage on hit
    Topaz,          // Attack % + flat attack
    Amethyst,       // Defense % + flat defense
    Diamond,        // Block chance
    Onyx,           // Reflect damage %
    Garnet,         // Fire bonus damage on hit
    Aquamarine,     // Ice bonus damage on hit
    Citrine,        // Max HP bonus
    Bloodstone,     // Lifesteal %
    Obsidian        // Attack cooldown reduction
}

/// <summary>Aggregated bonuses from all socketed gems on equipped gear.</summary>
public struct GemBonuses
{
    public float attackFlat;
    public float attackPercent;
    public float defenseFlat;
    public float defensePercent;
    public float maxHpFlat;
    public float critChance;
    public float critDamage;      // multiplier added on top of 1.5 base, e.g. 0.25 => 1.75x
    public float blockChance;
    public float damageReduction; // 0–0.6 soft cap applied later
    public float reflectPercent;
    public float fireDamage;
    public float iceDamage;
    public float poisonDamage;
    public float lifesteal;
    public float cooldownReduction; // 0–0.4
}

public static class GemCatalog
{
    public const int MaxSockets = 3;

    public static string KindName(GemKind kind)
    {
        switch (kind)
        {
            case GemKind.Ruby: return "Ruby";
            case GemKind.Sapphire: return "Sapphire";
            case GemKind.Emerald: return "Emerald";
            case GemKind.Topaz: return "Topaz";
            case GemKind.Amethyst: return "Amethyst";
            case GemKind.Diamond: return "Diamond";
            case GemKind.Onyx: return "Onyx";
            case GemKind.Garnet: return "Garnet";
            case GemKind.Aquamarine: return "Aquamarine";
            case GemKind.Citrine: return "Citrine";
            case GemKind.Bloodstone: return "Bloodstone";
            default: return "Obsidian";
        }
    }

    public static string EffectLabel(GemKind kind)
    {
        switch (kind)
        {
            case GemKind.Ruby: return "Crit chance & crit damage";
            case GemKind.Sapphire: return "Damage reduction";
            case GemKind.Emerald: return "Poison on hit";
            case GemKind.Topaz: return "Attack power";
            case GemKind.Amethyst: return "Defense";
            case GemKind.Diamond: return "Block chance";
            case GemKind.Onyx: return "Reflect damage";
            case GemKind.Garnet: return "Fire on hit";
            case GemKind.Aquamarine: return "Ice on hit";
            case GemKind.Citrine: return "Max HP";
            case GemKind.Bloodstone: return "Lifesteal";
            default: return "Faster attacks";
        }
    }

    public static Color KindColor(GemKind kind)
    {
        switch (kind)
        {
            case GemKind.Ruby: return new Color(0.95f, 0.2f, 0.25f);
            case GemKind.Sapphire: return new Color(0.25f, 0.45f, 0.95f);
            case GemKind.Emerald: return new Color(0.2f, 0.8f, 0.35f);
            case GemKind.Topaz: return new Color(1f, 0.85f, 0.2f);
            case GemKind.Amethyst: return new Color(0.7f, 0.35f, 0.95f);
            case GemKind.Diamond: return new Color(0.85f, 0.95f, 1f);
            case GemKind.Onyx: return new Color(0.2f, 0.2f, 0.22f);
            case GemKind.Garnet: return new Color(1f, 0.45f, 0.15f);
            case GemKind.Aquamarine: return new Color(0.35f, 0.85f, 0.95f);
            case GemKind.Citrine: return new Color(1f, 0.75f, 0.35f);
            case GemKind.Bloodstone: return new Color(0.65f, 0.12f, 0.18f);
            default: return new Color(0.35f, 0.35f, 0.4f);
        }
    }

    public static int SocketCountForRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return 1;
            case ItemRarity.Rare: return 1;
            case ItemRarity.Epic: return 2;
            case ItemRarity.Legendary: return 3;
            default: return 0;
        }
    }

    public static void EnsureSockets(ItemInstance gear)
    {
        if (gear == null || gear.slot == ItemSlot.Gem) return;
        int n = SocketCountForRarity(gear.rarity);
        if (gear.sockets == null || gear.sockets.Length != MaxSockets)
        {
            var next = new ItemInstance[MaxSockets];
            if (gear.sockets != null)
            {
                for (int i = 0; i < Mathf.Min(MaxSockets, gear.sockets.Length); i++)
                    next[i] = gear.sockets[i];
            }
            gear.sockets = next;
        }
        gear.socketCount = n;
    }

    public static float PowerScale(int itemLevel, ItemRarity rarity)
    {
        float r = 1f + (int)rarity * 0.22f;
        return (0.85f + itemLevel * 0.045f) * r;
    }

    public static void ApplyGemToBonuses(ItemInstance gem, ref GemBonuses b)
    {
        if (gem == null || gem.slot != ItemSlot.Gem) return;
        float s = PowerScale(gem.itemLevel, gem.rarity);
        switch (gem.gemKind)
        {
            case GemKind.Ruby:
                b.critChance += 0.04f * s;
                b.critDamage += 0.18f * s;
                break;
            case GemKind.Sapphire:
                b.damageReduction += 0.035f * s;
                break;
            case GemKind.Emerald:
                b.poisonDamage += 3.5f * s;
                break;
            case GemKind.Topaz:
                b.attackFlat += 2.5f * s;
                b.attackPercent += 0.04f * s;
                break;
            case GemKind.Amethyst:
                b.defenseFlat += 2f * s;
                b.defensePercent += 0.045f * s;
                break;
            case GemKind.Diamond:
                b.blockChance += 0.05f * s;
                break;
            case GemKind.Onyx:
                b.reflectPercent += 0.06f * s;
                break;
            case GemKind.Garnet:
                b.fireDamage += 4f * s;
                break;
            case GemKind.Aquamarine:
                b.iceDamage += 3.5f * s;
                break;
            case GemKind.Citrine:
                b.maxHpFlat += 18f * s;
                break;
            case GemKind.Bloodstone:
                b.lifesteal += 0.03f * s;
                break;
            case GemKind.Obsidian:
                b.cooldownReduction += 0.04f * s;
                break;
        }
    }

    public static GemBonuses SumSockets(ItemInstance weapon, ItemInstance armor)
    {
        var b = new GemBonuses();
        AddFromGear(weapon, ref b);
        AddFromGear(armor, ref b);
        b.damageReduction = Mathf.Min(0.55f, b.damageReduction);
        b.blockChance = Mathf.Min(0.5f, b.blockChance);
        b.critChance = Mathf.Min(0.55f, b.critChance);
        b.reflectPercent = Mathf.Min(0.6f, b.reflectPercent);
        b.lifesteal = Mathf.Min(0.35f, b.lifesteal);
        b.cooldownReduction = Mathf.Min(0.4f, b.cooldownReduction);
        return b;
    }

    static void AddFromGear(ItemInstance gear, ref GemBonuses b)
    {
        if (gear?.sockets == null) return;
        EnsureSockets(gear);
        for (int i = 0; i < gear.socketCount && i < gear.sockets.Length; i++)
            ApplyGemToBonuses(gear.sockets[i], ref b);
    }

    public static ItemInstance CreateGem(GemKind kind, int itemLevel, ItemRarity rarity)
    {
        itemLevel = Mathf.Clamp(itemLevel, 1, 40);
        var gem = new ItemInstance
        {
            id = System.Guid.NewGuid().ToString("N"),
            slot = ItemSlot.Gem,
            gemKind = kind,
            itemLevel = itemLevel,
            rarity = rarity,
            displayName = $"{ItemFactory.RarityName(rarity)}·{KindName(kind)}",
            attack = 0f,
            defense = 0f,
            maxHpBonus = 0f,
            socketCount = 0,
            sockets = null
        };
        return gem;
    }

    public static ItemInstance RollMineDrop(int contentLevel)
    {
        int lv = Mathf.Clamp(contentLevel + Random.Range(-1, 3), 1, 40);
        var rarity = ProgressionFormulas.RollRarity(contentLevel, contentLevel >= 20);
        // Slightly favor lower rarities from mining
        if (rarity == ItemRarity.Legendary && Random.value > 0.35f)
            rarity = ItemRarity.Epic;
        if (rarity == ItemRarity.Epic && Random.value > 0.5f)
            rarity = ItemRarity.Rare;

        var kinds = (GemKind[])System.Enum.GetValues(typeof(GemKind));
        var kind = kinds[Random.Range(0, kinds.Length)];
        return CreateGem(kind, lv, rarity);
    }

    public static string StatsLine(ItemInstance gem)
    {
        if (gem == null || gem.slot != ItemSlot.Gem) return "";
        var b = new GemBonuses();
        ApplyGemToBonuses(gem, ref b);
        switch (gem.gemKind)
        {
            case GemKind.Ruby: return $"Crit +{b.critChance * 100f:0.#}%  CritDmg +{b.critDamage * 100f:0}%";
            case GemKind.Sapphire: return $"DR +{b.damageReduction * 100f:0.#}%";
            case GemKind.Emerald: return $"Poison +{b.poisonDamage:0.#}";
            case GemKind.Topaz: return $"ATK +{b.attackFlat:0.#}  ATK +{b.attackPercent * 100f:0.#}%";
            case GemKind.Amethyst: return $"DEF +{b.defenseFlat:0.#}  DEF +{b.defensePercent * 100f:0.#}%";
            case GemKind.Diamond: return $"Block +{b.blockChance * 100f:0.#}%";
            case GemKind.Onyx: return $"Reflect +{b.reflectPercent * 100f:0.#}%";
            case GemKind.Garnet: return $"Fire +{b.fireDamage:0.#}";
            case GemKind.Aquamarine: return $"Ice +{b.iceDamage:0.#}";
            case GemKind.Citrine: return $"HP +{b.maxHpFlat:0}";
            case GemKind.Bloodstone: return $"Lifesteal +{b.lifesteal * 100f:0.#}%";
            default: return $"Atk Speed +{b.cooldownReduction * 100f:0.#}%";
        }
    }
}
