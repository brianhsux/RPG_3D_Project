using UnityEngine;

/// <summary>
/// One world = one large open square (~80x80).
/// Inside: 4 distinct scenic zones you walk between.
/// Monsters live in their own zone — not piled in one corner.
/// </summary>
public class MapRuntimeBuilder : MonoBehaviour
{
    public static MapRuntimeBuilder Instance { get; private set; }

    public const float WorldSize = 80f;

    [Tooltip("Optional. If empty, loads Resources/VisualPrefabCatalog.")]
    public VisualPrefabCatalog catalog;

    Transform _root;
    GameObject _worldPortal;
    GameObject _villageReturn;
    readonly Vector3[] _zoneCenters = new Vector3[5]; // 1..4
    Vector3 _villageSpawn = new Vector3(0f, 0.5f, 2f);
    WorldLayoutProfiles.Layout _layout;

    void Awake()
    {
        Instance = this;
        if (catalog != null)
            VisualPrefabCatalog.Active = catalog;
        else if (VisualPrefabCatalog.Active == null)
            VisualPrefabCatalog.Get();

        ApplyLayout(WorldLayoutProfiles.ForWorld(1));
    }

    VisualPrefabCatalog Cat => catalog != null ? catalog : VisualPrefabCatalog.Get();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ApplyLayout(WorldLayoutProfiles.Layout layout)
    {
        _layout = layout ?? WorldLayoutProfiles.ForWorld(1);
        for (int i = 1; i <= 4; i++)
            _zoneCenters[i] = _layout.zoneCenters[i];
    }

    public Vector3 GetTileCenter(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        return _zoneCenters[map];
    }

    public Vector3 GetTileSpawn(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        Vector3 offset = _layout != null ? _layout.spawnOffsets[map] : new Vector3(0f, 0.5f, -4f);
        return _zoneCenters[map] + offset;
    }

    public Vector3 GetVillageSpawn() => _villageSpawn;

    public void BuildCurrentMap() => BuildCurrentWorld();

    public void BuildCurrentWorld()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _worldPortal = null;
        _villageReturn = null;
        _root = new GameObject("RuntimeWorld").transform;

        int world = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
        if (world < 1) world = 1;

        ApplyLayout(WorldLayoutProfiles.ForWorld(world));
        // Stable prop scatter per world rebuild
        Random.InitState(world * 9176 + 41);

        BuildBigArena(world);

        for (int map = 1; map <= 4; map++)
            BuildZone(world, map, _zoneCenters[map]);

        SpawnChallengeAltar(_root, world);
        SpawnVillageReturnGate();

