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
    Obsidian,       // Attack speed
    Quartz          // Attack range
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
    public float attackSpeed;     // additive, 0.1 = +10% attack speed
    public float attackRange;     // meters
}

public static class GemCatalog
{
    public const int MaxSockets = 3;
    public static int KindCount => System.Enum.GetValues(typeof(GemKind)).Length;

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
            case GemKind.Obsidian: return "Obsidian";
            case GemKind.Quartz: return "Quartz";
            default: return "Gem";
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
            case GemKind.Obsidian: return "Attack speed";
            case GemKind.Quartz: return "Attack range";
            default: return "Gem bonus";
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
            case GemKind.Obsidian: return new Color(0.35f, 0.35f, 0.4f);
            case GemKind.Quartz: return new Color(0.92f, 0.9f, 0.85f);
            default: return Color.white;
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
        itemLevel = Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel);
        float r = 1f + (int)rarity * 0.22f;
        // Sqrt softens 1–100 so late gems stay strong but not 2.5× the old lv40 peak.
        return (0.75f + Mathf.Sqrt(itemLevel) * 0.38f) * r;
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
                b.attackSpeed += 0.045f * s;
                break;
            case GemKind.Quartz:
                b.attackRange += 0.14f * s;
                break;
        }
    }

    public static GemBonuses SumSockets(ItemInstance weapon, ItemInstance armor)
    {
        var b = new GemBonuses();
        AddFromGear(weapon, ref b);
        AddFromGear(armor, ref b);
        return ClampBonuses(b);
    }

    public static GemBonuses BonusesFromGear(ItemInstance gear)
    {
        var b = new GemBonuses();
        AddFromGear(gear, ref b);
        return ClampBonuses(b);
    }

    static GemBonuses ClampBonuses(GemBonuses b)
    {
        b.damageReduction = Mathf.Min(0.55f, b.damageReduction);
        b.blockChance = Mathf.Min(0.5f, b.blockChance);
        b.critChance = Mathf.Min(0.55f, b.critChance);
        b.reflectPercent = Mathf.Min(0.6f, b.reflectPercent);
        b.lifesteal = Mathf.Min(0.35f, b.lifesteal);
        b.attackSpeed = Mathf.Min(0.5f, b.attackSpeed);
        b.attackRange = Mathf.Min(1.2f, b.attackRange);
        return b;
    }

    static void AddFromGear(ItemInstance gear, ref GemBonuses b)
    {
        if (gear?.sockets == null) return;
        EnsureSockets(gear);
        for (int i = 0; i < gear.socketCount && i < gear.sockets.Length; i++)
            ApplyGemToBonuses(gear.sockets[i], ref b);
    }

    /// <summary>Compact list of active bonuses for UI (empty if none).</summary>
    public static string FormatBonusSummary(GemBonuses b)
    {
        var parts = new System.Collections.Generic.List<string>(8);
        if (b.attackFlat > 0.05f || b.attackPercent > 0.001f)
            parts.Add($"ATK +{b.attackFlat:0.#}" + (b.attackPercent > 0.001f ? $" (+{b.attackPercent * 100f:0.#}%)" : ""));
        if (b.defenseFlat > 0.05f || b.defensePercent > 0.001f)
            parts.Add($"DEF +{b.defenseFlat:0.#}" + (b.defensePercent > 0.001f ? $" (+{b.defensePercent * 100f:0.#}%)" : ""));
        if (b.maxHpFlat > 0.5f) parts.Add($"HP +{b.maxHpFlat:0}");
        if (b.critChance > 0.001f) parts.Add($"Crit {b.critChance * 100f:0.#}%");
        if (b.critDamage > 0.001f) parts.Add($"CritDmg +{b.critDamage * 100f:0}%");
        if (b.blockChance > 0.001f) parts.Add($"Block {b.blockChance * 100f:0.#}%");
        if (b.damageReduction > 0.001f) parts.Add($"DR {b.damageReduction * 100f:0.#}%");
        if (b.reflectPercent > 0.001f) parts.Add($"Reflect {b.reflectPercent * 100f:0.#}%");
        if (b.fireDamage > 0.05f) parts.Add($"Fire +{b.fireDamage:0.#}");
        if (b.iceDamage > 0.05f) parts.Add($"Ice +{b.iceDamage:0.#}");
        if (b.poisonDamage > 0.05f) parts.Add($"Poison +{b.poisonDamage:0.#}");
        if (b.lifesteal > 0.001f) parts.Add($"Lifesteal {b.lifesteal * 100f:0.#}%");
        if (b.attackSpeed > 0.001f) parts.Add($"AtkSpd +{b.attackSpeed * 100f:0.#}%");
        if (b.attackRange > 0.01f) parts.Add($"Range +{b.attackRange:0.00}");
        return parts.Count == 0 ? "No gem bonuses" : string.Join("  ·  ", parts);
    }

    public static ItemInstance CreateGem(GemKind kind, int itemLevel, ItemRarity rarity)
    {
        itemLevel = Mathf.Clamp(itemLevel, 1, ProgressionFormulas.MaxItemLevel);
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
        int lv = ProgressionFormulas.RollItemLevel(contentLevel, fromBoss: false);
        var rarity = ProgressionFormulas.RollRarity(contentLevel, contentLevel >= 20);
        if (rarity == ItemRarity.Legendary && Random.value > 0.35f)
            rarity = ItemRarity.Epic;
        if (rarity == ItemRarity.Epic && Random.value > 0.5f)
            rarity = ItemRarity.Rare;

        var kind = (GemKind)Random.Range(0, KindCount);
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
            case GemKind.Obsidian: return $"Atk Speed +{b.attackSpeed * 100f:0.#}%";
            case GemKind.Quartz: return $"Range +{b.attackRange:0.00}";
            default: return "";
        }
    }

    /// <summary>Higher = stronger gem for same-kind upgrade comparisons.</summary>
    public static float GemPowerScore(ItemInstance gem)
    {
        if (gem == null || gem.slot != ItemSlot.Gem) return -1f;
        return gem.itemLevel * 10f + (int)gem.rarity * 28f + PowerScale(gem.itemLevel, gem.rarity);
    }

    public static bool IsStrongerGem(ItemInstance candidate, ItemInstance current)
    {
        if (candidate == null || candidate.slot != ItemSlot.Gem) return false;
        if (current == null) return true;
        if (current.slot != ItemSlot.Gem) return true;
        if (candidate.gemKind != current.gemKind) return false;
        return GemPowerScore(candidate) > GemPowerScore(current) + 0.05f;
    }
}
