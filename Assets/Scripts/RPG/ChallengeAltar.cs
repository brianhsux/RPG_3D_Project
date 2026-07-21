using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Timed horde challenge at the open world hub.
/// Press E to pick a difficulty, then survive timed waves for rewards.
/// </summary>
public class ChallengeAltar : MonoBehaviour
{
    public static ChallengeAltar Active { get; private set; }

    public const int TotalWaves = 10;
    public const float ClearBuffer = 50f;
    public const float CooldownSeconds = 90f;
    public const float InteractRange = 3.2f;

    public enum Difficulty
    {
        Normal = 0,
        Hard = 1,
        Extreme = 2
    }

    public int world = 1;
    /// <summary>Used for content level / enemy kind (mid-world difficulty).</summary>
    public int mapIndex = 2;

    enum State { Idle, Selecting, Running, Success, Failed, Cooldown }

    State _state = State.Idle;
    Difficulty _difficulty = Difficulty.Normal;
    int _wave;
    float _timeLeft;
    float _waveTimer;
    float _waveInterval;
    float _cooldownLeft;
    readonly List<CharacterStats> _alive = new List<CharacterStats>();
    string _status = "Timed Challenge Altar";

    public bool IsRunning => _state == State.Running;
    public bool IsSelecting => _state == State.Selecting;
    public string StatusText => _status;

    public static ChallengeAltar FindNearest(Vector3 pos, float range)
    {
        ChallengeAltar best = null;
        float bestD = range;
        var all = FindObjectsByType<ChallengeAltar>(FindObjectsSortMode.None);
        foreach (var a in all)
        {
            if (a == null) continue;
            float d = Vector3.Distance(pos, a.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = a;
            }
        }
        return best;
    }

