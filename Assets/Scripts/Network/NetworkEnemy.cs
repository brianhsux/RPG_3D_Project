using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Host-authoritative adventure enemy. HP + pose sync; visuals from EnemyKind.
/// Kill rewards go to the last attacker; adventure progress notifies all clients.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkEnemy : NetworkBehaviour
{
    public const float DefaultMaxHp = 50f;
    public const float DefaultMoveSpeed = 3.2f;
    public const float DefaultAttackRange = 1.8f;
    public const float DefaultAttackDamage = 8f;
    public const float DefaultAttackCooldown = 0.9f;
    public const float DetectRange = 18f;

    public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
        DefaultMaxHp, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> MaxHp = new NetworkVariable<float>(
        DefaultMaxHp, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> KindId = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ContentLevel = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MapIndex = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsBoss = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    NetworkTransform _netTransform;
    float _moveSpeed = DefaultMoveSpeed;
    float _attackDamage = DefaultAttackDamage;
    float _attackRange = DefaultAttackRange;
    float _attackCooldown = DefaultAttackCooldown;
    float _leashRadius = 9f;
    Vector3 _homeCenter;
    Vector3 _spawnPos;
    string _spawnName = "NetworkEnemy";
    bool _allowRespawn = true;
    float _nextAttackTime;
    bool _despawning;
    bool _visualBuilt;
    bool _pendingConfigure;
    int _pendingKind;
    int _pendingLevel;
    int _pendingMap;
    bool _pendingBoss;
    float _pendingMove;
    float _pendingLeash;
    Vector3 _pendingHome;
    Vector3 _pendingSpawnPos;
    string _pendingName;
    bool _pendingAllowRespawn = true;
    bool _adventureEnemy;
    ulong _lastAttackerClientId = ulong.MaxValue;

    public bool IsAlive => IsSpawned && !_despawning && CurrentHp.Value > 0f;

    void Awake()
    {
        _netTransform = GetComponent<NetworkTransform>();
    }

    public void ConfigureAdventureBeforeSpawn(
        EnemyKind kind,
        int contentLevel,
        bool isBoss,
        int mapIndex,
        Vector3 homeCenter,
        float moveSpeed,
        float leashRadius,
        Vector3 spawnPos,
        string spawnName,
        bool allowRespawn = true)
    {
        _pendingConfigure = true;
        _adventureEnemy = true;
        _pendingKind = (int)kind;
        _pendingLevel = contentLevel;
        _pendingBoss = isBoss;
        _pendingMap = mapIndex;
        _pendingHome = homeCenter;
        _pendingMove = moveSpeed;
        _pendingLeash = leashRadius;
        _pendingSpawnPos = spawnPos;
        _pendingName = spawnName;
        _pendingAllowRespawn = allowRespawn;
        _homeCenter = homeCenter;
        _spawnPos = spawnPos;
        _spawnName = spawnName;
        _moveSpeed = moveSpeed;
        _leashRadius = leashRadius;
        _allowRespawn = allowRespawn;
    }

    public override void OnNetworkSpawn()
    {
        if (_netTransform != null)
            _netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;

        if (IsServer && _pendingConfigure)
            ApplyPendingConfigServer();

        KindId.OnValueChanged += OnKindChanged;
        EnsureAdventureVisual();

        if (IsServer && CurrentHp.Value <= 0f)
            CurrentHp.Value = MaxHp.Value > 0f ? MaxHp.Value : DefaultMaxHp;
    }

    public override void OnNetworkDespawn()
    {
        KindId.OnValueChanged -= OnKindChanged;
    }

    void ApplyPendingConfigServer()
    {
        _pendingConfigure = false;
        KindId.Value = _pendingKind;
        ContentLevel.Value = _pendingLevel;
        IsBoss.Value = _pendingBoss;
        MapIndex.Value = _pendingMap;
        _homeCenter = _pendingHome;
        _spawnPos = _pendingSpawnPos;
        _spawnName = string.IsNullOrEmpty(_pendingName) ? gameObject.name : _pendingName;
        _moveSpeed = _pendingMove;
        _leashRadius = _pendingLeash;
        _allowRespawn = _pendingAllowRespawn;
        _adventureEnemy = true;

        float maxHp = ProgressionFormulas.MobMaxHp(_pendingLevel, _pendingBoss);
        MaxHp.Value = maxHp;
        CurrentHp.Value = maxHp;
        _attackDamage = ProgressionFormulas.MobAttack(_pendingLevel, _pendingBoss);
        _attackRange = _pendingBoss ? 2.8f : DefaultAttackRange;
        _attackCooldown = _pendingBoss ? 1.15f : DefaultAttackCooldown;
    }

    void OnKindChanged(int previous, int current)
    {
        if (previous == current) return;
        _visualBuilt = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null) Destroy(child.gameObject);
        }
        EnsureAdventureVisual();
    }

    void Update()
    {
        if (!IsSpawned || !IsServer || _despawning) return;
        if (CurrentHp.Value <= 0f)
        {
            BeginDespawn();
            return;
        }

        var target = FindNearestLivingPlayer();
        if (target == null) return;

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (_adventureEnemy && _leashRadius > 0.5f)
        {
            float homeDist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_homeCenter.x, 0f, _homeCenter.z));
            if (homeDist > _leashRadius && dist > _attackRange)
            {
                Vector3 back = _homeCenter - transform.position;
                back.y = 0f;
                if (back.sqrMagnitude > 0.01f)
                {
                    transform.position += back.normalized * _moveSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(back.normalized, Vector3.up),
                        8f * Time.deltaTime);
                }
                return;
            }
        }

        if (dist > DetectRange) return;

        if (dist > 0.05f)
        {
            Vector3 step = to.normalized * _moveSpeed * Time.deltaTime;
            if (step.magnitude > dist - 0.05f)
                step = to.normalized * Mathf.Max(0f, dist - 0.05f);
            transform.position += step;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(to.normalized, Vector3.up),
                    10f * Time.deltaTime);
        }

        if (dist <= _attackRange && Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + _attackCooldown;
            var combat = target.GetComponent<NetworkPlayerCombat>();
            if (combat != null)
                combat.ApplyDamageServer(_attackDamage);
        }
    }

    public void ApplyDamageServer(float amount, ulong attackerClientId)
    {
        if (!IsServer || _despawning) return;
        if (CurrentHp.Value <= 0f) return;

        float dealt = Mathf.Max(0f, amount);
        if (dealt < 0.5f) return;

        _lastAttackerClientId = attackerClientId;
        CurrentHp.Value = Mathf.Max(0f, CurrentHp.Value - dealt);
        ShowDamageClientRpc(dealt, IsBoss.Value);

        if (CurrentHp.Value <= 0f)
            BeginDespawn();
    }

    [ClientRpc]
    void ShowDamageClientRpc(float amount, bool isBoss)
    {
        DamageNumbers.Spawn(transform.position, amount, false, isBoss);
    }

    void BeginDespawn()
    {
        if (!IsServer || _despawning) return;
        _despawning = true;

        GrantRewardsToKillerServer();
        OnEnemyKilledClientRpc(IsBoss.Value, MapIndex.Value, ContentLevel.Value, KindId.Value);

        if (_adventureEnemy && _allowRespawn && NetCombatSession.Instance != null)
        {
            NetCombatSession.Instance.ScheduleRespawn(new NetCombatSession.EnemySpawnSpec
            {
                name = _spawnName,
                pos = _spawnPos,
                zoneCenter = _homeCenter,
                level = ContentLevel.Value,
                isBoss = IsBoss.Value,
                kind = (EnemyKind)KindId.Value,
                mapIndex = MapIndex.Value,
                moveSpeed = _moveSpeed,
                leashRadius = _leashRadius,
                allowRespawn = true
            });
        }

        var no = NetworkObject;
        if (no != null && no.IsSpawned)
            no.Despawn(true);
    }

    void GrantRewardsToKillerServer()
    {
        int xp = ProgressionFormulas.MobXpReward(ContentLevel.Value, IsBoss.Value);
        string lootJson1 = "";
        string lootJson2 = "";

        bool drop = IsBoss.Value || Random.value < 0.55f;
        if (drop)
        {
            var item = ItemFactory.RollDrop(ContentLevel.Value, IsBoss.Value);
            var save = ItemSaveData.From(item);
            if (save != null) lootJson1 = JsonUtility.ToJson(save);
            if (IsBoss.Value && Random.value < 0.65f)
            {
                var item2 = ItemFactory.RollDrop(ContentLevel.Value, true);
                var save2 = ItemSaveData.From(item2);
                if (save2 != null) lootJson2 = JsonUtility.ToJson(save2);
            }
        }

        bool bossPet = false;
        int petKind = 0;
        int petLv = 1;
        if (IsBoss.Value && Random.value < 0.55f)
        {
            bossPet = true;
            int world = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
            petKind = (int)PlayerPetController.RollBossPetKind(world);
            petLv = Mathf.Clamp(
                ProgressionFormulas.ContentToItemLevel(ContentLevel.Value) / 2,
                1, ProgressionFormulas.MaxPetLevel);
        }

        var killer = FindCombatByClientId(_lastAttackerClientId);
        if (killer != null)
        {
            killer.DeliverKillRewardsServer(xp, lootJson1, lootJson2, bossPet, petKind, petLv, transform.position, IsBoss.Value);
            return;
        }

        // Fallback: host local inventory if attacker unknown (e.g. edge cases).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            var hostCombat = FindCombatByClientId(NetworkManager.ServerClientId);
            hostCombat?.DeliverKillRewardsServer(xp, lootJson1, lootJson2, bossPet, petKind, petLv, transform.position, IsBoss.Value);
        }
    }

    static NetworkPlayerCombat FindCombatByClientId(ulong clientId)
    {
        if (clientId == ulong.MaxValue) return null;
        var combats = FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            var c = combats[i];
            if (c != null && c.IsSpawned && c.OwnerClientId == clientId)
                return c;
        }
        return null;
    }

    [ClientRpc]
    void OnEnemyKilledClientRpc(bool isBoss, int mapIndex, int contentLevel, int kindId)
    {
        if (AdventureManager.Instance == null) return;

        var tmp = new GameObject("NetKillNotify");
        var stats = tmp.AddComponent<CharacterStats>();
        stats.isPlayer = false;
        stats.isBoss = isBoss;
        stats.mapIndex = mapIndex;
        stats.contentLevel = contentLevel;
        stats.enemyKind = (EnemyKind)kindId;
        stats.isDead = true;
        AdventureManager.Instance.NotifyEnemyDied(stats);
        Destroy(tmp);
    }

    NetworkPlayer FindNearestLivingPlayer()
    {
        NetworkPlayer best = null;
        float bestSq = float.MaxValue;
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.IsSpawned) continue;
            var combat = p.GetComponent<NetworkPlayerCombat>();
            if (combat != null && !combat.IsAlive) continue;

            float sq = (p.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = p;
            }
        }
        return best;
    }

    void EnsureAdventureVisual()
    {
        if (_visualBuilt) return;

        var kind = (EnemyKind)KindId.Value;
        var body = transform.Find("Body");
        if (body != null)
            Destroy(body.gameObject);

        float scale = ScaleFor(kind, IsBoss.Value);
        EnemyVisuals.Build(transform, kind, scale);

        var anim = GetComponent<ProceduralBodyAnim>();
        if (anim == null) anim = gameObject.AddComponent<ProceduralBodyAnim>();
        anim.style = EnemyAI.StyleForKind(kind);
        anim.Recache();

        _visualBuilt = true;
    }

    public static float ScaleFor(EnemyKind kind, bool isBoss)
    {
        float scale = isBoss ? 1.75f : 1f;
        if (kind == EnemyKind.WorldBoss) scale = 1.9f;
        else if (kind == EnemyKind.Golem || kind == EnemyKind.Treant || kind == EnemyKind.Demon || kind == EnemyKind.Yeti)
            scale = isBoss ? 1.6f : 1.15f;
        else if (kind == EnemyKind.Imp || kind == EnemyKind.Bat || kind == EnemyKind.Beetle ||
                 kind == EnemyKind.Mosquito || kind == EnemyKind.StormSprite)
            scale = 0.9f;
        return scale;
    }
}
