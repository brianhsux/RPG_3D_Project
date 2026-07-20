using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6.5f;
    public float gravity = -20f;
    public float rotateSpeed = 12f;

    CharacterController _controller;
    CharacterStats _stats;
    PlayerWeaponVisual _weaponVisual;
    ProceduralBodyAnim _bodyAnim;
    Vector3 _velocity;
    float _attackAnimT;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<CharacterStats>();
        _stats.isPlayer = true;
        _stats.displayName = "Hero";
        _stats.attackRange = 2.6f;
        _stats.attackCooldown = 0.5f;

        if (GetComponent<PlayerInventory>() == null)
            gameObject.AddComponent<PlayerInventory>();
        if (GetComponent<PlayerProgression>() == null)
            gameObject.AddComponent<PlayerProgression>();
        _weaponVisual = GetComponent<PlayerWeaponVisual>();
        if (_weaponVisual == null)
            _weaponVisual = gameObject.AddComponent<PlayerWeaponVisual>();
        if (GetComponent<PlayerArmorVisual>() == null)
            gameObject.AddComponent<PlayerArmorVisual>();
        _bodyAnim = GetComponent<ProceduralBodyAnim>();
        if (_bodyAnim == null)
            _bodyAnim = gameObject.AddComponent<ProceduralBodyAnim>();
        _bodyAnim.style = ProceduralBodyAnim.BodyStyle.Humanoid;
    }

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady)
            return;

        if (_stats.isDead)
        {
            if (_bodyAnim != null) _bodyAnim.SetDead(true);
            HandleRespawnInput();
            return;
        }

        if (_bodyAnim != null) _bodyAnim.SetDead(false);

        var inv = PlayerInventory.Instance;
        if (inv != null && inv.showPanel)
        {
            if (_bodyAnim != null) _bodyAnim.SetMoveAmount(0f);
            return;
        }

        Vector2 move = ReadMoveInput();
        Vector3 input = new Vector3(move.x, 0f, move.y);
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            _controller.Move(input * moveSpeed * Time.deltaTime);
        }

        if (_bodyAnim != null)
            _bodyAnim.SetMoveAmount(input.sqrMagnitude > 0.01f ? 1f : 0f);

        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);

        // Auto-attack when an enemy is in range (Space / click still works)
        if (HasEnemyInAttackRange() || WasAttackPressed())
            TryAttack();

        if (_attackAnimT > 0f)
            _attackAnimT -= Time.deltaTime;

        if (_weaponVisual == null)
            _weaponVisual = GetComponent<PlayerWeaponVisual>();
        if (_weaponVisual != null)
            _weaponVisual.SetAttackSwing(_attackAnimT);
    }

    static Vector2 ReadMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }

    static bool WasAttackPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        return false;
    }

    bool HasEnemyInAttackRange()
    {
        float range = _stats.attackRange;
        var enemies = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == _stats || enemy.isPlayer || enemy.isDead) continue;
            if (Vector3.Distance(transform.position, enemy.transform.position) <= range)
                return true;
        }
        return false;
    }

    void HandleRespawnInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.rKey.wasPressedThisFrame) return;

        Vector3 spawn = GetSafeRespawnPoint();
        _stats.Respawn(spawn);
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ApplyEquipmentStats(true);

        // Ensure visuals refresh even if Respawn ran before components were ready.
        var weaponFx = GetComponent<PlayerWeaponVisual>();
        var armorFx = GetComponent<PlayerArmorVisual>();
        if (weaponFx != null) weaponFx.ForceRebuild();
        if (armorFx != null) armorFx.ForceRebuild();
        if (_bodyAnim != null)
        {
            _bodyAnim.SetDead(false);
            _bodyAnim.Recache();
        }
    }

    static Vector3 GetSafeRespawnPoint()
    {
        if (AdventureManager.Instance != null && MapRuntimeBuilder.Instance != null)
        {
            int map = Mathf.Clamp(AdventureManager.Instance.currentMap, 1, 4);
            return MapRuntimeBuilder.Instance.GetTileSpawn(map);
        }
        return new Vector3(0f, 0.5f, 0f);
    }

    void TryAttack()
    {
        if (!_stats.CanAttack()) return;

        CharacterStats best = null;
        float bestDist = float.MaxValue;
        var enemies = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == _stats || enemy.isPlayer || enemy.isDead) continue;
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist <= _stats.attackRange && dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        if (best == null) return;

        // Face the target for clearer auto-combat
        Vector3 toEnemy = best.transform.position - transform.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toEnemy), 0.65f);

        _stats.MarkAttack();
        _attackAnimT = 0.28f;
        if (_bodyAnim != null) _bodyAnim.PlayAttack(0.35f);
        best.TakeDamage(_stats.attackDamage);
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;

        RPGUI.Ensure();

        float panelW = 560f;
        float panelH = 360f;
        Rect panel = new Rect(20f, 20f, panelW, panelH);
        RPGUI.DrawPanel(panel, new Color(0.05f, 0.08f, 0.12f, 0.9f));

        var prog = PlayerProgression.Instance;
        string lvText = prog != null ? $"英雄  Lv {prog.level}" : "英雄 HP";
        GUI.Label(new Rect(36f, 28f, 500f, RPGUI.TitleH), lvText, RPGUI.Title);

        RPGUI.DrawBar(
            new Rect(36f, 90f, 520f, 44f),
            _stats.HealthPercent,
            Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.2f, 0.85f, 0.3f), _stats.HealthPercent),
            $"HP  {Mathf.CeilToInt(_stats.currentHealth)} / {Mathf.CeilToInt(_stats.maxHealth)}"
        );

        if (prog != null)
        {
            RPGUI.DrawBar(
                new Rect(36f, 144f, 520f, 40f),
                prog.XpPercent,
                new Color(0.3f, 0.55f, 1f),
                $"EXP  {prog.xp} / {prog.xpToNext}"
            );
        }

        GUI.Label(new Rect(36f, 196f, 520f, RPGUI.HintH),
            $"攻擊 {_stats.attackDamage:0}    防禦 {_stats.defense:0}    按 I 開背包", RPGUI.Hint);

        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            DrawHudEquip(36f, 244f, 250f, "武器", inv.equippedWeapon);
            DrawHudEquip(306f, 244f, 250f, "防具", inv.equippedArmor);
        }

        GUI.Label(new Rect(20f, 400f, 980f, RPGUI.HintH),
            "WASD 移動｜靠近自動攻擊｜1-4 瞬移區域｜I 背包", RPGUI.Hint);

        if (_stats.isDead)
        {
            Rect banner = new Rect(Screen.width * 0.5f - 340f, Screen.height * 0.5f - 50f, 680f, RPGUI.BannerH + 20f);
            RPGUI.DrawPanel(banner, new Color(0.45f, 0.05f, 0.05f, 0.92f));
            GUI.Label(banner, "你倒下了！按 R 重生", RPGUI.Banner);
        }
    }

    static void DrawHudEquip(float x, float y, float width, string slotTitle, ItemInstance item)
    {
        Rect box = new Rect(x, y, width, 100f);
        RPGUI.DrawPanel(box, new Color(0.1f, 0.12f, 0.18f, 0.95f));

        if (item == null)
        {
            GUI.Label(new Rect(x + 12f, y + 12f, width - 24f, RPGUI.FieldTagH), slotTitle, RPGUI.FieldTag);
            GUI.Label(new Rect(x + 12f, y + 50f, width - 24f, RPGUI.HintH), "（空）", RPGUI.Hint);
            return;
        }

        RPGUI.DrawItemIcon(new Rect(x + 10f, y + 16f, 68f, 68f), item);

        GUI.Label(new Rect(x + 90f, y + 8f, width - 102f, RPGUI.FieldTagH), slotTitle, RPGUI.FieldTag);
        GUI.color = item.RarityColor;
        GUI.Label(new Rect(x + 90f, y + 40f, width - 102f, RPGUI.HintH), item.displayName, RPGUI.Hint);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 90f, y + 72f, width - 102f, RPGUI.FieldTagH), $"Lv {item.itemLevel}", RPGUI.FieldTag);
    }
}
