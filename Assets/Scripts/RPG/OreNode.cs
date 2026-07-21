using UnityEngine;

/// <summary>
/// Mineable crystal ore. Approach and hold/press E to extract a gem.
/// </summary>
public class OreNode : MonoBehaviour
{
    public int contentLevel = 1;
    public int hitsRequired = 4;
    public float interactRange = 2.8f;
    public string oreName = "Crystal Ore";

    int _hitsLeft;
    bool _depleted;
    Renderer[] _renderers;
    Color[] _baseColors;
    float _pulse;

    public bool IsDepleted => _depleted;
    public int HitsLeft => _hitsLeft;

    public void Configure(int contentLevel, string displayName, Color tint)
    {
        this.contentLevel = Mathf.Max(1, contentLevel);
        oreName = displayName;
        hitsRequired = Mathf.Clamp(2 + contentLevel / 8, 3, 7);
        _hitsLeft = hitsRequired;
        _depleted = false;
        CacheRenderers();
        ApplyTint(tint);
    }

    void Awake()
    {
        if (_hitsLeft <= 0) _hitsLeft = hitsRequired > 0 ? hitsRequired : 4;
        CacheRenderers();
    }

    void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_BaseColor"))
                _baseColors[i] = _renderers[i].sharedMaterial.GetColor("_BaseColor");
            else if (_renderers[i].sharedMaterial != null)
                _baseColors[i] = _renderers[i].sharedMaterial.color;
            else
                _baseColors[i] = Color.white;
        }
    }

    void ApplyTint(Color tint)
    {
        if (_renderers == null) CacheRenderers();
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = Color.Lerp(_baseColors[i], tint, 0.65f);
            _renderers[i].material.color = c;
            if (_renderers[i].material.HasProperty("_BaseColor"))
                _renderers[i].material.SetColor("_BaseColor", c);
        }
    }

    void Update()
    {
        if (_depleted) return;
        _pulse += Time.deltaTime * 2.5f;
        float s = 1f + Mathf.Sin(_pulse) * 0.04f;
        transform.localScale = Vector3.one * s;
    }

    public bool TryMine(PlayerInventory inv)
    {
        if (_depleted || inv == null) return false;
        _hitsLeft--;

        // Shrink / flash feedback
        transform.localScale *= 0.92f;

        if (_hitsLeft > 0)
        {
            inv.ShowLoot($"{oreName}… ({hitsRequired - _hitsLeft}/{hitsRequired})");
            return true;
        }

        _depleted = true;
        var gem = GemCatalog.RollMineDrop(contentLevel);
        if (!inv.AddItem(gem))
        {
            // Bag full — refund one hit so player can clear bag
            _depleted = false;
            _hitsLeft = 1;
            return false;
        }

        // Deplete visual
        if (_renderers == null) CacheRenderers();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            Color g = new Color(0.25f, 0.25f, 0.28f);
            r.material.color = g;
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", g);
        }
        transform.localScale = Vector3.one * 0.55f;
        inv.ShowLoot($"Mined {gem.displayName}!");
        return true;
    }

    public static OreNode FindNearest(Vector3 pos, float range)
    {
        OreNode best = null;
        float bestDist = range;
        var nodes = FindObjectsByType<OreNode>(FindObjectsSortMode.None);
        foreach (var n in nodes)
        {
            if (n == null || n._depleted) continue;
            float d = Vector3.Distance(pos, n.transform.position);
            if (d <= n.interactRange && d < bestDist)
            {
                bestDist = d;
                best = n;
            }
        }
        return best;
    }
}
