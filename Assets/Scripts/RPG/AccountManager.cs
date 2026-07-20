using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Simple local account select + JSON save/load for RPG progress.
/// </summary>
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    public bool IsReady { get; private set; }
    public string ActiveAccountId { get; private set; }
    public string ActiveAccountName { get; private set; }

    const float AutoSaveInterval = 20f;

    enum UiMode { Select, Create }

    UiMode _mode = UiMode.Select;
    string _newName = "";
    string _status = "";
    Vector2 _listScroll;
    int _selectedIndex = -1;
    float _autoSaveTimer;
    List<AccountIndexEntry> _accounts = new List<AccountIndexEntry>();
    AccountSaveData _lastGoodSave;
    bool _hasLastGoodSave;

    static string SaveRoot => Path.Combine(Application.persistentDataPath, "rpg_accounts");
    static string IndexPath => Path.Combine(SaveRoot, "index.json");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureFolder();
        ReloadIndex();
    }

    void OnDestroy()
    {
        // Flush cached progress before teardown wipes runtime fields.
        if (Instance == this && IsReady)
            FlushSaveOnExit();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsReady) return;

        _autoSaveTimer += Time.unscaledDeltaTime;
        if (_autoSaveTimer >= AutoSaveInterval)
        {
            _autoSaveTimer = 0f;
            SaveActive(silent: true);
        }
    }

    void OnApplicationQuit()
    {
        FlushSaveOnExit();
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) FlushSaveOnExit();
    }

    void FlushSaveOnExit()
    {
        if (!IsReady || string.IsNullOrEmpty(ActiveAccountId)) return;

        // Prefer a live capture if player data is still intact; otherwise reuse last good snapshot.
        if (CanCaptureLiveProgress())
            SaveActive(silent: true);
        else if (_hasLastGoodSave && _lastGoodSave != null)
            WriteSaveFile(_lastGoodSave, updateIndex: true);
    }

    bool CanCaptureLiveProgress()
    {
        var prog = PlayerProgression.Instance;
        var inv = PlayerInventory.Instance;
        if (prog == null || inv == null) return false;
        if (prog.level < 1) return false;
        if (inv.equippedWeapon == null || string.IsNullOrEmpty(inv.equippedWeapon.displayName))
            return false;
        if (inv.equippedArmor == null || string.IsNullOrEmpty(inv.equippedArmor.displayName))
            return false;
        return true;
    }

    static void EnsureFolder()
    {
        if (!Directory.Exists(SaveRoot))
            Directory.CreateDirectory(SaveRoot);
    }

    static string AccountPath(string accountId) => Path.Combine(SaveRoot, accountId + ".json");

    void ReloadIndex()
    {
        _accounts.Clear();
        if (!File.Exists(IndexPath)) return;

        try
        {
            var file = JsonUtility.FromJson<AccountIndexFile>(File.ReadAllText(IndexPath));
            if (file?.accounts != null)
                _accounts.AddRange(file.accounts);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to read account index: " + e.Message);
        }
    }

    void WriteIndex()
    {
        EnsureFolder();
        var file = new AccountIndexFile { accounts = _accounts.ToArray() };
        File.WriteAllText(IndexPath, JsonUtility.ToJson(file, true));
    }

    public void SaveActive(bool silent = false)
    {
        if (!IsReady || string.IsNullOrEmpty(ActiveAccountId)) return;
        if (!CanCaptureLiveProgress())
        {
            if (!silent)
                Debug.LogWarning("Skip save: player progress not ready.");
            return;
        }

        var data = CaptureCurrentSave();
        data.accountId = ActiveAccountId;
        data.accountName = ActiveAccountName;
        data.lastPlayedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        string path = AccountPath(ActiveAccountId);
        if (File.Exists(path))
        {
            try
            {
                var old = JsonUtility.FromJson<AccountSaveData>(File.ReadAllText(path));
                if (old != null && !string.IsNullOrEmpty(old.createdAt))
                    data.createdAt = old.createdAt;
            }
            catch { /* ignore */ }
        }
        if (string.IsNullOrEmpty(data.createdAt))
            data.createdAt = data.lastPlayedAt;

        // If capture somehow lost gear, keep previous gear from last good / disk.
        MergeMissingGear(data);

        WriteSaveFile(data, updateIndex: true);

        if (!silent)
            _status = "已儲存進度";
        if (!silent && PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot("進度已儲存");
    }

    void MergeMissingGear(AccountSaveData data)
    {
        AccountSaveData fallback = _hasLastGoodSave ? _lastGoodSave : null;
        if (fallback == null)
        {
            string path = AccountPath(ActiveAccountId);
            if (File.Exists(path))
            {
                try { fallback = JsonUtility.FromJson<AccountSaveData>(File.ReadAllText(path)); }
                catch { /* ignore */ }
            }
        }
        if (fallback == null) return;

        if (data.weapon == null || !data.weapon.IsValid)
            data.weapon = fallback.weapon;
        if (data.armor == null || !data.armor.IsValid)
            data.armor = fallback.armor;
    }

    void WriteSaveFile(AccountSaveData data, bool updateIndex)
    {
        if (data == null || string.IsNullOrEmpty(data.accountId)) return;

        EnsureFolder();
        File.WriteAllText(AccountPath(data.accountId), JsonUtility.ToJson(data, true));

        _lastGoodSave = CloneSave(data);
        _hasLastGoodSave = true;

        if (!updateIndex) return;

        for (int i = 0; i < _accounts.Count; i++)
        {
            if (_accounts[i].accountId != data.accountId) continue;
            _accounts[i].accountName = data.accountName;
            _accounts[i].lastPlayedAt = data.lastPlayedAt;
            _accounts[i].level = data.level;
            _accounts[i].currentWorld = data.currentWorld;
            break;
        }
        WriteIndex();
    }

    static AccountSaveData CloneSave(AccountSaveData src)
    {
        // Json round-trip is enough for a deep copy of our plain save DTO.
        return JsonUtility.FromJson<AccountSaveData>(JsonUtility.ToJson(src));
    }

    AccountSaveData CaptureCurrentSave()
    {
        var data = new AccountSaveData();

        var prog = PlayerProgression.Instance;
        if (prog != null)
        {
            data.level = prog.level;
            data.xp = prog.xp;
        }

        var adv = AdventureManager.Instance;
        if (adv != null)
            adv.WriteToSave(data);

        var inv = PlayerInventory.Instance;
        if (inv != null)
            inv.WriteToSave(data);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var stats = player.GetComponent<CharacterStats>();
            if (stats != null)
                data.currentHealth = stats.currentHealth;
        }

        return data;
    }

    void ApplySave(AccountSaveData data)
    {
        var prog = PlayerProgression.Instance;
        if (prog != null)
            prog.ApplySave(data.level, data.xp);

        var inv = PlayerInventory.Instance;
        if (inv != null)
            inv.ApplySave(data);

        var adv = AdventureManager.Instance;
        if (adv != null)
            adv.ApplySave(data);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var stats = player.GetComponent<CharacterStats>();
            if (stats != null && data.currentHealth > 0f)
            {
                // Stats may be recalculated by inventory apply; clamp after.
                stats.currentHealth = Mathf.Min(data.currentHealth, stats.maxHealth);
            }
        }
    }

    void CreateNewAccount(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            _status = "請輸入帳號名稱";
            return;
        }
        if (name.Length > 16)
        {
            _status = "名稱最多 16 字";
            return;
        }
        foreach (var a in _accounts)
        {
            if (string.Equals(a.accountName, name, StringComparison.OrdinalIgnoreCase))
            {
                _status = "這個名稱已經存在";
                return;
            }
        }

        string id = Guid.NewGuid().ToString("N").Substring(0, 12);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // Fresh starter state
        ResetToNewGame();

        var data = CaptureCurrentSave();
        data.accountId = id;
        data.accountName = name;
        data.createdAt = now;
        data.lastPlayedAt = now;

        ActiveAccountId = id;
        ActiveAccountName = name;
        IsReady = true;

        WriteSaveFile(data, updateIndex: false);
        _accounts.Add(new AccountIndexEntry
        {
            accountId = id,
            accountName = name,
            lastPlayedAt = now,
            level = data.level,
            currentWorld = data.currentWorld
        });
        WriteIndex();

        _status = $"已建立帳號：{name}";
        Time.timeScale = 1f;

        if (AdventureManager.Instance != null)
            AdventureManager.Instance.BeginAfterAccountReady();
    }

    void PlaySelectedAccount()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _accounts.Count)
        {
            _status = "請先選擇一個帳號";
            return;
        }

        var entry = _accounts[_selectedIndex];
        string path = AccountPath(entry.accountId);
        if (!File.Exists(path))
        {
            _status = "存檔遺失，無法讀取";
            return;
        }

        AccountSaveData data;
        try
        {
            data = JsonUtility.FromJson<AccountSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            _status = "讀取失敗：" + e.Message;
            return;
        }

        if (data == null)
        {
            _status = "存檔損毀";
            return;
        }

        ApplySave(data);
        ActiveAccountId = entry.accountId;
        ActiveAccountName = entry.accountName;
        IsReady = true;
        _lastGoodSave = CloneSave(data);
        _hasLastGoodSave = true;
        _status = $"歡迎回來，{ActiveAccountName}";
        Time.timeScale = 1f;

        entry.lastPlayedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        entry.level = data.level;
        entry.currentWorld = data.currentWorld;
        WriteIndex();

        if (AdventureManager.Instance != null)
            AdventureManager.Instance.BeginAfterAccountReady();
    }

    void DeleteSelectedAccount()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _accounts.Count)
        {
            _status = "請先選擇要刪除的帳號";
            return;
        }

        var entry = _accounts[_selectedIndex];
        string path = AccountPath(entry.accountId);
        if (File.Exists(path))
            File.Delete(path);

        _accounts.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        WriteIndex();
        _status = $"已刪除帳號：{entry.accountName}";
    }

    void ResetToNewGame()
    {
        var prog = PlayerProgression.Instance;
        if (prog != null)
            prog.ApplySave(1, 0);

        var inv = PlayerInventory.Instance;
        if (inv != null)
            inv.ResetToStarter();

        var adv = AdventureManager.Instance;
        if (adv != null)
        {
            var blank = new AccountSaveData();
            adv.ApplySave(blank);
        }
    }

    void OnGUI()
    {
        if (IsReady)
        {
            DrawInGameHud();
            return;
        }

        Time.timeScale = 0f;
        RPGUI.Ensure();

        float w = Mathf.Min(Screen.width - 40f, 760f);
        float h = Mathf.Min(Screen.height - 40f, 680f);
        Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
        RPGUI.DrawPanel(panel, new Color(0.04f, 0.05f, 0.08f, 0.97f));

        float y = panel.y + 18f;
        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.TitleH), "選擇帳號", RPGUI.Title);
        y += RPGUI.TitleH + 4f;
        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.HintH),
            "建立新帳號，或選擇已有帳號繼續遊玩", RPGUI.Hint);
        y += RPGUI.HintH + 12f;

        if (GUI.Button(new Rect(panel.x + 28f, y, 220f, RPGUI.ButtonH), "選擇帳號", RPGUI.Button))
            _mode = UiMode.Select;
        if (GUI.Button(new Rect(panel.x + 264f, y, 220f, RPGUI.ButtonH), "建立帳號", RPGUI.Button))
            _mode = UiMode.Create;

        if (_mode == UiMode.Select)
            DrawSelectMode(panel, w, h);
        else
            DrawCreateMode(panel, w, h);

        if (!string.IsNullOrEmpty(_status))
            GUI.Label(new Rect(panel.x + 28f, panel.yMax - 56f, w - 56f, RPGUI.HintH), _status, RPGUI.Hint);
    }

    void DrawSelectMode(Rect panel, float w, float h)
    {
        float listTop = panel.y + 18f + RPGUI.TitleH + 4f + RPGUI.HintH + 12f + RPGUI.ButtonH + 16f;
        float listH = h - (listTop - panel.y) - 140f;
        Rect view = new Rect(panel.x + 28f, listTop, w - 56f, Mathf.Max(140f, listH));
        float rowH = 88f;
        float contentH = Mathf.Max(view.height, Mathf.Max(1, _accounts.Count) * rowH + 8f);
        _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0, 0, view.width - 20f, contentH));

        if (_accounts.Count == 0)
        {
            GUI.Label(new Rect(8f, 16f, view.width - 40f, RPGUI.LabelH), "尚無帳號，請先建立", RPGUI.Label);
        }
        else
        {
            for (int i = 0; i < _accounts.Count; i++)
            {
                var a = _accounts[i];
                Rect row = new Rect(0f, i * rowH, view.width - 24f, rowH - 8f);
                Color bg = i == _selectedIndex
                    ? new Color(0.25f, 0.32f, 0.48f, 0.98f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.95f);
                RPGUI.DrawPanel(row, bg);

                GUI.Label(new Rect(row.x + 14f, row.y + 8f, row.width - 28f, RPGUI.ItemNameH),
                    a.accountName, RPGUI.ItemName);
                GUI.Label(new Rect(row.x + 14f, row.y + 48f, row.width - 28f, RPGUI.FieldTagH),
                    $"Lv {a.level}　世界 {a.currentWorld}　上次 {a.lastPlayedAt}", RPGUI.FieldTag);

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(panel.x + 28f, panel.yMax - 118f, 260f, RPGUI.ButtonH), "開始遊戲", RPGUI.Button))
            PlaySelectedAccount();
        if (GUI.Button(new Rect(panel.x + 308f, panel.yMax - 118f, 200f, RPGUI.ButtonH), "刪除帳號", RPGUI.Button))
            DeleteSelectedAccount();
    }

    void DrawCreateMode(Rect panel, float w, float h)
    {
        float y = panel.y + 18f + RPGUI.TitleH + 4f + RPGUI.HintH + 12f + RPGUI.ButtonH + 24f;
        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.LabelH), "帳號名稱", RPGUI.Label);
        y += RPGUI.LabelH + 8f;

        var tf = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 26,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 12, 10, 10),
            clipping = TextClipping.Overflow
        };
        _newName = GUI.TextField(new Rect(panel.x + 28f, y, w - 56f, 56f), _newName, 16, tf);
        y += 68f;

        GUI.Label(new Rect(panel.x + 28f, y, w - 56f, RPGUI.HintH),
            "新帳號從世界 1、等級 1 開始", RPGUI.Hint);

        if (GUI.Button(new Rect(panel.x + 28f, panel.yMax - 118f, 300f, RPGUI.ButtonH), "建立並開始", RPGUI.Button))
            CreateNewAccount(_newName);
    }

    void DrawInGameHud()
    {
        RPGUI.Ensure();
        Rect box = new Rect(20f, Screen.height - 78f, 460f, 58f);
        RPGUI.DrawPanel(box, new Color(0.05f, 0.06f, 0.09f, 0.85f));
        GUI.Label(new Rect(box.x + 14f, box.y + 8f, 270f, RPGUI.HintH),
            $"帳號：{ActiveAccountName}", RPGUI.Hint);

        if (GUI.Button(new Rect(box.x + 300f, box.y + 8f, 140f, 42f), "儲存", RPGUI.Button))
            SaveActive(silent: false);
    }
}
