using UnityEngine;

/// <summary>
/// Builds a procedural low-poly hero from primitives by armor tier,
/// or swaps in catalog prefabs when assigned.
/// </summary>
public class PlayerArmorVisual : MonoBehaviour
{
    Transform _gearRoot;
    int _lastStyle = -999;
    ItemRarity _lastRarity = (ItemRarity)(-1);

    static readonly string[] LegacyParts = { "Body", "Head", "LeftArm", "RightArm" };

    static readonly Color Skin = new Color(0.92f, 0.72f, 0.55f);
    static readonly Color Hair = new Color(0.22f, 0.14f, 0.08f);
    static readonly Color Boot = new Color(0.2f, 0.12f, 0.08f);

    void Awake()
    {
        EnsureGearRoot();
    }

    void EnsureGearRoot()
    {
        if (_gearRoot != null) return;
        var existing = transform.Find("ArmorGear");
        if (existing != null)
        {
            _gearRoot = existing;
            return;
        }
        var go = new GameObject("ArmorGear");
        go.transform.SetParent(transform, false);
        _gearRoot = go.transform;
    }

    void LateUpdate()
    {
        var inv = GetComponent<PlayerInventory>();
        if (inv == null) return;

        var armor = inv.equippedArmor;
        int style = armor != null ? ArmorVisualUtil.StyleIndex(armor.itemLevel) : 0;
        ItemRarity rarity = armor != null ? armor.rarity : ItemRarity.Common;
        if (armor == null) style = 0;
        if (style == _lastStyle && rarity == _lastRarity) return;
        _lastStyle = style;
        _lastRarity = rarity;

        Apply(style, rarity);
    }

    public void ForceRebuild()
    {
        _lastStyle = -999;
        LateUpdate();
    }

    void Apply(int style, ItemRarity rarity)
    {
        EnsureGearRoot();
        ClearGear();

        Color plate = ArmorVisualUtil.BodyColor(style, rarity);
        Color accent = ArmorVisualUtil.AccentColor(style, rarity);
        var catalog = VisualPrefabCatalog.Get();
        GameObject prefab = catalog != null ? catalog.GetArmorPrefab(style) : null;

        if (prefab != null)
        {
            SetLegacyVisible(false);
            var mesh = VisualPrefabUtil.SpawnLocal(
                prefab, _gearRoot, "ArmorMesh",
                catalog.armorLocalPosition, catalog.armorLocalEuler,
                catalog.armorPrefabScale, stripColliders: true);
            if (mesh != null && catalog.tintArmorPrefab)
                VisualPrefabUtil.TintRenderers(mesh.transform, plate);
            RecacheAnim();
            return;
        }

        SetLegacyVisible(false);
        BuildProceduralHero(style, plate, accent);
        RecacheAnim();
    }