        if (AdventureManager.Instance != null && AdventureManager.Instance.worldPortalReady)
            SpawnWorldPortal();
    }

    public void BuildVillage()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _worldPortal = null;
        _villageReturn = null;
        _root = new GameObject("RuntimeVillage").transform;

        float size = 56f;
        float half = size * 0.5f;
        var ground = MakeTempMat(new Color(0.34f, 0.58f, 0.32f));
        var wall = MakeTempMat(new Color(0.48f, 0.42f, 0.34f));
        var path = MakeTempMat(new Color(0.62f, 0.55f, 0.4f));
        var wood = MakeTempMat(new Color(0.45f, 0.3f, 0.18f));
        var roof = MakeTempMat(new Color(0.55f, 0.22f, 0.18f));
        var water = MakeTempMat(new Color(0.25f, 0.55f, 0.85f));

        Prim(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(size, 0.5f, size), ground);
        float wh = 2.8f;
        Prim(PrimitiveType.Cube, "Wall_N", new Vector3(0f, wh * 0.5f, half), new Vector3(size, wh, 0.7f), wall);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0f, wh * 0.5f, -half), new Vector3(size, wh, 0.7f), wall);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(half, wh * 0.5f, 0f), new Vector3(0.7f, wh, size), wall);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-half, wh * 0.5f, 0f), new Vector3(0.7f, wh, size), wall);

        Prim(PrimitiveType.Cylinder, "Plaza", new Vector3(0f, 0.04f, 0f), new Vector3(12f, 0.05f, 12f), path);
        Prim(PrimitiveType.Cylinder, "FountainBase", new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.35f, 2.4f), wall);
        Prim(PrimitiveType.Cylinder, "FountainWater", new Vector3(0f, 0.85f, 0f), new Vector3(1.6f, 0.4f, 1.6f), water);

        BuildHouse(_root, new Vector3(-10f, 0f, 8f), wood, roof);
        BuildHouse(_root, new Vector3(10f, 0f, 7f), wood, roof);
        BuildHouse(_root, new Vector3(-9f, 0f, -9f), wood, roof);
        BuildHouse(_root, new Vector3(11f, 0f, -8f), wood, roof);

        // Decorative trees around the rim
        var foliage = MakeTempMat(new Color(0.2f, 0.5f, 0.22f));
        for (int i = 0; i < 10; i++)
        {
            float ang = i / 10f * Mathf.PI * 2f;
            Vector3 tp = new Vector3(Mathf.Cos(ang) * 22f, 0f, Mathf.Sin(ang) * 22f);
            Prim(PrimitiveType.Cylinder, "TreeTrunk" + i, tp + Vector3.up * 1.1f, new Vector3(0.45f, 1.1f, 0.45f), wood);
            Prim(PrimitiveType.Sphere, "TreeTop" + i, tp + Vector3.up * 2.6f, Vector3.one * 2.2f, foliage);
        }

        _villageSpawn = new Vector3(0f, 0.5f, 2f);
        SpawnVillageWorldPortals();
    }

    void SpawnVillageWorldPortals()
    {
        int unlocked = AdventureManager.Instance != null
            ? AdventureManager.Instance.highestWorldUnlocked
            : 1;

        for (int w = 1; w <= ProgressionFormulas.WorldCount; w++)
        {
            float ang = ((w - 1) / (float)ProgressionFormulas.WorldCount) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float rad = ProgressionFormulas.WorldCount > 12 ? 22f : 16f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            bool open = w <= unlocked;

            Color accent = open ? Color.Lerp(ThemeGround(w), ThemeBossAccent(w), 0.55f) : new Color(0.22f, 0.22f, 0.24f);
            var mat = MakeTempMat(accent);
            var frame = MakeTempMat(open ? new Color(0.55f, 0.48f, 0.35f) : new Color(0.18f, 0.18f, 0.2f));

            Prim(PrimitiveType.Cube, $"GateFrame_W{w}", pos + new Vector3(0f, 1.6f, 0f), new Vector3(0.35f, 3.2f, 2.4f), frame);
            var gate = Prim(PrimitiveType.Cylinder, $"WorldGate_W{w}", pos + new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 1.4f, 0.35f), mat);
            Object.Destroy(gate.GetComponent<Collider>());

            var trigger = new GameObject($"VillagePortal_W{w}");
            trigger.transform.SetParent(_root, false);
            trigger.transform.position = pos + new Vector3(0f, 1f, 0f);
            var col = trigger.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = open ? 1.55f : 1.35f;

            var portal = trigger.AddComponent<VillageWorldPortalTrigger>();
            portal.targetWorld = w;

            var label = trigger.AddComponent<WorldPortalLabel>();
            label.worldId = w;
            label.unlocked = open;

            if (open)
            {
                var lightGo = new GameObject("GateLight");
                lightGo.transform.SetParent(trigger.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = accent;
                light.intensity = 3.5f;
                light.range = 8f;
            }
        }
    }

    void SpawnVillageReturnGate()
    {
        if (_root == null || _villageReturn != null) return;

        // Always at the hub center — the middle door back to the Village.
        Vector3 pos = _layout != null ? _layout.hub : Vector3.zero;
        var mat = MakeTempMat(new Color(0.35f, 0.75f, 0.45f));
        var frame = MakeTempMat(new Color(0.5f, 0.42f, 0.28f));
        Prim(PrimitiveType.Cube, "VillageGateFrame", pos + new Vector3(0f, 1.5f, 0f), new Vector3(2.6f, 3f, 0.35f), frame);
        _villageReturn = Prim(PrimitiveType.Cylinder, "VillageReturnGate", pos + new Vector3(0f, 1.4f, 0f), new Vector3(2.2f, 1.3f, 0.4f), mat);
        Object.Destroy(_villageReturn.GetComponent<Collider>());

        var trigger = new GameObject("VillageReturnTrigger");
        trigger.transform.SetParent(_root, false);
        trigger.transform.position = pos + new Vector3(0f, 1f, 0f);
        var col = trigger.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.7f;
        trigger.AddComponent<VillageReturnTrigger>();

        var label = trigger.AddComponent<WorldPortalLabel>();
        label.worldId = 0;
        label.unlocked = true;

        var lightGo = new GameObject("VillageGateLight");
        lightGo.transform.SetParent(trigger.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.4f, 0.9f, 0.5f);
        light.intensity = 4f;
        light.range = 10f;
    }

    Color ThemeGround(int world)
    {
        Color c;
        switch (ThemeIndex(world))
        {
            case 1: c = new Color(0.32f, 0.55f, 0.25f); break;
            case 2: c = new Color(0.16f, 0.4f, 0.18f); break;
            case 3: c = new Color(0.45f, 0.42f, 0.38f); break;
            case 4: c = new Color(0.45f, 0.22f, 0.12f); break;
            case 5: c = new Color(0.22f, 0.12f, 0.28f); break;
            case 6: c = new Color(0.72f, 0.82f, 0.92f); break;
            case 7: c = new Color(0.72f, 0.58f, 0.32f); break;
            case 8: c = new Color(0.22f, 0.38f, 0.28f); break;
            case 9: c = new Color(0.55f, 0.7f, 0.9f); break;
            default: c = new Color(0.12f, 0.05f, 0.14f); break;
        }
        return ShiftTheme(c, world);
    }

    Color ThemeWall(int world)
    {
        Color c;
        switch (ThemeIndex(world))
        {
            case 1: c = new Color(0.5f, 0.48f, 0.42f); break;
            case 2: c = new Color(0.28f, 0.22f, 0.14f); break;
            case 3: c = new Color(0.55f, 0.55f, 0.52f); break;
            case 4: c = new Color(0.35f, 0.15f, 0.1f); break;
            case 5: c = new Color(0.15f, 0.08f, 0.2f); break;
            case 6: c = new Color(0.55f, 0.7f, 0.85f); break;
            case 7: c = new Color(0.55f, 0.4f, 0.22f); break;
            case 8: c = new Color(0.18f, 0.28f, 0.2f); break;
            case 9: c = new Color(0.75f, 0.8f, 0.9f); break;
            default: c = new Color(0.25f, 0.08f, 0.12f); break;
        }
        return ShiftTheme(c, world);
    }

    Color ThemeBossAccent(int world)
    {
        Color c;
        switch (ThemeIndex(world))
        {
            case 4: c = new Color(1f, 0.35f, 0.05f); break;
            case 6: c = new Color(0.65f, 0.85f, 1f); break;
            case 7: c = new Color(0.9f, 0.7f, 0.25f); break;
            case 8: c = new Color(0.35f, 0.7f, 0.25f); break;
            case 9: c = new Color(0.85f, 0.9f, 1f); break;
            case 10: c = new Color(0.7f, 0.15f, 0.85f); break;
            case 5: c = new Color(0.55f, 0.2f, 0.75f); break;
            default: c = new Color(0.55f, 0.12f, 0.12f); break;
        }
        return ShiftTheme(c, world);
    }

    static int ThemeIndex(int world)
    {
        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        return ((world - 1) % 10) + 1;
    }

    static Color ShiftTheme(Color c, int world)
    {
        if (world <= 10) return c;
        float t = ((world - 11) % 15) / 15f;
        // Mild channel rotate so late worlds don't look identical to early clones.
        return Color.Lerp(c, new Color(c.b, c.r, c.g, c.a), 0.18f + t * 0.22f);
    }

    Color ZoneFloor(int world, int map)
    {
        Color baseCol = ThemeGround(world);
        Color accent;
        switch (map)
        {
            case 1: accent = new Color(0.55f, 0.48f, 0.32f); break; // settlement
            case 2: accent = ThemeFoliage(world); break;
            case 3: accent = ThemeWall(world); break;
            case 4: accent = ThemeBossAccent(world); break;
            default: return baseCol;
        }
        float t = map == 4 ? 0.35f : 0.45f;
        return Color.Lerp(baseCol, accent, t);
    }

    Material MakeTempMat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : _root, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;

        // Thin floor discs / pads use Cylinder CapsuleColliders (or thin BoxColliders) that
        // block CharacterController. Real walk collision comes from the main Ground cube.
        bool flatFloorOverlay = scale.y <= 0.12f && Mathf.Max(scale.x, scale.z) >= 1f;
        if (flatFloorOverlay)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        return go;
    }

    void BuildBigArena(int world)
    {
        var groundMat = MakeTempMat(ThemeGround(world));
        var wallMat = MakeTempMat(ThemeWall(world));
        var pathMat = MakeTempMat(ThemePath(world));
        var blockMat = MakeTempMat(Color.Lerp(ThemeWall(world), ThemeBossAccent(world), 0.25f));

        float half = WorldSize * 0.5f;
        float wh = 3f;

        Prim(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(WorldSize, 0.5f, WorldSize), groundMat);

        Prim(PrimitiveType.Cube, "Wall_N", new Vector3(0f, wh / 2f, half), new Vector3(WorldSize, wh, 0.8f), wallMat);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0f, wh / 2f, -half), new Vector3(WorldSize, wh, 0.8f), wallMat);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(half, wh / 2f, 0f), new Vector3(0.8f, wh, WorldSize), wallMat);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-half, wh / 2f, 0f), new Vector3(0.8f, wh, WorldSize), wallMat);

        Vector3 hub = _layout != null ? _layout.hub : Vector3.zero;
        float hubScale = _layout != null ? _layout.hubScale : 8f;
        Prim(PrimitiveType.Cylinder, "Hub", hub + new Vector3(0f, 0.04f, 0f), new Vector3(hubScale, 0.05f, hubScale), pathMat);

        if (_layout != null && _layout.paths != null)
        {
            for (int i = 0; i < _layout.paths.Length; i++)
            {
                var seg = _layout.paths[i];
                var go = Prim(PrimitiveType.Cube, "Path_" + i, seg.pos, seg.scale, pathMat);
                if (Mathf.Abs(seg.yawY) > 0.01f)
                    go.transform.rotation = Quaternion.Euler(0f, seg.yawY, 0f);
            }
        }
        else
        {
            Prim(PrimitiveType.Cube, "Path_W", new Vector3(-12f, 0.03f, 0f), new Vector3(14f, 0.06f, 4f), pathMat);
            Prim(PrimitiveType.Cube, "Path_E", new Vector3(12f, 0.03f, 0f), new Vector3(14f, 0.06f, 4f), pathMat);
            Prim(PrimitiveType.Cube, "Path_N", new Vector3(0f, 0.03f, 12f), new Vector3(4f, 0.06f, 14f), pathMat);
            Prim(PrimitiveType.Cube, "Path_S", new Vector3(0f, 0.03f, -11f), new Vector3(4f, 0.06f, 12f), pathMat);
        }

        if (_layout != null && _layout.blockers != null)
        {
            for (int i = 0; i < _layout.blockers.Length; i++)
            {
                var b = _layout.blockers[i];
                var go = Prim(PrimitiveType.Cube, "RouteBlock_" + i, b.pos, b.scale, blockMat);
                if (Mathf.Abs(b.yawY) > 0.01f)
                    go.transform.rotation = Quaternion.Euler(0f, b.yawY, 0f);
            }
        }

        float p = half - 2f;
        BuildCornerLandmark(world, new Vector3(p, 0f, p), "PillarNE");
        BuildCornerLandmark(world, new Vector3(-p, 0f, p), "PillarNW");
        BuildCornerLandmark(world, new Vector3(p, 0f, -p), "PillarSE");
        BuildCornerLandmark(world, new Vector3(-p, 0f, -p), "PillarSW");
    }

    void BuildCornerLandmark(int world, Vector3 pos, string name)
    {
        var wall = MakeTempMat(ThemeWall(world));
        var accent = MakeTempMat(ThemeBossAccent(world));
        switch (ThemeIndex(world))
        {
            case 6:
                Prim(PrimitiveType.Cube, name, pos + Vector3.up * 2.2f, new Vector3(0.7f, 2.2f, 0.7f), accent);
                break;
            case 7:
                Prim(PrimitiveType.Cube, name, pos + Vector3.up * 2.4f, new Vector3(0.9f, 2.4f, 0.9f), wall);
                Prim(PrimitiveType.Cube, name + "Tip", pos + Vector3.up * 5.0f, new Vector3(0.45f, 0.45f, 0.45f), accent);
                break;
            case 9:
                Prim(PrimitiveType.Cylinder, name, pos + Vector3.up * 2.5f, new Vector3(1.0f, 2.5f, 1.0f), wall);
                Prim(PrimitiveType.Sphere, name + "Cloud", pos + Vector3.up * 5.2f, Vector3.one * 2.2f, MakeTempMat(new Color(0.92f, 0.95f, 1f)));
                break;
            case 10:
                Prim(PrimitiveType.Cube, name, pos + Vector3.up * 2.8f, new Vector3(0.8f, 2.8f, 0.8f), accent);
                break;
            default:
                Prim(PrimitiveType.Cylinder, name, pos + Vector3.up * 1.6f, new Vector3(1.2f, 1.6f, 1.2f), wall);
                break;
        }
    }

    Color ThemePath(int world)
    {
        switch (ThemeIndex(world))
        {
            case 3: return new Color(0.45f, 0.42f, 0.4f);
            case 4: return new Color(0.35f, 0.22f, 0.16f);
            case 5: return new Color(0.3f, 0.18f, 0.35f);
            case 6: return new Color(0.7f, 0.78f, 0.88f);
            case 7: return new Color(0.78f, 0.65f, 0.4f);
            case 8: return new Color(0.3f, 0.35f, 0.22f);
            case 9: return new Color(0.75f, 0.8f, 0.9f);
            case 10: return new Color(0.2f, 0.12f, 0.22f);
            default: return new Color(0.62f, 0.55f, 0.38f);
        }
    }

    void BuildZone(int world, int map, Vector3 center)
    {
        var zoneRoot = new GameObject($"Zone_Map{map}").transform;
        zoneRoot.SetParent(_root, false);

        float zoneSize = 18f;
        if (_layout != null && map >= 1 && map < _layout.zoneSizes.Length)
            zoneSize = _layout.zoneSizes[map];
        else if (map == 4)
            zoneSize = 20f;

        var floorMat = MakeTempMat(ZoneFloor(world, map));
        Prim(PrimitiveType.Cube, "ZoneFloor", center + new Vector3(0f, 0.02f, 0f), new Vector3(zoneSize, 0.05f, zoneSize), floorMat, zoneRoot);

        Prim(PrimitiveType.Cube, "ZonePole", center + new Vector3(-zoneSize * 0.4f, 1.2f, -zoneSize * 0.4f),
            new Vector3(0.35f, 2.4f, 0.35f), MakeTempMat(ThemeBossAccent(world)), zoneRoot);

        AddZoneDecor(zoneRoot, world, map, center, zoneSize);
        SpawnOreNodes(zoneRoot, world, map, center, zoneSize);

        var zone = new GameObject("TileZone_Map" + map);
        zone.transform.SetParent(zoneRoot, false);
        zone.transform.position = center + Vector3.up;
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(zoneSize + 2f, 5f, zoneSize + 2f);
        var zt = zone.AddComponent<MapTileZone>();
        zt.mapIndex = map;

        SpawnEnemiesInZone(zoneRoot, world, map, center);
    }

    struct ScatterRecipe
    {
        public int trees, bushes, rocks, mushrooms, grass, stumps;
        public float treeScaleMul;
        public bool houses, fences, bridge, ruins;
    }

    ScatterRecipe RecipeFor(int world, int map)
    {
        ScatterRecipe r;
        switch (map)
        {
            case 1:
                r = new ScatterRecipe { trees = 1, bushes = 3, rocks = 2, mushrooms = 1, grass = 4, stumps = 0, treeScaleMul = 1f, houses = true, fences = true, bridge = true, ruins = false };
                break;
            case 2:
                r = new ScatterRecipe { trees = 10, bushes = 6, rocks = 4, mushrooms = 4, grass = 10, stumps = 2, treeScaleMul = 1f, houses = false, fences = false, bridge = false, ruins = false };
                break;
            case 3:
                r = new ScatterRecipe { trees = 1, bushes = 1, rocks = 5, mushrooms = 0, grass = 2, stumps = 1, treeScaleMul = 0.9f, houses = false, fences = false, bridge = false, ruins = true };
                break;
            default:
                r = new ScatterRecipe { trees = 0, bushes = 0, rocks = 5, mushrooms = 0, grass = 0, stumps = 0, treeScaleMul = 1f, houses = false, fences = false, bridge = false, ruins = false };
                break;
        }

        switch (ThemeIndex(world))
        {
            case 1:
                if (map == 2) { r.trees = 5; r.grass = 14; r.bushes = 4; r.rocks = 2; }
                if (map == 1) { r.grass = 8; r.trees = 2; }
                break;
            case 2:
                if (map == 2) { r.trees = 14; r.bushes = 8; r.stumps = 3; r.mushrooms = 6; }
                if (map == 1) { r.trees = 3; }
                break;
            case 3:
                r.trees = Mathf.Max(0, r.trees - 1);
                r.rocks += 3;
                r.grass = Mathf.Max(0, r.grass - 2);
                r.bushes = Mathf.Max(0, r.bushes - 1);
                if (map == 1) { r.fences = false; r.ruins = true; }
                break;
            case 4:
                r.trees = map == 2 ? 3 : 0;
                r.rocks += 4;
                r.grass = 0;
                r.mushrooms = 0;
                r.bushes = Mathf.Min(r.bushes, 2);
                if (map == 1) { r.fences = false; r.bridge = false; }
                break;
            case 5:
                r.trees = map == 2 ? 6 : r.trees;
                r.treeScaleMul = 1.15f;
                r.mushrooms += 2;
                r.rocks += 2;
                if (map == 1) r.fences = false;
                break;
            case 6:
                r.trees = map == 2 ? 6 : Mathf.Min(r.trees, 1);
                r.grass = Mathf.Max(0, r.grass / 2);
                r.rocks += 3;
                r.mushrooms = 0;
                break;
            case 7:
                r.trees = 0;
                r.stumps = 0;
                r.grass = 0;
                r.mushrooms = 0;
                r.bushes = map == 1 ? 2 : 1;
                r.rocks += 6;
                if (map == 1) { r.fences = false; r.bridge = false; }
                if (map == 2) r.rocks = 10;
                break;
            case 8:
                r.trees = map == 2 ? 5 : r.trees;
                r.mushrooms += 6;
                r.stumps += 3;
                r.grass += 3;
                r.bushes += 2;
                if (map == 1) { r.bridge = true; r.fences = false; }
                break;
            case 9:
                r.trees = map == 2 ? 4 : 1;
                r.grass += 2;
                r.rocks = Mathf.Max(1, r.rocks / 2);
                r.mushrooms = 0;
                break;
            case 10:
                r.trees = map == 2 ? 3 : 0;
                r.grass = 0;
                r.bushes = 0;
                r.mushrooms = 2;
                r.rocks += 4;
                if (map == 1) { r.fences = false; r.bridge = false; }
                break;
        }
        return r;
    }

    void AddZoneDecor(Transform parent, int world, int map, Vector3 center, float zoneSize)
    {
        var wood = MakeTempMat(ThemeWood(world));
        var stone = MakeTempMat(ThemeWall(world));
        var leaf = MakeTempMat(ThemeFoliage(world));
        var cat = Cat;
        var recipe = RecipeFor(world, map);

        if (recipe.houses)
        {
            BuildHouse(parent, center + new Vector3(-4f, 0f, 3f), wood, stone);
            BuildHouse(parent, center + new Vector3(4f, 0f, -2f), wood, stone);
            if (cat == null || !TryDecor(cat.stallPrefab, "Stall", center + new Vector3(0f, 0f, 5f), cat.stallScale, parent))
                Prim(PrimitiveType.Cube, "Stall", center + new Vector3(0f, 0.6f, 5f), new Vector3(2.2f, 1.2f, 1.4f), wood, parent);
        }

        if (recipe.fences)
            BuildVillageFences(parent, center, cat);

        if (recipe.bridge && cat != null && cat.bridgePrefab != null)
            TryDecor(cat.bridgePrefab, "Bridge", center + new Vector3(0f, 0f, -7f), cat.bridgeScale, parent, 90f);

        if (recipe.ruins)
            BuildRuinSet(parent, center, stone, cat);

        for (int i = 0; i < recipe.trees; i++)
        {
            Vector3 p = center + RandOffset(map == 2 ? 7f : 5.5f);
            float scale = (cat != null ? cat.treeScale : 1f) * recipe.treeScaleMul;
            var tree = cat != null ? cat.PickTree() : null;
            if (tree != null)
                TryDecor(tree, "Tree" + i, p, scale, parent);
            else
            {
                Prim(PrimitiveType.Cylinder, "Trunk" + i, p + Vector3.up * 1.1f, new Vector3(0.4f, 1.1f, 0.4f), wood, parent);
                Prim(PrimitiveType.Sphere, "Canopy" + i, p + Vector3.up * 2.5f, Vector3.one * 1.9f * recipe.treeScaleMul, leaf, parent);
            }
        }

        ScatterNature(parent, center, cat,
            bushes: recipe.bushes,
            rocks: recipe.rocks,
            mushrooms: recipe.mushrooms,
            grass: recipe.grass,
            trees: 0,
            stumps: recipe.stumps);

        if (map == 4)
            BuildBossArena(parent, center, world, cat);
        else
            AddWorldLandmarks(parent, center, world, map, wood, stone, leaf);
    }

    Color ThemeWood(int world)
    {
        switch (ThemeIndex(world))
        {
            case 4: return new Color(0.25f, 0.12f, 0.08f);
            case 6: return new Color(0.45f, 0.4f, 0.35f);
            case 7: return new Color(0.55f, 0.4f, 0.22f);
            case 8: return new Color(0.28f, 0.22f, 0.12f);
            case 10: return new Color(0.18f, 0.1f, 0.14f);
            default: return new Color(0.4f, 0.25f, 0.12f);
        }
    }

    Color ThemeFoliage(int world)
    {
        switch (ThemeIndex(world))
        {
            case 4: return new Color(0.35f, 0.2f, 0.1f);
            case 5: return new Color(0.35f, 0.15f, 0.45f);
            case 6: return new Color(0.55f, 0.7f, 0.75f);
            case 7: return new Color(0.45f, 0.5f, 0.25f);
            case 8: return new Color(0.2f, 0.4f, 0.22f);
            case 9: return new Color(0.45f, 0.75f, 0.55f);
            case 10: return new Color(0.25f, 0.08f, 0.2f);
            default: return new Color(0.15f, 0.5f, 0.2f);
        }
    }

    void BuildRuinSet(Transform parent, Vector3 center, Material stone, VisualPrefabCatalog cat)
    {
        if (cat == null || !TryDecor(cat.ruinWallPrefab, "RuinWall", center + new Vector3(-3f, 0f, 2f), cat.ruinScale, parent))
            Prim(PrimitiveType.Cube, "RuinWall", center + new Vector3(-3f, 1.1f, 2f), new Vector3(5f, 2.2f, 0.5f), stone, parent);
        if (cat == null || !TryDecor(cat.ruinPillarPrefab, "RuinPillar", center + new Vector3(4f, 0f, -1f), cat.ruinScale, parent))
            Prim(PrimitiveType.Cube, "RuinPillar", center + new Vector3(4f, 1.5f, -1f), new Vector3(1f, 3f, 1f), stone, parent);
        if (cat != null && cat.PickRock() != null)
        {
            for (int i = 0; i < 4; i++)
                TryDecor(cat.PickRock(), "Rubble" + i, center + RandOffset(5f), cat.rockScale, parent);
        }
        else if (cat == null || !TryDecor(cat.rubblePrefab, "Rubble", center + new Vector3(1f, 0f, 4f), cat != null ? cat.ruinScale : 1f, parent))
            Prim(PrimitiveType.Cube, "Rubble", center + new Vector3(1f, 0.35f, 4f), new Vector3(2.5f, 0.7f, 1.5f), stone, parent);
    }

    void BuildBossArena(Transform parent, Vector3 center, int world, VisualPrefabCatalog cat)
    {
        if (cat == null || !TryDecor(cat.bossAltarPrefab, "BossAltar", center + new Vector3(0f, 0f, 2f), cat.bossAltarScale, parent))
        {
            Prim(PrimitiveType.Cube, "BossAltar", center + new Vector3(0f, 0.4f, 2f), new Vector3(6f, 0.8f, 6f),
                MakeTempMat(ThemeBossAccent(world)), parent);
        }
        Prim(PrimitiveType.Cylinder, "BossRing", center + new Vector3(0f, 0.05f, 2f), new Vector3(10f, 0.06f, 10f),
            MakeTempMat(Color.Lerp(ThemeBossAccent(world), Color.black, 0.45f)), parent);

        var accent = MakeTempMat(ThemeBossAccent(world));
        switch (ThemeIndex(world))
        {
            case 4:
            case 6:
            case 7:
            case 8:
            case 10:
                Prim(PrimitiveType.Cylinder, "Hazard", center + new Vector3(6f, 0.03f, -4f), new Vector3(4f, 0.05f, 4f), accent, parent);
                break;
            case 9:
                Prim(PrimitiveType.Cube, "SkyPad", center + new Vector3(-5f, 1.2f, -3f), new Vector3(3f, 0.25f, 3f), MakeTempMat(ThemeWall(world)), parent);
                Prim(PrimitiveType.Cube, "SkyPad2", center + new Vector3(5f, 1.8f, 1f), new Vector3(2.5f, 0.25f, 2.5f), MakeTempMat(ThemeWall(world)), parent);
                break;
            case 5:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f * Mathf.Deg2Rad;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * 6f, 1.5f, Mathf.Sin(a) * 6f + 2f);
                    Prim(PrimitiveType.Cube, "VoidSpire" + i, p, new Vector3(0.6f, 3f, 0.6f), accent, parent);
                }
                break;
        }

        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * 7.5f, 0f, Mathf.Sin(a) * 7.5f + 2f);
            if (cat != null && cat.PickRock() != null)
                TryDecor(cat.PickRock(), "BossRock" + i, p, cat.rockScale * 1.1f, parent);
            else
                Prim(PrimitiveType.Cube, "BossRock" + i, p + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f), MakeTempMat(ThemeWall(world)), parent);
        }
    }

    void AddWorldLandmarks(Transform parent, Vector3 center, int world, int map, Material wood, Material stone, Material leaf)
    {
        var accent = MakeTempMat(ThemeBossAccent(world));
        switch (ThemeIndex(world))
        {
            case 1:
                if (map == 1)
                {
                    Prim(PrimitiveType.Sphere, "Hay1", center + new Vector3(2f, 0.5f, 4f), new Vector3(1.6f, 1f, 1.6f), MakeTempMat(new Color(0.7f, 0.6f, 0.25f)), parent);
                    Prim(PrimitiveType.Sphere, "Hay2", center + new Vector3(-5f, 0.45f, -2f), new Vector3(1.4f, 0.9f, 1.4f), MakeTempMat(new Color(0.65f, 0.55f, 0.22f)), parent);
                }
                if (map == 2)
                    Prim(PrimitiveType.Cube, "HillRock", center + new Vector3(0f, 0.6f, 0f), new Vector3(3f, 1.2f, 2f), stone, parent);
                break;
            case 2:
                if (map == 2)
                {
                    Prim(PrimitiveType.Cylinder, "FallenLog", center + new Vector3(3f, 0.35f, -2f), new Vector3(0.5f, 0.35f, 2.8f), wood, parent);
                    Prim(PrimitiveType.Cylinder, "FallenLog2", center + new Vector3(-4f, 0.3f, 3f), new Vector3(0.4f, 0.3f, 2.2f), wood, parent);
                }
                break;
            case 3:
                Prim(PrimitiveType.Cube, "ArchL", center + new Vector3(-2f, 2f, -3f), new Vector3(0.5f, 4f, 0.5f), stone, parent);
                Prim(PrimitiveType.Cube, "ArchR", center + new Vector3(2f, 2f, -3f), new Vector3(0.5f, 4f, 0.5f), stone, parent);
                Prim(PrimitiveType.Cube, "ArchTop", center + new Vector3(0f, 4f, -3f), new Vector3(4.5f, 0.5f, 0.5f), stone, parent);
                break;
            case 4:
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = center + RandOffset(5f);
                    Prim(PrimitiveType.Cylinder, "Vent" + i, p + Vector3.up * 0.05f, new Vector3(2.2f, 0.08f, 2.2f), accent, parent);
                    Prim(PrimitiveType.Cube, "VentRock" + i, p + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f), stone, parent);
                }
                break;
            case 5:
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = center + RandOffset(6f);
                    Prim(PrimitiveType.Cube, "Crystal" + i, p + Vector3.up * 1.2f, new Vector3(0.45f, 2.2f + i * 0.15f, 0.45f), accent, parent);
                }
                break;
            case 6:
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = center + RandOffset(6f);
                    Prim(PrimitiveType.Cube, "IceSpike" + i, p + Vector3.up * (1.2f + i * 0.2f), new Vector3(0.35f, 2.4f, 0.35f), accent, parent);
                }
                Prim(PrimitiveType.Sphere, "SnowMound", center + new Vector3(-3f, 0.4f, 2f), new Vector3(2.5f, 0.8f, 2.5f), MakeTempMat(new Color(0.9f, 0.94f, 1f)), parent);
                break;
            case 7:
                Prim(PrimitiveType.Sphere, "Dune1", center + new Vector3(-4f, 0.35f, 3f), new Vector3(4f, 0.7f, 3f), MakeTempMat(ThemeGround(world)), parent);
                Prim(PrimitiveType.Sphere, "Dune2", center + new Vector3(4f, 0.3f, -3f), new Vector3(3.5f, 0.6f, 2.8f), MakeTempMat(ThemeGround(world)), parent);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = center + RandOffset(5f);
                    Prim(PrimitiveType.Cylinder, "Cactus" + i, p + Vector3.up * 0.9f, new Vector3(0.35f, 0.9f, 0.35f), leaf, parent);
                    Prim(PrimitiveType.Sphere, "CactusTop" + i, p + Vector3.up * 1.85f, Vector3.one * 0.45f, leaf, parent);
                }
                if (map == 3)
                    Prim(PrimitiveType.Cube, "SandArch", center + new Vector3(0f, 2.5f, 0f), new Vector3(6f, 0.6f, 0.6f), stone, parent);
                break;
            case 8:
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = center + RandOffset(6f);
                    Prim(PrimitiveType.Cylinder, "Lily" + i, p + Vector3.up * 0.06f, new Vector3(1.8f, 0.05f, 1.8f), leaf, parent);
                }
                Prim(PrimitiveType.Cylinder, "MudPool", center + new Vector3(2f, 0.04f, -2f), new Vector3(4f, 0.06f, 4f), accent, parent);
                break;
            case 9:
                Prim(PrimitiveType.Cube, "Float1", center + new Vector3(-4f, 1.5f, 2f), new Vector3(3f, 0.3f, 3f), stone, parent);
                Prim(PrimitiveType.Cube, "Float2", center + new Vector3(4f, 2.2f, -2f), new Vector3(2.5f, 0.3f, 2.5f), stone, parent);
                Prim(PrimitiveType.Sphere, "Cloud1", center + new Vector3(0f, 4f, 0f), new Vector3(3.5f, 1.2f, 3.5f), MakeTempMat(new Color(0.95f, 0.97f, 1f)), parent);
                Prim(PrimitiveType.Sphere, "Cloud2", center + new Vector3(-5f, 3.5f, -4f), new Vector3(2.8f, 1f, 2.8f), MakeTempMat(new Color(0.92f, 0.95f, 1f)), parent);
                break;
            case 10:
                Prim(PrimitiveType.Cylinder, "VoidRing", center + new Vector3(0f, 0.08f, 0f), new Vector3(8f, 0.08f, 8f), accent, parent);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f * Mathf.Deg2Rad;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * 5f, 1.8f, Mathf.Sin(a) * 5f);
                    Prim(PrimitiveType.Cube, "DarkSpire" + i, p, new Vector3(0.55f, 3.5f, 0.55f), MakeTempMat(ThemeWall(world)), parent);
                }
                break;
        }
    }

    void ScatterNature(Transform parent, Vector3 center, VisualPrefabCatalog cat,
        int bushes, int rocks, int mushrooms, int grass, int trees, int stumps)
    {
        if (cat == null) return;

        for (int i = 0; i < bushes; i++)
        {
            var p = cat.PickBush();
            if (p == null) break;
            TryDecor(p, "Bush" + i, center + RandOffset(6.5f), cat.bushScale, parent);
        }
        for (int i = 0; i < rocks; i++)
        {
            var p = cat.PickRock();
            if (p == null) break;
            TryDecor(p, "Rock" + i, center + RandOffset(6.5f), cat.rockScale, parent);
        }
        for (int i = 0; i < mushrooms; i++)
        {
            var p = cat.PickMushroom();
            if (p == null) break;
            TryDecor(p, "Mushroom" + i, center + RandOffset(5.5f), cat.mushroomScale, parent);
        }
        for (int i = 0; i < grass; i++)
        {
            var p = cat.PickGrass();
            if (p == null) break;
            TryDecor(p, "Grass" + i, center + RandOffset(7f), cat.grassScale, parent);
        }
        for (int i = 0; i < trees; i++)
        {
            var p = cat.PickTree();
            if (p == null) break;
            TryDecor(p, "ExtraTree" + i, center + RandOffset(6f), cat.treeScale, parent);
        }
        for (int i = 0; i < stumps; i++)
        {
            if (cat.stumpPrefab == null) break;
            TryDecor(cat.stumpPrefab, "Stump" + i, center + RandOffset(5f), cat.stumpScale, parent);
        }
    }

    void BuildVillageFences(Transform parent, Vector3 center, VisualPrefabCatalog cat)
    {
        if (cat == null) return;

        // South opening with gate toward the hub path.
        PlaceFenceRow(parent, cat, center + new Vector3(-6f, 0f, -6f), 4, 3f, 0f, "FenceS");
        if (cat.gatePrefab != null)
            TryDecor(cat.gatePrefab, "Gate", center + new Vector3(0f, 0f, -6f), cat.gateScale, parent, 0f);
        else
            PlaceFenceRow(parent, cat, center + new Vector3(0f, 0f, -6f), 1, 0f, 0f, "FenceSMid");
        PlaceFenceRow(parent, cat, center + new Vector3(3f, 0f, -6f), 3, 3f, 0f, "FenceSE");

        PlaceFenceRow(parent, cat, center + new Vector3(-6f, 0f, 6f), 5, 3f, 180f, "FenceN");
        PlaceFenceRow(parent, cat, center + new Vector3(-6f, 0f, -3f), 4, 3f, 90f, "FenceW");
        PlaceFenceRow(parent, cat, center + new Vector3(6f, 0f, -3f), 4, 3f, -90f, "FenceE");
    }

    void PlaceFenceRow(Transform parent, VisualPrefabCatalog cat, Vector3 start, int count, float spacing, float yaw, string namePrefix)
    {
        Vector3 step = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * spacing;
        for (int i = 0; i < count; i++)
        {
            var fence = cat.PickFence();
            if (fence == null) return;
            TryDecor(fence, namePrefix + i, start + step * i, cat.fenceScale, parent, yaw);
        }
    }

    static Vector3 RandOffset(float range)
    {
        return new Vector3(Random.Range(-range, range), 0f, Random.Range(-range, range));
    }

    bool TryDecor(GameObject prefab, string name, Vector3 worldPos, float scale, Transform parent, float yawDegrees = 0f)
    {
        if (prefab == null) return false;
        var go = VisualPrefabUtil.Spawn(prefab, parent, name, worldPos, scale <= 0f ? 1f : scale, stripColliders: false);
        if (go != null && Mathf.Abs(yawDegrees) > 0.01f)
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        return go != null;
    }

    void BuildHouse(Transform parent, Vector3 pos, Material wood, Material roof)
    {
        var cat = Cat;
        if (cat != null && TryDecor(cat.housePrefab, "House", pos, cat.houseScale, parent))
            return;
        Prim(PrimitiveType.Cube, "HouseBase", pos + new Vector3(0f, 1.0f, 0f), new Vector3(3.2f, 2.0f, 2.8f), wood, parent);
        Prim(PrimitiveType.Cube, "HouseRoof", pos + new Vector3(0f, 2.3f, 0f), new Vector3(3.6f, 0.5f, 3.2f), roof, parent);
    }

    void SpawnOreNodes(Transform parent, int world, int map, Vector3 center, float zoneSize)
    {
        // Mining focus: map 3 = crystal mine cluster; map 2 = a few veins; map 1 light; map 4 none.
        int count;
        switch (map)
        {
            case 3: count = 7; break;
            case 2: count = 3; break;
            case 1: count = 1; break;
            default: return;
        }

        int level = ProgressionFormulas.ContentLevel(world, map);
        Color[] tints =
        {
            new Color(0.95f, 0.25f, 0.3f),
            new Color(0.3f, 0.5f, 1f),
            new Color(0.25f, 0.85f, 0.4f),
            new Color(1f, 0.85f, 0.25f),
            new Color(0.75f, 0.4f, 1f),
            new Color(0.4f, 0.9f, 0.95f),
            new Color(1f, 0.5f, 0.2f)
        };

        // Dedicated mine pocket on map 3
        if (map == 3)
        {
            Vector3 mineCenter = center + new Vector3(0f, 0f, 4.5f);
            var stone = MakeTempMat(new Color(0.28f, 0.26f, 0.3f));
            Prim(PrimitiveType.Cube, "MineWallL", mineCenter + new Vector3(-5f, 1.2f, 0f), new Vector3(1.2f, 2.4f, 6f), stone, parent);
            Prim(PrimitiveType.Cube, "MineWallR", mineCenter + new Vector3(5f, 1.2f, 0f), new Vector3(1.2f, 2.4f, 6f), stone, parent);
            Prim(PrimitiveType.Cube, "MineBack", mineCenter + new Vector3(0f, 1.2f, 3.2f), new Vector3(10f, 2.4f, 1.2f), stone, parent);
            var marker = MakeTempMat(new Color(0.55f, 0.85f, 1f));
            Prim(PrimitiveType.Cube, "MineSign", mineCenter + new Vector3(0f, 2.6f, -2.5f), new Vector3(2.4f, 0.35f, 0.35f), marker, parent);
        }

        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + world * 0.3f;
            float rad = map == 3 ? 3.2f + (i % 3) * 0.8f : zoneSize * 0.28f + (i % 2) * 1.2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad + (map == 3 ? 4f : 0f));
            CreateOreNode(parent, $"Ore_M{map}_{i}", pos, level, tints[i % tints.Length]);
        }
    }

    void CreateOreNode(Transform parent, string name, Vector3 pos, int level, Color tint)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        var mat = MakeTempMat(tint);
        // Crystal cluster: tall shard + side crystals
        Prim(PrimitiveType.Cube, "Shard", pos + new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 1.8f, 0.55f), mat, root.transform);
        Prim(PrimitiveType.Cube, "ShardL", pos + new Vector3(-0.45f, 0.55f, 0.2f), new Vector3(0.35f, 1.1f, 0.35f), mat, root.transform);
        Prim(PrimitiveType.Cube, "ShardR", pos + new Vector3(0.4f, 0.45f, -0.15f), new Vector3(0.3f, 0.9f, 0.3f), mat, root.transform);
        Prim(PrimitiveType.Cube, "Base", pos + new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.3f, 1.1f), MakeTempMat(new Color(0.25f, 0.24f, 0.28f)), root.transform);

        // Prim parents under root but uses world pos — check Prim implementation
        var ore = root.AddComponent<OreNode>();
        ore.Configure(level, "Crystal Vein", tint);
    }

    /// <summary>Open hub arena — position comes from each world's layout profile.</summary>
    void SpawnChallengeAltar(Transform parent, int world)
    {
        Vector3 pos = _layout != null ? _layout.altarPos : new Vector3(10f, 0f, 10f);
        var root = new GameObject("ChallengeAltar");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        var pad = MakeTempMat(new Color(0.42f, 0.32f, 0.26f));
        var stone = MakeTempMat(new Color(0.35f, 0.22f, 0.18f));
        var glow = MakeTempMat(new Color(1f, 0.45f, 0.15f));
        Prim(PrimitiveType.Cylinder, "ArenaPad", pos + new Vector3(0f, 0.03f, 0f), new Vector3(16f, 0.04f, 16f), pad, root.transform);
        Prim(PrimitiveType.Cylinder, "Base", pos + new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.35f, 2.4f), stone, root.transform);
        Prim(PrimitiveType.Cube, "Pillar", pos + new Vector3(0f, 1.4f, 0f), new Vector3(0.7f, 2.2f, 0.7f), stone, root.transform);
        var flame = Prim(PrimitiveType.Sphere, "Flame", pos + new Vector3(0f, 2.7f, 0f), Vector3.one * 0.85f, glow, root.transform);
        var flameCol = flame.GetComponent<Collider>();
        if (flameCol != null) Object.Destroy(flameCol);

        var altar = root.AddComponent<ChallengeAltar>();
        altar.world = world;
        altar.mapIndex = 2;
    }

    /// <summary>Spawns a non-respawning challenge mob (used by ChallengeAltar waves).</summary>
    public GameObject SpawnChallengeEnemy(string name, Vector3 pos, Vector3 zoneCenter, int level, bool isBoss, EnemyKind kind, int mapIndex)
    {
        Transform parent = _root != null ? _root : transform;
        var go = CreateEnemy(parent, name, pos, zoneCenter, level, isBoss, kind, mapIndex);
        if (go == null) return null;

        var ai = go.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.disableAutoRespawn = true;
            ai.leashRadius = 20f;
            ai.homeCenter = zoneCenter;
        }

        var stats = go.GetComponent<CharacterStats>();
        if (stats != null)
            stats.isChallengeEnemy = true;

        return go;
    }

    void SpawnEnemiesInZone(Transform parent, int world, int map, Vector3 center)
    {
        int level = ProgressionFormulas.ContentLevel(world, map);

        // Map 4: boss only.
        if (map == 4)
        {
            Vector3 bossOff = new Vector3(0f, 0f, 3f);
            CreateEnemy(parent, $"M{map}_Boss", center + bossOff, center, level, true, EnemyKind.WorldBoss, map);
            return;
        }

        EnemyKind kind = EnemyVisuals.KindForZone(world, map);

        int count = 4;
        var pattern = WorldLayoutProfiles.SpawnPattern.Diamond;
        if (_layout != null)
        {
            if (map < _layout.mobCounts.Length)
                count = Mathf.Clamp(_layout.mobCounts[map], 2, 6);
            if (map < _layout.spawnPatterns.Length)
                pattern = _layout.spawnPatterns[map];
        }

        Vector3[] localSpots = WorldLayoutProfiles.MobLocalSpots(pattern, count);
        float leashMul = _layout != null ? _layout.leashMul : 1f;

        for (int i = 0; i < count; i++)
        {
            var go = CreateEnemy(parent, $"M{map}_Mob{i}_{kind}", center + localSpots[i], center, level, false, kind, map);
            if (go == null) continue;
            var ai = go.GetComponent<EnemyAI>();
            if (ai != null)
                ai.leashRadius = 9f * leashMul;
        }
    }

    GameObject CreateEnemy(Transform parent, string name, Vector3 pos, Vector3 zoneCenter, int level, bool isBoss, EnemyKind kind, int mapIndex)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        float scale = isBoss ? 1.75f : 1f;
        if (kind == EnemyKind.WorldBoss) scale = 1.9f;
        else if (kind == EnemyKind.Golem || kind == EnemyKind.Treant || kind == EnemyKind.Demon || kind == EnemyKind.Yeti)
            scale = isBoss ? 1.6f : 1.15f;
        else if (kind == EnemyKind.Imp || kind == EnemyKind.Bat || kind == EnemyKind.Beetle ||
                 kind == EnemyKind.Mosquito || kind == EnemyKind.StormSprite)
            scale = 0.9f;

        EnemyVisuals.Build(root.transform, kind, scale);

        float height = isBoss ? 3.4f : 2f;
        float radius = isBoss ? 0.85f : 0.45f;
        if (kind == EnemyKind.Wolf || kind == EnemyKind.Spider || kind == EnemyKind.Beetle ||
            kind == EnemyKind.Scorpion || kind == EnemyKind.ShadowHound || kind == EnemyKind.Leech ||
            kind == EnemyKind.Toad || kind == EnemyKind.Griffon)
        {
            height = 1.4f;
            radius = 0.55f;
        }
        else if (kind == EnemyKind.Bat || kind == EnemyKind.Mosquito || kind == EnemyKind.Harpy || kind == EnemyKind.StormSprite)
        {
            height = 1.8f;
            radius = 0.5f;
        }
        else if (kind == EnemyKind.VoidEye || kind == EnemyKind.SandWorm)
        {
            height = 2.4f;
            radius = 0.65f;
        }

        var cc = root.AddComponent<CharacterController>();
        cc.height = height * (isBoss ? 1f : 1f);
        cc.radius = radius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

        var stats = root.AddComponent<CharacterStats>();
        stats.ConfigureAsEnemy(level, isBoss, kind);
        stats.mapIndex = mapIndex;

        var ai = root.AddComponent<EnemyAI>();
        ai.enemyType = kind.ToString();
        ai.SetSpawnPosition(pos);
        ai.moveSpeed = ProgressionFormulas.MobMoveSpeed(level, isBoss);
        if (kind == EnemyKind.Wolf || kind == EnemyKind.Bat || kind == EnemyKind.Imp ||
            kind == EnemyKind.ShadowHound || kind == EnemyKind.Mosquito || kind == EnemyKind.Harpy)
            ai.moveSpeed *= 1.15f;
        if (kind == EnemyKind.Golem || kind == EnemyKind.Treant || kind == EnemyKind.Yeti ||
            kind == EnemyKind.Mummy || kind == EnemyKind.SandWorm || kind == EnemyKind.Tentacle)
            ai.moveSpeed *= 0.75f;
        ai.disableAutoRespawn = false;
        ai.homeCenter = zoneCenter;
        ai.leashRadius = 9f;

        var anim = root.GetComponent<ProceduralBodyAnim>();
        if (anim == null) anim = root.AddComponent<ProceduralBodyAnim>();
        anim.style = EnemyAI.StyleForKind(kind);
        anim.Recache();

        return root;
    }

    public void SpawnWorldPortal()
    {
        if (_root == null || _worldPortal != null) return;

        Vector3 center = GetTileCenter(4);
        var cat = Cat;
        if (cat != null && cat.worldPortalPrefab != null)
        {
            _worldPortal = VisualPrefabUtil.Spawn(
                cat.worldPortalPrefab, _root, "WorldPortal",
                center + new Vector3(0f, 0f, -6f), cat.worldPortalScale, stripColliders: true);
        }
        else
        {
            var mat = MakeTempMat(new Color(0.55f, 0.35f, 1f));
            _worldPortal = Prim(PrimitiveType.Cylinder, "WorldPortal", center + new Vector3(0f, 1.5f, -6f), new Vector3(3f, 1.5f, 0.4f), mat);
            Object.Destroy(_worldPortal.GetComponent<Collider>());
        }

        var trigger = new GameObject("WorldPortalTrigger");
        trigger.transform.SetParent(_root, false);
        trigger.transform.position = center + new Vector3(0f, 1f, -6f);
        var col = trigger.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;
        trigger.AddComponent<WorldPortalTrigger>();

        int next = (AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1) + 1;
        if (next <= ProgressionFormulas.WorldCount)
        {
            var label = trigger.AddComponent<WorldPortalLabel>();
            label.worldId = next;
            label.unlocked = true;
            label.showInAdventure = true;
            label.customLabel = $"Next World → {ProgressionFormulas.WorldThemeName(next)}";
        }

        var lightGo = new GameObject("PortalLight");
        lightGo.transform.SetParent(trigger.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.7f, 0.45f, 1f);
        light.intensity = 5f;
        light.range = 12f;
    }
}
