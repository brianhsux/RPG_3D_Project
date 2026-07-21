using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6.5f;
    public float gravity = -20f;
    public float rotateSpeed = 12f;
    [Tooltip("Jump apex height in meters.")]
    public float jumpHeight = 1.75f;

    CharacterController _controller;
    CharacterStats _stats;
    PlayerWeaponVisual _weaponVisual;
    ProceduralBodyAnim _bodyAnim;
    Vector3 _velocity;
    float _attackAnimT;
    bool _jumpQueued;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<CharacterStats>();
        _stats.isPlayer = true;
        _stats.displayName = "Hero";
        _stats.attackRange = ProgressionFormulas.PlayerBaseAttackRange;
        _stats.attackCooldown = ProgressionFormulas.PlayerBaseAttackCooldown;
        _stats.attackSpeed = 1f;

        if (GetComponent<PlayerInventory>() == null)
            gameObject.AddComponent<PlayerInventory>();
        if (GetComponent<PlayerProgression>() == null)
            gameObject.AddComponent<PlayerProgression>();
        if (GetComponent<PlayerPetController>() == null)
            gameObject.AddComponent<PlayerPetController>();
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

        if (WasJumpPressed())
            _jumpQueued = true;

        if (input.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            _controller.Move(input * moveSpeed * Time.deltaTime);
        }

        bool grounded = _controller.isGrounded;
        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (_jumpQueued && grounded)
        {
            // v = sqrt(2 * g * h); gravity is negative
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
            if (_bodyAnim != null) _bodyAnim.PlayJump();
        }
        else if (grounded)
        {
            _jumpQueued = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);

        if (_bodyAnim != null)
        {
            _bodyAnim.SetMoveAmount(input.sqrMagnitude > 0.01f ? 1f : 0f);
            _bodyAnim.SetGrounded(grounded);
        }

        // Auto-attack in range; click still forces an attack attempt
        if (HasEnemyInAttackRange() || WasAttackPressed())
            TryAttack();

        TryInteractInput();

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

    static bool WasJumpPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            return true;

        return false;
    }

    static bool WasAttackPressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
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
            if (AdventureManager.Instance.IsInVillage)
                return MapRuntimeBuilder.Instance.GetVillageSpawn();
            int map = Mathf.Clamp(AdventureManager.Instance.currentMap, 1, 4);
            return MapRuntimeBuilder.Instance.GetTileSpawn(map);
        }
        return new Vector3(0f, 0.5f, 0f);
    }

    void TryInteractInput()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        var altar = ChallengeAltar.FindNearest(transform.position, ChallengeAltar.InteractRange);
        if (altar != null)
        {
            altar.TryInteract();
            return;
        }

        var ore = OreNode.FindNearest(transform.position, 3.5f);
        if (ore == null) return;
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        ore.TryMine(inv);
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
        float anim = Mathf.Clamp(0.32f / Mathf.Max(0.7f, _stats.attackSpeed), 0.16f, 0.35f);
        _attackAnimT = anim;
        if (_bodyAnim != null) _bodyAnim.PlayAttack(anim + 0.05f);
        _stats.DealAttack(best);
    }

    void OnGUI()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;

        RPGUI.Ensure();

        float panelW = 400f;
        float panelH = 228f;
        Rect panel = new Rect(16f, 16f, panelW, panelH);
        RPGUI.DrawPanel(panel, RPGUI.PanelBg);

        var prog = PlayerProgression.Instance;
        string lvText = prog != null ? $"Hero  Lv {prog.level}" : "Hero";
        RPGUI.DrawText(new Rect(panel.x + 14f, panel.y + 10f, 220f, RPGUI.LabelH), lvText, RPGUI.Label);

        RPGUI.DrawText(new Rect(panel.x + 220f, panel.y + 10f, 166f, RPGUI.HintH),
            $"ATK {_stats.attackDamage:0}  DEF {_stats.defense:0}", RPGUI.Hint);
        RPGUI.DrawText(new Rect(panel.x + 220f, panel.y + 28f, 166f, RPGUI.FieldTagH),
            $"SPD {_stats.attackSpeed:0.00}x  RNG {_stats.attackRange:0.0}", RPGUI.FieldTag);

        float barY = panel.y + 58f;
        float elem = _stats.fireBonus + _stats.iceBonus + _stats.poisonBonus;
        bool showGems = _stats.critChance > 0.01f || elem > 0.5f || _stats.blockChance > 0.01f
            || _stats.lifesteal > 0.01f || _stats.damageReduction > 0.01f;
        if (showGems)
        {
            string extras = "";
            if (_stats.critChance > 0.01f) extras += $"Crit {_stats.critChance * 100f:0}%  ";
            if (_stats.blockChance > 0.01f) extras += $"Block {_stats.blockChance * 100f:0}%  ";
            if (_stats.damageReduction > 0.01f) extras += $"DR {_stats.damageReduction * 100f:0}%  ";
            if (elem > 0.5f) extras += $"Elem +{elem:0}";
            RPGUI.DrawText(new Rect(panel.x + 14f, panel.y + 48f, panelW - 28f, 16f), extras.Trim(), RPGUI.FieldTag);
            barY = panel.y + 66f;
        }

        RPGUI.DrawBar(
            new Rect(panel.x + 14f, barY, panelW - 28f, 26f),
            _stats.HealthPercent,
            Color.Lerp(new Color(0.85f, 0.18f, 0.18f), new Color(0.22f, 0.82f, 0.35f), _stats.HealthPercent),
            $"HP  {Mathf.CeilToInt(_stats.currentHealth)} / {Mathf.CeilToInt(_stats.maxHealth)}"
        );

        if (prog != null)
        {
            RPGUI.DrawBar(
                new Rect(panel.x + 14f, barY + 32f, panelW - 28f, 22f),
                prog.XpPercent,
                new Color(0.3f, 0.55f, 1f),
                $"EXP  {prog.xp} / {prog.xpToNext}"
            );
        }

        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            float eqY = barY + (prog != null ? 60f : 34f);
            DrawHudEquip(panel.x + 14f, eqY, 180f, "WPN", inv.equippedWeapon);
            DrawHudEquip(panel.x + 206f, eqY, 180f, "ARM", inv.equippedArmor);
        }

        var petCtrl = PlayerPetController.Instance;
        if (petCtrl != null && petCtrl.ActivePet != null)
        {
            var pet = petCtrl.ActivePet;
            RPGUI.DrawText(new Rect(16f, panel.yMax + 28f, 400f, RPGUI.FieldTagH),
                $"Pet: {pet.displayName} Lv{pet.level}  (~{PetCatalog.ComputeDamage(pet, _stats.attackDamage):0} dmg)",
                RPGUI.FieldTag);
        }

        RPGUI.DrawText(new Rect(16f, panel.yMax + 6f, 560f, RPGUI.HintH),
            AdventureManager.Instance != null && AdventureManager.Instance.IsInVillage
                ? "WASD move · Space jump · Walk into portals · I bag"
                : "WASD move · Space jump · Attack · E interact · 1-4 travel · I bag",
            RPGUI.Hint);

        var nearAltar = ChallengeAltar.FindNearest(transform.position, ChallengeAltar.InteractRange);
        if (nearAltar != null && !nearAltar.IsRunning)
        {
            Rect tip = new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.72f, 600f, 40f);
            RPGUI.DrawPanel(tip, new Color(0.22f, 0.1f, 0.06f, 0.92f));
            string msg;
            if (nearAltar.IsSelecting)
                msg = "Choose a difficulty (Esc to cancel)";
            else if (nearAltar.StatusText.StartsWith("Cooldown") || nearAltar.StatusText.StartsWith("Failed") || nearAltar.StatusText.StartsWith("Cleared"))
                msg = nearAltar.StatusText;
            else
                msg = "Press E — Timed Challenge (pick difficulty)";
            RPGUI.DrawText(tip, msg, RPGUI.Hint);
        }
        else
        {
            var nearOre = OreNode.FindNearest(transform.position, 3.5f);
            if (nearOre != null && !nearOre.IsDepleted)
            {
                Rect tip = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.72f, 480f, 40f);
                RPGUI.DrawPanel(tip, new Color(0.08f, 0.14f, 0.2f, 0.9f));
                RPGUI.DrawText(tip, $"Press E to mine — {nearOre.HitsLeft} hits left", RPGUI.Hint);
            }
        }

        if (_stats.isDead)
        {
            Rect banner = new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.5f - 40f, 600f, RPGUI.BannerH);
            RPGUI.DrawPanel(banner, new Color(0.42f, 0.06f, 0.06f, 0.94f));
            RPGUI.DrawText(banner, "You fell! Press R to respawn", RPGUI.Banner);
        }
    }

    static void DrawHudEquip(float x, float y, float width, string slotTitle, ItemInstance item)
    {
        Rect box = new Rect(x, y, width, 64f);
        RPGUI.DrawPanel(box, RPGUI.PanelBgSoft, false);

        if (item == null)
        {
            RPGUI.DrawText(new Rect(x + 10f, y + 16f, width - 20f, RPGUI.HintH), $"{slotTitle} (empty)", RPGUI.Hint);
            return;
        }

        RPGUI.Fill(new Rect(x, y, 3f, 64f), item.RarityColor);
        RPGUI.DrawItemIcon(new Rect(x + 8f, y + 8f, 48f, 48f), item);
        GUI.color = item.RarityColor;
        RPGUI.DrawText(new Rect(x + 64f, y + 6f, width - 72f, RPGUI.HintH), item.displayName, RPGUI.Hint);
        GUI.color = Color.white;
        string stat = item.slot == ItemSlot.Weapon
            ? $"ATK {item.attack:0}  SPD +{item.attackSpeedBonus * 100f:0}%  RNG +{item.attackRangeBonus:0.0}"
            : $"Lv{item.itemLevel}  DEF {item.defense:0}";
        RPGUI.DrawText(new Rect(x + 64f, y + 34f, width - 72f, RPGUI.FieldTagH), stat, RPGUI.FieldTag);
    }
}