    void BuildProceduralHero(int style, Color plate, Color accent)
    {
        // Core humanoid (names match ProceduralBodyAnim roles).
        Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 1.05f, 0f), new Vector3(0.72f, 0.55f, 0.55f), plate);
        Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.95f, 0.02f), Vector3.one * 0.48f, Skin);
        Part(PrimitiveType.Capsule, "LeftArm", new Vector3(-0.55f, 1.15f, 0f), new Vector3(0.22f, 0.38f, 0.22f), Skin);
        Part(PrimitiveType.Capsule, "RightArm", new Vector3(0.55f, 1.15f, 0f), new Vector3(0.22f, 0.38f, 0.22f), Skin);
        Part(PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.2f, 0.4f, 0f), new Vector3(0.24f, 0.38f, 0.24f), Boot);
        Part(PrimitiveType.Capsule, "RightLeg", new Vector3(0.2f, 0.4f, 0f), new Vector3(0.24f, 0.38f, 0.24f), Boot);

        // Hair / face accents
        Part(PrimitiveType.Sphere, "Hair", new Vector3(0f, 2.12f, -0.05f), new Vector3(0.5f, 0.28f, 0.5f), Hair);
        Part(PrimitiveType.Sphere, "EyeL", new Vector3(-0.12f, 1.98f, 0.2f), Vector3.one * 0.07f, Color.black);
        Part(PrimitiveType.Sphere, "EyeR", new Vector3(0.12f, 1.98f, 0.2f), Vector3.one * 0.07f, Color.black);

        switch (style)
        {
            case 0: // cloth
                Part(PrimitiveType.Cube, "Tunic", new Vector3(0f, 0.95f, 0.05f), new Vector3(0.78f, 0.7f, 0.5f), plate);
                Part(PrimitiveType.Cube, "Hood", new Vector3(0f, 2.05f, -0.05f), new Vector3(0.55f, 0.35f, 0.55f), accent);
                Part(PrimitiveType.Cube, "Sash", new Vector3(0f, 0.85f, 0.28f), new Vector3(0.7f, 0.12f, 0.08f), accent);
                break;
            case 1: // leather
                Part(PrimitiveType.Cube, "Vest", new Vector3(0f, 1.1f, 0.08f), new Vector3(0.8f, 0.55f, 0.45f), plate);
                Part(PrimitiveType.Cube, "ShoulderL", new Vector3(-0.55f, 1.45f, 0f), new Vector3(0.35f, 0.2f, 0.35f), accent);
                Part(PrimitiveType.Cube, "ShoulderR", new Vector3(0.55f, 1.45f, 0f), new Vector3(0.35f, 0.2f, 0.35f), accent);
                Part(PrimitiveType.Cube, "Belt", new Vector3(0f, 0.78f, 0.05f), new Vector3(0.75f, 0.12f, 0.5f), Boot);
                Part(PrimitiveType.Cube, "BracerL", new Vector3(-0.55f, 0.9f, 0f), new Vector3(0.26f, 0.18f, 0.26f), accent);
                Part(PrimitiveType.Cube, "BracerR", new Vector3(0.55f, 0.9f, 0f), new Vector3(0.26f, 0.18f, 0.26f), accent);
                break;
            case 2: // chain
                Part(PrimitiveType.Capsule, "Hauberk", new Vector3(0f, 1.05f, 0f), new Vector3(0.82f, 0.58f, 0.6f), plate);
                Part(PrimitiveType.Sphere, "Coif", new Vector3(0f, 1.92f, 0f), Vector3.one * 0.52f, plate);
                Part(PrimitiveType.Cube, "Skirt", new Vector3(0f, 0.7f, 0f), new Vector3(0.85f, 0.25f, 0.55f), accent);
                break;
            case 3: // steel
                Part(PrimitiveType.Cube, "Breastplate", new Vector3(0f, 1.15f, 0.1f), new Vector3(0.85f, 0.6f, 0.45f), plate);
                Part(PrimitiveType.Cube, "Helm", new Vector3(0f, 2.15f, 0.02f), new Vector3(0.55f, 0.4f, 0.58f), plate);
                Part(PrimitiveType.Cube, "Visor", new Vector3(0f, 2.0f, 0.28f), new Vector3(0.42f, 0.12f, 0.12f), accent);
                Part(PrimitiveType.Cube, "PauldronL", new Vector3(-0.62f, 1.5f, 0f), new Vector3(0.4f, 0.28f, 0.4f), plate);
                Part(PrimitiveType.Cube, "PauldronR", new Vector3(0.62f, 1.5f, 0f), new Vector3(0.4f, 0.28f, 0.4f), plate);
                Part(PrimitiveType.Cube, "GreaveL", new Vector3(-0.2f, 0.35f, 0.05f), new Vector3(0.28f, 0.35f, 0.28f), plate);
                Part(PrimitiveType.Cube, "GreaveR", new Vector3(0.2f, 0.35f, 0.05f), new Vector3(0.28f, 0.35f, 0.28f), plate);
                break;
            case 4: // dragon
                Part(PrimitiveType.Capsule, "ScaleMail", new Vector3(0f, 1.05f, 0f), new Vector3(0.85f, 0.6f, 0.62f), plate);
                Part(PrimitiveType.Cube, "HornL", new Vector3(-0.22f, 2.35f, -0.05f), new Vector3(0.1f, 0.35f, 0.1f), accent);
                Part(PrimitiveType.Cube, "HornR", new Vector3(0.22f, 2.35f, -0.05f), new Vector3(0.1f, 0.35f, 0.1f), accent);
                Part(PrimitiveType.Cube, "Spine1", new Vector3(0f, 1.4f, -0.32f), new Vector3(0.12f, 0.2f, 0.18f), accent);
                Part(PrimitiveType.Cube, "Spine2", new Vector3(0f, 1.1f, -0.32f), new Vector3(0.12f, 0.2f, 0.18f), accent);
                Part(PrimitiveType.Cube, "ClawL", new Vector3(-0.55f, 0.85f, 0.15f), new Vector3(0.2f, 0.12f, 0.28f), accent);
                Part(PrimitiveType.Cube, "ClawR", new Vector3(0.55f, 0.85f, 0.15f), new Vector3(0.2f, 0.12f, 0.28f), accent);
                break;
            case 5: // rune
                Part(PrimitiveType.Cube, "Robe", new Vector3(0f, 1.0f, 0f), new Vector3(0.9f, 0.85f, 0.55f), plate);
                Part(PrimitiveType.Sphere, "Crystal", new Vector3(0f, 1.35f, 0.32f), Vector3.one * 0.22f, accent);
                Part(PrimitiveType.Cube, "RuneL", new Vector3(-0.35f, 1.2f, 0.3f), new Vector3(0.12f, 0.35f, 0.08f), accent);
                Part(PrimitiveType.Cube, "RuneR", new Vector3(0.35f, 1.2f, 0.3f), new Vector3(0.12f, 0.35f, 0.08f), accent);
                Part(PrimitiveType.Cube, "Hood", new Vector3(0f, 2.1f, -0.02f), new Vector3(0.6f, 0.4f, 0.6f), plate);
                break;
            case 6: // holy
                Part(PrimitiveType.Capsule, "Plate", new Vector3(0f, 1.05f, 0f), new Vector3(0.8f, 0.58f, 0.58f), plate);
                Part(PrimitiveType.Cube, "Cape", new Vector3(0f, 1.2f, -0.4f), new Vector3(0.95f, 1.1f, 0.08f), accent);
                Part(PrimitiveType.Cylinder, "Halo", new Vector3(0f, 2.45f, 0f), new Vector3(0.55f, 0.04f, 0.55f), accent);
                Part(PrimitiveType.Cube, "Crest", new Vector3(0f, 2.25f, 0.05f), new Vector3(0.15f, 0.35f, 0.15f), accent);
                Part(PrimitiveType.Cube, "PauldronL", new Vector3(-0.58f, 1.48f, 0f), new Vector3(0.38f, 0.25f, 0.38f), plate);
                Part(PrimitiveType.Cube, "PauldronR", new Vector3(0.58f, 1.48f, 0f), new Vector3(0.38f, 0.25f, 0.38f), plate);
                break;
            default: // end / void
                Part(PrimitiveType.Capsule, "DarkPlate", new Vector3(0f, 1.05f, 0f), new Vector3(0.88f, 0.62f, 0.62f), plate);
                Part(PrimitiveType.Cube, "HornL", new Vector3(-0.28f, 2.4f, -0.08f), new Vector3(0.12f, 0.45f, 0.12f), accent);
                Part(PrimitiveType.Cube, "HornR", new Vector3(0.28f, 2.4f, -0.08f), new Vector3(0.12f, 0.45f, 0.12f), accent);
                Part(PrimitiveType.Cube, "Cape", new Vector3(0f, 1.15f, -0.45f), new Vector3(1.05f, 1.2f, 0.1f), Boot);
                Part(PrimitiveType.Sphere, "Core", new Vector3(0f, 1.25f, 0.35f), Vector3.one * 0.2f, accent);
                Part(PrimitiveType.Cube, "SpauldL", new Vector3(-0.65f, 1.55f, 0f), new Vector3(0.42f, 0.3f, 0.42f), plate);
                Part(PrimitiveType.Cube, "SpauldR", new Vector3(0.65f, 1.55f, 0f), new Vector3(0.42f, 0.3f, 0.42f), plate);
                break;
        }
    }

    GameObject Part(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(_gearRoot, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        Object.Destroy(go.GetComponent<Collider>());

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            r.sharedMaterial = mat;
        }

        if (type == PrimitiveType.Cylinder)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        return go;
    }

    void SetLegacyVisible(bool visible)
    {
        for (int i = 0; i < LegacyParts.Length; i++)
        {
            var t = transform.Find(LegacyParts[i]);
            if (t == null) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = visible;
        }
    }

    void ClearGear()
    {
        if (_gearRoot == null) return;
        for (int i = _gearRoot.childCount - 1; i >= 0; i--)
            Destroy(_gearRoot.GetChild(i).gameObject);
    }

    void RecacheAnim()
    {
        var anim = GetComponent<ProceduralBodyAnim>();
        if (anim != null) anim.Recache();
    }
}