    public static string DifficultyName(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return "Hard";
            case Difficulty.Extreme: return "Extreme";
            default: return "Normal";
        }
    }

    public static string DifficultyBlurb(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard:
                return "Faster waves · stronger foes · elites appear · better loot";
            case Difficulty.Extreme:
                return "Very fast waves · brutal elites · toughest champ · best loot";
            default:
                return "Standard pace · solid rewards";
        }
    }

    static float WaveIntervalFor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return 6.5f;
            case Difficulty.Extreme: return 4.5f;
            default: return 9f;
        }
    }

    static float StatMultFor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return 1.25f;
            case Difficulty.Extreme: return 1.55f;
            default: return 1f;
        }
    }

    static float EliteChanceFor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return 0.28f;
            case Difficulty.Extreme: return 0.48f;
            default: return 0.08f;
        }
    }

    static int ExtraLevelFor(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return 1;
            case Difficulty.Extreme: return 3;
            default: return 0;
        }
    }

    static float RewardXpMult(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Hard: return 1.5f;
            case Difficulty.Extreme: return 2.2f;
            default: return 1f;
        }
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        CleanupMobs();
    }

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel)
        {
            if (_state == State.Selecting)
                CancelSelect();
            return;
        }

        if (_state == State.Selecting)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractRange + 1.5f)
                CancelSelect();

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                CancelSelect();
            return;
        }

        if (_state == State.Cooldown)
        {
            _cooldownLeft -= Time.deltaTime;
            _status = $"Cooldown {Mathf.CeilToInt(_cooldownLeft)}s";
            if (_cooldownLeft <= 0f)
            {
                _state = State.Idle;
                _status = "Timed Challenge Altar";
            }
            return;
        }

        if (_state != State.Running) return;

        _timeLeft -= Time.deltaTime;
        PruneAlive();

        if (_timeLeft <= 0f)
        {
            Fail("Time up!");
            return;
        }

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            var st = p.GetComponent<CharacterStats>();
            if (st != null && st.isDead)
            {
                Fail("You fell — challenge failed");
                return;
            }
        }

        if (_wave < TotalWaves)
        {
            _waveTimer -= Time.deltaTime;
            if (_waveTimer <= 0f)
            {
                BeginWave(_wave + 1);
                _waveTimer = _waveInterval;
            }
        }

        if (_wave >= TotalWaves && _alive.Count == 0)
        {
            Succeed();
            return;
        }

        string next = _wave < TotalWaves
            ? $"Next {Mathf.CeilToInt(Mathf.Max(0f, _waveTimer))}s"
            : "Final clear";
        _status = $"{DifficultyName(_difficulty)}  ·  Wave {_wave}/{TotalWaves}  ·  {_alive.Count} alive  ·  {next}  ·  {Mathf.CeilToInt(_timeLeft)}s";
    }

    public bool TryInteract()
    {
        if (_state == State.Running) return false;
        if (_state == State.Cooldown)
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.ShowLoot($"Challenge cooling down ({Mathf.CeilToInt(_cooldownLeft)}s)");
            return true;
        }

        if (_state == State.Selecting)
        {
            CancelSelect();
            return true;
        }

        OpenSelect();
        return true;
    }

    void OpenSelect()
    {
        _state = State.Selecting;
        _status = "Choose difficulty";
    }

    void CancelSelect()
    {
        if (_state != State.Selecting) return;
        _state = State.Idle;
        _status = "Timed Challenge Altar";
    }

    void StartChallenge(Difficulty difficulty)
    {
        CleanupMobs();
        Active = this;
        _difficulty = difficulty;
        _waveInterval = WaveIntervalFor(difficulty);
        _state = State.Running;
        _timeLeft = TotalWaves * _waveInterval + ClearBuffer;
        _wave = 0;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"{DifficultyName(difficulty)} challenge started!");
        BeginWave(1);
        _waveTimer = _waveInterval;
    }

    void BeginWave(int wave)
    {
        _wave = wave;

        int content = ProgressionFormulas.ContentLevel(world, mapIndex) + ExtraLevelFor(_difficulty);
        GetWaveSpawn(wave, out int count, out int levelBonus, out bool miniBoss);

        // Harder difficulties pack a bit more trash into mid/late waves.
        if (_difficulty == Difficulty.Hard && !miniBoss && wave >= 4)
            count += 1;
        if (_difficulty == Difficulty.Extreme && !miniBoss && wave >= 3)
            count += 2;

        EnemyKind kind = EnemyVisuals.KindForZone(world, mapIndex);
        var builder = MapRuntimeBuilder.Instance;
        if (builder == null) return;

        float eliteChance = EliteChanceFor(_difficulty);
        float statMult = StatMultFor(_difficulty);

        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f + wave * 0.55f;
            float rad = 5.5f + (i % 3) * 0.9f;
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            pos.y = transform.position.y;

            bool elite = !miniBoss && Random.value < eliteChance;

            var go = builder.SpawnChallengeEnemy(
                $"Challenge_W{wave}_{i}",
                pos,
                transform.position,
                content + levelBonus + (elite ? 1 : 0),
                miniBoss,
                miniBoss ? EnemyKind.WorldBoss : kind,
                mapIndex);

            if (go == null) continue;
            var stats = go.GetComponent<CharacterStats>();
            if (stats == null) continue;

            stats.isChallengeEnemy = true;
            stats.isBoss = false;

            if (miniBoss)
            {
                float champHp = _difficulty == Difficulty.Extreme ? 2.4f : (_difficulty == Difficulty.Hard ? 2.0f : 1.8f);
                float champAtk = _difficulty == Difficulty.Extreme ? 1.5f : (_difficulty == Difficulty.Hard ? 1.35f : 1.25f);
                stats.displayName = "Challenge Champion Lv" + (content + levelBonus);
                stats.maxHealth *= champHp * statMult;
                stats.currentHealth = stats.maxHealth;
                stats.attackDamage *= champAtk * statMult;
                go.transform.localScale *= 1.15f;
            }
            else if (elite)
            {
                stats.displayName = "Elite " + stats.displayName;
                stats.maxHealth *= 1.65f * statMult;
                stats.currentHealth = stats.maxHealth;
                stats.attackDamage *= 1.4f * statMult;
                stats.defense *= 1.25f;
                go.transform.localScale *= 1.2f;
            }
            else if (statMult > 1.01f)
            {
                stats.maxHealth *= statMult;
                stats.currentHealth = stats.maxHealth;
                stats.attackDamage *= statMult;
            }

            _alive.Add(stats);
        }

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"Wave {_wave}/{TotalWaves}!");
    }

    static void GetWaveSpawn(int wave, out int count, out int levelBonus, out bool miniBoss)
    {
        miniBoss = false;
        levelBonus = 0;
        switch (wave)
        {
            case 1: count = 2; break;
            case 2: count = 3; break;
            case 3: count = 3; levelBonus = 1; break;
            case 4: count = 4; levelBonus = 1; break;
            case 5: count = 4; levelBonus = 2; break;
            case 6: count = 5; levelBonus = 2; break;
            case 7: count = 5; levelBonus = 3; break;
            case 8: count = 6; levelBonus = 3; break;
            case 9: count = 4; levelBonus = 4; break;
            default:
                count = 1;
                levelBonus = 5;
                miniBoss = true;
                break;
        }
    }

    public void NotifyEnemyKilled(CharacterStats stats)
    {
        if (_state != State.Running) return;
        _alive.Remove(stats);
    }

    void Succeed()
    {
        CleanupMobs();
        _state = State.Success;
        GrantRewards();
        _state = State.Cooldown;
        _cooldownLeft = CooldownSeconds;
        _status = $"Cleared {DifficultyName(_difficulty)}! Cooldown {CooldownSeconds:0}s";
        Active = null;
    }

    void Fail(string reason)
    {
        CleanupMobs();
        _state = State.Failed;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot(reason);
        _state = State.Cooldown;
        _cooldownLeft = CooldownSeconds * 0.5f;
        _status = "Failed — cooldown…";
        Active = null;
    }

    void GrantRewards()
    {
        var inv = PlayerInventory.Instance;
        int content = ProgressionFormulas.ContentLevel(world, mapIndex) + ExtraLevelFor(_difficulty);
        int gemLevelBonus = _difficulty == Difficulty.Extreme ? 4 : (_difficulty == Difficulty.Hard ? 2 : 1);

        ItemRarity gemRarity = ItemRarity.Legendary;
        int gemBase = ProgressionFormulas.ContentToItemLevel(content);
        var gem = GemCatalog.CreateGem(
            (GemKind)Random.Range(0, GemCatalog.KindCount),
            Mathf.Clamp(gemBase + Random.Range(gemLevelBonus, gemLevelBonus + 4), 1, ProgressionFormulas.MaxItemLevel),
            gemRarity);
        bool gemOk = inv != null && inv.AddItem(gem);

        // Extreme: bonus second gem (Epic+)
        bool gem2Ok = false;
        ItemInstance gem2 = null;
        if (_difficulty == Difficulty.Extreme && inv != null)
        {
            gem2 = GemCatalog.CreateGem(
                (GemKind)Random.Range(0, GemCatalog.KindCount),
                Mathf.Clamp(gemBase + Random.Range(1, 5), 1, ProgressionFormulas.MaxItemLevel),
                Random.value < 0.45f ? ItemRarity.Legendary : ItemRarity.Epic);
            gem2Ok = inv.AddItem(gem2);
        }

        bool petOk = false;
        if (PlayerPetController.Instance != null)
        {
            var kind = PlayerPetController.RollBossPetKind(world);
            int petLv = Mathf.Clamp(gemBase / 2 + ExtraLevelFor(_difficulty) * 2, 1, ProgressionFormulas.MaxPetLevel);
            petOk = PlayerPetController.Instance.TryAddPet(kind, petLv, out _);
        }

        if (PlayerProgression.Instance != null)
        {
            int xp = Mathf.RoundToInt((40 + content * 12) * RewardXpMult(_difficulty));
            PlayerProgression.Instance.GainXp(xp);
        }

        if (inv != null)
        {
            string msg = $"{DifficultyName(_difficulty)} cleared!";
            if (gemOk) msg += $" {gem.displayName}";
            if (gem2Ok && gem2 != null) msg += $" + {gem2.displayName}";
            if (petOk) msg += " + Pet egg!";
            if (!gemOk) msg += " (bag full — gem lost)";
            inv.ShowLoot(msg);
        }

        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    void PruneAlive()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null || _alive[i].isDead)
                _alive.RemoveAt(i);
        }
    }

    void CleanupMobs()
    {
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i] != null)
                Destroy(_alive[i].gameObject);
        }
        _alive.Clear();
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        RPGUI.Ensure();

        if (_state == State.Selecting)
        {
            DrawDifficultyPicker();
            return;
        }

        if (_state != State.Running && _state != State.Cooldown) return;

        float w = Mathf.Min(680f, Screen.width - 40f);
        Rect tip = new Rect(Screen.width * 0.5f - w * 0.5f, 70f, w, 44f);
        Color bg = _state == State.Running
            ? new Color(0.25f, 0.12f, 0.08f, 0.92f)
            : new Color(0.1f, 0.1f, 0.14f, 0.88f);
        RPGUI.DrawPanel(tip, bg);
        RPGUI.DrawText(tip, _status, RPGUI.Banner);
    }

    void DrawDifficultyPicker()
    {
        float panelW = Mathf.Min(560f, Screen.width - 48f);
        float rowH = 78f;
        float panelH = 56f + rowH * 3f + 70f;
        Rect panel = new Rect(Screen.width * 0.5f - panelW * 0.5f, Screen.height * 0.28f, panelW, panelH);
        RPGUI.DrawPanel(panel, new Color(0.1f, 0.08f, 0.07f, 0.96f));

        RPGUI.DrawText(new Rect(panel.x + 16f, panel.y + 12f, panelW - 32f, RPGUI.LabelH),
            "Timed Challenge — Choose Difficulty", RPGUI.Label);
        RPGUI.DrawText(new Rect(panel.x + 16f, panel.y + 40f, panelW - 32f, RPGUI.HintH),
            "Esc / E / walk away to cancel", RPGUI.Hint);

        float y = panel.y + 68f;
        DrawDiffRow(panel.x + 16f, y, panelW - 32f, rowH - 8f, Difficulty.Normal,
            new Color(0.18f, 0.28f, 0.2f, 0.95f));
        y += rowH;
        DrawDiffRow(panel.x + 16f, y, panelW - 32f, rowH - 8f, Difficulty.Hard,
            new Color(0.32f, 0.22f, 0.12f, 0.95f));
        y += rowH;
        DrawDiffRow(panel.x + 16f, y, panelW - 32f, rowH - 8f, Difficulty.Extreme,
            new Color(0.35f, 0.12f, 0.12f, 0.95f));
    }

    void DrawDiffRow(float x, float y, float w, float h, Difficulty d, Color bg)
    {
        Rect row = new Rect(x, y, w, h);
        RPGUI.DrawPanel(row, bg, false);
        RPGUI.DrawText(new Rect(x + 12f, y + 8f, w - 140f, RPGUI.ItemNameH),
            DifficultyName(d), RPGUI.ItemName);
        RPGUI.DrawText(new Rect(x + 12f, y + 36f, w - 140f, RPGUI.FieldTagH),
            DifficultyBlurb(d), RPGUI.FieldTag);

        if (RPGUI.Button(new Rect(x + w - 120f, y + (h - RPGUI.ButtonH) * 0.5f, 108f, RPGUI.ButtonH),
            "Start", primary: d != Difficulty.Normal))
            StartChallenge(d);
    }
}
