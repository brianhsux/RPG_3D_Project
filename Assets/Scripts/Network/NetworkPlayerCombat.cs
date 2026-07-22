using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner requests attacks via ServerRpc; Host validates range/cooldown and applies damage.
/// Combat stats sync from the owner's local gear CharacterStats.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerCombat : NetworkBehaviour
{
    public const float DefaultMaxHp = 100f;
    public const float DefaultAttack = 12f;
    public const float DefaultAttackRange = 2.6f;
    public const float DefaultAttackCooldown = 0.5f;
    public const float RespawnDelay = 3f;
    const float StatsSyncInterval = 0.5f;

    public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
        DefaultMaxHp, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> MaxHp = new NetworkVariable<float>(
        DefaultMaxHp, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> AttackPower = new NetworkVariable<float>(
        DefaultAttack, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> AttackRangeNv = new NetworkVariable<float>(
        DefaultAttackRange, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> AttackCooldownNv = new NetworkVariable<float>(
        DefaultAttackCooldown, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    float _clientNextAttackTime;
    float _serverNextAttackTime;
    float _respawnAt = -1f;
    float _nextStatsSync;
    Renderer _bodyRenderer;
    Color _aliveColor = new Color(0.35f, 0.75f, 0.95f);
    Color _deadColor = new Color(0.35f, 0.35f, 0.38f);

    public bool IsAlive => IsSpawned && CurrentHp.Value > 0f;
    public float HealthPercent =>
        MaxHp.Value > 0.01f ? Mathf.Clamp01(CurrentHp.Value / MaxHp.Value) : 0f;

    public static void RefreshLocalOwnerStats()
    {
        var combats = Object.FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            if (combats[i] != null && combats[i].IsSpawned && combats[i].IsOwner)
                combats[i].PushLocalStatsToServer();
        }
    }

    public override void OnNetworkSpawn()
    {
        CacheBody();
        CurrentHp.OnValueChanged += OnHpChanged;

        if (IsServer)
        {
            if (MaxHp.Value <= 0f) MaxHp.Value = DefaultMaxHp;
            if (CurrentHp.Value <= 0f) CurrentHp.Value = MaxHp.Value;
            if (AttackPower.Value <= 0f) AttackPower.Value = DefaultAttack;
            if (AttackRangeNv.Value <= 0f) AttackRangeNv.Value = DefaultAttackRange;
            if (AttackCooldownNv.Value <= 0f) AttackCooldownNv.Value = DefaultAttackCooldown;
        }

        if (IsOwner)
        {
            _nextStatsSync = 0f;
            PushLocalStatsToServer();
        }

        ApplyDeathVisual(!IsAlive);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHp.OnValueChanged -= OnHpChanged;
    }

    void OnHpChanged(float previous, float current)
    {
        ApplyDeathVisual(current <= 0f);
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (IsServer && _respawnAt > 0f && Time.time >= _respawnAt)
        {
            _respawnAt = -1f;
            CurrentHp.Value = MaxHp.Value;
        }

        if (IsOwner && Time.time >= _nextStatsSync)
        {
            _nextStatsSync = Time.time + StatsSyncInterval;
            PushLocalStatsToServer();
        }

        if (IsOwner)
            TryChallengeInteractInput();

        if (!IsOwner || !IsAlive) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel)
            return;

        float range = AttackRangeNv.Value > 0.1f ? AttackRangeNv.Value : DefaultAttackRange;
        float cd = AttackCooldownNv.Value > 0.05f ? AttackCooldownNv.Value : DefaultAttackCooldown;

        bool pressed = WasAttackPressed();
        bool auto = HasEnemyInRange(range);
        if (!pressed && !auto) return;
        if (Time.time < _clientNextAttackTime) return;

        _clientNextAttackTime = Time.time + cd;
        AttackServerRpc();
    }

    void TryChallengeInteractInput()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        var altar = ChallengeAltar.FindNearest(transform.position, ChallengeAltar.InteractRange);
        if (altar != null)
            altar.TryInteract();
    }

    public void PushLocalStatsToServer()
    {
        if (!IsOwner || !IsSpawned) return;
        var stats = FindLocalPlayerStats();
        if (stats == null) return;

        float atk = Mathf.Max(1f, stats.attackDamage);
        float range = Mathf.Max(1f, stats.attackRange);
        float cd = Mathf.Max(0.15f, stats.EffectiveAttackCooldown);
        float maxHp = Mathf.Max(1f, stats.maxHealth);
        SyncCombatStatsServerRpc(atk, range, cd, maxHp);
    }

    [ServerRpc]
    void SyncCombatStatsServerRpc(float attack, float range, float cooldown, float maxHp)
    {
        AttackPower.Value = Mathf.Max(1f, attack);
        AttackRangeNv.Value = Mathf.Max(1f, range);
        AttackCooldownNv.Value = Mathf.Max(0.15f, cooldown);

        float prevMax = MaxHp.Value;
        MaxHp.Value = Mathf.Max(1f, maxHp);
        if (CurrentHp.Value <= 0f)
            return;
        if (prevMax <= 0.01f || CurrentHp.Value >= prevMax * 0.98f)
            CurrentHp.Value = MaxHp.Value;
        else
            CurrentHp.Value = Mathf.Min(CurrentHp.Value, MaxHp.Value);
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        if (!IsAlive) return;
        float cd = AttackCooldownNv.Value > 0.05f ? AttackCooldownNv.Value : DefaultAttackCooldown;
        if (Time.time < _serverNextAttackTime) return;
        _serverNextAttackTime = Time.time + cd;

        float range = AttackRangeNv.Value > 0.1f ? AttackRangeNv.Value : DefaultAttackRange;
        float dmg = AttackPower.Value > 0.1f ? AttackPower.Value : DefaultAttack;

        var enemy = FindNearestEnemyInRange(range);
        if (enemy == null) return;
        enemy.ApplyDamageServer(dmg, OwnerClientId);
    }

    /// <summary>Host-only damage entry used by NetworkEnemy AI.</summary>
    public void ApplyDamageServer(float amount)
    {
        if (!IsServer) return;
        if (CurrentHp.Value <= 0f) return;

        float dealt = Mathf.Max(0f, amount);
        CurrentHp.Value = Mathf.Max(0f, CurrentHp.Value - dealt);
        if (dealt >= 0.5f)
            ShowDamageClientRpc(dealt);

        if (CurrentHp.Value <= 0f)
            _respawnAt = Time.time + RespawnDelay;
    }

    /// <summary>Host routes kill loot/XP to this player's owning client.</summary>
    public void DeliverKillRewardsServer(
        int xp,
        string lootJson1,
        string lootJson2,
        bool bossPet,
        int petKind,
        int petLv,
        Vector3 killPos,
        bool wasBoss)
    {
        if (!IsServer) return;
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };
        ReceiveKillRewardClientRpc(xp, lootJson1 ?? "", lootJson2 ?? "", bossPet, petKind, petLv, killPos, wasBoss, rpcParams);
    }

    [ClientRpc]
    void ReceiveKillRewardClientRpc(
        int xp,
        string lootJson1,
        string lootJson2,
        bool bossPet,
        int petKind,
        int petLv,
        Vector3 killPos,
        bool wasBoss,
        ClientRpcParams rpcParams = default)
    {
        if (PlayerProgression.Instance != null && xp > 0)
            PlayerProgression.Instance.GainXp(xp);

        TryAddLootFromJson(lootJson1);
        TryAddLootFromJson(lootJson2);

        if (PlayerPetController.Instance != null)
        {
            PlayerPetController.Instance.OnEnemyKilledNearby(killPos, wasBoss);
            if (bossPet)
            {
                PlayerPetController.Instance.TryAddPet((PetKind)petKind, petLv, out _);
                if (AccountManager.Instance != null)
                    AccountManager.Instance.SaveActive(silent: true);
            }
        }

        if (AccountManager.Instance != null && (xp > 0 || !string.IsNullOrEmpty(lootJson1)))
            AccountManager.Instance.SaveActive(silent: true);
    }

    static void TryAddLootFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        ItemSaveData data;
        try { data = JsonUtility.FromJson<ItemSaveData>(json); }
        catch { return; }
        if (data == null || !data.IsValid) return;
        var item = data.ToItem();
        if (item == null) return;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.AddItem(item);
    }

    [ClientRpc]
    void ShowDamageClientRpc(float amount)
    {
        DamageNumbers.Spawn(transform.position, amount, true, false);
    }

    NetworkEnemy FindNearestEnemyInRange(float range)
    {
        NetworkEnemy best = null;
        float bestSq = range * range;
        var enemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive) continue;
            float sq = (e.transform.position - transform.position).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = e;
            }
        }
        return best;
    }

    bool HasEnemyInRange(float range)
    {
        var enemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        float rangeSq = range * range;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive) continue;
            if ((e.transform.position - transform.position).sqrMagnitude <= rangeSq)
                return true;
        }
        return false;
    }

    static CharacterStats FindLocalPlayerStats()
    {
        // Prefer the suspended solo hero (still holds gear stats).
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            if (players[i].GetComponent<NetworkPlayer>() != null) continue;
            var stats = players[i].GetComponent<CharacterStats>();
            if (stats != null && stats.isPlayer) return stats;
        }

        if (PlayerInventory.Instance != null)
        {
            var stats = PlayerInventory.Instance.GetComponent<CharacterStats>();
            if (stats != null) return stats;
        }
        return null;
    }

    static bool WasAttackPressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
        var kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame)
            return true;
        var pad = Gamepad.current;
        if (pad != null && pad.buttonWest.wasPressedThisFrame)
            return true;
        return false;
    }

    void CacheBody()
    {
        var body = transform.Find("Body");
        if (body != null)
            _bodyRenderer = body.GetComponent<Renderer>();
        if (IsOwner)
            _aliveColor = new Color(0.35f, 0.75f, 0.95f);
        else
            _aliveColor = new Color(0.85f, 0.45f, 0.35f);
    }

    void ApplyDeathVisual(bool dead)
    {
        if (_bodyRenderer == null) CacheBody();
        if (_bodyRenderer == null) return;
        Color c = dead ? _deadColor : _aliveColor;
        var mat = _bodyRenderer.material;
        mat.color = c;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
    }

    // ── Challenge altar (Host-authoritative) ───────────────────────────

    public static void RequestStartChallenge(ChallengeAltar altar, ChallengeAltar.Difficulty difficulty)
    {
        if (altar == null) return;
        var local = FindOwnedCombat();
        if (local == null)
        {
            altar.HostStartChallenge(difficulty);
            return;
        }
        local.StartChallengeServerRpc((int)difficulty);
    }

    public static void BroadcastChallengeHudServer(int state, string status, int difficulty, float cooldownLeft)
    {
        if (!NetPlay.IsServer) return;
        var host = FindAnySpawnedCombat();
        host?.ChallengeHudClientRpc(state, status ?? "", difficulty, cooldownLeft);
    }

    public static void BroadcastChallengeLootServer(string message)
    {
        if (!NetPlay.IsServer) return;
        var host = FindAnySpawnedCombat();
        host?.ChallengeLootClientRpc(message ?? "");
    }

    public static void DeliverChallengeRewardsToAllServer(
        int xp, string gemJson, string gem2Json, bool givePet, int petKind, int petLv, string banner)
    {
        if (!NetPlay.IsServer) return;
        var combats = FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        NetworkPlayerCombat any = null;
        for (int i = 0; i < combats.Length; i++)
        {
            var c = combats[i];
            if (c == null || !c.IsSpawned) continue;
            any = c;
            c.DeliverKillRewardsServer(xp, gemJson, gem2Json, givePet, petKind, petLv, c.transform.position, true);
        }
        if (any != null && !string.IsNullOrEmpty(banner))
            any.ChallengeLootClientRpc(banner);
    }

    [ServerRpc]
    void StartChallengeServerRpc(int difficulty)
    {
        var altar = ChallengeAltar.FindNearest(transform.position, ChallengeAltar.InteractRange + 8f)
                    ?? ChallengeAltar.Active
                    ?? Object.FindFirstObjectByType<ChallengeAltar>();
        if (altar == null) return;
        altar.HostStartChallenge((ChallengeAltar.Difficulty)Mathf.Clamp(difficulty, 0, 2));
    }

    [ClientRpc]
    void ChallengeHudClientRpc(int state, string status, int difficulty, float cooldownLeft)
    {
        if (IsServer) return; // Host already owns the sim
        var altar = ChallengeAltar.Active ?? Object.FindFirstObjectByType<ChallengeAltar>();
        altar?.ApplyRemoteHud(state, status, difficulty, cooldownLeft);
    }

    [ClientRpc]
    public void ChallengeLootClientRpc(string message)
    {
        if (PlayerInventory.Instance != null && !string.IsNullOrEmpty(message))
            PlayerInventory.Instance.ShowLoot(message);
    }

    // ── Pet attacks ────────────────────────────────────────────────────

    public void RequestPetAttack(NetworkEnemy enemy, float damage)
    {
        if (!IsOwner || !IsSpawned || enemy == null) return;
        PetAttackServerRpc(enemy.NetworkObject, damage);
    }

    [ServerRpc]
    void PetAttackServerRpc(NetworkObjectReference enemyRef, float damage)
    {
        if (!enemyRef.TryGet(out NetworkObject no) || no == null) return;
        var enemy = no.GetComponent<NetworkEnemy>();
        if (enemy == null || !enemy.IsAlive) return;
        enemy.ApplyDamageServer(Mathf.Max(1f, damage), OwnerClientId);
    }

    static NetworkPlayerCombat FindOwnedCombat()
    {
        var combats = Object.FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            if (combats[i] != null && combats[i].IsSpawned && combats[i].IsOwner)
                return combats[i];
        }
        return null;
    }

    static NetworkPlayerCombat FindAnySpawnedCombat()
    {
        var combats = Object.FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            if (combats[i] != null && combats[i].IsSpawned)
                return combats[i];
        }
        return null;
    }
}
