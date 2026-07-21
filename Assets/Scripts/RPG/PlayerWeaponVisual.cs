using UnityEngine;

/// <summary>
/// Builds a procedural 3D weapon on the player from the equipped item.
/// Shape / color / size vary by weapon tier and rarity.
/// </summary>
public class PlayerWeaponVisual : MonoBehaviour
{
    Transform _root;
    Quaternion _restLocalRot = Quaternion.Euler(15f, 0f, 0f);
    string _builtKey = "";

    public Transform WeaponRoot => _root;

    void Awake()
    {
        EnsureRoot();
    }

    void LateUpdate()
    {
        // Keep in sync if equipment changed elsewhere.
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        RefreshIfNeeded(inv.equippedWeapon);
    }

    public void ForceRebuild()
    {
        _builtKey = "";
        var inv = PlayerInventory.Instance;
        Rebuild(inv != null ? inv.equippedWeapon : null);
    }

    public void RefreshIfNeeded(ItemInstance weapon)
    {
        string key = WeaponKey(weapon);
        if (key == _builtKey && _root != null) return;
        Rebuild(weapon);
    }

    static string WeaponKey(ItemInstance weapon)
    {
        if (weapon == null) return "none";
        return $"{weapon.itemLevel}_{weapon.rarity}_{weapon.displayName}";
    }

