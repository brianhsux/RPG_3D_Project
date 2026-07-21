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
    /// <summary>Attack rate multiplier. 1.0 = base; soft-capped ~1.75 on player.</summary>
    public float attackSpeed = 1f;
    public bool isPlayer;
    public bool isDead;

    // Gem-driven combat mods (player)
    public float critChance;
    public float critDamageBonus = 0.5f; // added to 1.0 => 1.5x default crit
    public float blockChance;
    public float damageReduction;
    public float reflectPercent;
    public float fireBonus;
    public float iceBonus;
    public float poisonBonus;
    public float lifesteal;

    public int contentLevel = 1;
    public bool isBoss;
    public bool isChallengeEnemy;
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
        attackSpeed = isBoss ? 0.9f : 1f;
    }

    public float EffectiveAttackCooldown
    {
        get
        {
            float spd = Mathf.Max(0.5f, attackSpeed);
            return attackCooldown / spd;
        }
    }

    public bool CanAttack()
    {
        return !isDead && Time.time >= _lastAttackTime + EffectiveAttackCooldown;
    }

    public void MarkAttack()
    {
        _lastAttackTime = Time.time;
    }

    /// <summary>Player (or any) attack with gem crit / elemental / lifesteal.</summary>
    public float DealAttack(CharacterStats target)
    {
        if (target == null || target.isDead || isDead) return 0f;

        float dmg = attackDamage;
        bool crit = Random.value < Mathf.Clamp01(critChance);
        if (crit)
            dmg *= 1f + Mathf.Max(0.35f, critDamageBonus);

        float total = dmg + fireBonus + iceBonus + poisonBonus;
        float dealt = target.TakeDamage(total, this);

        if (lifesteal > 0f && dealt > 0f)
            currentHealth = Mathf.Min(maxHealth, currentHealth + dealt * lifesteal);

        return dealt;
    }

    public float TakeDamage(float amount, CharacterStats source = null)
    {
        if (isDead) return 0f;

        float incoming = Mathf.Max(0f, amount);

        if (isPlayer)
        {
            if (blockChance > 0f && Random.value < blockChance)
                incoming *= 0.2f; // Block absorbs most damage

            incoming *= 1f - Mathf.Clamp01(damageReduction);
            incoming = ProgressionFormulas.MitigateDamage(incoming, defense);
        }

        float finalDmg = Mathf.Max(0f, incoming);
        currentHealth = Mathf.Max(0f, currentHealth - finalDmg);

        DamageNumbers.Spawn(transform.position, finalDmg, isPlayer, isBoss);

        if (isPlayer && reflectPercent > 0f && source != null && !source.isDead && finalDmg > 0.5f)
        {
            float reflected = finalDmg * reflectPercent;
            source.ApplyRawDamage(reflected);
            DamageNumbers.Spawn(source.transform.position, reflected, false, source.isBoss);
        }

        if (currentHealth <= 0f)
            Die();

        return finalDmg;
    }

    public void ApplyRawDamage(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
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
            if (PlayerPetController.Instance != null)
            {
                PlayerPetController.Instance.OnEnemyKilledNearby(transform.position, isBoss);
                if (isBoss && Random.value < 0.55f)
                {
                    int world = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
                    var kind = PlayerPetController.RollBossPetKind(world);
                    int lv = Mathf.Clamp(contentLevel / 2, 1, 20);
                    PlayerPetController.Instance.TryAddPet(kind, lv, out _);
                    if (AccountManager.Instance != null)
                        AccountManager.Instance.SaveActive(silent: true);
                }
            }
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
