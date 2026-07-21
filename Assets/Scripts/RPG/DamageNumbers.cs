using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floating combat numbers — punch scale, arc drift, fade. Readable bold + thick outline.
/// </summary>
public class DamageNumbers : MonoBehaviour
{
    public static DamageNumbers Instance { get; private set; }

    const float Lifetime = 0.95f;
    const float RisePx = 92f;
    const float Outline = 3f;

    struct Popup
    {
        public Vector3 worldAnchor;
        public float born;
        public int amount;
        public bool onPlayer;
        public float driftX;
        public float startLift;
    }

    readonly List<Popup> _popups = new List<Popup>(32);
    GUIStyle _style;
    GUIStyle _styleBig;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("DamageNumbers");
        go.AddComponent<DamageNumbers>();
    }

    public static void Spawn(Vector3 worldPos, float damage, bool onPlayer, bool isBoss = false)
    {
        if (damage < 0.5f) return;
        EnsureExists();
        Instance.Add(worldPos, damage, onPlayer, isBoss);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Add(Vector3 worldPos, float damage, bool onPlayer, bool isBoss)
    {
        float height = onPlayer ? 1.9f : (isBoss ? 3.6f : 2.4f);
        _popups.Add(new Popup
        {
            worldAnchor = worldPos + Vector3.up * height,
            born = Time.unscaledTime,
            amount = Mathf.Max(1, Mathf.RoundToInt(damage)),
            onPlayer = onPlayer,
            driftX = Random.Range(-42f, 42f),
            startLift = Random.Range(-6f, 10f)
        });
    }

    void LateUpdate()
    {
        float now = Time.unscaledTime;
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (now - _popups[i].born >= Lifetime)
                _popups.RemoveAt(i);
        }
    }

    void OnGUI()
    {
        if (_popups.Count == 0) return;
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel) return;
        if (Camera.main == null) return;

        EnsureStyles();
        RPGUI.Ensure();

        int prevDepth = GUI.depth;
        GUI.depth = -500;

        float now = Time.unscaledTime;
        for (int i = 0; i < _popups.Count; i++)
        {
            var p = _popups[i];
            float t = (now - p.born) / Lifetime;
            if (t < 0f || t > 1f) continue;

            Vector3 screen = Camera.main.WorldToScreenPoint(p.worldAnchor);
            if (screen.z < 0.1f) continue;

            // Motion: ease-out rise + horizontal drift + early punch scale
            float ease = 1f - (1f - t) * (1f - t);
            float x = screen.x + p.driftX * ease;
            float y = Screen.height - screen.y - p.startLift - RisePx * ease;

            float alpha;
            if (t < 0.55f) alpha = 1f;
            else alpha = 1f - (t - 0.55f) / 0.45f;

            float scale;
            if (t < 0.12f)
                scale = Mathf.Lerp(0.55f, 1.35f, t / 0.12f);
            else if (t < 0.28f)
                scale = Mathf.Lerp(1.35f, 1f, (t - 0.12f) / 0.16f);
            else
                scale = Mathf.Lerp(1f, 0.88f, (t - 0.28f) / 0.72f);

            bool big = p.amount >= 40 || (!p.onPlayer && p.amount >= 25);
            GUIStyle style = big ? _styleBig : _style;
            string text = p.amount.ToString();

            float w = big ? 160f : 120f;
            float h = big ? 64f : 52f;
            Rect rect = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);

            Color face = p.onPlayer
                ? new Color(1f, 0.35f, 0.38f, alpha)
                : (big
                    ? new Color(1f, 0.92f, 0.25f, alpha)
                    : new Color(1f, 0.98f, 0.85f, alpha));

            Matrix4x4 prevMatrix = GUI.matrix;
            Vector2 pivot = new Vector2(x, y);
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, pivot);

            DrawOutlinedNumber(rect, text, style, face);

            GUI.matrix = prevMatrix;
        }

        GUI.depth = prevDepth;
    }

    void EnsureStyles()
    {
        if (_style != null) return;
        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false,
            richText = false,
            normal = { textColor = Color.white }
        };
        _styleBig = new GUIStyle(_style) { fontSize = 46 };
    }

    static void DrawOutlinedNumber(Rect rect, string text, GUIStyle style, Color face)
    {
        Color saved = style.normal.textColor;
        style.normal.textColor = Color.white;

        GUI.color = new Color(0f, 0f, 0f, face.a);
        float o = Outline;
        GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x - o, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x - o, rect.y + o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), text, style);
        // Extra thickness for readability
        GUI.Label(new Rect(rect.x - o * 1.5f, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o * 1.5f, rect.y, rect.width, rect.height), text, style);

        style.normal.textColor = Color.white;
        GUI.color = face;
        GUI.Label(rect, text, style);

        style.normal.textColor = saved;
        GUI.color = Color.white;
    }
}
