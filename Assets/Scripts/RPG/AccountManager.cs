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
            _status = "Progress saved";
        if (!silent && PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot("Progress saved");
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
            _status = "Please enter an account name";
            return;
        }
        if (name.Length > 16)
        {
            _status = "Name must be 16 characters or fewer";
            return;
        }
        foreach (var a in _accounts)
        {
            if (string.Equals(a.accountName, name, StringComparison.OrdinalIgnoreCase))
            {
                _status = "That name already exists";
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

        _status = $"Account created: {name}";
        Time.timeScale = 1f;

        if (AdventureManager.Instance != null)
            AdventureManager.Instance.BeginAfterAccountReady();
    }

    void PlaySelectedAccount()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _accounts.Count)
        {
            _status = "Please select an account first";
            return;
        }

        var entry = _accounts[_selectedIndex];
        string path = AccountPath(entry.accountId);
        if (!File.Exists(path))
        {
            _status = "Save file missing — cannot load";
            return;
        }

        AccountSaveData data;
        try
        {
            data = JsonUtility.FromJson<AccountSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            _status = "Load failed: " + e.Message;
            return;
        }

        if (data == null)
        {
            _status = "Save file corrupted";
            return;
        }

        ApplySave(data);
        ActiveAccountId = entry.accountId;
        ActiveAccountName = entry.accountName;
        IsReady = true;
        _lastGoodSave = CloneSave(data);
        _hasLastGoodSave = true;
        _status = $"Welcome back, {ActiveAccountName}";
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
            _status = "Please select an account to delete";
            return;
        }

        var entry = _accounts[_selectedIndex];
        string path = AccountPath(entry.accountId);
        if (File.Exists(path))
            File.Delete(path);

        _accounts.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        WriteIndex();
        _status = $"Deleted account: {entry.accountName}";
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
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;
            DrawInGameHud();
            return;
        }

        Time.timeScale = 0f;
        RPGUI.Ensure();

        RPGUI.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.72f));

        float w = Mathf.Min(Screen.width - 40f, 720f);
        float h = Mathf.Min(Screen.height - 40f, 640f);
        Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
        RPGUI.DrawPanel(panel, new Color(0.04f, 0.05f, 0.08f, 0.98f));

        float y = panel.y + 16f;
        RPGUI.DrawText(new Rect(panel.x + 24f, y, w - 48f, RPGUI.TitleH), "Select Account", RPGUI.Title);
        y += RPGUI.TitleH;
        RPGUI.DrawText(new Rect(panel.x + 24f, y, w - 48f, RPGUI.HintH),
            "Create a new account or select an existing one to continue", RPGUI.Hint);
        y += RPGUI.HintH + 10f;

        float tabW = 160f;
        if (RPGUI.Button(new Rect(panel.x + 24f, y, tabW, RPGUI.ButtonH), "Select Account", _mode == UiMode.Select))
            _mode = UiMode.Select;
        if (RPGUI.Button(new Rect(panel.x + 24f + tabW + 10f, y, tabW, RPGUI.ButtonH), "Create Account", _mode == UiMode.Create))
            _mode = UiMode.Create;

        if (_mode == UiMode.Select)
            DrawSelectMode(panel, w, h);
        else
            DrawCreateMode(panel, w, h);

        if (!string.IsNullOrEmpty(_status))
            RPGUI.DrawText(new Rect(panel.x + 24f, panel.yMax - 48f, w - 48f, RPGUI.HintH), _status, RPGUI.Hint);
    }

    void DrawSelectMode(Rect panel, float w, float h)
    {
        float listTop = panel.y + 16f + RPGUI.TitleH + RPGUI.HintH + 10f + RPGUI.ButtonH + 14f;
        float listH = h - (listTop - panel.y) - 120f;
        Rect view = new Rect(panel.x + 24f, listTop, w - 48f, Mathf.Max(120f, listH));
        float rowH = 80f;
        float contentH = Mathf.Max(view.height, Mathf.Max(1, _accounts.Count) * rowH + 8f);
        _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0, 0, view.width - 20f, contentH));

        if (_accounts.Count == 0)
        {
            RPGUI.DrawText(new Rect(8f, 16f, view.width - 40f, RPGUI.LabelH), "No accounts yet — switch to Create Account", RPGUI.Label);
        }
        else
        {
            for (int i = 0; i < _accounts.Count; i++)
            {
                var a = _accounts[i];
                Rect row = new Rect(0f, i * rowH, view.width - 24f, rowH - 8f);
                Color bg = i == _selectedIndex
                    ? new Color(0.22f, 0.3f, 0.48f, 0.98f)
                    : new Color(0.11f, 0.13f, 0.17f, 0.95f);
                RPGUI.DrawPanel(row, bg, false);
                if (i == _selectedIndex)
                    RPGUI.Fill(new Rect(row.x, row.y, 4f, row.height), RPGUI.Accent);

                RPGUI.DrawText(new Rect(row.x + 16f, row.y + 8f, row.width - 28f, RPGUI.ItemNameH),
                    a.accountName, RPGUI.ItemName);
                RPGUI.DrawText(new Rect(row.x + 16f, row.y + 44f, row.width - 28f, RPGUI.FieldTagH),
                    $"Lv {a.level} · World {a.currentWorld} · {a.lastPlayedAt}", RPGUI.FieldTag);

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }
        }

        GUI.EndScrollView();

        if (RPGUI.Button(new Rect(panel.x + 24f, panel.yMax - 100f, 240f, RPGUI.ButtonH), "Start Game", primary: true))
            PlaySelectedAccount();
        if (RPGUI.Button(new Rect(panel.x + 280f, panel.yMax - 100f, 160f, RPGUI.ButtonH), "Delete Account"))
            DeleteSelectedAccount();
    }

    void DrawCreateMode(Rect panel, float w, float h)
    {
        float y = panel.y + 16f + RPGUI.TitleH + RPGUI.HintH + 10f + RPGUI.ButtonH + 20f;
        RPGUI.DrawText(new Rect(panel.x + 24f, y, w - 48f, RPGUI.LabelH), "Account Name", RPGUI.Label);
        y += RPGUI.LabelH + 6f;

        var tf = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 12, 10, 10),
            clipping = TextClipping.Overflow,
            normal = { textColor = Color.white },
            focused = { textColor = Color.white }
        };
        _newName = GUI.TextField(new Rect(panel.x + 24f, y, w - 48f, 48f), _newName, 16, tf);
        y += 60f;

        RPGUI.DrawText(new Rect(panel.x + 24f, y, w - 48f, RPGUI.HintH),
            "New accounts start at World 1, Level 1", RPGUI.Hint);

        if (RPGUI.Button(new Rect(panel.x + 24f, panel.yMax - 100f, 280f, RPGUI.ButtonH), "Create & Start", primary: true))
            CreateNewAccount(_newName);
    }

    void DrawInGameHud()
    {
        RPGUI.Ensure();
        // Bottom-right — avoids the center control hint bar.
        float boxW = 280f;
        Rect box = new Rect(Screen.width - boxW - 16f, Screen.height - 52f, boxW, 40f);
        RPGUI.DrawPanel(box, new Color(0.05f, 0.06f, 0.09f, 0.88f), false);
        RPGUI.DrawText(new Rect(box.x + 10f, box.y + 4f, 150f, RPGUI.HintH),
            ActiveAccountName, RPGUI.Hint);

        if (RPGUI.ButtonSm(new Rect(box.xMax - 96f, box.y + 4f, 86f, 32f), "Save"))
            SaveActive(silent: false);
    }
}