    void EnsureRoot()
    {
        if (_root != null) return;

        // Replace legacy single-cube Sword if present.
        var legacy = transform.Find("Sword");
        if (legacy != null)
        {
            _root = legacy;
            ClearChildren(_root);
            StripRootMesh(_root.gameObject);
            // Hide leftover sibling hilt from old scene build.
            var oldHilt = transform.Find("Hilt");
            if (oldHilt != null) oldHilt.gameObject.SetActive(false);
        }
        else
        {
            var go = new GameObject("Sword");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        _root.localPosition = new Vector3(0.55f, 1.15f, 0.35f);
        _root.localRotation = _restLocalRot;
        _root.localScale = Vector3.one;
    }

    static void StripRootMesh(GameObject go)
    {
        // Old scene Sword was a Cube primitive — its own mesh looks like a huge block.
        foreach (var c in go.GetComponents<Collider>())
            Object.Destroy(c);
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null) Object.Destroy(mf);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) Object.Destroy(mr);
    }

    public void SetAttackSwing(float attackAnimT)
    {
        EnsureRoot();
        if (attackAnimT > 0f)
            _root.localRotation = Quaternion.Euler(15f + Mathf.Sin(attackAnimT * 20f) * 40f, 0f, 0f);
        else
            _root.localRotation = _restLocalRot;
    }

    void Rebuild(ItemInstance weapon)
    {
        EnsureRoot();
        StripRootMesh(_root.gameObject);
        ClearChildren(_root);
        _builtKey = WeaponKey(weapon);

        if (weapon == null || weapon.slot != ItemSlot.Weapon)
        {
            BuildFallbackBlade();
            return;
        }

        int style = WeaponVisualUtil.StyleIndex(weapon.itemLevel);
        float size = WeaponVisualUtil.SizeScale(weapon.itemLevel, weapon.rarity);
        Color blade = WeaponVisualUtil.BladeColor(style, weapon.rarity);
        Color accent = WeaponVisualUtil.AccentColor(style, weapon.rarity);
        Color grip = WeaponVisualUtil.GripColor(style);

        _root.localScale = Vector3.one * size;

        var catalog = VisualPrefabCatalog.Get();
        if (catalog != null)
        {
            var prefab = catalog.GetWeaponPrefab(style);
            if (prefab != null)
            {
                var mesh = VisualPrefabUtil.SpawnLocal(
                    prefab, _root, "WeaponMesh",
                    catalog.weaponLocalPosition, catalog.weaponLocalEuler,
                    catalog.weaponPrefabScale, stripColliders: true);
                if (mesh != null && catalog.tintWeaponPrefab)
                    VisualPrefabUtil.TintRenderers(mesh.transform, blade);
                return;
            }
        }

        switch (style)
        {
            case 1: BuildIronSword(blade, accent, grip); break;
            case 2: BuildWindBlade(blade, accent, grip); break;
            case 3: BuildBoneBlade(blade, accent, grip); break;
            case 4: BuildFlameCleaver(blade, accent, grip); break;
            case 5: BuildFrostBlade(blade, accent, grip); break;
            case 6: BuildThunderSword(blade, accent, grip); break;
            case 7: BuildVoidBlade(blade, accent, grip); break;
            default: BuildWoodSword(blade, accent, grip); break;
        }
    }

    void BuildFallbackBlade()
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.55f), new Vector3(0.1f, 0.08f, 1.1f),
            new Color(0.7f, 0.7f, 0.75f));
    }

    void BuildWoodSword(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.5f), new Vector3(0.1f, 0.08f, 1.0f), blade);
        Part(PrimitiveType.Cube, "Tip", new Vector3(0f, 0f, 1.05f), new Vector3(0.06f, 0.06f, 0.18f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.05f), new Vector3(0.32f, 0.08f, 0.08f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.22f), new Vector3(0.08f, 0.18f, 0.08f), grip);
    }

    void BuildIronSword(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.55f), new Vector3(0.12f, 0.06f, 1.2f), blade);
        Part(PrimitiveType.Cube, "Edge", new Vector3(0f, 0.04f, 0.55f), new Vector3(0.04f, 0.02f, 1.15f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.0f), new Vector3(0.42f, 0.1f, 0.1f), accent);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.28f), new Vector3(0.09f, 0.22f, 0.09f), grip);
        Part(PrimitiveType.Sphere, "Pommel", new Vector3(0f, 0f, -0.48f), Vector3.one * 0.12f, accent);
    }

    void BuildWindBlade(Color blade, Color accent, Color grip)
    {
        // Long thin curved saber — no chunky blocks
        Part(PrimitiveType.Cube, "Blade", new Vector3(0.02f, 0f, 0.72f), new Vector3(0.045f, 0.035f, 1.45f), blade);
        Part(PrimitiveType.Cube, "Edge", new Vector3(0.05f, 0f, 0.85f), new Vector3(0.02f, 0.028f, 1.1f), accent);
        Part(PrimitiveType.Cube, "Tip", new Vector3(0.06f, 0f, 1.48f), new Vector3(0.03f, 0.025f, 0.16f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.02f), new Vector3(0.22f, 0.05f, 0.05f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.28f), new Vector3(0.055f, 0.2f, 0.055f), grip);
        Part(PrimitiveType.Sphere, "Pommel", new Vector3(0f, 0f, -0.48f), Vector3.one * 0.07f, accent);
    }

    void BuildBoneBlade(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.5f), new Vector3(0.14f, 0.1f, 1.05f), blade);
        Part(PrimitiveType.Cube, "Spike1", new Vector3(0.12f, 0f, 0.35f), new Vector3(0.08f, 0.08f, 0.25f), accent);
        Part(PrimitiveType.Cube, "Spike2", new Vector3(-0.12f, 0f, 0.55f), new Vector3(0.08f, 0.08f, 0.25f), accent);
        Part(PrimitiveType.Cube, "Spike3", new Vector3(0.1f, 0f, 0.8f), new Vector3(0.07f, 0.07f, 0.22f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.0f), new Vector3(0.36f, 0.12f, 0.12f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.26f), new Vector3(0.1f, 0.2f, 0.1f), grip);
    }

    void BuildFlameCleaver(Color blade, Color accent, Color grip)
    {
        // Wide cleaver
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.55f), new Vector3(0.28f, 0.08f, 1.15f), blade);
        Part(PrimitiveType.Cube, "Fang", new Vector3(0.12f, 0f, 1.05f), new Vector3(0.14f, 0.07f, 0.3f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, -0.02f), new Vector3(0.5f, 0.12f, 0.12f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.32f), new Vector3(0.11f, 0.24f, 0.11f), grip);
        Part(PrimitiveType.Sphere, "Ember", new Vector3(0f, 0.08f, 0.7f), Vector3.one * 0.14f, accent);
    }

    void BuildFrostBlade(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.6f), new Vector3(0.11f, 0.11f, 1.25f), blade);
        Part(PrimitiveType.Cube, "CrystalL", new Vector3(-0.1f, 0.05f, 0.7f), new Vector3(0.08f, 0.18f, 0.18f), accent);
        Part(PrimitiveType.Cube, "CrystalR", new Vector3(0.1f, 0.05f, 0.9f), new Vector3(0.08f, 0.18f, 0.18f), accent);
        Part(PrimitiveType.Cube, "Tip", new Vector3(0f, 0f, 1.28f), new Vector3(0.08f, 0.08f, 0.22f), Color.white);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.0f), new Vector3(0.4f, 0.1f, 0.1f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.28f), new Vector3(0.09f, 0.22f, 0.09f), grip);
    }

    void BuildThunderSword(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.6f), new Vector3(0.16f, 0.08f, 1.3f), blade);
        Part(PrimitiveType.Cube, "Core", new Vector3(0f, 0.02f, 0.6f), new Vector3(0.05f, 0.05f, 1.25f), accent);
        Part(PrimitiveType.Cube, "Zig1", new Vector3(0.1f, 0f, 0.4f), new Vector3(0.12f, 0.05f, 0.12f), accent);
        Part(PrimitiveType.Cube, "Zig2", new Vector3(-0.1f, 0f, 0.7f), new Vector3(0.12f, 0.05f, 0.12f), accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, 0.0f), new Vector3(0.48f, 0.12f, 0.12f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.3f), new Vector3(0.1f, 0.24f, 0.1f), grip);
        Part(PrimitiveType.Sphere, "Pommel", new Vector3(0f, 0f, -0.52f), Vector3.one * 0.14f, accent);
    }

    void BuildVoidBlade(Color blade, Color accent, Color grip)
    {
        Part(PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.7f), new Vector3(0.18f, 0.1f, 1.55f), blade);
        Part(PrimitiveType.Cube, "WingL", new Vector3(-0.16f, 0f, 0.5f), new Vector3(0.12f, 0.06f, 0.7f), accent);
        Part(PrimitiveType.Cube, "WingR", new Vector3(0.16f, 0f, 0.5f), new Vector3(0.12f, 0.06f, 0.7f), accent);
        Part(PrimitiveType.Sphere, "Orb", new Vector3(0f, 0.12f, 0.35f), Vector3.one * 0.18f, accent);
        Part(PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, -0.02f), new Vector3(0.55f, 0.14f, 0.14f), grip);
        Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0f, -0.35f), new Vector3(0.12f, 0.28f, 0.12f), grip);
        Part(PrimitiveType.Sphere, "Pommel", new Vector3(0f, 0f, -0.6f), Vector3.one * 0.16f, accent);
    }

    GameObject Part(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(_root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            r.sharedMaterial = mat;
        }

        // Cylinder default is Y-up; rotate to Z for handles.
        if (type == PrimitiveType.Cylinder)
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        return go;
    }

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.Destroy(t.GetChild(i).gameObject);
    }
}

