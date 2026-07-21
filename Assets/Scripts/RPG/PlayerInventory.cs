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
            _panelTab = TabEquip;
            ShowLoot($"Selected {item.displayName} — socket it on the Equip tab");
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
    Vector2 _statusScroll;
    int _selectedGemBagIndex = -1;

    const int TabStatus = 0;
    const int TabInfo = 1;
    const int TabEquip = 2;
    const int TabGear = 3;
    const int TabGems = 4;
    const int TabPets = 5;
    int _panelTab;

    static readonly string[] PanelTabLabels = { "Status", "Info", "Equip", "Bag", "Gems", "Pets" };

    public void SetPanelOpen(bool open)
    {
        if (showPanel == open) return;
        showPanel = open;
        // Do not pause — combat and movement continue while browsing.
        if (!open)
        {
            _selectedGemBagIndex = -1;
            _panelTab = TabStatus;
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

        if (showPanel)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) _panelTab = TabStatus;
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) _panelTab = TabInfo;
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) _panelTab = TabEquip;
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) _panelTab = TabGear;
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) _panelTab = TabGems;
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) _panelTab = TabPets;
        }
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

        float w = MathMin(Screen.width - 32f, 1040f);
        float h = Mathf.Min(Screen.height - 24f, 820f);
        Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);

        RPGUI.DrawPanel(panel, new Color(0.045f, 0.055f, 0.08f, 0.98f));

        float pad = 22f;
        float y = panel.y + 14f;

        RPGUI.DrawText(new Rect(panel.x + pad, y, w - 180f, RPGUI.TitleH), "Character", RPGUI.Title);
        if (RPGUI.ButtonSm(new Rect(panel.xMax - pad - 100f, y + 6f, 100f, RPGUI.ButtonSmH), "Close"))
            SetPanelOpen(false);
        y += RPGUI.TitleH + 4f;

        _panelTab = RPGUI.DrawTabs(new Rect(panel.x + pad, y, w - pad * 2f, RPGUI.ButtonSmH), PanelTabLabels, _panelTab);
        y += RPGUI.ButtonSmH + 10f;

        // Socket mode banner (Equip / Gems)
        if (_selectedGemBagIndex >= 0 && _selectedGemBagIndex < bag.Count && bag[_selectedGemBagIndex] != null
            && bag[_selectedGemBagIndex].slot == ItemSlot.Gem)
        {
            var sel = bag[_selectedGemBagIndex];
            Rect ban = new Rect(panel.x + pad, y, w - pad * 2f, 36f);
            RPGUI.DrawPanel(ban, new Color(0.15f, 0.22f, 0.18f, 0.98f), false);
            RPGUI.Fill(new Rect(ban.x, ban.y, 6f, ban.height), GemCatalog.KindColor(sel.gemKind));
            string banMsg = _panelTab == TabEquip
                ? $"Selected: {sel.displayName} — click a glowing (+) socket"
                : $"Selected: {sel.displayName} — open Equip tab to socket";
            RPGUI.DrawText(new Rect(ban.x + 14f, ban.y, ban.width - 220f, ban.height), banMsg, RPGUI.Hint);
            if (RPGUI.ButtonSm(new Rect(ban.xMax - 200f, ban.y + 4f, 90f, 28f), "Equip"))
                _panelTab = TabEquip;
            if (RPGUI.ButtonSm(new Rect(ban.xMax - 100f, ban.y + 4f, 90f, 28f), "Cancel"))
                _selectedGemBagIndex = -1;
            y += 42f;
        }

        bool showFooter = _panelTab == TabGear || _panelTab == TabGems;
        float listBottom = showFooter ? panel.yMax - 78f : panel.yMax - 24f;
        float bodyH = Mathf.Max(80f, listBottom - y);
        Rect body = new Rect(panel.x + pad, y, w - pad * 2f, bodyH);

        switch (_panelTab)
        {
            case TabStatus:
                DrawStatusTab(body);
                break;
            case TabInfo:
                DrawInfoTab(body);
                break;
            case TabEquip:
                DrawEquipTab(body);
                break;
            case TabGear:
                DrawBagListTab(body, gems: false);
                break;
            case TabGems:
                DrawBagListTab(body, gems: true);
                break;
            case TabPets:
                DrawPetsTab(body.x, body.y, body.width, body.height, body.width - 20f, 96f);
                break;
        }

        if (showFooter)
        {
            float btnY = panel.yMax - 66f;
            float btnGap = 12f;
            float btnW = (w - pad * 2f - btnGap) * 0.5f;
            if (RPGUI.Button(new Rect(panel.x + pad, btnY, btnW, RPGUI.ButtonH), "Equip Best", primary: true))
                EquipBest();

            int craftable = CountCraftableBatches();
            string craftLabel = craftable > 0 ? $"Auto Craft ({craftable})" : "Auto Craft";
            if (RPGUI.Button(new Rect(panel.x + pad + btnW + btnGap, btnY, btnW, RPGUI.ButtonH), craftLabel))
                AutoSynthesizeAll();
        }

        GUI.depth = prevDepth;
    }

    void DrawStatusTab(Rect body)
    {
        if (_stats == null) _stats = GetComponent<CharacterStats>();
        var prog = PlayerProgression.Instance;

        float contentH = 420f;
        _statusScroll = GUI.BeginScrollView(body, _statusScroll, new Rect(0f, 0f, body.width - 20f, contentH), false, true);
        float x = 4f;
        float y = 4f;
        float cw = body.width - 28f;

        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.LabelH), "Combat Stats", RPGUI.Label);
        y += RPGUI.LabelH + 4f;

        float hpPct = _stats != null ? _stats.HealthPercent : 0f;
        RPGUI.DrawBar(new Rect(x, y, cw, 28f), hpPct,
            Color.Lerp(new Color(0.85f, 0.18f, 0.18f), new Color(0.22f, 0.82f, 0.35f), hpPct),
            _stats != null
                ? $"HP  {Mathf.CeilToInt(_stats.currentHealth)} / {Mathf.CeilToInt(_stats.maxHealth)}"
                : "HP");
        y += 36f;

        if (prog != null)
        {
            RPGUI.DrawBar(new Rect(x, y, cw, 24f), prog.XpPercent, new Color(0.3f, 0.55f, 1f),
                $"EXP  {prog.xp} / {prog.xpToNext}");
            y += 34f;
        }

        DrawStatRow(x, y, cw, "Attack", _stats != null ? $"{_stats.attackDamage:0}" : "-");
        y += 34f;
        DrawStatRow(x, y, cw, "Defense", _stats != null ? $"{_stats.defense:0}" : "-");
        y += 34f;
        DrawStatRow(x, y, cw, "Attack Speed", _stats != null ? $"{_stats.attackSpeed:0.00}x" : "-");
        y += 34f;
        DrawStatRow(x, y, cw, "Attack Range", _stats != null ? $"{_stats.attackRange:0.0} m" : "-");
        y += 42f;

        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.LabelH), "Gem Bonuses", RPGUI.Label);
        y += RPGUI.LabelH + 4f;

        if (_stats != null)
        {
            float elem = _stats.fireBonus + _stats.iceBonus + _stats.poisonBonus;
            if (_stats.critChance > 0.005f)
            { DrawStatRow(x, y, cw, "Crit", $"{_stats.critChance * 100f:0}%"); y += 34f; }
            if (_stats.blockChance > 0.005f)
            { DrawStatRow(x, y, cw, "Block", $"{_stats.blockChance * 100f:0}%"); y += 34f; }
            if (_stats.damageReduction > 0.005f)
            { DrawStatRow(x, y, cw, "Damage Reduction", $"{_stats.damageReduction * 100f:0}%"); y += 34f; }
            if (_stats.lifesteal > 0.005f)
            { DrawStatRow(x, y, cw, "Lifesteal", $"{_stats.lifesteal * 100f:0}%"); y += 34f; }
            if (_stats.reflectPercent > 0.005f)
            { DrawStatRow(x, y, cw, "Reflect", $"{_stats.reflectPercent * 100f:0}%"); y += 34f; }
            if (elem > 0.5f)
            { DrawStatRow(x, y, cw, "Elemental", $"+{elem:0}"); y += 34f; }

            var gems = GemCatalog.SumSockets(equippedWeapon, equippedArmor);
            string bonus = GemCatalog.FormatBonusSummary(gems);
            if (string.IsNullOrEmpty(bonus) || bonus == "No gem bonuses")
                RPGUI.DrawText(new Rect(x, y, cw, RPGUI.HintH), "No gems socketed yet — open Gems + Equip", RPGUI.Hint);
            else
            {
                RPGUI.DrawPanel(new Rect(x, y, cw, 52f), new Color(0.1f, 0.14f, 0.2f, 0.98f), false);
                RPGUI.DrawText(new Rect(x + 12f, y + 8f, cw - 24f, 36f), bonus, RPGUI.Hint);
            }
        }

        GUI.EndScrollView();
    }

    void DrawInfoTab(Rect body)
    {
        var prog = PlayerProgression.Instance;
        var adv = AdventureManager.Instance;
        float x = body.x;
        float y = body.y;
        float cw = body.width;

        string account = AccountManager.Instance != null ? AccountManager.Instance.ActiveAccountName : "";
        if (string.IsNullOrEmpty(account)) account = "Hero";

        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.LabelH), "Hero Info", RPGUI.Label);
        y += RPGUI.LabelH + 8f;

        DrawStatRow(x, y, cw, "Account", account);
        y += 36f;
        DrawStatRow(x, y, cw, "Level", prog != null ? prog.level.ToString() : "-");
        y += 36f;
        DrawStatRow(x, y, cw, "Experience", prog != null ? $"{prog.xp} / {prog.xpToNext}" : "-");
        y += 44f;

        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.LabelH), "Journey", RPGUI.Label);
        y += RPGUI.LabelH + 8f;

        if (adv == null)
        {
            RPGUI.DrawText(new Rect(x, y, cw, RPGUI.HintH), "Adventure data unavailable", RPGUI.Hint);
            return;
        }

        if (adv.IsInVillage)
        {
            DrawStatRow(x, y, cw, "Location", "Home Village");
            y += 36f;
        }
        else
        {
            DrawStatRow(x, y, cw, "Location", ProgressionFormulas.MapName(adv.currentWorld, adv.currentMap));
            y += 36f;
            DrawStatRow(x, y, cw, "World / Zone", $"{adv.currentWorld}/{ProgressionFormulas.WorldCount} · Zone {adv.currentMap}/4");
            y += 36f;
            DrawStatRow(x, y, cw, "Zone Power", $"Lv {adv.ContentLevel}");
            y += 36f;
            DrawStatRow(x, y, cw, "Map Boss", adv.mapBossDefeated ? "Defeated" : "Not yet");
            y += 36f;
        }

        DrawStatRow(x, y, cw, "Worlds Unlocked", $"1–{adv.highestWorldUnlocked} / {ProgressionFormulas.WorldCount}");
        y += 36f;
        DrawStatRow(x, y, cw, "Enemies Defeated", adv.killCount.ToString());
        y += 44f;

        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.HintH),
            "Hub green gate → Village · Boss purple portal → next world", RPGUI.Hint);
        y += RPGUI.HintH + 6f;
        RPGUI.DrawText(new Rect(x, y, cw, RPGUI.HintH),
            "Keys 1–6 switch tabs while this panel is open", RPGUI.Hint);
    }

    void DrawEquipTab(Rect body)
    {
        float gap = 12f;
        float cardW = (body.width - gap) * 0.5f;
        float cardH = body.height;
        DrawEquippedCard(body.x, body.y, cardW, cardH, "Weapon", equippedWeapon, true);
        DrawEquippedCard(body.x + cardW + gap, body.y, cardW, cardH, "Armor", equippedArmor, false);
    }

    void DrawBagListTab(Rect body, bool gems)
    {
        int gearCount = 0;
        int gemCount = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if (bag[i].slot == ItemSlot.Gem) gemCount++;
            else gearCount++;
        }

        float y = body.y;
        if (gems)
            RPGUI.DrawText(new Rect(body.x, y, body.width, RPGUI.HintH),
                $"Gems {gemCount}/{MaxGems}  ·  Select a gem, then socket on Equip", RPGUI.Hint);
        else
            RPGUI.DrawText(new Rect(body.x, y, body.width, RPGUI.HintH),
                $"Gear {gearCount}/{MaxGear}  ·  {CountUpgradesInBag()} upgrade(s) ready", RPGUI.Hint);
        y += RPGUI.HintH + 6f;

        float listTop = y;
        float viewH = Mathf.Max(60f, body.yMax - listTop);
        float rowH = 96f;
        float contentW = body.width - 20f;

        var visible = new List<int>(bag.Count);
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if ((bag[i].slot == ItemSlot.Gem) == gems)
                visible.Add(i);
        }

        float contentH = Mathf.Max(viewH, visible.Count * rowH + 8f);
        Rect viewRect = new Rect(body.x, listTop, body.width, viewH);
        if (gems)
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
            string empty = gems
                ? "No gems — mine crystal ores on Map 3 (press E)"
                : "No gear — defeat monsters for weapons and armor";
            RPGUI.DrawText(new Rect(8f, 16f, contentW - 16f, RPGUI.HintH), empty, RPGUI.Hint);
        }

        GUI.EndScrollView();
    }

    static void DrawStatRow(float x, float y, float width, string label, string value)
    {
        RPGUI.DrawPanel(new Rect(x, y, width, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f), false);
        RPGUI.DrawText(new Rect(x + 12f, y, width * 0.45f, 30f), label, RPGUI.FieldTag);
        RPGUI.DrawText(new Rect(x + width * 0.45f, y, width * 0.55f - 12f, 30f), value, RPGUI.Hint);
    }

    void DrawPetsTab(float x, float listTop, float viewW, float viewH, float contentW, float rowH)
    {
        var pets = PlayerPetController.Instance;
        int count = pets != null ? pets.owned.Count : 0;

        // Hint row eats a bit of vertical space.
        RPGUI.DrawText(new Rect(x, listTop, viewW, RPGUI.HintH),
            $"Pets {count}/{PlayerPetController.MaxPets}  ·  Deploy one combat companion", RPGUI.Hint);
        float listY = listTop + RPGUI.HintH + 6f;
        float listH = Mathf.Max(60f, viewH - (RPGUI.HintH + 6f));

        float contentH = Mathf.Max(listH, count * rowH + 8f);
        Rect viewRect = new Rect(x, listY, viewW, listH);
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
        var gemCounts = new int[GemCatalog.KindCount];
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if (bag[i].slot == ItemSlot.Weapon) weapons++;
            else if (bag[i].slot == ItemSlot.Armor) armors++;
            else if (bag[i].slot == ItemSlot.Gem)
            {
                gems++;
                int k = Mathf.Clamp((int)bag[i].gemKind, 0, GemCatalog.KindCount - 1);
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
                if (!selected) _panelTab = TabEquip;
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
