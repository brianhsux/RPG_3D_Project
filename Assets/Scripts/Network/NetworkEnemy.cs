using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Host-authoritative test enemy: HP sync, simple chase, despawn on death.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkEnemy : NetworkBehaviour
{
    public const float DefaultMaxHp = 50f;
    public const float MoveSpeed = 3.2f;
    public const float AttackRange = 1.8f;
    public const float AttackDamage = 8f;
    public const float AttackCooldown = 0.9f;
    public const float DetectRange = 18f;

    public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
        DefaultMaxHp,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> MaxHp = new NetworkVariable<float>(
        DefaultMaxHp,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] Color aliveColor = new Color(0.75f, 0.25f, 0.28f);
    [SerializeField] Color hurtColor = new Color(0.95f, 0.55f, 0.2f);

    NetworkTransform _netTransform;
    Renderer _bodyRenderer;
    float _nextAttackTime;
    bool _despawning;

    public bool IsAlive => IsSpawned && !_despawning && CurrentHp.Value > 0f;

    void Awake()
    {
        _netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        if (_netTransform != null)
            _netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;

        EnsureVisual();
        CurrentHp.OnValueChanged += OnHpChanged;
        ApplyHpTint();

        if (IsServer && CurrentHp.Value <= 0f)
            CurrentHp.Value = MaxHp.Value > 0f ? MaxHp.Value : DefaultMaxHp;
    }

    public override void OnNetworkDespawn()
    {
        CurrentHp.OnValueChanged -= OnHpChanged;
    }

    void OnHpChanged(float previous, float current)
    {
        ApplyHpTint();
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
        if (dist > DetectRange) return;

        if (dist > 0.05f)
        {
            Vector3 step = to.normalized * MoveSpeed * Time.deltaTime;
            if (step.magnitude > dist - 0.05f)
                step = to.normalized * Mathf.Max(0f, dist - 0.05f);
            transform.position += step;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(to.normalized, Vector3.up),
                    10f * Time.deltaTime);
        }

        if (dist <= AttackRange && Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + AttackCooldown;
            var combat = target.GetComponent<NetworkPlayerCombat>();
            if (combat != null)
                combat.ApplyDamageServer(AttackDamage);
        }
    }

    public void ApplyDamageServer(float amount)
    {
        if (!IsServer || _despawning) return;
        if (CurrentHp.Value <= 0f) return;

        CurrentHp.Value = Mathf.Max(0f, CurrentHp.Value - Mathf.Max(0f, amount));
        if (CurrentHp.Value <= 0f)
            BeginDespawn();
    }

    void BeginDespawn()
    {
        if (!IsServer || _despawning) return;
        _despawning = true;
        var no = NetworkObject;
        if (no != null && no.IsSpawned)
            no.Despawn(true);
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

    void EnsureVisual()
    {
        var body = transform.Find("Body");
        if (body == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Body";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            go.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            body = go.transform;
        }
        _bodyRenderer = body.GetComponent<Renderer>();
    }

    void ApplyHpTint()
    {
        if (_bodyRenderer == null) return;
        float pct = MaxHp.Value > 0.01f ? Mathf.Clamp01(CurrentHp.Value / MaxHp.Value) : 0f;
        Color c = Color.Lerp(hurtColor, aliveColor, pct);
        var mat = _bodyRenderer.material;
        mat.color = c;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
    }
}