public static class WeaponVisualUtil
{
    public static int StyleIndex(int itemLevel)
    {
        return Mathf.Clamp((Mathf.Max(1, itemLevel) - 1) / 12, 0, 7);
    }

    public static float SizeScale(int itemLevel, ItemRarity rarity)
    {
        float lv = Mathf.Sqrt(Mathf.Max(1, itemLevel));
        float s = 0.88f + lv * 0.055f + (int)rarity * 0.07f;
        return Mathf.Clamp(s, 0.85f, 1.55f);
    }

    public static Color BladeColor(int style, ItemRarity rarity)
    {
        Color baseCol;
        switch (style)
        {
            case 0: baseCol = new Color(0.55f, 0.35f, 0.18f); break; // wood
            case 1: baseCol = new Color(0.72f, 0.74f, 0.78f); break; // iron
            case 2: baseCol = new Color(0.55f, 0.9f, 0.85f); break;  // wind
            case 3: baseCol = new Color(0.92f, 0.88f, 0.75f); break; // bone
            case 4: baseCol = new Color(0.95f, 0.35f, 0.12f); break; // flame
            case 5: baseCol = new Color(0.45f, 0.75f, 1f); break;    // frost
            case 6: baseCol = new Color(0.95f, 0.85f, 0.25f); break; // thunder
            default: baseCol = new Color(0.28f, 0.12f, 0.4f); break; // void
        }
        return Color.Lerp(baseCol, RarityTint(rarity), 0.35f);
    }

    public static Color AccentColor(int style, ItemRarity rarity)
    {
        switch (style)
        {
            case 0: return new Color(0.4f, 0.25f, 0.12f);
            case 1: return RarityTint(rarity);
            case 2: return new Color(0.8f, 1f, 0.95f);
            case 3: return new Color(0.75f, 0.7f, 0.55f);
            case 4: return new Color(1f, 0.7f, 0.15f);
            case 5: return new Color(0.75f, 0.95f, 1f);
            case 6: return new Color(1f, 1f, 0.55f);
            default: return new Color(0.7f, 0.35f, 1f);
        }
    }

    public static Color GripColor(int style)
    {
        switch (style)
        {
            case 0: return new Color(0.35f, 0.2f, 0.1f);
            case 3: return new Color(0.45f, 0.4f, 0.3f);
            case 7: return new Color(0.12f, 0.08f, 0.16f);
            default: return new Color(0.25f, 0.16f, 0.1f);
        }
    }

    public static Color RarityTint(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return new Color(0.36f, 0.79f, 0.42f);
            case ItemRarity.Rare: return new Color(0.30f, 0.63f, 1f);
            case ItemRarity.Epic: return new Color(0.76f, 0.42f, 1f);
            case ItemRarity.Legendary: return new Color(1f, 0.70f, 0.24f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }

    public static string StyleLabel(int style)
    {
        switch (style)
        {
            case 0: return "Wood";
            case 1: return "Iron";
            case 2: return "Thin Blade";
            case 3: return "Bone";
            case 4: return "Flame";
            case 5: return "Frost";
            case 6: return "Thunder";
            default: return "Void";
        }
    }
}
