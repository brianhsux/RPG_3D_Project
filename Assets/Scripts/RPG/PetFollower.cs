using UnityEngine;

/// <summary>
/// Visual + follow/combat brain for the active pet.
/// </summary>
public class PetFollower : MonoBehaviour
{
    public PetKind kind;
    public int level = 1;

    Transform _owner;
    CharacterStats _ownerStats;
    float _attackCd;
    float _bob;

    public void Bind(Transform owner, CharacterStats ownerStats, PetInstance pet)
    {
        _owner = owner;
        _ownerStats = ownerStats;
        if (pet != null)
        {
            kind = pet.kind;
            level = pet.level;
        }
        RebuildVisual();
    }

    public void SetLevel(int lv) => level = Mathf.Max(1, lv);

    void RebuildVisual()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        PetVisuals.Build(transform, kind);
    }

    void Update()
    {
        if (_owner == null || AccountManager.Instance == null || !AccountManager.Instance.IsReady)
            return;

        var inv = PlayerInventory.Instance;
        if (inv != null && inv.showPanel) return;

        if (_ownerStats != null && _ownerStats.isDead)
            return;

        float followDist = PetCatalog.FollowDistance(kind);
        Vector3 behind = _owner.position - _owner.forward * followDist + Vector3.up * 0.15f;
        // Slight side offset so pet isn't inside player
        behind += _owner.right * 0.65f;

        Vector3 to = behind - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        float speed = dist > 6f ? 14f : 8.5f;
        if (dist > 0.15f)
        {
            Vector3 step = to.normalized * speed * Time.deltaTime;
            if (step.magnitude > dist) step = to;
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized), 10f * Time.deltaTime);
        }

        _bob += Time.deltaTime * 4f;
        var p = transform.position;
        p.y = _owner.position.y + 0.2f + Mathf.Sin(_bob) * 0.08f;
        if (kind == PetKind.StormHawk)
            p.y += 0.55f;
        transform.position = p;

        _attackCd -= Time.deltaTime;
        TryAttack();
    }

    void TryAttack()
    {
        if (_attackCd > 0f || _ownerStats == null) return;

        float range = PetCatalog.AttackRange(kind);
        CharacterStats best = null;
        float bestDist = range;
        var enemies = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null || e.isPlayer || e.isDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            // Also require reasonably near the player so pet doesn't leash across the map
            float toPlayer = Vector3.Distance(e.transform.position, _owner.position);
            if (toPlayer > range + 4f) continue;
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        if (best == null) return;

        Vector3 face = best.transform.position - transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(face);

        float playerAtk = _ownerStats.attackDamage;
        var pet = PlayerPetController.Instance != null ? PlayerPetController.Instance.ActivePet : null;
        float dmg = PetCatalog.ComputeDamage(pet, playerAtk);

        // Shadow Wolf: small crit chance
        if (kind == PetKind.ShadowWolf && Random.value < 0.18f)
            dmg *= 1.6f;

        // Ember Fox: fire flavor (already in formula); Storm Hawk: slight bonus vs full HP optional — skip

        best.TakeDamage(dmg, _ownerStats);
        _attackCd = PetCatalog.AttackCooldown(kind);

        // Tiny lunge
        transform.position += transform.forward * 0.25f;
    }
}

public static class PetVisuals
{
    public static void Build(Transform root, PetKind kind)
    {
        Color c = PetCatalog.KindColor(kind);
        switch (kind)
        {
            case PetKind.EmberFox:
                Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.35f, 0f), new Vector3(0.35f, 0.28f, 0.55f), c);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0.35f), new Vector3(0.32f, 0.32f, 0.32f), Color.Lerp(c, Color.white, 0.2f));
                Part(root, PrimitiveType.Cube, new Vector3(0.12f, 0.75f, 0.25f), new Vector3(0.08f, 0.22f, 0.12f), c); // ear
                Part(root, PrimitiveType.Cube, new Vector3(-0.12f, 0.75f, 0.25f), new Vector3(0.08f, 0.22f, 0.12f), c);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.4f, -0.35f), new Vector3(0.2f, 0.2f, 0.35f), new Color(1f, 0.7f, 0.2f)); // flame tip
                break;
            case PetKind.ShadowWolf:
                Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.35f, 0.7f), c);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0.4f), new Vector3(0.38f, 0.38f, 0.38f), Color.Lerp(c, Color.black, 0.25f));
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.35f, -0.45f), new Vector3(0.15f, 0.2f, 0.4f), c); // tail
                Part(root, PrimitiveType.Cube, new Vector3(0.18f, 0.15f, 0.2f), new Vector3(0.12f, 0.25f, 0.12f), c);
                Part(root, PrimitiveType.Cube, new Vector3(-0.18f, 0.15f, 0.2f), new Vector3(0.12f, 0.25f, 0.12f), c);
                break;
            default: // StormHawk
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.35f, 0.28f, 0.45f), c);
                Part(root, PrimitiveType.Cube, new Vector3(0.45f, 0.25f, 0f), new Vector3(0.55f, 0.08f, 0.25f), Color.Lerp(c, Color.white, 0.3f));
                Part(root, PrimitiveType.Cube, new Vector3(-0.45f, 0.25f, 0f), new Vector3(0.55f, 0.08f, 0.25f), Color.Lerp(c, Color.white, 0.3f));
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.35f), new Vector3(0.1f, 0.1f, 0.3f), c);
                break;
        }
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = "Part";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Object.Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            r.sharedMaterial = mat;
        }
    }
}