public static class ArmorVisualUtil
{
    public static int StyleIndex(int itemLevel)
    {
        return Mathf.Clamp((Mathf.Max(1, itemLevel) - 1) / 12, 0, 7);
    }

    public static Color BodyColor(int style, ItemRarity rarity)
    {
        Color c;
        switch (style)
        {
            case 0: c = new Color(0.35f, 0.45f, 0.7f); break;   // cloth
            case 1: c = new Color(0.45f, 0.28f, 0.16f); break;   // leather
            case 2: c = new Color(0.45f, 0.48f, 0.5f); break;    // chain
            case 3: c = new Color(0.65f, 0.68f, 0.75f); break;   // steel
            case 4: c = new Color(0.2f, 0.45f, 0.22f); break;    // dragon
            case 5: c = new Color(0.35f, 0.2f, 0.55f); break;    // rune
            case 6: c = new Color(0.85f, 0.85f, 0.9f); break;    // holy
            default: c = new Color(0.18f, 0.08f, 0.1f); break;   // end
        }
        return Color.Lerp(c, WeaponVisualUtil.RarityTint(rarity), 0.28f);
    }

    public static Color PlateColor(int style, ItemRarity rarity) => BodyColor(style, rarity);

    public static Color AccentColor(int style, ItemRarity rarity)
    {
        switch (style)
        {
            case 0: return new Color(0.7f, 0.75f, 0.9f);
            case 1: return new Color(0.3f, 0.18f, 0.1f);
            case 2: return new Color(0.7f, 0.72f, 0.78f);
            case 3: return WeaponVisualUtil.RarityTint(rarity);
            case 4: return new Color(0.85f, 0.55f, 0.15f);
            case 5: return new Color(0.85f, 0.45f, 1f);
            case 6: return new Color(1f, 0.85f, 0.35f);
            default: return new Color(0.7f, 0.12f, 0.15f);
        }
    }

    public static string StyleLabel(int style)
    {
        switch (style)
        {
            case 0: return "Cloth";
            case 1: return "Leather";
            case 2: return "Chain";
            case 3: return "Steel";
            case 4: return "Dragon";
            case 5: return "Rune";
            case 6: return "Holy";
            default: return "Apocalypse";
        }
    }
}
