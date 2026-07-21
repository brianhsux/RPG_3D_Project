using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 2.8f;
    public float detectRange = 16f;
    public float gravity = -20f;
    public string enemyType = "Slime";
    public bool disableAutoRespawn;
    public Vector3 homeCenter;
    public float leashRadius = 10f;

    CharacterController _controller;
    CharacterStats _stats;
    ProceduralBodyAnim _bodyAnim;
    Transform _player;
    Vector3 _velocity;
    Vector3 _spawnPosition;
    bool _wasMoving;

    public Vector3 SpawnPosition => _spawnPosition;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<CharacterStats>();
        _bodyAnim = GetComponent<ProceduralBodyAnim>();
        if (_bodyAnim == null)
            _bodyAnim = gameObject.AddComponent<ProceduralBodyAnim>();

        _spawnPosition = transform.position;
        if (homeCenter == Vector3.zero)
            homeCenter = transform.position;
        if (!_stats.isPlayer && _stats.contentLevel <= 1 && string.IsNullOrEmpty(_stats.displayName))
            ApplyLegacyFromName();

        ApplyAnimStyleFromKind();
    }

    void ApplyAnimStyleFromKind()
    {
        if (_bodyAnim == null) return;
        var kind = _stats != null ? _stats.enemyKind : EnemyKind.Slime;
        _bodyAnim.style = StyleForKind(kind);
        _bodyAnim.Recache();
    }

    public static ProceduralBodyAnim.BodyStyle StyleForKind(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Wolf:
            case EnemyKind.ShadowHound:
            case EnemyKind.Griffon:
            case EnemyKind.SandWorm:
            case EnemyKind.Leech:
                return ProceduralBodyAnim.BodyStyle.Quadruped;
            case EnemyKind.Bat:
            case EnemyKind.Imp:
            case EnemyKind.Demon:
            case EnemyKind.Mosquito:
            case EnemyKind.Harpy:
            case EnemyKind.StormSprite:
                return ProceduralBodyAnim.BodyStyle.Flyer;
            case EnemyKind.Spider:
            case EnemyKind.Beetle:
            case EnemyKind.Scorpion:
                return ProceduralBodyAnim.BodyStyle.Crawler;
            case EnemyKind.Slime:
            case EnemyKind.Mushroom:
            case EnemyKind.LavaSpawn:
            case EnemyKind.Crystal:
            case EnemyKind.Toad:
            case EnemyKind.IceElemental:
            case EnemyKind.VoidEye:
                return ProceduralBodyAnim.BodyStyle.Blob;
            default:
                return ProceduralBodyAnim.BodyStyle.Humanoid;
        }
    }

    void ApplyLegacyFromName()
    {
        string typeKey = enemyType;
        if (gameObject.name.Contains("Goblin")) typeKey = "Goblin";
        else if (gameObject.name.Contains("Skeleton")) typeKey = "Skeleton";
        else if (gameObject.name.Contains("Slime")) typeKey = "Slime";
        enemyType = typeKey;
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _player = player.transform;
        ApplyAnimStyleFromKind();
    }

    void OnEnable()
    {
        if (_stats != null)
            _stats.Died += OnDied;
        if (_bodyAnim != null)
        {
            _bodyAnim.SetDead(false);
            _bodyAnim.Recache();
        }
    }

    void OnDisable()
    {
        if (_stats != null)
            _stats.Died -= OnDied;
    }

    void OnDied(CharacterStats stats)
    {
        if (_bodyAnim != null) _bodyAnim.SetDead(true);

        if (AdventureManager.Instance != null)
            AdventureManager.Instance.NotifyEnemyDied(stats);

        if (GameManager.Instance != null)
            GameManager.Instance.NotifyEnemyDied(this);
    }

    public void SetSpawnPosition(Vector3 pos)
    {
        _spawnPosition = pos;
    }

    void Update()
    {
        if (_stats == null || _stats.isDead)
        {
            if (_bodyAnim != null) _bodyAnim.SetMoveAmount(0f);
            return;
        }
        // Die() disables CharacterController; never Move while inactive.
        if (_controller == null || !_controller.enabled)
            return;

        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            else return;
        }

        float dist = Vector3.Distance(transform.position, _player.position);
        float homeDist = Vector3.Distance(transform.position, homeCenter);
        _wasMoving = false;

        if (homeDist > leashRadius)
        {
            Vector3 back = homeCenter - transform.position;
            back.y = 0f;
            if (back.sqrMagnitude > 0.01f)
            {
                back.Normalize();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(back), 8f * Time.deltaTime);
                _controller.Move(back * moveSpeed * 1.2f * Time.deltaTime);
                _wasMoving = true;
            }
        }
        else if (dist <= detectRange)
        {
            Vector3 dir = (_player.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                dir.Normalize();
                Vector3 next = transform.position + dir * moveSpeed * Time.deltaTime;
                if (Vector3.Distance(next, homeCenter) <= leashRadius)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
                    if (dist > _stats.attackRange * 0.85f)
                    {
                        _controller.Move(dir * moveSpeed * Time.deltaTime);
                        _wasMoving = true;
                    }
                }
            }

            if (dist <= _stats.attackRange && _stats.CanAttack())
            {
                _stats.MarkAttack();
                if (_bodyAnim != null)
                    _bodyAnim.PlayAttack(_stats.isBoss ? 0.45f : 0.35f);
                var playerStats = _player.GetComponent<CharacterStats>();
                if (playerStats != null)
                    playerStats.TakeDamage(_stats.attackDamage, _stats);
                // Reflect / on-hit can kill this enemy mid-Update and disable the controller.
                if (_stats.isDead || !_controller.enabled)
                    return;
            }
        }

        if (_bodyAnim != null)
            _bodyAnim.SetMoveAmount(_wasMoving ? 1f : 0f);

        if (!_controller.enabled)
            return;

        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    void OnGUI()
    {
        if (_stats == null || _stats.isDead || Camera.main == null) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        Vector3 world = transform.position + Vector3.up * (_stats.isBoss ? 4.2f : 2.8f);
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        if (screen.z < 0f) return;

        RPGUI.Ensure();

        float barW = _stats.isBoss ? 260f : 180f;
        float barH = _stats.isBoss ? 36f : 30f;
        float x = screen.x - barW * 0.5f;
        float y = Screen.height - screen.y - 20f;

        RPGUI.DrawText(new Rect(x - 10f, y - RPGUI.HintH - 2f, barW + 40f, RPGUI.HintH),
            _stats.displayName, RPGUI.Hint);
        RPGUI.DrawBar(
            new Rect(x, y, barW, barH),
            _stats.HealthPercent,
            Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.25f, 0.9f, 0.3f), _stats.HealthPercent),
            $"{Mathf.CeilToInt(_stats.currentHealth)}"
        );
    }
}
