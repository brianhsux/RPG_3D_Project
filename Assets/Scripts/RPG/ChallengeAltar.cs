using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Timed wave challenge on Map 2. Clear for pet egg + legendary gem.
/// Press E near the altar to start.
/// </summary>
public class ChallengeAltar : MonoBehaviour
{
    public static ChallengeAltar Active { get; private set; }

    public const int TotalWaves = 5;
    public const float TimeLimit = 100f;
    public const float CooldownSeconds = 90f;
    public const float InteractRange = 3.2f;

    public int world = 1;
    public int mapIndex = 2;

    enum State { Idle, Running, Success, Failed, Cooldown }

    State _state = State.Idle;
    int _wave;
    float _timeLeft;
    float _cooldownLeft;
    readonly List<CharacterStats> _alive = new List<CharacterStats>();
    string _status = "Timed Challenge Altar";

    public bool IsRunning => _state == State.Running;
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

    void OnDestroy()
    {
        if (Active == this) Active = null;
        CleanupMobs();
    }

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

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

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var st = player.GetComponent<CharacterStats>();
            if (st != null && st.isDead)
            {
                Fail("You fell — challenge failed");
                return;
            }
        }

        if (_alive.Count == 0)
        {
            if (_wave >= TotalWaves)
                Succeed();
            else
                BeginWave(_wave + 1);
        }

        _status = $"Wave {_wave}/{TotalWaves}  ·  {_alive.Count} left  ·  {Mathf.CeilToInt(_timeLeft)}s";
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

        StartChallenge();
        return true;
    }

    void StartChallenge()
    {
        CleanupMobs();
        Active = this;
        _state = State.Running;
        _timeLeft = TimeLimit;
        _wave = 0;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot("Challenge started! Survive the waves!");
        BeginWave(1);
    }

    void BeginWave(int wave)
    {
        _wave = wave;
        _alive.Clear();

        int content = ProgressionFormulas.ContentLevel(world, mapIndex);
        int count;
        int levelBonus;
        bool miniBoss = false;
        switch (wave)
        {
            case 1: count = 3; levelBonus = 0; break;
            case 2: count = 4; levelBonus = 0; break;
            case 3: count = 5; levelBonus = 1; break;
            case 4: count = 4; levelBonus = 2; break;
            default:
                count = 1;
                levelBonus = 3;
                miniBoss = true;
                break;
        }

        EnemyKind kind = EnemyVisuals.KindForZone(world, mapIndex);
        var builder = MapRuntimeBuilder.Instance;
        if (builder == null) return;

        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f + wave * 0.4f;
            float rad = 4.5f + (i % 2) * 0.8f;
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            pos.y = transform.position.y;

            var go = builder.SpawnChallengeEnemy(
                $"Challenge_W{wave}_{i}",
                pos,
                transform.position,
                content + levelBonus,
                miniBoss,
                miniBoss ? EnemyKind.WorldBoss : kind,
                mapIndex);

            if (go == null) continue;
            var stats = go.GetComponent<CharacterStats>();
            if (stats != null)
            {
                stats.isChallengeEnemy = true;
                // Mini-boss for wave 5 but not a world boss for portal logic
                if (miniBoss)
                {
                    stats.isBoss = false;
                    stats.displayName = "Challenge Champion Lv" + (content + levelBonus);
                    stats.maxHealth *= 1.8f;
                    stats.currentHealth = stats.maxHealth;
                    stats.attackDamage *= 1.25f;
                }
                _alive.Add(stats);
            }
        }

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"Wave {_wave}/{TotalWaves}!");
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
        _status = $"Cleared! Cooldown {CooldownSeconds:0}s";
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
        int content = ProgressionFormulas.ContentLevel(world, mapIndex);

        // Legendary gem (or Epic if bag issues)
        var gem = GemCatalog.CreateGem(
            (GemKind)Random.Range(0, 12),
            Mathf.Clamp(content + Random.Range(1, 4), 1, 40),
            ItemRarity.Legendary);
        bool gemOk = inv != null && inv.AddItem(gem);

        // Pet egg
        bool petOk = false;
        if (PlayerPetController.Instance != null)
        {
            var kind = PlayerPetController.RollBossPetKind(world);
            petOk = PlayerPetController.Instance.TryAddPet(kind, Mathf.Max(1, content / 2), out _);
        }

        if (PlayerProgression.Instance != null)
            PlayerProgression.Instance.GainXp(40 + content * 12);

        if (inv != null)
        {
            string msg = "Challenge cleared!";
            if (gemOk) msg += $" {gem.displayName}";
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
        if (_state != State.Running && _state != State.Cooldown) return;
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        RPGUI.Ensure();
        float w = Mathf.Min(560f, Screen.width - 40f);
        Rect tip = new Rect(Screen.width * 0.5f - w * 0.5f, 70f, w, 44f);
        Color bg = _state == State.Running
            ? new Color(0.25f, 0.12f, 0.08f, 0.92f)
            : new Color(0.1f, 0.1f, 0.14f, 0.88f);
        RPGUI.DrawPanel(tip, bg);
        RPGUI.DrawText(tip, _status, RPGUI.Banner);
    }
}
