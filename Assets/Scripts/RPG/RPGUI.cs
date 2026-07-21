using UnityEngine;

public static class RPGUI
{
    static GUIStyle _title;
    static GUIStyle _label;
    static GUIStyle _value;
    static GUIStyle _hint;
    static GUIStyle _banner;
    static GUIStyle _buttonLabel;
    static GUIStyle _buttonLabelSm;
    static GUIStyle _hitBox;
    static GUIStyle _itemName;
    static GUIStyle _itemMeta;
    static GUIStyle _fieldTag;
    static GUIStyle _chip;
    static GUIStyle _toast;
    static Texture2D _white;

    const float OutlinePx = 2f;

    public const float TitleH = 48f;
    public const float LabelH = 40f;
    public const float HintH = 32f;
    public const float BannerH = 56f;
    public const float ItemNameH = 40f;
    public const float ItemMetaH = 34f;
    public const float FieldTagH = 28f;
    public const float ButtonH = 52f;
    public const float ButtonSmH = 40f;

    public static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.09f, 0.94f);
    public static readonly Color PanelBgSoft = new Color(0.08f, 0.1f, 0.14f, 0.92f);
    public static readonly Color Accent = new Color(0.35f, 0.55f, 0.95f, 1f);
    public static readonly Color Good = new Color(0.35f, 0.82f, 0.48f, 1f);
    public static readonly Color Warn = new Color(0.95f, 0.72f, 0.28f, 1f);
    public static readonly Color Bad = new Color(0.85f, 0.35f, 0.35f, 1f);

    public static void Ensure()
    {
        if (_white != null && _title != null) return;
        if (_white == null) _white = Texture2D.whiteTexture;

        _title = MakeLabel(34, TextAnchor.MiddleLeft, Color.white);
        _label = MakeLabel(26, TextAnchor.MiddleLeft, Color.white);
        _value = MakeLabel(22, TextAnchor.MiddleCenter, Color.white);
        _hint = MakeLabel(20, TextAnchor.MiddleLeft, new Color(0.86f, 0.88f, 0.82f));
        _banner = MakeLabel(30, TextAnchor.MiddleCenter, Color.white);
        _itemName = MakeLabel(26, TextAnchor.MiddleLeft, Color.white);
        _itemMeta = MakeLabel(20, TextAnchor.MiddleLeft, new Color(0.88f, 0.9f, 0.95f));
        _fieldTag = MakeLabel(18, TextAnchor.MiddleLeft, new Color(0.68f, 0.74f, 0.86f));
        _chip = MakeLabel(18, TextAnchor.MiddleCenter, Color.white);
        _toast = MakeLabel(26, TextAnchor.MiddleCenter, Color.white);
        _buttonLabel = MakeLabel(24, TextAnchor.MiddleCenter, Color.white);
        _buttonLabelSm = MakeLabel(20, TextAnchor.MiddleCenter, Color.white);

        if (_hitBox == null)
            _hitBox = new GUIStyle();
    }

    static GUIStyle MakeLabel(int size, TextAnchor align, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = align,
            clipping = TextClipping.Overflow,
            wordWrap = false,
            richText = false,
            padding = new RectOffset(2, 2, 2, 2),
            normal = { textColor = color }
        };
    }

    /// <summary>Bold text with a black outline (drawn in 8 directions).</summary>
    public static void DrawText(Rect rect, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text) || style == null) return;
        Ensure();

        Color faceMul = GUI.color;
        Color savedStyle = style.normal.textColor;

        // Outline: force black regardless of style / GUI.color tint (keep alpha).
        style.normal.textColor = Color.white;
        GUI.color = new Color(0f, 0f, 0f, faceMul.a);
        float o = OutlinePx;
        GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x - o, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x - o, rect.y + o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), text, style);

        // Face
        style.normal.textColor = savedStyle;
        GUI.color = faceMul;
        GUI.Label(rect, text, style);
    }

    public static void Fill(Rect rect, Color color)
    {
        Ensure();
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _white);
        GUI.color = prev;
    }

    public static void DrawPanel(Rect rect, Color bg)
    {
        DrawPanel(rect, bg, true);
    }

    public static void DrawPanel(Rect rect, Color bg, bool topAccent)
    {
        Ensure();
        Fill(rect, bg);
        if (topAccent)
            Fill(new Rect(rect.x, rect.y, rect.width, 3f), new Color(1f, 1f, 1f, 0.28f));
        Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
        Fill(new Rect(rect.x, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.08f));
        Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.25f));
    }

    public static void DrawBar(Rect rect, float percent, Color fill, string text)
    {
        Ensure();
        percent = Mathf.Clamp01(percent);

        Fill(rect, new Color(0.08f, 0.08f, 0.1f, 0.9f));
        Fill(new Rect(rect.x + 3f, rect.y + 3f, (rect.width - 6f) * percent, rect.height - 6f), fill);
        DrawText(rect, text, _value);
    }

    public static void DrawChip(Rect rect, string text, Color bg)
    {
        Ensure();
        Fill(rect, bg);
        DrawText(rect, text, _chip);
    }

    public static bool Button(Rect rect, string text, bool primary = false)
    {
        Ensure();
        Color fill = primary
            ? new Color(0.28f, 0.42f, 0.72f, 1f)
            : new Color(0.22f, 0.24f, 0.3f, 1f);
        if (rect.Contains(Event.current.mousePosition))
            fill = Color.Lerp(fill, Color.white, 0.14f);

        bool hit = GUI.Button(rect, GUIContent.none, _hitBox);
        if (Event.current.type == EventType.Repaint)
        {
            Fill(rect, fill);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), new Color(1f, 1f, 1f, 0.28f));
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.4f));
            DrawText(rect, text, _buttonLabel);
        }
        return hit;
    }

    public static bool ButtonSm(Rect rect, string text, bool emphasized = false)
    {
        Ensure();
        Color fill = emphasized
            ? new Color(0.22f, 0.55f, 0.32f, 1f)
            : new Color(0.22f, 0.24f, 0.3f, 1f);
        if (rect.Contains(Event.current.mousePosition))
            fill = Color.Lerp(fill, Color.white, 0.14f);

        bool hit = GUI.Button(rect, GUIContent.none, _hitBox);
        if (Event.current.type == EventType.Repaint)
        {
            Fill(rect, fill);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), new Color(1f, 1f, 1f, 0.28f));
            DrawText(rect, text, _buttonLabelSm);
        }
        return hit;
    }

    public static void DrawToast(string text, Color bg)
    {
        Ensure();
        float w = Mathf.Min(Screen.width - 40f, 720f);
        Rect tip = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 120f, w, BannerH);
        DrawPanel(tip, bg);
        DrawText(tip, text, _toast);
    }

    public static void DrawMapDots(Rect rect, int currentMap, int maps, bool bossCleared)
    {
        Ensure();
        float gap = 10f;
        float size = Mathf.Min(18f, (rect.width - gap * (maps - 1)) / maps);
        float y = rect.y + (rect.height - size) * 0.5f;
        for (int i = 1; i <= maps; i++)
        {
            float x = rect.x + (i - 1) * (size + gap);
            Color c;
            if (i == currentMap) c = Accent;
            else if (i == 4 && bossCleared) c = Good;
            else if (i < currentMap) c = new Color(0.45f, 0.5f, 0.58f, 1f);
            else c = new Color(0.2f, 0.22f, 0.28f, 1f);
            Fill(new Rect(x, y, size, size), c);
        }
    }

    /// <summary>Item icon — weapons vary by tier shape/color/size.</summary>
    public static void DrawItemIcon(Rect rect, ItemInstance item)
    {
        if (item == null)
        {
            DrawItemIcon(rect, ItemSlot.Weapon, Color.white, 0);
            return;
        }
        if (item.slot == ItemSlot.Gem)
        {
            DrawGemIcon(rect, item);
            return;
        }
        int style = item.slot == ItemSlot.Weapon
            ? WeaponVisualUtil.StyleIndex(item.itemLevel)
            : ArmorVisualUtil.StyleIndex(item.itemLevel);
        DrawItemIcon(rect, item.slot, item.RarityColor, style, item.itemLevel, item.rarity);
    }

    public static void DrawGemIcon(Rect rect, ItemInstance gem)
    {
        Ensure();
        Color c = gem != null ? GemCatalog.KindColor(gem.gemKind) : Color.white;
        Fill(rect, new Color(0.08f, 0.08f, 0.1f, 0.95f));
        Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 4f), c);

        float cx = rect.x + rect.width * 0.5f;
        float cy = rect.y + rect.height * 0.55f;
        float s = rect.height * 0.28f;
        // Diamond facet
        Fill(new Rect(cx - s * 0.7f, cy - s * 0.2f, s * 1.4f, s * 0.85f), c);
        Fill(new Rect(cx - s * 0.35f, cy - s * 0.75f, s * 0.7f, s * 0.55f), Color.Lerp(c, Color.white, 0.35f));
        Fill(new Rect(cx - 3f, cy - s * 0.15f, 6f, 6f), Color.white);
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
        else if (slot == ItemSlot.Gem)
        {
            GUI.color = rarityColor;
            GUI.DrawTexture(new Rect(cx - 12f, top + h * 0.25f, 24f, 24f), _white);
        }
        else
            DrawArmorGlyph(cx, top, h, style, rarity);

        GUI.color = prev;
    }

    static void DrawArmorGlyph(float cx, float top, float h, int style, ItemRarity rarity)
    {
        Color body = ArmorVisualUtil.BodyColor(style, rarity);
        Color plate = ArmorVisualUtil.PlateColor(style, rarity);
        Color accent = ArmorVisualUtil.AccentColor(style, rarity);

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
            case 1:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 5f * sizeMul, top, 10f * sizeMul, h * 0.58f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 18f * sizeMul, top + h * 0.52f, 36f * sizeMul, 7f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 2:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 2.5f, top, 5f, h * 0.72f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx + 1f, top + 6f, 3f, h * 0.55f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 10f, top + h * 0.66f, 20f, 5f), _white);
                GUI.DrawTexture(new Rect(cx - 2.5f, top + h * 0.7f, 5f, h * 0.2f), _white);
                break;
            case 3:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 7f, top + 6f, 14f, h * 0.5f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx + 6f, top + h * 0.2f, 8f, 10f), _white);
                GUI.DrawTexture(new Rect(cx - 14f, top + h * 0.35f, 8f, 10f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 4:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 12f * sizeMul, top + 4f, 24f * sizeMul, h * 0.5f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 4f, top, 8f, 10f), _white);
                GUI.DrawTexture(new Rect(cx + 6f, top + 8f, 8f, 8f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.52f, 32f, 7f), _white);
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 5:
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
            case 6:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 7f * sizeMul, top, 14f * sizeMul, h * 0.55f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 2f, top + 4f, 4f, h * 0.5f), _white);
                GUI.DrawTexture(new Rect(cx + 6f, top + h * 0.2f, 10f, 6f), _white);
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.4f, 10f, 6f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.58f, 8f, h * 0.28f), _white);
                break;
            case 7:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 8f * sizeMul, top, 16f * sizeMul, h * 0.6f), _white);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(cx - 16f, top + h * 0.2f, 8f, h * 0.35f), _white);
                GUI.DrawTexture(new Rect(cx + 8f, top + h * 0.2f, 8f, h * 0.35f), _white);
                GUI.DrawTexture(new Rect(cx - 6f, top + h * 0.15f, 12f, 12f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.62f, 8f, h * 0.26f), _white);
                break;
            default:
                GUI.color = blade;
                GUI.DrawTexture(new Rect(cx - 5f, top + 4f, 10f, h * 0.5f), _white);
                GUI.color = grip;
                GUI.DrawTexture(new Rect(cx - 14f, top + h * 0.5f, 28f, 6f), _white);
                GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.56f, 8f, h * 0.28f), _white);
                break;
        }

        GUI.color = rarityColor;
        GUI.DrawTexture(new Rect(cx - 4f, top + h * 0.48f, 8f, 8f), _white);
    }

    public static GUIStyle Title => _title;
    public static GUIStyle Label => _label;
    public static GUIStyle Hint => _hint;
    public static GUIStyle Banner => _banner;
    public static GUIStyle ItemName => _itemName;
    public static GUIStyle ItemMeta => _itemMeta;
    public static GUIStyle FieldTag => _fieldTag;
    public static GUIStyle Chip => _chip;
}
