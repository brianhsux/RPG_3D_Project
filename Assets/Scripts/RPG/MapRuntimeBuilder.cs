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

    Transform _root;
    GameObject _worldPortal;
    readonly Vector3[] _zoneCenters = new Vector3[5]; // 1..4

    void Awake()
    {
        Instance = this;
        // Four scenic zones inside the big square
        _zoneCenters[1] = new Vector3(-24f, 0f, 0f);   // West  - Map1
        _zoneCenters[2] = new Vector3(24f, 0f, 0f);    // East  - Map2
        _zoneCenters[3] = new Vector3(0f, 0f, 24f);    // North - Map3
        _zoneCenters[4] = new Vector3(0f, 0f, -22f);   // South - Map4 Boss
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Vector3 GetTileCenter(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        return _zoneCenters[map];
    }

    public Vector3 GetTileSpawn(int map)
    {
        map = Mathf.Clamp(map, 1, 4);
        // Spawn slightly south of zone center, on solid ground
        return _zoneCenters[map] + new Vector3(0f, 0.5f, -4f);
    }

    public void BuildCurrentMap() => BuildCurrentWorld();

    public void BuildCurrentWorld()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _worldPortal = null;
        _root = new GameObject("RuntimeWorld").transform;

        int world = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;

        BuildBigArena(world);

        for (int map = 1; map <= 4; map++)
            BuildZone(world, map, _zoneCenters[map]);

        if (AdventureManager.Instance != null && AdventureManager.Instance.worldPortalReady)
            SpawnWorldPortal();
    }

    Color ThemeGround(int world)
    {
        switch (world)
        {
            case 1: return new Color(0.32f, 0.55f, 0.25f);
            case 2: return new Color(0.16f, 0.4f, 0.18f);
            case 3: return new Color(0.45f, 0.42f, 0.38f);
            case 4: return new Color(0.45f, 0.22f, 0.12f);
            case 5: return new Color(0.22f, 0.12f, 0.28f);
            default: return Color.gray;
        }
    }

    Color ThemeWall(int world)
    {
        switch (world)
        {
            case 1: return new Color(0.5f, 0.48f, 0.42f);
            case 2: return new Color(0.28f, 0.22f, 0.14f);
            case 3: return new Color(0.55f, 0.55f, 0.52f);
            case 4: return new Color(0.35f, 0.15f, 0.1f);
            case 5: return new Color(0.15f, 0.08f, 0.2f);
            default: return Color.gray;
        }
    }

    Color ZoneFloor(int world, int map)
    {
        Color baseCol = ThemeGround(world);
        switch (map)
        {
            case 1: return Color.Lerp(baseCol, new Color(0.55f, 0.48f, 0.32f), 0.45f); // village dirt
            case 2: return Color.Lerp(baseCol, new Color(0.12f, 0.35f, 0.14f), 0.5f);  // forest
            case 3: return Color.Lerp(baseCol, new Color(0.5f, 0.48f, 0.45f), 0.55f); // ruins
            case 4: return Color.Lerp(baseCol, new Color(0.5f, 0.12f, 0.12f), 0.4f);  // boss
            default: return baseCol;
        }
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
        return go;
    }

    void BuildBigArena(int world)
    {
        var groundMat = MakeTempMat(ThemeGround(world));
        var wallMat = MakeTempMat(ThemeWall(world));
        var pathMat = MakeTempMat(new Color(0.62f, 0.55f, 0.38f));

        float half = WorldSize * 0.5f;
        float wh = 3f;

        Prim(PrimitiveType.Cube, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(WorldSize, 0.5f, WorldSize), groundMat);

        Prim(PrimitiveType.Cube, "Wall_N", new Vector3(0f, wh / 2f, half), new Vector3(WorldSize, wh, 0.8f), wallMat);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0f, wh / 2f, -half), new Vector3(WorldSize, wh, 0.8f), wallMat);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(half, wh / 2f, 0f), new Vector3(0.8f, wh, WorldSize), wallMat);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-half, wh / 2f, 0f), new Vector3(0.8f, wh, WorldSize), wallMat);

        // Paths from center hub to each zone
        Prim(PrimitiveType.Cube, "Path_W", new Vector3(-12f, 0.03f, 0f), new Vector3(14f, 0.06f, 4f), pathMat);
        Prim(PrimitiveType.Cube, "Path_E", new Vector3(12f, 0.03f, 0f), new Vector3(14f, 0.06f, 4f), pathMat);
        Prim(PrimitiveType.Cube, "Path_N", new Vector3(0f, 0.03f, 12f), new Vector3(4f, 0.06f, 14f), pathMat);
        Prim(PrimitiveType.Cube, "Path_S", new Vector3(0f, 0.03f, -11f), new Vector3(4f, 0.06f, 12f), pathMat);
        Prim(PrimitiveType.Cylinder, "Hub", new Vector3(0f, 0.04f, 0f), new Vector3(8f, 0.05f, 8f), pathMat);

        // Corner pillars
        float p = half - 2f;
        Prim(PrimitiveType.Cylinder, "PillarNE", new Vector3(p, 1.6f, p), new Vector3(1.2f, 1.6f, 1.2f), wallMat);
        Prim(PrimitiveType.Cylinder, "PillarNW", new Vector3(-p, 1.6f, p), new Vector3(1.2f, 1.6f, 1.2f), wallMat);
        Prim(PrimitiveType.Cylinder, "PillarSE", new Vector3(p, 1.6f, -p), new Vector3(1.2f, 1.6f, 1.2f), wallMat);
        Prim(PrimitiveType.Cylinder, "PillarSW", new Vector3(-p, 1.6f, -p), new Vector3(1.2f, 1.6f, 1.2f), wallMat);
    }

    void BuildZone(int world, int map, Vector3 center)
    {
        var zoneRoot = new GameObject($"Zone_Map{map}").transform;
        zoneRoot.SetParent(_root, false);

        float zoneSize = map == 4 ? 20f : 18f;
        var floorMat = MakeTempMat(ZoneFloor(world, map));
        Prim(PrimitiveType.Cube, "ZoneFloor", center + new Vector3(0f, 0.02f, 0f), new Vector3(zoneSize, 0.05f, zoneSize), floorMat, zoneRoot);

        // Marker pole
        Prim(PrimitiveType.Cube, "ZonePole", center + new Vector3(-zoneSize * 0.4f, 1.2f, -zoneSize * 0.4f),
            new Vector3(0.35f, 2.4f, 0.35f), MakeTempMat(new Color(1f, 0.85f, 0.25f)), zoneRoot);

        AddZoneDecor(zoneRoot, world, map, center, zoneSize);

        // Soft zone trigger (updates current map while walking)
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

    void AddZoneDecor(Transform parent, int world, int map, Vector3 center, float zoneSize)
    {
        var wood = MakeTempMat(new Color(0.4f, 0.25f, 0.12f));
        var stone = MakeTempMat(ThemeWall(world));
        var leaf = MakeTempMat(new Color(0.15f, 0.5f, 0.2f));

        switch (map)
        {
            case 1: // Village
                BuildHouse(parent, center + new Vector3(-4f, 0f, 3f), wood, stone);
                BuildHouse(parent, center + new Vector3(4f, 0f, -2f), wood, stone);
                Prim(PrimitiveType.Cube, "Stall", center + new Vector3(0f, 0.6f, 5f), new Vector3(2.2f, 1.2f, 1.4f), wood, parent);
                break;
            case 2: // Forest
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = center + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                    Prim(PrimitiveType.Cylinder, "Trunk" + i, p + Vector3.up * 1.1f, new Vector3(0.4f, 1.1f, 0.4f), wood, parent);
                    Prim(PrimitiveType.Sphere, "Canopy" + i, p + Vector3.up * 2.5f, Vector3.one * 1.9f, leaf, parent);
                }
                break;
            case 3: // Ruins
                Prim(PrimitiveType.Cube, "RuinWall", center + new Vector3(-3f, 1.1f, 2f), new Vector3(5f, 2.2f, 0.5f), stone, parent);
                Prim(PrimitiveType.Cube, "RuinPillar", center + new Vector3(4f, 1.5f, -1f), new Vector3(1f, 3f, 1f), stone, parent);
                Prim(PrimitiveType.Cube, "Rubble", center + new Vector3(1f, 0.35f, 4f), new Vector3(2.5f, 0.7f, 1.5f), stone, parent);
                break;
            case 4: // Boss arena
                Prim(PrimitiveType.Cube, "BossAltar", center + new Vector3(0f, 0.4f, 2f), new Vector3(6f, 0.8f, 6f),
                    MakeTempMat(new Color(0.55f, 0.12f, 0.12f)), parent);
                Prim(PrimitiveType.Cylinder, "BossRing", center + new Vector3(0f, 0.05f, 2f), new Vector3(10f, 0.06f, 10f),
                    MakeTempMat(new Color(0.35f, 0.08f, 0.08f)), parent);
                if (world == 4)
                    Prim(PrimitiveType.Cylinder, "Lava", center + new Vector3(6f, 0.03f, -4f), new Vector3(4f, 0.05f, 4f),
                        MakeTempMat(new Color(1f, 0.35f, 0.05f)), parent);
                break;
        }
    }

    void BuildHouse(Transform parent, Vector3 pos, Material wood, Material roof)
    {
        Prim(PrimitiveType.Cube, "HouseBase", pos + new Vector3(0f, 1.0f, 0f), new Vector3(3.2f, 2.0f, 2.8f), wood, parent);
        Prim(PrimitiveType.Cube, "HouseRoof", pos + new Vector3(0f, 2.3f, 0f), new Vector3(3.6f, 0.5f, 3.2f), roof, parent);
    }

    void SpawnEnemiesInZone(Transform parent, int world, int map, Vector3 center)
    {
        int level = ProgressionFormulas.ContentLevel(world, map);

        // Map 4: boss only. Maps 1-3: exactly 4 of one monster type.
        if (map == 4)
        {
            CreateEnemy(parent, $"M{map}_Boss", center + new Vector3(0f, 0f, 3f), center, level, true, EnemyKind.WorldBoss, map);
            return;
        }

        EnemyKind kind = EnemyVisuals.KindForZone(world, map);

        Vector3[] localSpots =
        {
            new Vector3(-4f, 0f, 2f),
            new Vector3(4f, 0f, 2f),
            new Vector3(-3f, 0f, -3f),
            new Vector3(3f, 0f, -3f)
        };

        for (int i = 0; i < 4; i++)
            CreateEnemy(parent, $"M{map}_Mob{i}_{kind}", center + localSpots[i], center, level, false, kind, map);
    }

    GameObject CreateEnemy(Transform parent, string name, Vector3 pos, Vector3 zoneCenter, int level, bool isBoss, EnemyKind kind, int mapIndex)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        float scale = isBoss ? 1.75f : 1f;
        if (kind == EnemyKind.WorldBoss) scale = 1.9f;
        else if (kind == EnemyKind.Golem || kind == EnemyKind.Treant || kind == EnemyKind.Demon) scale = isBoss ? 1.6f : 1.15f;
        else if (kind == EnemyKind.Imp || kind == EnemyKind.Bat || kind == EnemyKind.Beetle) scale = 0.9f;

        EnemyVisuals.Build(root.transform, kind, scale);

        float height = isBoss ? 3.4f : 2f;
        float radius = isBoss ? 0.85f : 0.45f;
        if (kind == EnemyKind.Wolf || kind == EnemyKind.Spider || kind == EnemyKind.Beetle)
        {
            height = 1.4f;
            radius = 0.55f;
        }
        else if (kind == EnemyKind.Bat)
        {
            height = 1.8f;
            radius = 0.5f;
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
        if (kind == EnemyKind.Wolf || kind == EnemyKind.Bat || kind == EnemyKind.Imp)
            ai.moveSpeed *= 1.15f;
        if (kind == EnemyKind.Golem || kind == EnemyKind.Treant)
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
        var mat = MakeTempMat(new Color(0.55f, 0.35f, 1f));
        _worldPortal = Prim(PrimitiveType.Cylinder, "WorldPortal", center + new Vector3(0f, 1.5f, -6f), new Vector3(3f, 1.5f, 0.4f), mat);
        Object.Destroy(_worldPortal.GetComponent<Collider>());

        var trigger = new GameObject("WorldPortalTrigger");
        trigger.transform.SetParent(_root, false);
        trigger.transform.position = center + new Vector3(0f, 1f, -6f);
        var col = trigger.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;
        trigger.AddComponent<WorldPortalTrigger>();

        var lightGo = new GameObject("PortalLight");
        lightGo.transform.SetParent(trigger.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.7f, 0.45f, 1f);
        light.intensity = 5f;
        light.range = 12f;
    }
}
