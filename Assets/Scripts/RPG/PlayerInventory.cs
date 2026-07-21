using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    /// <summary>Max non-gem items (weapons / armor) in the bag.</summary>
    public const int MaxGear = 200;
    /// <summary>Max gems in the bag (separate from gear).</summary>
    public const int MaxGems = 200;

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
        if (GetComponent<PlayerPetController>() == null)
            gameObject.AddComponent<PlayerPetController>();

        if (bag == null) bag = new List<ItemInstance>();
        ResetToStarter();
        if (PlayerPetController.Instance != null)
            PlayerPetController.Instance.ResetToStarter();
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
            int gems = 0;
            int gear = 0;
            foreach (var entry in data.bag)
            {
                if (entry == null) continue;
                var item = entry.ToItem();
                if (item == null) continue;
                if (item.slot == ItemSlot.Gem)
                {
                    if (gems >= MaxGems) continue;
                    gems++;
                }
                else
                {
                    if (gear >= MaxGear) continue;
                    gear++;
                }
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

    public int CountGemsInBag()
    {
        int n = 0;
        if (bag == null) return 0;
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] != null && bag[i].slot == ItemSlot.Gem) n++;
        }
        return n;
    }

    public int CountGearInBag()
    {
        int n = 0;
        if (bag == null) return 0;
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] != null && bag[i].slot != ItemSlot.Gem) n++;
        }
        return n;
    }

    public bool CanAddItem(ItemInstance item)
    {
        if (item == null) return false;
        if (item.slot == ItemSlot.Gem)
            return CountGemsInBag() < MaxGems;
        return CountGearInBag() < MaxGear;
    }

    public bool AddItem(ItemInstance item)
    {
        if (item == null) return false;
        if (!CanAddItem(item))
        {
            ShowLoot(item.slot == ItemSlot.Gem ? "Gem bag full!" : "Gear bag full!");
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
            _selectedGemBagIndex = index;
            _bagTab = 1;
            ShowLoot($"Selected {item.displayName} — click an empty (+) socket on Weapon/Armor");
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
        return TrySocketInto(bagIndex, preferWeapon, slot);
    }

    public bool TrySocketInto(int bagIndex, bool intoWeapon, int socketIndex)
    {
        if (bagIndex < 0 || bagIndex >= bag.Count) return false;
        var gem = bag[bagIndex];
        if (gem == null || gem.slot != ItemSlot.Gem) return false;

        var gear = intoWeapon ? equippedWeapon : equippedArmor;
        if (gear == null) return false;
        GemCatalog.EnsureSockets(gear);
        if (socketIndex < 0 || socketIndex >= gear.socketCount) return false;
        if (gear.sockets[socketIndex] != null) return false;

        bag.RemoveAt(bagIndex);
        // Bag index may shift selection
        if (_selectedGemBagIndex == bagIndex) _selectedGemBagIndex = -1;
        else if (_selectedGemBagIndex > bagIndex) _selectedGemBagIndex--;

        gear.sockets[socketIndex] = gem;
        ApplyEquipmentStats(false);
        ShowLoot($"Socketed {gem.displayName} → {(intoWeapon ? "Weapon" : "Armor")} slot {socketIndex + 1}");
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
        if (CountGemsInBag() >= MaxGems)
        {
            ShowLoot("Gem bag full — cannot unsocket");
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

        _stats.attackCooldown = ProgressionFormulas.PlayerBaseAttackCooldown;
        float weaponSpd = equippedWeapon != null ? equippedWeapon.attackSpeedBonus : 0f;
        float weaponRng = equippedWeapon != null ? equippedWeapon.attackRangeBonus : 0f;
        _stats.attackSpeed = ProgressionFormulas.SoftCapAttackSpeed(1f + weaponSpd + gems.attackSpeed);
        _stats.attackRange = ProgressionFormulas.SoftCapAttackRange(
            ProgressionFormulas.PlayerBaseAttackRange + weaponRng + gems.attackRange);

        _stats.critChance = gems.critChance;
        _stats.critDamageBonus = 0.5f + gems.critDamage;
        _stats.blockChance = gems.blockChance;
        _stats.damageReduction = gems.damageReduction;
        _stats.reflectPercent = gems.reflectPercent;
        _stats.fireBonus = gems.fireDamage;
        _stats.iceBonus = gems.iceDamage;
        _stats.poisonBonus = gems.poisonDamage;
        _stats.lifesteal = gems.lifesteal;

        if (refillHp || _stats.currentHealth > _stats.maxHealth)
            _stats.currentHealth = _stats.maxHealth;
        else
            _stats.currentHealth = Mathf.Min(_stats.currentHealth, _stats.maxHealth);
    }

    /// <summary>
    /// Equip the strongest weapon and armor from bag + currently equipped.
    /// Weapon ranked by attack; armor by defense + HP bonus.
    /// Gems on currently worn gear move onto the new pieces when possible.
    /// </summary>
    public int EquipBest()
    {
        int changed = 0;
        string weaponMsg = null;
        string armorMsg = null;
        int gemsMoved = 0;

        int bestWeaponIdx = FindBestBagIndex(ItemSlot.Weapon, equippedWeapon);
        if (bestWeaponIdx >= 0)
        {
            var item = bag[bestWeaponIdx];
            bag.RemoveAt(bestWeaponIdx);
            var old = equippedWeapon;
            equippedWeapon = item;
            gemsMoved += TransferGemsToNewGear(old, equippedWeapon);
            if (old != null) bag.Add(old);
            weaponMsg = item.displayName;
            changed++;
        }

        int bestArmorIdx = FindBestBagIndex(ItemSlot.Armor, equippedArmor);
        if (bestArmorIdx >= 0)
        {
            var item = bag[bestArmorIdx];
            bag.RemoveAt(bestArmorIdx);
            var old = equippedArmor;
            equippedArmor = item;
            gemsMoved += TransferGemsToNewGear(old, equippedArmor);
            if (old != null) bag.Add(old);
            armorMsg = item.displayName;
            changed++;
        }

        if (changed > 0)
        {
            ApplyEquipmentStats(false);
            string gemNote = gemsMoved > 0 ? $" · moved {gemsMoved} gem{(gemsMoved > 1 ? "s" : "")}" : "";
            if (weaponMsg != null && armorMsg != null)
                ShowLoot($"Equip Best: Weapon {weaponMsg}, Armor {armorMsg}{gemNote}");
            else if (weaponMsg != null)
                ShowLoot($"Equip Best: Weapon {weaponMsg}{gemNote}");
            else
                ShowLoot($"Equip Best: Armor {armorMsg}{gemNote}");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
        else
            ShowLoot("Already wearing best gear");

        return changed;
    }

    /// <summary>
    /// Move socketed gems from old gear onto empty sockets on new gear.
    /// Leftovers that do not fit stay on the old piece (which returns to the bag).
    /// Returns how many gems were moved onto the new piece.
    /// </summary>
    static int TransferGemsToNewGear(ItemInstance fromOld, ItemInstance toNew)
    {
        if (fromOld == null || toNew == null) return 0;
        GemCatalog.EnsureSockets(fromOld);
        GemCatalog.EnsureSockets(toNew);
        if (fromOld.sockets == null || toNew.sockets == null) return 0;

        var moving = new System.Collections.Generic.List<ItemInstance>(GemCatalog.MaxSockets);
        for (int i = 0; i < fromOld.socketCount && i < fromOld.sockets.Length; i++)
        {
            if (fromOld.sockets[i] == null) continue;
            moving.Add(fromOld.sockets[i]);
            fromOld.sockets[i] = null;
        }
        if (moving.Count == 0) return 0;

        int moved = 0;
        int mi = 0;
        for (int i = 0; i < toNew.socketCount && i < toNew.sockets.Length && mi < moving.Count; i++)
        {
            if (toNew.sockets[i] != null) continue;
            toNew.sockets[i] = moving[mi++];
            moved++;
        }

        // Put leftovers back on the old gear so they travel with it into the bag.
        for (int i = 0; i < fromOld.socketCount && i < fromOld.sockets.Length && mi < moving.Count; i++)
        {
            if (fromOld.sockets[i] != null) continue;
            fromOld.sockets[i] = moving[mi++];
        }

        return moved;
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
            return item.attack + item.attackSpeedBonus * 18f + item.attackRangeBonus * 8f;
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
        for (int k = 0; k < GemCatalog.KindCount; k++)
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
    Vector2 _gemScroll;
    Vector2 _petScroll;
    int _selectedGemBagIndex = -1;
    int _bagTab; // 0 gear, 1 gems, 2 pets

    public void SetPanelOpen(bool open)
    {
        if (showPanel == open) return;
        showPanel = open;
        Time.timeScale = open ? 0f : 1f;
        if (!open)
        {
            _selectedGemBagIndex = -1;
            _bagTab = 0;
        }
    }

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

        float w = MathMin(Screen.width - 32f, 1000f);
        float h = Mathf.Min(Screen.height - 24f, 860f);
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
            "I / Esc close  ·  Select a gem, then click an empty socket (+)  ·  Click filled gem to remove", RPGUI.Hint);
        y += RPGUI.HintH + 6f;

        // Live build summary (what combat actually uses)
        float summaryH = 56f;
        DrawBuildSummary(panel.x + pad, y, w - pad * 2f, summaryH);
        y += summaryH + 8f;

        RPGUI.DrawText(new Rect(panel.x + pad, y, 200f, RPGUI.LabelH), "Equipped", RPGUI.Label);
        y += RPGUI.LabelH;

        float cardW = (w - pad * 2f - 12f) * 0.5f;
        float cardH = 248f;
        DrawEquippedCard(panel.x + pad, y, cardW, cardH, "Weapon", equippedWeapon, true);
        DrawEquippedCard(panel.x + pad + cardW + 12f, y, cardW, cardH, "Armor", equippedArmor, false);
        y += cardH + 10f;

        // Socket mode banner
        if (_selectedGemBagIndex >= 0 && _selectedGemBagIndex < bag.Count && bag[_selectedGemBagIndex] != null
            && bag[_selectedGemBagIndex].slot == ItemSlot.Gem)
        {
            var sel = bag[_selectedGemBagIndex];
            Rect ban = new Rect(panel.x + pad, y, w - pad * 2f, 36f);
            RPGUI.DrawPanel(ban, new Color(0.15f, 0.22f, 0.18f, 0.98f), false);
            RPGUI.Fill(new Rect(ban.x, ban.y, 6f, ban.height), GemCatalog.KindColor(sel.gemKind));
            RPGUI.DrawText(new Rect(ban.x + 14f, ban.y, ban.width - 120f, ban.height),
                $"Selected: {sel.displayName} — click a glowing (+) socket above", RPGUI.Hint);
            if (RPGUI.ButtonSm(new Rect(ban.xMax - 100f, ban.y + 4f, 90f, 28f), "Cancel"))
                _selectedGemBagIndex = -1;
            y += 42f;
        }

        int gearCount = 0;
        int gemCount = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if (bag[i].slot == ItemSlot.Gem) gemCount++;
            else gearCount++;
        }
        int petCount = PlayerPetController.Instance != null ? PlayerPetController.Instance.owned.Count : 0;

        // Tabs: Gear | Gems | Pets
        float tabW = 140f;
        float tabH = RPGUI.ButtonSmH;
        if (RPGUI.Button(new Rect(panel.x + pad, y, tabW, tabH), $"Gear ({gearCount})", _bagTab == 0))
            _bagTab = 0;
        if (RPGUI.Button(new Rect(panel.x + pad + tabW + 8f, y, tabW, tabH), $"Gems ({gemCount})", _bagTab == 1))
            _bagTab = 1;
        if (RPGUI.Button(new Rect(panel.x + pad + (tabW + 8f) * 2f, y, tabW, tabH), $"Pets ({petCount})", _bagTab == 2))
            _bagTab = 2;
        y += tabH + 8f;

        int craftable = CountCraftableBatches();
        int upgradeCount = CountUpgradesInBag();
        string bagHint;
        if (_bagTab == 1)
            bagHint = $"Gems {gemCount}/{MaxGems}  |  Select a gem, then click an empty socket on equipped gear";
        else if (_bagTab == 2)
            bagHint = $"Pets {petCount}/{PlayerPetController.MaxPets}  |  Deploy one combat pet  |  Bosses can drop/upgrade pets";
        else
            bagHint = $"Gear {gearCount}/{MaxGear}  |  {upgradeCount} upgrade(s)  |  Equip from list";
        RPGUI.DrawText(new Rect(panel.x + pad, y, w - pad * 2f, RPGUI.HintH), bagHint, RPGUI.Hint);
        y += RPGUI.HintH + 6f;

        float listTop = y;
        float listBottom = panel.yMax - 78f;
        float viewH = Mathf.Max(80f, listBottom - listTop);
        float rowH = 96f;
        float contentW = w - pad * 2f - 20f;

        if (_bagTab == 2)
            DrawPetsTab(panel.x + pad, listTop, w - pad * 2f, viewH, contentW, rowH);
        else
        {
            var visible = new List<int>(bag.Count);
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i] == null) continue;
                bool isGem = bag[i].slot == ItemSlot.Gem;
                if ((_bagTab == 1) == isGem)
                    visible.Add(i);
            }

            float contentH = Mathf.Max(viewH, visible.Count * rowH + 8f);
            Rect viewRect = new Rect(panel.x + pad, listTop, w - pad * 2f, viewH);
            if (_bagTab == 1)
                _gemScroll = GUI.BeginScrollView(viewRect, _gemScroll, new Rect(0f, 0f, contentW, contentH), false, true);
            else
                _bagScroll = GUI.BeginScrollView(viewRect, _bagScroll, new Rect(0f, 0f, contentW, contentH), false, true);

            for (int v = 0; v < visible.Count; v++)
            {
                int bagIndex = visible[v];
                DrawItemLine(0f, v * rowH, contentW - 4f, bag[bagIndex], bagIndex);
            }

            if (visible.Count == 0)
            {
                string empty = _bagTab == 1
                    ? "No gems — mine crystal ores on Map 3 (press E)"
                    : "No gear — defeat monsters for weapons and armor";
                RPGUI.DrawText(new Rect(8f, 16f, contentW - 16f, RPGUI.HintH), empty, RPGUI.Hint);
            }

            GUI.EndScrollView();
        }

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

    void DrawPetsTab(float x, float listTop, float viewW, float viewH, float contentW, float rowH)
    {
        var pets = PlayerPetController.Instance;
        int count = pets != null ? pets.owned.Count : 0;
        float contentH = Mathf.Max(viewH, count * rowH + 8f);
        Rect viewRect = new Rect(x, listTop, viewW, viewH);
        _petScroll = GUI.BeginScrollView(viewRect, _petScroll, new Rect(0f, 0f, contentW, contentH), false, true);

        if (pets == null || count == 0)
        {
            RPGUI.DrawText(new Rect(8f, 16f, contentW - 16f, RPGUI.HintH),
                "No pets — starter Ember Fox should appear; defeat bosses for more", RPGUI.Hint);
            GUI.EndScrollView();
            return;
        }

        float playerAtk = _stats != null ? _stats.attackDamage : 20f;
        for (int i = 0; i < pets.owned.Count; i++)
        {
            var pet = pets.owned[i];
            if (pet == null) continue;
            float yy = i * rowH;
            bool active = pets.activePetId == pet.id;

            Rect r = new Rect(0f, yy, contentW - 4f, rowH - 6f);
            RPGUI.DrawPanel(r, active ? new Color(0.14f, 0.22f, 0.18f, 0.98f) : new Color(0.11f, 0.12f, 0.16f, 0.95f), false);
            RPGUI.Fill(new Rect(r.x, r.y, 5f, r.height), PetCatalog.KindColor(pet.kind));

            // Mini icon block
            RPGUI.Fill(new Rect(r.x + 12f, r.y + 14f, 56f, 56f), new Color(0.08f, 0.08f, 0.1f));
            RPGUI.Fill(new Rect(r.x + 22f, r.y + 24f, 36f, 36f), PetCatalog.KindColor(pet.kind));

            GUI.color = PetCatalog.KindColor(pet.kind);
            RPGUI.DrawText(new Rect(r.x + 80f, r.y + 8f, contentW - 220f, RPGUI.ItemNameH),
                pet.displayName + (active ? "  [ACTIVE]" : ""), RPGUI.ItemName);
            GUI.color = Color.white;

            RPGUI.DrawText(new Rect(r.x + 80f, r.y + 48f, contentW - 220f, RPGUI.ItemMetaH),
                PetCatalog.StatsLine(pet, playerAtk), RPGUI.ItemMeta);

            if (!active && RPGUI.ButtonSm(new Rect(r.xMax - 110f, r.y + 24f, 100f, RPGUI.ButtonSmH), "Deploy", true))
                pets.SetActive(pet.id);
            else if (active)
                RPGUI.DrawChip(new Rect(r.xMax - 110f, r.y + 28f, 100f, 26f), "Fighting", RPGUI.Good);
        }

        GUI.EndScrollView();
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

    void DrawBuildSummary(float x, float y, float width, float height)
    {
        RPGUI.DrawPanel(new Rect(x, y, width, height), new Color(0.1f, 0.14f, 0.2f, 0.98f), false);
        if (_stats == null) _stats = GetComponent<CharacterStats>();

        float atk = _stats != null ? _stats.attackDamage : 0f;
        float def = _stats != null ? _stats.defense : 0f;
        float hp = _stats != null ? _stats.maxHealth : 0f;
        float elem = _stats != null ? (_stats.fireBonus + _stats.iceBonus + _stats.poisonBonus) : 0f;

        RPGUI.DrawText(new Rect(x + 12f, y + 4f, width - 24f, RPGUI.FieldTagH),
            "YOUR BUILD (live combat stats)", RPGUI.FieldTag);

        string main = $"ATK {atk:0}   DEF {def:0}   HP {hp:0}";
        if (_stats != null)
            main += $"   SPD {_stats.attackSpeed:0.00}x   RNG {_stats.attackRange:0.0}";
        if (elem > 0.5f) main += $"   Elemental +{elem:0}";
        RPGUI.DrawText(new Rect(x + 12f, y + 24f, width - 24f, RPGUI.HintH), main, RPGUI.Hint);

        var gems = GemCatalog.SumSockets(equippedWeapon, equippedArmor);
        string bonus = GemCatalog.FormatBonusSummary(gems);
        // Truncate if extreme
        if (bonus.Length > 110) bonus = bonus.Substring(0, 107) + "...";
        GUI.color = new Color(0.75f, 0.9f, 1f);
        RPGUI.DrawText(new Rect(x + 12f, y + 44f, width - 24f, 20f), bonus, RPGUI.FieldTag);
        GUI.color = Color.white;
    }

    void DrawEquippedCard(float x, float y, float width, float height, string slotTitle, ItemInstance item, bool isWeapon)
    {
        bool waitingGem = _selectedGemBagIndex >= 0 && _selectedGemBagIndex < bag.Count
            && bag[_selectedGemBagIndex] != null && bag[_selectedGemBagIndex].slot == ItemSlot.Gem;

        Rect r = new Rect(x, y, width, height);
        RPGUI.DrawPanel(r, new Color(0.12f, 0.14f, 0.2f, 0.98f), false);
        if (item != null)
            RPGUI.Fill(new Rect(x, y, 4f, height), item.RarityColor);

        RPGUI.DrawText(new Rect(x + 12f, y + 6f, width - 24f, RPGUI.FieldTagH), slotTitle, RPGUI.FieldTag);

        if (item == null)
        {
            RPGUI.DrawText(new Rect(x + 12f, y + 48f, width - 24f, RPGUI.HintH), "(not equipped)", RPGUI.Hint);
            RPGUI.DrawText(new Rect(x + 12f, y + 90f, width - 24f, RPGUI.FieldTagH),
                "Equip Uncommon+ gear to unlock sockets", RPGUI.FieldTag);
            return;
        }

        GemCatalog.EnsureSockets(item);
        var gearGems = GemCatalog.BonusesFromGear(item);

        float bodyY = y + 30f;
        RPGUI.DrawItemIcon(new Rect(x + 10f, bodyY, 52f, 52f), item);

        float textX = x + 72f;
        float textW = width - 84f;

        GUI.color = item.RarityColor;
        RPGUI.DrawText(new Rect(textX, bodyY, textW, RPGUI.ItemNameH), item.displayName, RPGUI.ItemName);
        GUI.color = Color.white;

        // Base vs gem contribution
        if (isWeapon)
        {
            RPGUI.DrawText(new Rect(textX, bodyY + 34f, textW, RPGUI.FieldTagH),
                $"ATK {item.attack:0} · {ItemFactory.ProfileLabel(item.weaponProfile)} · SPD +{item.attackSpeedBonus * 100f:0}% · RNG +{item.attackRangeBonus:0.0}",
                RPGUI.FieldTag);
            string gemAtk = gearGems.attackFlat > 0.05f || gearGems.attackPercent > 0.001f
                || gearGems.fireDamage + gearGems.iceDamage + gearGems.poisonDamage > 0.05f
                ? GemCatalog.FormatBonusSummary(gearGems)
                : "No gems socketed yet";
            if (gemAtk.Length > 42) gemAtk = gemAtk.Substring(0, 40) + "..";
            GUI.color = new Color(0.55f, 1f, 0.7f);
            RPGUI.DrawText(new Rect(textX, bodyY + 54f, textW, RPGUI.FieldTagH), "Gems: " + gemAtk, RPGUI.FieldTag);
            GUI.color = Color.white;
        }
        else
        {
            RPGUI.DrawText(new Rect(textX, bodyY + 34f, textW, RPGUI.FieldTagH),
                $"Base DEF {item.defense:0}  HP +{item.maxHpBonus:0}", RPGUI.FieldTag);
            string gemDef = CountFilledSockets(item) > 0
                ? GemCatalog.FormatBonusSummary(gearGems)
                : "No gems socketed yet";
            if (gemDef.Length > 42) gemDef = gemDef.Substring(0, 40) + "..";
            GUI.color = new Color(0.55f, 1f, 0.7f);
            RPGUI.DrawText(new Rect(textX, bodyY + 54f, textW, RPGUI.FieldTagH), "Gems: " + gemDef, RPGUI.FieldTag);
            GUI.color = Color.white;
        }

        // Socket slots — large, labeled
        float sockTop = y + 100f;
        RPGUI.DrawText(new Rect(x + 12f, sockTop, width - 24f, RPGUI.FieldTagH),
            $"Sockets {CountFilledSockets(item)}/{item.socketCount}" +
            (item.socketCount == 0 ? "  (need Uncommon+ rarity)" : ""), RPGUI.FieldTag);

        float rowY = sockTop + 26f;
        for (int i = 0; i < GemCatalog.MaxSockets; i++)
        {
            float sy = rowY + i * 36f;
            Rect sr = new Rect(x + 12f, sy, width - 24f, 32f);
            bool usable = i < item.socketCount;

            if (!usable)
            {
                RPGUI.Fill(sr, new Color(0.07f, 0.07f, 0.09f, 0.6f));
                RPGUI.DrawText(new Rect(sr.x + 8f, sr.y, sr.width - 16f, sr.height),
                    $"Slot {i + 1}  — locked", RPGUI.FieldTag);
                continue;
            }

            var gem = item.sockets[i];
            if (gem != null)
            {
                RPGUI.Fill(sr, new Color(0.14f, 0.16f, 0.22f, 1f));
                RPGUI.Fill(new Rect(sr.x, sr.y, 6f, sr.height), GemCatalog.KindColor(gem.gemKind));
                RPGUI.DrawItemIcon(new Rect(sr.x + 10f, sr.y + 2f, 28f, 28f), gem);
                string line = $"{gem.displayName}  ·  {GemCatalog.StatsLine(gem)}";
                if (line.Length > 36) line = line.Substring(0, 34) + "..";
                RPGUI.DrawText(new Rect(sr.x + 44f, sr.y, sr.width - 130f, sr.height), line, RPGUI.FieldTag);
                if (RPGUI.ButtonSm(new Rect(sr.xMax - 78f, sr.y + 2f, 70f, 28f), "Remove"))
                    Unsocket(isWeapon, i);
            }
            else
            {
                // Empty — highlight if gem selected
                Color emptyBg = waitingGem
                    ? new Color(0.18f, 0.42f, 0.28f, 1f)
                    : new Color(0.16f, 0.18f, 0.24f, 1f);
                RPGUI.Fill(sr, emptyBg);
                string label = waitingGem
                    ? $"Slot {i + 1}  (+) Click to socket"
                    : $"Slot {i + 1}  empty — select a gem below";
                RPGUI.DrawText(new Rect(sr.x + 10f, sr.y, sr.width - 20f, sr.height), label, RPGUI.Hint);

                if (waitingGem && Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 && sr.Contains(Event.current.mousePosition))
                {
                    TrySocketInto(_selectedGemBagIndex, isWeapon, i);
                    Event.current.Use();
                }
            }
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
        bool selected = isGem && bagIndex == _selectedGemBagIndex;
        var equipped = item.slot == ItemSlot.Weapon ? equippedWeapon : equippedArmor;
        bool better = !isGem && IsBetterGear(item, equipped);
        bool worse = !isGem && equipped != null && !better && GearScore(item) < GearScore(equipped) - 0.5f;

        Rect r = new Rect(x, y, width, rowH);
        Color bg;
        if (selected) bg = new Color(0.16f, 0.32f, 0.22f, 0.98f);
        else if (isGem) bg = new Color(0.12f, 0.1f, 0.16f, 0.96f);
        else if (better) bg = new Color(0.1f, 0.18f, 0.14f, 0.96f);
        else bg = new Color(0.11f, 0.12f, 0.16f, 0.94f);

        RPGUI.DrawPanel(r, bg, false);
        RPGUI.Fill(new Rect(x, y, 4f, rowH), item.RarityColor);
        if (selected)
            RPGUI.Fill(new Rect(x, y, width, 3f), RPGUI.Good);

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
                ? $"ATK {item.attack:0} · {ItemFactory.ProfileLabel(item.weaponProfile)} · SPD +{item.attackSpeedBonus * 100f:0}% · RNG +{item.attackRangeBonus:0.0}"
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
            RPGUI.DrawChip(new Rect(x + width - 210f, y + 12f, 88f, 28f),
                selected ? "Selected" : "Gem",
                selected ? RPGUI.Good : GemCatalog.KindColor(item.gemKind));

            string btn = selected ? "Cancel" : "Select";
            if (bagIndex >= 0 &&
                RPGUI.ButtonSm(new Rect(x + width - 112f, y + 24f, 100f, RPGUI.ButtonSmH), btn, !selected))
            {
                _selectedGemBagIndex = selected ? -1 : bagIndex;
                if (!selected) _bagTab = 1;
            }

            // Also: quick socket into first empty if already selected and click again? handled by Cancel/Select
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
