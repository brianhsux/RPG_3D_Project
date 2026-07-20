using UnityEngine;

/// <summary>
/// Drag Asset Store / custom prefabs here. Empty slots keep the procedural primitives.
/// Place the asset at Resources/VisualPrefabCatalog (or assign on MapRuntimeBuilder).
/// </summary>
[CreateAssetMenu(menuName = "RPG/Visual Prefab Catalog", fileName = "VisualPrefabCatalog")]
public class VisualPrefabCatalog : ScriptableObject
{
    public static VisualPrefabCatalog Active { get; set; }

    const string ResourcesName = "VisualPrefabCatalog";

    [Header("Environment (zone decor)")]
    [Tooltip("Single tree prefab. Used if Tree Variants is empty.")]
    public GameObject treePrefab;
    [Tooltip("Optional random pool for forest trees.")]
    public GameObject[] treeVariants;
    public float treeScale = 1f;

    public GameObject[] bushVariants;
    public float bushScale = 1f;

    public GameObject[] rockVariants;
    public float rockScale = 1f;

    public GameObject[] mushroomVariants;
    public float mushroomScale = 1f;

    public GameObject stumpPrefab;
    public float stumpScale = 1f;

    public GameObject[] grassVariants;
    public float grassScale = 1f;

    [Header("Village props (Polytope fence / bridge)")]
    public GameObject[] fenceVariants;
    public float fenceScale = 1f;
    public GameObject gatePrefab;
    public float gateScale = 1f;
    public GameObject bridgePrefab;
    public float bridgeScale = 1f;

    public GameObject housePrefab;
    public float houseScale = 1f;

    public GameObject stallPrefab;
    public float stallScale = 1f;

    public GameObject ruinWallPrefab;
    public GameObject ruinPillarPrefab;
    public GameObject rubblePrefab;
    public float ruinScale = 1f;

    public GameObject bossAltarPrefab;
    public float bossAltarScale = 1f;

    public GameObject worldPortalPrefab;
    public float worldPortalScale = 1f;

    [Header("Enemies — leave Kind empty slots unused")]
    public EnemyPrefabEntry[] enemies;

    [Header("Weapons by style 0–7 (木/鐵/細刃/骨/焰/霜/雷/虛空)")]
    public GameObject[] weaponStylePrefabs = new GameObject[8];
    public Vector3 weaponLocalPosition = Vector3.zero;
    public Vector3 weaponLocalEuler = Vector3.zero;
    public float weaponPrefabScale = 1f;

    [Header("Player armor body by style 0–7 (布衣…終焉)")]
    [Tooltip("Full-body mesh replacing Body+Head when set.")]
    public GameObject[] armorStylePrefabs = new GameObject[8];
    public GameObject defaultPlayerBodyPrefab;
    public Vector3 armorLocalPosition = Vector3.zero;
    public Vector3 armorLocalEuler = Vector3.zero;
    public float armorPrefabScale = 1f;

    [Header("Tint prefab materials with rarity colors")]
    public bool tintWeaponPrefab = false;
    public bool tintArmorPrefab = true;

    public static VisualPrefabCatalog Get()
    {
        if (Active != null) return Active;
        Active = Resources.Load<VisualPrefabCatalog>(ResourcesName);
        return Active;
    }

    public GameObject PickTree()
    {
        return PickFrom(treeVariants, treePrefab);
    }

    public GameObject PickBush() => PickFrom(bushVariants, null);
    public GameObject PickRock() => PickFrom(rockVariants, rubblePrefab);
    public GameObject PickMushroom() => PickFrom(mushroomVariants, null);
    public GameObject PickGrass() => PickFrom(grassVariants, null);
    public GameObject PickFence() => PickFrom(fenceVariants, null);

    static GameObject PickFrom(GameObject[] variants, GameObject fallback)
    {
        if (variants != null && variants.Length > 0)
        {
            // Prefer non-null entries.
            int start = Random.Range(0, variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                var pick = variants[(start + i) % variants.Length];
                if (pick != null) return pick;
            }
        }
        return fallback;
    }

    public GameObject GetEnemyPrefab(EnemyKind kind, out float scaleMul)
    {
        scaleMul = 1f;
        if (enemies == null) return null;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].kind == kind && enemies[i].prefab != null)
            {
                scaleMul = enemies[i].scale <= 0f ? 1f : enemies[i].scale;
                return enemies[i].prefab;
            }
        }
        return null;
    }

    public GameObject GetWeaponPrefab(int style)
    {
        if (weaponStylePrefabs == null || style < 0 || style >= weaponStylePrefabs.Length)
            return null;
        return weaponStylePrefabs[style];
    }

    public GameObject GetArmorPrefab(int style)
    {
        if (style < 0)
            return defaultPlayerBodyPrefab;
        if (armorStylePrefabs != null && style < armorStylePrefabs.Length && armorStylePrefabs[style] != null)
            return armorStylePrefabs[style];
        return defaultPlayerBodyPrefab;
    }
}

[System.Serializable]
public class EnemyPrefabEntry
{
    public EnemyKind kind;
    public GameObject prefab;
    [Tooltip("Extra scale on top of CreateEnemy scale.")]
    public float scale = 1f;
}

/// <summary>Shared spawn helpers for catalog prefabs.</summary>
public static class VisualPrefabUtil
{
    public static GameObject Spawn(
        GameObject prefab,
        Transform parent,
        string name,
        Vector3 worldPos,
        float scaleMul = 1f,
        bool stripColliders = true)
    {
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, parent);
        go.name = name;
        go.transform.position = worldPos;
        if (Mathf.Abs(scaleMul - 1f) > 0.0001f)
            go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * scaleMul);
        if (stripColliders)
            StripColliders(go);
        return go;
    }

    public static GameObject SpawnLocal(
        GameObject prefab,
        Transform parent,
        string name,
        Vector3 localPos,
        Vector3 localEuler,
        float scaleMul = 1f,
        bool stripColliders = true)
    {
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        if (Mathf.Abs(scaleMul - 1f) > 0.0001f)
            go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * scaleMul);
        if (stripColliders)
            StripColliders(go);
        return go;
    }

    public static void StripColliders(GameObject root)
    {
        if (root == null) return;
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(c);
        foreach (var cc in root.GetComponentsInChildren<CharacterController>(true))
            Object.Destroy(cc);
    }

    public static void TintRenderers(Transform root, Color color)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                if (mats[m].HasProperty("_BaseColor"))
                    mats[m].SetColor("_BaseColor", color);
                else
                    mats[m].color = color;
            }
            r.materials = mats;
        }
    }

    public static void SetPrimitiveRenderersEnabled(Transform root, bool enabled, params string[] skipNames)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            string n = child.name;
            bool skip = false;
            if (skipNames != null)
            {
                for (int s = 0; s < skipNames.Length; s++)
                {
                    if (n == skipNames[s]) { skip = true; break; }
                }
            }
            if (skip) continue;
            var r = child.GetComponent<Renderer>();
            if (r != null) r.enabled = enabled;
        }
    }
}
