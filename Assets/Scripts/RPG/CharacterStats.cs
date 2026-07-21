using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public string displayName = "Character";
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float attackDamage = 15f;
    public float baseAttack = 15f;
    public float defense;
    public float attackRange = 2.2f;
    public float attackCooldown = 0.8f;
    public bool isPlayer;
    public bool isDead;

    public int contentLevel = 1;
    public bool isBoss;
    public EnemyKind enemyKind = EnemyKind.Slime;
    public int mapIndex = 1;

    public System.Action<CharacterStats> Died;

    float _lastAttackTime = -999f;
    Color[] _originalColors;
    Renderer[] _renderers;

    void Awake()
    {
        currentHealth = maxHealth;
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
        CacheColors();
    }

    void CacheColors()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                _originalColors[i] = _renderers[i].sharedMaterial.GetColor("_BaseColor");
            else if (_renderers[i].sharedMaterial != null)
                _originalColors[i] = _renderers[i].sharedMaterial.color;
            else
                _originalColors[i] = Color.white;
        }
    }

    public void ConfigureAsEnemy(int contentLevel, bool isBoss, EnemyKind kind)
    {
        this.contentLevel = contentLevel;
        this.isBoss = isBoss;
        this.enemyKind = kind;
        isPlayer = false;

        maxHealth = ProgressionFormulas.MobMaxHp(contentLevel, isBoss);
        currentHealth = maxHealth;
        attackDamage = ProgressionFormulas.MobAttack(contentLevel, isBoss);
        baseAttack = attackDamage;
        defense = isBoss ? contentLevel * 2.5f : contentLevel * 0.8f;
        displayName = ProgressionFormulas.MobDisplayName(contentLevel, isBoss, kind);
        attackRange = isBoss ? 2.8f : 2.0f;
        attackCooldown = isBoss ? 1.15f : 0.95f;
    }

    public bool CanAttack()
    {
        return !isDead && Time.time >= _lastAttackTime + attackCooldown;
    }

    public void MarkAttack()
    {
        _lastAttackTime = Time.time;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        float finalDmg = isPlayer
            ? ProgressionFormulas.MitigateDamage(amount, defense)
            : amount;

        currentHealth = Mathf.Max(0f, currentHealth - finalDmg);

        DamageNumbers.Spawn(transform.position, finalDmg, isPlayer, isBoss);

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        isDead = true;
        currentHealth = 0f;

        if (_renderers == null) CacheColors();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.material.color = Color.gray;
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", Color.gray);
        }

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (!isPlayer)
        {
            TryDropLoot();
            GrantXpToPlayer();
        }

        Died?.Invoke(this);
    }

    void GrantXpToPlayer()
    {
        if (PlayerProgression.Instance == null) return;
        int xp = ProgressionFormulas.MobXpReward(contentLevel, isBoss);
        PlayerProgression.Instance.GainXp(xp);
    }

    void TryDropLoot()
    {
        // Boss always drops; trash ~55%
        bool drop = isBoss || Random.value < 0.55f;
        if (!drop) return;

        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        var item = ItemFactory.RollDrop(contentLevel, isBoss);
        inv.AddItem(item);

        // Boss guaranteed second drop sometimes
        if (isBoss && Random.value < 0.65f)
            inv.AddItem(ItemFactory.RollDrop(contentLevel, true));
    }

    public void Respawn(Vector3 position)
    {
        isDead = false;
        currentHealth = maxHealth;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        if (cc != null) cc.enabled = true;

        if (isPlayer)
        {
            var weaponFx = GetComponent<PlayerWeaponVisual>();
            var armorFx = GetComponent<PlayerArmorVisual>();
            if (weaponFx != null) weaponFx.ForceRebuild();
            if (armorFx != null) armorFx.ForceRebuild();
            var bodyAnim = GetComponent<ProceduralBodyAnim>();
            if (bodyAnim != null)
            {
                bodyAnim.SetDead(false);
                bodyAnim.Recache();
            }
        }
        else
        {
            if (_renderers == null || _originalColors == null) CacheColors();
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].material.color = _originalColors[i];
                if (_renderers[i].material.HasProperty("_BaseColor"))
                    _renderers[i].material.SetColor("_BaseColor", _originalColors[i]);
            }
        }

        gameObject.SetActive(true);
    }

    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
}
