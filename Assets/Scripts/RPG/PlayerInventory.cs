using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    public const int MaxBag = 32;

    // Must not be Unity-serialized: exiting Play Mode restores scene defaults
    // (empty items) and was overwriting save files on quit.
    [System.NonSerialized] public List<ItemInstance> bag = new List<ItemInstance>();
    [System.NonSerialized] public ItemInstance equippedWeapon;
    [System.NonSerialized] public ItemInstance equippedArmor;

    public string lastLootMessage = "";
    float _lootMsgUntil;

    CharacterStats _stats;

    public static PlayerInventory Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        _stats = GetComponent<CharacterStats>();

        if (GetComponent<PlayerProgression>() == null)
            gameObject.AddComponent<PlayerProgression>();

        if (bag == null) bag = new List<ItemInstance>();
        ResetToStarter();
    }

    public void ResetToStarter()
    {
        if (bag == null) bag = new List<ItemInstance>();
        bag.Clear();
        equippedWeapon = ItemFactory.StarterWeapon();
        equippedArmor = ItemFactory.StarterArmor();
        ApplyEquipmentStats(true);
    }

    public void ApplySave(AccountSaveData data)
    {
        if (bag == null) bag = new List<ItemInstance>();
        bag.Clear();
        if (data == null)
        {
            ResetToStarter();
            return;
        }

        var weapon = data.weapon != null ? data.weapon.ToItem() : null;
        var armor = data.armor != null ? data.armor.ToItem() : null;
        equippedWeapon = weapon ?? ItemFactory.StarterWeapon();
        equippedArmor = armor ?? ItemFactory.StarterArmor();

        if (data.bag != null)
        {
            foreach (var entry in data.bag)
            {
                if (entry == null) continue;
                var item = entry.ToItem();
                if (item == null) continue;
                if (bag.Count >= MaxBag) break;
                bag.Add(item);
            }
        }

        ApplyEquipmentStats(true);
    }

    public void WriteToSave(AccountSaveData data)
    {
        if (data == null) return;
        data.weapon = ItemSaveData.From(equippedWeapon);
        data.armor = ItemSaveData.From(equippedArmor);

        var list = new List<ItemSaveData>();
        if (bag != null)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                var entry = ItemSaveData.From(bag[i]);
                if (entry != null) list.Add(entry);
            }
        }
        data.bag = list.ToArray();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool AddItem(ItemInstance item)
    {
        if (item == null) return false;
        if (bag.Count >= MaxBag)
        {
            ShowLoot("Bag full!");
            return false;
        }
        bag.Add(item);
        ShowLoot($"Obtained {item.displayName} Lv{item.itemLevel}");
        if (AccountManager.Instance != null && AccountManager.Instance.IsReady)
            AccountManager.Instance.SaveActive(silent: true);
        return true;
    }

    public void ShowLoot(string msg)
    {
        lastLootMessage = msg;
        _lootMsgUntil = Time.time + 3.5f;
    }

    public bool HasLootMessage => Time.time < _lootMsgUntil;

    public void EquipFromBag(int index)
    {
        if (index < 0 || index >= bag.Count) return;
        var item = bag[index];
        if (item == null) return;

        if (item.slot == ItemSlot.Gem)
        {
            // Prefer weapon sockets, then armor.
            if (TrySocketGem(index, preferWeapon: true) || TrySocketGem(index, preferWeapon: false))
                return;
            ShowLoot("No empty sockets (need Uncommon+ gear)");
            return;
        }

        bag.RemoveAt(index);
        GemCatalog.EnsureSockets(item);

        if (item.slot == ItemSlot.Weapon)
        {
            if (equippedWeapon != null) bag.Add(equippedWeapon);
            equippedWeapon = item;
        }
        else
        {
            if (equippedArmor != null) bag.Add(equippedArmor);
            equippedArmor = item;
        }

        ApplyEquipmentStats(false);
        ShowLoot($"Equipped {item.displayName}");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public bool TrySocketGem(int bagIndex, bool preferWeapon)
    {
        if (bagIndex < 0 || bagIndex >= bag.Count) return false;
        var gem = bag[bagIndex];
        if (gem == null || gem.slot != ItemSlot.Gem) return false;

        ItemInstance gear = preferWeapon ? equippedWeapon : equippedArmor;
        if (gear == null) return false;
        GemCatalog.EnsureSockets(gear);

        int slot = -1;
        for (int i = 0; i < gear.socketCount; i++)
        {
            if (gear.sockets[i] == null)
            {
                slot = i;
                break;
            }
        }
        if (slot < 0) return false;

        bag.RemoveAt(bagIndex);
        gear.sockets[slot] = gem;
        ApplyEquipmentStats(false);
        ShowLoot($"Socketed {gem.displayName} into {(preferWeapon ? "Weapon" : "Armor")}");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
        return true;
    }

    public void Unsocket(bool fromWeapon, int socketIndex)
    {
        var gear = fromWeapon ? equippedWeapon : equippedArmor;
        if (gear?.sockets == null) return;
        if (socketIndex < 0 || socketIndex >= gear.sockets.Length) return;
        var gem = gear.sockets[socketIndex];
        if (gem == null) return;
        if (bag.Count >= MaxBag)
        {
            ShowLoot("Bag full — cannot unsocket");
            return;
        }
        gear.sockets[socketIndex] = null;
        bag.Add(gem);
        ApplyEquipmentStats(false);
        ShowLoot($"Removed {gem.displayName}");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public void ApplyEquipmentStats(bool refillHp)
    {
        if (_stats == null) _stats = GetComponent<CharacterStats>();

        int lv = PlayerProgression.Instance != null ? PlayerProgression.Instance.level : 1;

        float baseHp = 100f + ProgressionFormulas.PlayerLevelBonusHp(lv);
        float baseAtk = 12f + ProgressionFormulas.PlayerLevelBonusAtk(lv);
        float baseDef = ProgressionFormulas.PlayerLevelBonusDef(lv);

        if (equippedWeapon != null) GemCatalog.EnsureSockets(equippedWeapon);
        if (equippedArmor != null) GemCatalog.EnsureSockets(equippedArmor);

        float weaponAtk = equippedWeapon != null ? equippedWeapon.attack : 0f;
        float armorDef = equippedArmor != null ? equippedArmor.defense : 0f;
        float armorHp = equippedArmor != null ? equippedArmor.maxHpBonus : 0f;

        var gems = GemCatalog.SumSockets(equippedWeapon, equippedArmor);

        float atk = (baseAtk + weaponAtk + gems.attackFlat) * (1f + gems.attackPercent);
        float def = (baseDef + armorDef + gems.defenseFlat) * (1f + gems.defensePercent);
        float hp = baseHp + armorHp + gems.maxHpFlat;

        _stats.baseAttack = atk;
        _stats.attackDamage = atk;
        _stats.defense = def;
        _stats.maxHealth = hp;

        _stats.critChance = gems.critChance;
        _stats.critDamageBonus = 0.5f + gems.critDamage;
        _stats.blockChance = gems.blockChance;
        _stats.damageReduction = gems.damageReduction;
        _stats.reflectPercent = gems.reflectPercent;
        _stats.fireBonus = gems.fireDamage;
        _stats.iceBonus = gems.iceDamage;
        _stats.poisonBonus = gems.poisonDamage;
        _stats.lifesteal = gems.lifesteal;
        _stats.cooldownReduction = gems.cooldownReduction;

        if (refillHp || _stats.currentHealth > _stats.maxHealth)
            _stats.currentHealth = _stats.maxHealth;
        else
            _stats.currentHealth = Mathf.Min(_stats.currentHealth, _stats.maxHealth);
    }

    /// <summary>
    /// Equip the strongest weapon and armor from bag + currently equipped.
    /// Weapon ranked by attack; armor by defense + HP bonus.
    /// </summary>
    public int EquipBest()
    {
        int changed = 0;
        string weaponMsg = null;
        string armorMsg = null;

        int bestWeaponIdx = FindBestBagIndex(ItemSlot.Weapon, equippedWeapon);
        if (bestWeaponIdx >= 0)
        {
            var item = bag[bestWeaponIdx];
            bag.RemoveAt(bestWeaponIdx);
            if (equippedWeapon != null) bag.Add(equippedWeapon);
            equippedWeapon = item;
            weaponMsg = item.displayName;
            changed++;
        }

        int bestArmorIdx = FindBestBagIndex(ItemSlot.Armor, equippedArmor);
        if (bestArmorIdx >= 0)
        {
            var item = bag[bestArmorIdx];
            bag.RemoveAt(bestArmorIdx);
            if (equippedArmor != null) bag.Add(equippedArmor);
            equippedArmor = item;
            armorMsg = item.displayName;
            changed++;
        }

        if (changed > 0)
        {
            ApplyEquipmentStats(false);
            if (weaponMsg != null && armorMsg != null)
                ShowLoot($"Equip Best: Weapon {weaponMsg}, Armor {armorMsg}");
            else if (weaponMsg != null)
                ShowLoot($"Equip Best: Weapon {weaponMsg}");
            else
                ShowLoot($"Equip Best: Armor {armorMsg}");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
        else
            ShowLoot("Already wearing best gear");

        return changed;
    }

    /// <summary>Bag index of an item better than <paramref name="current"/>, or -1.</summary>
    int FindBestBagIndex(ItemSlot slot, ItemInstance current)
    {
        int bestIdx = -1;
        ItemInstance best = current;
        for (int i = 0; i < bag.Count; i++)
        {
            var item = bag[i];
            if (item == null || item.slot != slot) continue;
            if (!IsBetterGear(item, best)) continue;
            best = item;
            bestIdx = i;
        }
        return bestIdx;
    }

    public static bool IsBetterGear(ItemInstance candidate, ItemInstance current)
    {
        if (candidate == null) return false;
        if (current == null) return true;

        float a = GearScore(candidate);
        float b = GearScore(current);
        if (a > b + 0.01f) return true;
        if (a < b - 0.01f) return false;
        if (candidate.rarity != current.rarity)
            return candidate.rarity > current.rarity;
        return candidate.itemLevel > current.itemLevel;
    }

    public static float GearScore(ItemInstance item)
    {
        if (item == null) return float.NegativeInfinity;
        if (item.slot == ItemSlot.Weapon)
            return item.attack;
        return item.defense * 2.5f + item.maxHpBonus * 0.12f;
    }

    public string CompareLabel(ItemInstance item)
    {
        if (item == null) return "";
        var equipped = item.slot == ItemSlot.Weapon ? equippedWeapon : equippedArmor;
        if (equipped == null) return "Equip";
        if (IsBetterGear(item, equipped))
        {
            if (item.slot == ItemSlot.Weapon)
            {
                float d = item.attack - equipped.attack;
                return d > 0.5f ? $"+{d:0} ATK" : "Better";
            }
            float def = item.defense - equipped.defense;
            float hp = item.maxHpBonus - equipped.maxHpBonus;
            if (def > 0.5f && hp > 0.5f) return "Better";
            if (def > 0.5f) return $"+{def:0} DEF";
            if (hp > 0.5f) return $"+{hp:0} HP";
            return "Better";
        }
        if (Mathf.Abs(GearScore(item) - GearScore(equipped)) < 0.5f)
            return "Similar";
        return "Weaker";
    }

    public void SetPanelOpen(bool open)
    {
        if (showPanel == open) return;
        showPanel = open;
        Time.timeScale = open ? 0f : 1f;
    }

    /// <summary>
    /// One-click craft: repeatedly fuse every 3 same-slot items until none left to craft.
    /// Prefers similar item levels (sorted ascending) so upgrades stay coherent.
    /// </summary>
    public int AutoSynthesizeAll()
    {
        int crafted = 0;
        bool progressed = true;

        while (progressed)
        {
            progressed = false;
            if (TryAutoSynthesizeOne(ItemSlot.Weapon, out _))
            {
                crafted++;
                progressed = true;
            }
            if (TryAutoSynthesizeOne(ItemSlot.Armor, out _))
            {
                crafted++;
                progressed = true;
            }
            if (TryAutoSynthesizeGems(out _))
            {
                crafted++;
                progressed = true;
            }
        }

        if (crafted > 0)
        {
            ShowLoot($"Auto Craft complete: {crafted} crafts");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
        else
            ShowLoot("Nothing to craft (need 3 of same type)");

        return crafted;
    }

    bool TryAutoSynthesizeOne(ItemSlot slot, out ItemInstance result)
    {
        result = null;
        var idxs = new List<int>();
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] != null && bag[i].slot == slot)
                idxs.Add(i);
        }
        if (idxs.Count < 3) return false;

        idxs.Sort((a, b) =>
        {
            int lv = bag[a].itemLevel.CompareTo(bag[b].itemLevel);
            if (lv != 0) return lv;
            return bag[a].rarity.CompareTo(bag[b].rarity);
        });

        int i0 = idxs[0];
        int i1 = idxs[1];
        int i2 = idxs[2];
        result = ProgressionFormulas.Synthesize(bag[i0], bag[i1], bag[i2]);
        if (result == null) return false;

        var remove = new List<int> { i0, i1, i2 };
        remove.Sort();
        for (int i = remove.Count - 1; i >= 0; i--)
            bag.RemoveAt(remove[i]);

        bag.Add(result);
        return true;
    }

    bool TryAutoSynthesizeGems(out ItemInstance result)
    {
        result = null;
        // Group bag gems by kind; craft 3 of same kind (lowest levels first).
        for (int k = 0; k <= 11; k++)
        {
            var idxs = new List<int>();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i] != null && bag[i].slot == ItemSlot.Gem && (int)bag[i].gemKind == k)
                    idxs.Add(i);
            }
            if (idxs.Count < 3) continue;
            idxs.Sort((a, b) => bag[a].itemLevel.CompareTo(bag[b].itemLevel));
            result = ProgressionFormulas.Synthesize(bag[idxs[0]], bag[idxs[1]], bag[idxs[2]]);
            if (result == null) continue;
            var remove = new List<int> { idxs[0], idxs[1], idxs[2] };
            remove.Sort();
            for (int i = remove.Count - 1; i >= 0; i--)
                bag.RemoveAt(remove[i]);
            bag.Add(result);
            return true;
        }
        return false;
    }

    public bool showPanel;
    Vector2 _bagScroll;

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.iKey.wasPressedThisFrame)
            SetPanelOpen(!showPanel);

        if (showPanel && kb.escapeKey.wasPressedThisFrame)
            SetPanelOpen(false);
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;

        RPGUI.Ensure();

        if (HasLootMessage)
            RPGUI.DrawToast(lastLootMessage, new Color(0.1f, 0.16f, 0.1f, 0.94f));

        if (!showPanel) return;

        int prevDepth = GUI.depth;
        GUI.depth = -1000;

        Rect dim = new Rect(0f, 0f, Screen.width, Screen.height);
        RPGUI.Fill(dim, new Color(0f, 0f, 0f, 0.58f));

        float w = MathMin(Screen.width - 32f, 980f);
        float h = Mathf.Min(Screen.height - 32f, 760f);
        Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);

        // Click outside panel closes inventory.
        if (Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 &&
            !panel.Contains(Event.current.mousePosition))
        {
            SetPanelOpen(false);
            Event.current.Use();
            GUI.depth = prevDepth;
            return;
        }

        RPGUI.DrawPanel(panel, new Color(0.045f, 0.055f, 0.08f, 0.98f));

        float pad = 24f;
        float y = panel.y + 16f;

        RPGUI.DrawText(new Rect(panel.x + pad, y, w - 160f, RPGUI.TitleH), "Inventory", RPGUI.Title);
        if (RPGUI.ButtonSm(new Rect(panel.xMax - pad - 100f, y + 4f, 100f, RPGUI.ButtonSmH), "Close"))
            SetPanelOpen(false);
        y += RPGUI.TitleH + 2f;

        RPGUI.DrawText(new Rect(panel.x + pad, y, w - pad * 2f, RPGUI.HintH),
            "I / Esc Close  |  Green = better than equipped", RPGUI.Hint);
        y += RPGUI.HintH + 8f;

        RPGUI.DrawText(new Rect(panel.x + pad, y, 200f, RPGUI.LabelH), "Equipped", RPGUI.Label);
        y += RPGUI.LabelH;

        float cardW = (w - pad * 2f - 12f) * 0.5f;
        float cardH = 168f;
        DrawEquippedCard(panel.x + pad, y, cardW, cardH, "Weapon", equippedWeapon, true);
        DrawEquippedCard(panel.x + pad + cardW + 12f, y, cardW, cardH, "Armor", equippedArmor, false);
        y += cardH + 12f;

        int craftable = CountCraftableBatches();
        int upgradeCount = CountUpgradesInBag();
        string bagHint = upgradeCount > 0
            ? $"Bag {bag.Count}/{MaxBag}  |  {upgradeCount} upgrade(s)  |  Click gem to socket"
            : $"Bag {bag.Count}/{MaxBag}  |  Click gem to socket into gear";
        RPGUI.DrawText(new Rect(panel.x + pad, y, w - pad * 2f, RPGUI.HintH), bagHint, RPGUI.Hint);
        y += RPGUI.HintH + 6f;

        float listTop = y;
        float listBottom = panel.yMax - 78f;
        float viewH = Mathf.Max(80f, listBottom - listTop);
        float rowH = 96f;
        float contentW = w - pad * 2f - 20f;
        float contentH = Mathf.Max(viewH, bag.Count * rowH + 8f);

        Rect viewRect = new Rect(panel.x + pad, listTop, w - pad * 2f, viewH);
        _bagScroll = GUI.BeginScrollView(viewRect, _bagScroll, new Rect(0f, 0f, contentW, contentH), false, true);

        for (int i = 0; i < bag.Count; i++)
            DrawItemLine(0f, i * rowH, contentW - 4f, bag[i], i);

        if (bag.Count == 0)
            RPGUI.DrawText(new Rect(8f, 16f, contentW - 16f, RPGUI.HintH),
                "Bag empty — defeat monsters or mine ores (E)", RPGUI.Hint);

        GUI.EndScrollView();

        float btnY = panel.yMax - 66f;
        float btnGap = 12f;
        float btnW = (w - pad * 2f - btnGap) * 0.5f;
        if (RPGUI.Button(new Rect(panel.x + pad, btnY, btnW, RPGUI.ButtonH), "Equip Best", primary: true))
            EquipBest();

        string craftLabel = craftable > 0 ? $"Auto Craft ({craftable})" : "Auto Craft";
        if (RPGUI.Button(new Rect(panel.x + pad + btnW + btnGap, btnY, btnW, RPGUI.ButtonH), craftLabel))
            AutoSynthesizeAll();

        GUI.depth = prevDepth;
    }

    int CountUpgradesInBag()
    {
        int n = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            var item = bag[i];
            if (item == null || item.slot == ItemSlot.Gem) continue;
            var eq = item.slot == ItemSlot.Weapon ? equippedWeapon : equippedArmor;
            if (IsBetterGear(item, eq)) n++;
        }
        return n;
    }

    int CountCraftableBatches()
    {
        int weapons = 0;
        int armors = 0;
        int gems = 0;
        var gemCounts = new int[12];
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if (bag[i].slot == ItemSlot.Weapon) weapons++;
            else if (bag[i].slot == ItemSlot.Armor) armors++;
            else if (bag[i].slot == ItemSlot.Gem)
            {
                gems++;
                int k = Mathf.Clamp((int)bag[i].gemKind, 0, 11);
                gemCounts[k]++;
            }
        }

        int total = CountRecursiveCrafts(weapons) + CountRecursiveCrafts(armors);
        for (int k = 0; k < gemCounts.Length; k++)
            total += CountRecursiveCrafts(gemCounts[k]);
        return total;
    }

    static int CountRecursiveCrafts(int count)
    {
        int total = 0;
        while (count >= 3)
        {
            int batches = count / 3;
            total += batches;
            count = batches + (count % 3); // products + leftovers
        }
        return total;
    }

    static float MathMin(float a, float b) => a < b ? a : b;

    void DrawEquippedCard(float x, float y, float width, float height, string slotTitle, ItemInstance item, bool isWeapon)
    {
        Rect r = new Rect(x, y, width, height);
        RPGUI.DrawPanel(r, new Color(0.12f, 0.14f, 0.2f, 0.98f), false);
        if (item != null)
            RPGUI.Fill(new Rect(x, y, 4f, height), item.RarityColor);

        RPGUI.DrawText(new Rect(x + 14f, y + 6f, width - 28f, RPGUI.FieldTagH), slotTitle, RPGUI.FieldTag);

        if (item == null)
        {
            RPGUI.DrawText(new Rect(x + 14f, y + 48f, width - 28f, RPGUI.HintH), "(not equipped)", RPGUI.Hint);
            return;
        }

        float bodyY = y + 32f;
        RPGUI.DrawItemIcon(new Rect(x + 12f, bodyY, 56f, 56f), item);

        float textX = x + 78f;
        float textW = width - 90f;

        GUI.color = item.RarityColor;
        RPGUI.DrawText(new Rect(textX, bodyY, textW, RPGUI.ItemNameH), item.displayName, RPGUI.ItemName);
        GUI.color = Color.white;

        string styleTag = item.slot == ItemSlot.Weapon
            ? WeaponVisualUtil.StyleLabel(WeaponVisualUtil.StyleIndex(item.itemLevel))
            : ArmorVisualUtil.StyleLabel(ArmorVisualUtil.StyleIndex(item.itemLevel));
        string stats = item.slot == ItemSlot.Weapon
            ? $"Lv {item.itemLevel} · {styleTag} · ATK {item.attack:0}"
            : $"Lv {item.itemLevel} · {styleTag} · DEF {item.defense:0} · HP +{item.maxHpBonus:0}";
        RPGUI.DrawText(new Rect(textX, bodyY + 36f, textW, RPGUI.ItemMetaH), stats, RPGUI.ItemMeta);

        GemCatalog.EnsureSockets(item);
        float sockY = y + height - 42f;
        RPGUI.DrawText(new Rect(x + 12f, sockY - 22f, 160f, RPGUI.FieldTagH),
            $"Sockets {CountFilledSockets(item)}/{item.socketCount}", RPGUI.FieldTag);
        for (int i = 0; i < GemCatalog.MaxSockets; i++)
        {
            float sx = x + 12f + i * 44f;
            Rect sr = new Rect(sx, sockY, 38f, 32f);
            bool usable = i < item.socketCount;
            if (!usable)
            {
                RPGUI.Fill(sr, new Color(0.08f, 0.08f, 0.1f, 0.5f));
                continue;
            }
            var gem = item.sockets[i];
            if (gem != null)
            {
                RPGUI.Fill(sr, GemCatalog.KindColor(gem.gemKind));
                if (RPGUI.ButtonSm(sr, "x"))
                    Unsocket(isWeapon, i);
            }
            else
                RPGUI.Fill(sr, new Color(0.18f, 0.2f, 0.26f, 1f));
        }
    }

    static int CountFilledSockets(ItemInstance gear)
    {
        if (gear?.sockets == null) return 0;
        int n = 0;
        for (int i = 0; i < gear.socketCount && i < gear.sockets.Length; i++)
            if (gear.sockets[i] != null) n++;
        return n;
    }

    void DrawItemLine(float x, float y, float width, ItemInstance item, int bagIndex)
    {
        float rowH = 90f;
        if (item == null)
        {
            RPGUI.DrawPanel(new Rect(x, y, width, rowH), new Color(0.1f, 0.1f, 0.12f, 0.85f), false);
            RPGUI.DrawText(new Rect(x + 16f, y, width - 32f, rowH), "(empty slot)", RPGUI.Hint);
            return;
        }

        bool isGem = item.slot == ItemSlot.Gem;
        var equipped = item.slot == ItemSlot.Weapon ? equippedWeapon : equippedArmor;
        bool better = !isGem && IsBetterGear(item, equipped);
        bool worse = !isGem && equipped != null && !better && GearScore(item) < GearScore(equipped) - 0.5f;

        Rect r = new Rect(x, y, width, rowH);
        Color bg = isGem
            ? new Color(0.12f, 0.1f, 0.16f, 0.96f)
            : (better ? new Color(0.1f, 0.18f, 0.14f, 0.96f) : new Color(0.11f, 0.12f, 0.16f, 0.94f));
        RPGUI.DrawPanel(r, bg, false);
        RPGUI.Fill(new Rect(x, y, 4f, rowH), item.RarityColor);

        RPGUI.DrawItemIcon(new Rect(x + 14f, y + 12f, 66f, 66f), item);

        string meta;
        if (isGem)
            meta = $"Lv {item.itemLevel} · {GemCatalog.EffectLabel(item.gemKind)} · {GemCatalog.StatsLine(item)}";
        else
        {
            string styleTag = item.slot == ItemSlot.Weapon
                ? WeaponVisualUtil.StyleLabel(WeaponVisualUtil.StyleIndex(item.itemLevel))
                : ArmorVisualUtil.StyleLabel(ArmorVisualUtil.StyleIndex(item.itemLevel));
            string stats = item.slot == ItemSlot.Weapon
                ? $"ATK {item.attack:0}"
                : $"DEF {item.defense:0} · HP +{item.maxHpBonus:0}";
            meta = $"Lv {item.itemLevel} · {styleTag} · {stats}";
        }

        float textX = x + 94f;
        float textW = width - 230f;

        GUI.color = item.RarityColor;
        RPGUI.DrawText(new Rect(textX, y + 10f, textW, RPGUI.ItemNameH), item.displayName, RPGUI.ItemName);
        GUI.color = Color.white;

        RPGUI.DrawText(new Rect(textX, y + 50f, textW, RPGUI.ItemMetaH), meta, RPGUI.ItemMeta);

        if (isGem)
        {
            RPGUI.DrawChip(new Rect(x + width - 210f, y + 12f, 88f, 28f), "Gem", GemCatalog.KindColor(item.gemKind));
            if (bagIndex >= 0 &&
                RPGUI.ButtonSm(new Rect(x + width - 112f, y + 24f, 100f, RPGUI.ButtonSmH), "Socket", true))
                EquipFromBag(bagIndex);
        }
        else
        {
            string cmp = CompareLabel(item);
            Color chipBg = better ? RPGUI.Good : (worse ? new Color(0.35f, 0.28f, 0.28f, 1f) : new Color(0.28f, 0.32f, 0.4f, 1f));
            RPGUI.DrawChip(new Rect(x + width - 210f, y + 12f, 88f, 28f), cmp, chipBg);

            if (bagIndex >= 0 &&
                RPGUI.ButtonSm(new Rect(x + width - 112f, y + 24f, 100f, RPGUI.ButtonSmH), "Equip", better))
                EquipFromBag(bagIndex);
        }
    }
}
