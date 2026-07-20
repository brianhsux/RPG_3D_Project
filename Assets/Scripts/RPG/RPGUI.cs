using UnityEngine;

public static class RPGUI
{
    static GUIStyle _title;
    static GUIStyle _label;
    static GUIStyle _value;
    static GUIStyle _hint;
    static GUIStyle _banner;
    static GUIStyle _button;
    static GUIStyle _itemName;
    static GUIStyle _itemMeta;
    static GUIStyle _fieldTag;
    static Texture2D _white;

    // Recommended row heights so Chinese glyphs aren't clipped.
    public const float TitleH = 56f;
    public const float LabelH = 48f;
    public const float HintH = 40f;
    public const float BannerH = 64f;
    public const float ItemNameH = 48f;
    public const float ItemMetaH = 42f;
    public const float FieldTagH = 36f;
    public const float ButtonH = 56f;

    public static void Ensure()
    {
        if (_white == null) _white = Texture2D.whiteTexture;

        _title = MakeLabel(40, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        _label = MakeLabel(30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        _value = MakeLabel(26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        _hint = MakeLabel(24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.85f));
        _banner = MakeLabel(36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        _itemName = MakeLabel(30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        _itemMeta = MakeLabel(24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.94f, 0.98f));
        _fieldTag = MakeLabel(22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.75f, 0.82f, 0.92f));

        _button = new GUIStyle(GUI.skin.button)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(12, 12, 10, 10)
        };
    }

    static GUIStyle MakeLabel(int size, FontStyle style, TextAnchor align, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = align,
            // Overflow avoids vertical/horizontal glyph cropping common with bold CJK.
            clipping = TextClipping.Overflow,
            wordWrap = false,
            richText = false,
            padding = new RectOffset(2, 2, 2, 2),
            normal = { textColor = color }
        };
    }

