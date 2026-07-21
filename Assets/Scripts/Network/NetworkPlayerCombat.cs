using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner requests attacks via ServerRpc; Host validates range/cooldown and applies damage.
/// Player HP is Host-authoritative.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerCombat : NetworkBehaviour
{
    public const float DefaultMaxHp = 100f;
    public const float AttackRange = 2.6f;
    public const float AttackCooldown = 0.5f;
    public const float AttackDamage = 12f;
    public const float RespawnDelay = 3f;

    public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
        DefaultMaxHp,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> MaxHp = new NetworkVariable<float>(
        DefaultMaxHp,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    float _clientNextAttackTime;
    float _serverNextAttackTime;
    float _respawnAt = -1f;
    Renderer _bodyRenderer;
    Color _aliveColor = new Color(0.35f, 0.75f, 0.95f);
    Color _deadColor = new Color(0.35f, 0.35f, 0.38f);

    public bool IsAlive => IsSpawned && CurrentHp.Value > 0f;
    public float HealthPercent =>
        MaxHp.Value > 0.01f ? Mathf.Clamp01(CurrentHp.Value / MaxHp.Value) : 0f;

    public override void OnNetworkSpawn()
    {
        CacheBody();
        CurrentHp.OnValueChanged += OnHpChanged;

        if (IsServer)
        {
            if (MaxHp.Value <= 0f) MaxHp.Value = DefaultMaxHp;
            if (CurrentHp.Value <= 0f) CurrentHp.Value = MaxHp.Value;
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

        if (!IsOwner || !IsAlive) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel)
            return;

        bool pressed = WasAttackPressed();
        bool auto = HasEnemyInRange();
        if (!pressed && !auto) return;
        if (Time.time < _clientNextAttackTime) return;

        _clientNextAttackTime = Time.time + AttackCooldown;
        AttackServerRpc();
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        if (!IsAlive) return;
        if (Time.time < _serverNextAttackTime) return;
        _serverNextAttackTime = Time.time + AttackCooldown;

        var enemy = FindNearestEnemyInRange(AttackRange);
        if (enemy == null) return;
        enemy.ApplyDamageServer(AttackDamage);
    }

    /// <summary>Host-only damage entry used by NetworkEnemy AI.</summary>
    public void ApplyDamageServer(float amount)
    {
        if (!IsServer) return;
        if (CurrentHp.Value <= 0f) return;

        CurrentHp.Value = Mathf.Max(0f, CurrentHp.Value - Mathf.Max(0f, amount));
        if (CurrentHp.Value <= 0f)
            _respawnAt = Time.time + RespawnDelay;
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

    bool HasEnemyInRange()
    {
        // Client-side hint for auto-attack; server re-validates.
        var enemies = FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        float rangeSq = AttackRange * AttackRange;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive) continue;
            if ((e.transform.position - transform.position).sqrMagnitude <= rangeSq)
                return true;
        }
        return false;
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
}
