using UnityEngine;

/// <summary>
/// Recolors the player body mesh by armor tier — same shape, different colors only.
/// </summary>
public class PlayerArmorVisual : MonoBehaviour
{
    Transform _body;
    int _lastStyle = -1;
    ItemRarity _lastRarity = (ItemRarity)(-1);
    Color _baseBodyColor = new Color(0.25f, 0.55f, 0.95f);

    void Awake()
    {
        _body = transform.Find("Body");
        if (_body != null)
        {
            var r = _body.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                    _baseBodyColor = r.sharedMaterial.GetColor("_BaseColor");
                else
                    _baseBodyColor = r.sharedMaterial.color;
            }
        }
    }

    void LateUpdate()
    {
        var inv = GetComponent<PlayerInventory>();
        if (inv == null || _body == null) return;

        var armor = inv.equippedArmor;
        int style = armor != null ? ArmorVisualUtil.StyleIndex(armor.itemLevel) : -1;
        ItemRarity rarity = armor != null ? armor.rarity : (ItemRarity)(-1);
        if (style == _lastStyle && rarity == _lastRarity) return;
        _lastStyle = style;
        _lastRarity = rarity;

        if (armor == null)
            SetColor(_body, _baseBodyColor);
        else
            SetColor(_body, ArmorVisualUtil.BodyColor(style, rarity));
    }

    public void ForceRebuild()
    {
        _lastStyle = -999;
        LateUpdate();
    }

    static void SetColor(Transform t, Color color)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        r.sharedMaterial = mat;
    }
}

public static class ArmorVisualUtil
{
    public static int StyleIndex(int itemLevel)
    {
        return Mathf.Clamp((Mathf.Max(1, itemLevel) - 1) / 5, 0, 7);
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
            case 0: return "布衣型";
            case 1: return "皮甲型";
            case 2: return "鎖甲型";
            case 3: return "精鋼型";
            case 4: return "龍鱗型";
            case 5: return "符文型";
            case 6: return "聖光型";
            default: return "終焉型";
        }
    }
}