    public static void DrawPanel(Rect rect, Color bg)
    {
        Ensure();
        Color prev = GUI.color;
        GUI.color = bg;
        GUI.DrawTexture(rect, _white);
        GUI.color = new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4f), _white);
        GUI.color = prev;
    }

    public static void DrawBar(Rect rect, float percent, Color fill, string text)
    {
        Ensure();
        percent = Mathf.Clamp01(percent);

        Color prev = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.DrawTexture(rect, _white);

        GUI.color = fill;
        GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, (rect.width - 8f) * percent, rect.height - 8f), _white);

        GUI.color = Color.white;
        GUI.Label(rect, text, _value);
        GUI.color = prev;
    }

    /// <summary>Item icon — weapons vary by tier shape/color/size.</summary>
    public static void DrawItemIcon(Rect rect, ItemInstance item)
    {
        if (item == null)
        {
            DrawItemIcon(rect, ItemSlot.Weapon, Color.white, 0);
            return;
        }
        int style = item.slot == ItemSlot.Weapon
            ? WeaponVisualUtil.StyleIndex(item.itemLevel)
            : ArmorVisualUtil.StyleIndex(item.itemLevel);
        DrawItemIcon(rect, item.slot, item.RarityColor, style, item.itemLevel, item.rarity);
    }

    public static void DrawItemIcon(Rect rect, ItemSlot slot, Color rarityColor)
    {
        DrawItemIcon(rect, slot, rarityColor, 0);
    }

    public static void DrawItemIcon(Rect rect, ItemSlot slot, Color rarityColor, int style,
        int itemLevel = 1, ItemRarity rarity = ItemRarity.Common)
    {
        Ensure();
        Color prev = GUI.color;

        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        GUI.DrawTexture(rect, _white);
        GUI.color = rarityColor;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 4f), _white);

        float cx = rect.x + rect.width * 0.5f;
        float top = rect.y + 8f;
        float h = rect.height - 14f;

        if (slot == ItemSlot.Weapon)
            DrawWeaponGlyph(cx, top, h, style, rarityColor, itemLevel, rarity);
        else
            DrawArmorGlyph(cx, top, h, style, rarity);

        GUI.color = prev;
    }

    static void DrawArmorGlyph(float cx, float top, float h, int style, ItemRarity rarity)
    {
        Color body = ArmorVisualUtil.BodyColor(style, rarity);
        Color plate = ArmorVisualUtil.PlateColor(style, rarity);
        Color accent = ArmorVisualUtil.AccentColor(style, rarity);

        // Same size for all armor — only colors change
        GUI.color = body;
        GUI.DrawTexture(new Rect(cx - 14f, top + h * 0.18f, 28f, h * 0.5f), _white);
        GUI.color = plate;
        GUI.DrawTexture(new Rect(cx - 10f, top + h * 0.22f, 20f, h * 0.38f), _white);
        GUI.color = accent;
        GUI.DrawTexture(new Rect(cx - 5f, top + h * 0.32f, 10f, 10f), _white);
    }

    static void DrawWeaponGlyph(float cx, float top, float h, int style, Color rarityColor, int itemLevel, ItemRarity rarity)
    {
        float sizeMul = Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01((WeaponVisualUtil.SizeScale(itemLevel, rarity) - 0.85f) / 0.7f));
        Color blade = WeaponVisualUtil.BladeColor(style, rarity);
        Color accent = WeaponVisualUtil.AccentColor(style, rarity);
        Color grip = WeaponVisualUtil.GripColor(style);

        switch (style)
        {
            case 1: // iron — classic
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 5f * sizeMul, top, 10f * sizeMul, h * 0.58f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 18f * sizeMul, top + h * 0.52f, 36f * sizeMul, 7f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 2: // wind — thin long saber
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 2.5f, top, 5f, h * 0.72f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx + 1f, top + 6f, 3f, h * 0.55f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 10f, top + h * 0.66f, 20f, 5f), _white);
                GUI.DrawTexture(new Rect(cx - 2.5f, top + h * 0.7f, 5f, h * 0.2f), _white);
                break;
            case 3: // bone — jagged
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 7f, top + 6f, 14f, h * 0.5f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx + 6f, top + h * 0.2f, 8f, 10f), _white);
                GUI.DrawTexture(new Rect(cx - 14f, top + h * 0.35f, 8f, 10f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 4: // flame — wide
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 12f * sizeMul, top + 4f, 24f * sizeMul, h * 0.5f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 4f, top, 8f, 10f), _white);
                GUI.DrawTexture(new Rect(cx + 6f, top + 8f, 8f, 8f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.52f, 32f, 7f), _white);
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 5: // frost
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 5f, top, 10f, h * 0.55f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 12f, top + h * 0.25f, 8f, 12f), _white);
                GUI.DrawTexture(new Rect(cx + 4f, top + h * 0.35f, 8f, 12f), _white);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(cx - 3f, top, 6f, 8f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 6: // thunder
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 7f * sizeMul, top, 14f * sizeMul, h * 0.55f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 2f, top + 4f, 4f, h * 0.5f), _white);
                GUI.DrawTexture(new Rect(cx + 6f, top + h * 0.2f, 10f, 6f), _white);
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.4f, 10f, 6f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 7: // void — large
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 8f * sizeMul, top, 16f * sizeMul, h * 0.6f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.2f, 8f, h * 0.35f), _white);
                GUI.DrawTexture(new Rect(cx + 8f, top + h * 0.2f, 8f, h * 0.35f), _white);
                GUI.DrawTexture(new Rect(cx - 6f, top + h * 0.15f, 12f, 12f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.62f, 8f, h * 0.26f), _white);
                break;
            default: // wood
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 5f, top + 4f, 10f, h * 0.5f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 14f, top + h * 0.5f, 28f, 6f), _white);
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.56f, 8f, h * 0.28f), _white);
                break;
        }

        // Rarity gem
        GUI.color = rarityColor;
        GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.48f, 8f, 8f), _white);
    }

    public static GUIStyle Title => _title;
    public static GUIStyle Label => _label;
    public static GUIStyle Hint => _hint;
    public static GUIStyle Banner => _banner;
    public static GUIStyle Button => _button;
    public static GUIStyle ItemName => _itemName;
    public static GUIStyle ItemMeta => _itemMeta;
    public static GUIStyle FieldTag => _fieldTag;
}
