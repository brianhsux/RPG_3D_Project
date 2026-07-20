using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    public const int MaxBag = 24;

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
            ShowLoot("背包已滿！");
            return false;
        }
        bag.Add(item);
        ShowLoot($"獲得 {item.displayName} Lv{item.itemLevel}");
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
        bag.RemoveAt(index);

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
        ShowLoot($"裝備了 {item.displayName}");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
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
        }

        if (crafted > 0)
        {
            ShowLoot($"一鍵合成完成：共 {crafted} 次");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
        else
            ShowLoot("沒有可合成的裝備（需同類型 ×3）");

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

    public void ApplyEquipmentStats(bool refillHp)
    {
        if (_stats == null) _stats = GetComponent<CharacterStats>();

        int lv = PlayerProgression.Instance != null ? PlayerProgression.Instance.level : 1;

        float baseHp = 100f + ProgressionFormulas.PlayerLevelBonusHp(lv);
        float baseAtk = 12f + ProgressionFormulas.PlayerLevelBonusAtk(lv);
        float baseDef = ProgressionFormulas.PlayerLevelBonusDef(lv);

        float weaponAtk = equippedWeapon != null ? equippedWeapon.attack : 0f;
        float armorDef = equippedArmor != null ? equippedArmor.defense : 0f;
        float armorHp = equippedArmor != null ? equippedArmor.maxHpBonus : 0f;

        _stats.baseAttack = baseAtk + weaponAtk;
        _stats.defense = baseDef + armorDef;
        _stats.maxHealth = baseHp + armorHp;
        _stats.attackDamage = _stats.baseAttack;

        if (refillHp || _stats.currentHealth > _stats.maxHealth)
            _stats.currentHealth = _stats.maxHealth;
        else
            _stats.currentHealth = Mathf.Min(_stats.currentHealth, _stats.maxHealth);
    }

    public bool showPanel;
    Vector2 _bagScroll;

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;

        var kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame)
            showPanel = !showPanel;
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;

        RPGUI.Ensure();

        if (HasLootMessage)
        {
            Rect tip = new Rect(Screen.width * 0.5f - 400f, Screen.height - 140f, 800f, RPGUI.BannerH + 16f);
            RPGUI.DrawPanel(tip, new Color(0.1f, 0.15f, 0.08f, 0.9f));
            GUI.Label(tip, lastLootMessage, RPGUI.Banner);
        }

        if (!showPanel) return;

        float w = MathMin(Screen.width - 40f, 1040f);
        float h = Mathf.Min(Screen.height - 40f, 820f);
        Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, 24f, w, h);
        RPGUI.DrawPanel(panel, new Color(0.05f, 0.06f, 0.09f, 0.96f));

        float y = panel.y + 14f;
        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.TitleH),
            "背包 / 裝備 / 合成　（按 I 關閉）", RPGUI.Title);
        y += RPGUI.TitleH + 6f;

        GUI.Label(new Rect(panel.x + 28f, y, 400f, RPGUI.LabelH), "目前裝備", RPGUI.Label);
        y += RPGUI.LabelH + 4f;

        float cardW = (w - 72f) * 0.5f;
        float cardH = 160f;
        DrawEquippedCard(panel.x + 28f, y, cardW, cardH, "武器", equippedWeapon);
        DrawEquippedCard(panel.x + 44f + cardW, y, cardW, cardH, "防具", equippedArmor);
        y += cardH + 12f;

        var prog = PlayerProgression.Instance;
        if (prog != null)
        {
            GUI.Label(new Rect(panel.x + 28f, y, 300f, RPGUI.LabelH),
                $"角色等級  Lv {prog.level}", RPGUI.Label);
            RPGUI.DrawBar(
                new Rect(panel.x + 320f, y + 4f, w - 360f, 40f),
                prog.XpPercent,
                new Color(0.35f, 0.55f, 1f),
                $"EXP {prog.xp} / {prog.xpToNext}"
            );
            y += RPGUI.LabelH + 8f;
        }

        int craftable = CountCraftableBatches();
        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.HintH),
            $"背包 {bag.Count}/{MaxBag}　｜　左鍵裝備　｜　可合成約 {craftable} 次　｜　可捲動", RPGUI.Hint);
        y += RPGUI.HintH + 8f;

        float listTop = y;
        float listBottom = panel.yMax - 88f;
        float viewH = Mathf.Max(80f, listBottom - listTop);
        float rowH = 104f;
        float contentW = w - 76f;
        float contentH = Mathf.Max(viewH, bag.Count * rowH + 8f);

        Rect viewRect = new Rect(panel.x + 28f, listTop, w - 56f, viewH);
        Rect contentRect = new Rect(0f, 0f, contentW, contentH);
        _bagScroll = GUI.BeginScrollView(viewRect, _bagScroll, contentRect, false, true);

        for (int i = 0; i < bag.Count; i++)
            DrawItemLine(0f, i * rowH, contentW - 4f, bag[i], i, false);

        if (bag.Count == 0)
            GUI.Label(new Rect(8f, 12f, contentW - 16f, RPGUI.HintH), "背包是空的", RPGUI.Hint);

        GUI.EndScrollView();

        string craftLabel = craftable > 0
            ? $"一鍵合成（約 {craftable} 次）"
            : "一鍵合成（無可合成）";
        if (GUI.Button(new Rect(panel.x + 28f, panel.yMax - 74f, 440f, RPGUI.ButtonH), craftLabel, RPGUI.Button))
            AutoSynthesizeAll();
    }

    int CountCraftableBatches()
    {
        int weapons = 0;
        int armors = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i] == null) continue;
            if (bag[i].slot == ItemSlot.Weapon) weapons++;
            else armors++;
        }

        // Approximate recursive craft count: every 3 items → 1, leftover stays.
        return CountRecursiveCrafts(weapons) + CountRecursiveCrafts(armors);
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

    void DrawEquippedCard(float x, float y, float width, float height, string slotTitle, ItemInstance item)
    {
        Rect r = new Rect(x, y, width, height);
        RPGUI.DrawPanel(r, new Color(0.14f, 0.16f, 0.22f, 0.98f));

        Color prev = GUI.color;
        GUI.color = new Color(0.22f, 0.28f, 0.4f, 1f);
        GUI.DrawTexture(new Rect(x, y, width, RPGUI.LabelH), Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.Label(new Rect(x + 14f, y, width - 28f, RPGUI.LabelH), slotTitle, RPGUI.Label);

        if (item == null)
        {
            GUI.Label(new Rect(x + 14f, y + RPGUI.LabelH + 16f, width - 28f, RPGUI.HintH),
                "（尚未裝備）", RPGUI.Hint);
            return;
        }

        float bodyY = y + RPGUI.LabelH + 10f;
        Rect iconRect = new Rect(x + 14f, bodyY, 78f, 78f);
        RPGUI.DrawItemIcon(iconRect, item);

        float textX = x + 106f;
        float textW = width - 120f;

        GUI.Label(new Rect(textX, bodyY, 70f, RPGUI.FieldTagH), "名稱", RPGUI.FieldTag);
        GUI.color = item.RarityColor;
        GUI.Label(new Rect(textX + 70f, bodyY, textW - 70f, RPGUI.ItemNameH), item.displayName, RPGUI.ItemName);
        GUI.color = Color.white;

        float row2 = bodyY + RPGUI.ItemNameH + 2f;
        GUI.Label(new Rect(textX, row2, 70f, RPGUI.FieldTagH), "等級", RPGUI.FieldTag);
        string styleTag = item.slot == ItemSlot.Weapon
            ? WeaponVisualUtil.StyleLabel(WeaponVisualUtil.StyleIndex(item.itemLevel))
            : ArmorVisualUtil.StyleLabel(ArmorVisualUtil.StyleIndex(item.itemLevel));
        string stats = item.slot == ItemSlot.Weapon
            ? $"Lv {item.itemLevel}　{styleTag}　攻擊 {item.attack:0}"
            : $"Lv {item.itemLevel}　{styleTag}　防禦 {item.defense:0}　生命 +{item.maxHpBonus:0}";
        GUI.Label(new Rect(textX + 70f, row2, textW - 70f, RPGUI.ItemMetaH), stats, RPGUI.ItemMeta);
    }

    void DrawItemLine(float x, float y, float width, ItemInstance item, int bagIndex, bool equipped)
    {
        float rowH = 98f;
        if (item == null)
        {
            RPGUI.DrawPanel(new Rect(x, y, width, rowH), new Color(0.1f, 0.1f, 0.12f, 0.85f));
            GUI.Label(new Rect(x + 16f, y, width - 32f, rowH), "（空欄位）", RPGUI.Hint);
            return;
        }

        Rect r = new Rect(x, y, width, rowH);
        RPGUI.DrawPanel(r, new Color(0.12f, 0.12f, 0.16f, 0.94f));

        Rect iconRect = new Rect(x + 10f, y + 14f, 70f, 70f);
        RPGUI.DrawItemIcon(iconRect, item);

        string styleTag = item.slot == ItemSlot.Weapon
            ? WeaponVisualUtil.StyleLabel(WeaponVisualUtil.StyleIndex(item.itemLevel))
            : ArmorVisualUtil.StyleLabel(ArmorVisualUtil.StyleIndex(item.itemLevel));
        string stats = item.slot == ItemSlot.Weapon
            ? $"攻擊 {item.attack:0}"
            : $"防禦 {item.defense:0}　生命 +{item.maxHpBonus:0}";
        string equippedTag = equipped ? "　[已裝備]" : "";

        float textX = x + 94f;
        float textW = width - 108f;

        GUI.Label(new Rect(textX, y + 8f, 70f, RPGUI.FieldTagH), "名稱", RPGUI.FieldTag);
        GUI.color = item.RarityColor;
        GUI.Label(new Rect(textX + 70f, y + 6f, textW - 70f, RPGUI.ItemNameH),
            $"{item.displayName}{equippedTag}", RPGUI.ItemName);
        GUI.color = Color.white;

        GUI.Label(new Rect(textX, y + 54f, 70f, RPGUI.FieldTagH), "等級", RPGUI.FieldTag);
        GUI.Label(new Rect(textX + 70f, y + 52f, textW - 70f, RPGUI.ItemMetaH),
            $"Lv {item.itemLevel}　｜　{styleTag}　｜　{stats}", RPGUI.ItemMeta);

        if (bagIndex >= 0 && Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 && r.Contains(Event.current.mousePosition))
        {
            EquipFromBag(bagIndex);
            Event.current.Use();
        }
    }
}
