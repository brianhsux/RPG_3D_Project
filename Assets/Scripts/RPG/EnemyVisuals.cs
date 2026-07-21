using UnityEngine;

/// <summary>
/// Procedural enemy meshes — distinct silhouettes per EnemyKind.
/// </summary>
public static class EnemyVisuals
{
    public static void Build(Transform root, EnemyKind kind, float scale)
    {
        var catalog = VisualPrefabCatalog.Get();
        if (catalog != null)
        {
            float mul;
            var prefab = catalog.GetEnemyPrefab(kind, out mul);
            if (prefab != null)
            {
                VisualPrefabUtil.SpawnLocal(
                    prefab, root, "Model", Vector3.zero, Vector3.zero, scale * mul, stripColliders: true);
                NormalizeVisualHeight(root, TargetVisualHeight(kind, scale));
                return;
            }
        }

        switch (kind)
        {
            case EnemyKind.Goblin: BuildGoblin(root, scale); break;
            case EnemyKind.Skeleton: BuildSkeleton(root, scale); break;
            case EnemyKind.Wolf: BuildWolf(root, scale); break;
            case EnemyKind.Spider: BuildSpider(root, scale); break;
            case EnemyKind.Bat: BuildBat(root, scale); break;
            case EnemyKind.Mushroom: BuildMushroom(root, scale); break;
            case EnemyKind.Golem: BuildGolem(root, scale); break;
            case EnemyKind.Ghost: BuildGhost(root, scale); break;
            case EnemyKind.Imp: BuildImp(root, scale); break;
            case EnemyKind.Beetle: BuildBeetle(root, scale); break;
            case EnemyKind.Treant: BuildTreant(root, scale); break;
            case EnemyKind.LavaSpawn: BuildLavaSpawn(root, scale); break;
            case EnemyKind.Crystal: BuildCrystal(root, scale); break;
            case EnemyKind.Wraith: BuildWraith(root, scale); break;
            case EnemyKind.Demon: BuildDemon(root, scale); break;
            case EnemyKind.Yeti: BuildYeti(root, scale); break;
            case EnemyKind.IceElemental: BuildIceElemental(root, scale); break;
            case EnemyKind.Scorpion: BuildScorpion(root, scale); break;
            case EnemyKind.Mummy: BuildMummy(root, scale); break;
            case EnemyKind.SandWorm: BuildSandWorm(root, scale); break;
            case EnemyKind.Toad: BuildToad(root, scale); break;
            case EnemyKind.Leech: BuildLeech(root, scale); break;
            case EnemyKind.Mosquito: BuildMosquito(root, scale); break;
            case EnemyKind.Harpy: BuildHarpy(root, scale); break;
            case EnemyKind.StormSprite: BuildStormSprite(root, scale); break;
            case EnemyKind.Griffon: BuildGriffon(root, scale); break;
            case EnemyKind.ShadowHound: BuildShadowHound(root, scale); break;
            case EnemyKind.VoidEye: BuildVoidEye(root, scale); break;
            case EnemyKind.Tentacle: BuildTentacle(root, scale); break;
            case EnemyKind.WorldBoss: BuildWorldBoss(root, scale); break;
            default: BuildSlime(root, scale); break;
        }

        NormalizeVisualHeight(root, TargetVisualHeight(kind, scale));
    }

    static float TargetVisualHeight(EnemyKind kind, float scale)
    {
        float h;
        switch (kind)
        {
            case EnemyKind.WorldBoss: h = 3.6f; break;
            case EnemyKind.Golem:
            case EnemyKind.Treant:
            case EnemyKind.Demon:
            case EnemyKind.Yeti: h = 2.6f; break;
            case EnemyKind.Wolf:
            case EnemyKind.Spider:
            case EnemyKind.Beetle:
            case EnemyKind.Scorpion:
            case EnemyKind.ShadowHound:
            case EnemyKind.Leech:
            case EnemyKind.Toad:
            case EnemyKind.Griffon: h = 1.35f; break;
            case EnemyKind.Bat:
            case EnemyKind.Mosquito:
            case EnemyKind.Harpy:
            case EnemyKind.StormSprite: h = 1.6f; break;
            case EnemyKind.VoidEye:
            case EnemyKind.SandWorm: h = 2.2f; break;
            case EnemyKind.Slime:
            case EnemyKind.Mushroom:
            case EnemyKind.LavaSpawn:
            case EnemyKind.Crystal: h = 1.45f; break;
            default: h = 2.0f; break;
        }
        return h * Mathf.Max(0.5f, scale);
    }

    /// <summary>
    /// Prefab variants can differ wildly in authored size (e.g. some Slimes).
    /// Uniformly rescale the visual so every mob of a kind matches a target height.
    /// </summary>
    public static void NormalizeVisualHeight(Transform root, float targetHeight)
    {
        if (root == null || targetHeight < 0.2f) return;

        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i] != null)
                b.Encapsulate(rends[i].bounds);
        }

        float h = b.size.y;
        if (h < 0.05f || h > 80f) return;

        float factor = targetHeight / h;
        // Ignore tiny float error; clamp extreme corrections from broken prefabs.
        if (factor > 0.92f && factor < 1.08f) return;
        factor = Mathf.Clamp(factor, 0.15f, 4f);

        Transform model = root.Find("Model");
        if (model != null)
            model.localScale *= factor;
        else
        {
            for (int i = 0; i < root.childCount; i++)
                root.GetChild(i).localScale *= factor;
        }
    }

    /// <summary>
    /// Monster kinds for maps 1–3. Worlds 1–10 each have a unique trio;
    /// worlds 11–25 rotate that palette so every zone still feels themed.
    /// </summary>
    public static EnemyKind KindForZone(int world, int map)
    {
        EnemyKind[,] table =
        {
            { EnemyKind.Slime, EnemyKind.Wolf, EnemyKind.Beetle },
            { EnemyKind.Goblin, EnemyKind.Mushroom, EnemyKind.Spider },
            { EnemyKind.Skeleton, EnemyKind.Ghost, EnemyKind.Wraith },
            { EnemyKind.Imp, EnemyKind.LavaSpawn, EnemyKind.Golem },
            { EnemyKind.Demon, EnemyKind.Crystal, EnemyKind.Treant },
            { EnemyKind.Bat, EnemyKind.Yeti, EnemyKind.IceElemental },
            { EnemyKind.Scorpion, EnemyKind.Mummy, EnemyKind.SandWorm },
            { EnemyKind.Toad, EnemyKind.Leech, EnemyKind.Mosquito },
            { EnemyKind.Harpy, EnemyKind.StormSprite, EnemyKind.Griffon },
            { EnemyKind.ShadowHound, EnemyKind.VoidEye, EnemyKind.Tentacle }
        };

        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        map = Mathf.Clamp(map, 1, 3);
        int theme = (world - 1) % 10;
        int mapIdx = (map - 1 + (world - 1) / 10) % 3;
        return table[theme, mapIdx];
    }

    /// <summary>Map4 always uses the world boss silhouette.</summary>
    public static EnemyKind BossKindForWorld(int world) => EnemyKind.WorldBoss;

    static Material Mat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    static void BuildSlime(Transform parent, float scale)
    {
        var body = Mat(new Color(0.25f, 0.85f, 0.35f));
        var dark = Mat(new Color(0.1f, 0.45f, 0.2f));
        var shine = Mat(new Color(0.75f, 1f, 0.8f));
        var eye = Mat(Color.black);
        var white = Mat(Color.white);
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.7f * scale, 0f), new Vector3(1.6f, 1.2f, 1.6f) * scale, body);
        Part(parent, PrimitiveType.Sphere, "Blob", new Vector3(0.35f * scale, 0.45f * scale, 0.2f * scale), Vector3.one * 0.55f * scale, dark);
        Part(parent, PrimitiveType.Sphere, "Shine", new Vector3(-0.35f * scale, 1.05f * scale, 0.4f * scale), Vector3.one * 0.28f * scale, shine);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.3f * scale, 1.0f * scale, 0.55f * scale), Vector3.one * 0.26f * scale, white);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.3f * scale, 1.0f * scale, 0.55f * scale), Vector3.one * 0.26f * scale, white);
        Part(parent, PrimitiveType.Sphere, "PupilL", new Vector3(-0.3f * scale, 1.0f * scale, 0.66f * scale), Vector3.one * 0.12f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "PupilR", new Vector3(0.3f * scale, 1.0f * scale, 0.66f * scale), Vector3.one * 0.12f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "Mouth", new Vector3(0f, 0.75f * scale, 0.7f * scale), new Vector3(0.35f, 0.12f, 0.12f) * scale, dark);
    }

    static void BuildGoblin(Transform parent, float scale)
    {
        var skin = Mat(new Color(0.35f, 0.6f, 0.22f));
        var cloth = Mat(new Color(0.45f, 0.2f, 0.12f));
        var dark = Mat(new Color(0.15f, 0.25f, 0.1f));
        var eye = Mat(new Color(1f, 0.85f, 0.2f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f * scale, 0f), new Vector3(0.75f, 0.7f, 0.65f) * scale, cloth);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.7f * scale, 0.05f * scale), Vector3.one * 0.55f * scale, skin);
        Part(parent, PrimitiveType.Cube, "EarL", new Vector3(-0.35f * scale, 1.85f * scale, 0f), new Vector3(0.12f, 0.35f, 0.08f) * scale, skin);
        Part(parent, PrimitiveType.Cube, "EarR", new Vector3(0.35f * scale, 1.85f * scale, 0f), new Vector3(0.12f, 0.35f, 0.08f) * scale, skin);
        Part(parent, PrimitiveType.Cube, "Nose", new Vector3(0f, 1.65f * scale, 0.28f * scale), new Vector3(0.12f, 0.1f, 0.2f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.15f * scale, 1.75f * scale, 0.25f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.15f * scale, 1.75f * scale, 0.25f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Capsule, "ArmL", new Vector3(-0.55f * scale, 1.0f * scale, 0f), new Vector3(0.25f, 0.4f, 0.25f) * scale, skin);
        Part(parent, PrimitiveType.Capsule, "ArmR", new Vector3(0.55f * scale, 1.0f * scale, 0f), new Vector3(0.25f, 0.4f, 0.25f) * scale, skin);
        Part(parent, PrimitiveType.Capsule, "LegL", new Vector3(-0.2f * scale, 0.3f * scale, 0f), new Vector3(0.22f, 0.3f, 0.22f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegR", new Vector3(0.2f * scale, 0.3f * scale, 0f), new Vector3(0.22f, 0.3f, 0.22f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Club", new Vector3(0.75f * scale, 0.7f * scale, 0.2f * scale), new Vector3(0.18f, 0.18f, 0.7f) * scale, Mat(new Color(0.4f, 0.25f, 0.12f)));
        Part(parent, PrimitiveType.Sphere, "ClubHead", new Vector3(0.75f * scale, 0.7f * scale, 0.55f * scale), Vector3.one * 0.28f * scale, Mat(new Color(0.35f, 0.2f, 0.1f)));
    }

    static void BuildSkeleton(Transform parent, float scale)
    {
        var bone = Mat(new Color(0.92f, 0.9f, 0.82f));
        var dark = Mat(new Color(0.35f, 0.32f, 0.28f));
        Part(parent, PrimitiveType.Capsule, "Ribs", new Vector3(0f, 1.15f * scale, 0f), new Vector3(0.7f, 0.95f, 0.45f) * scale, bone);
        Part(parent, PrimitiveType.Sphere, "Skull", new Vector3(0f, 2.35f * scale, 0f), Vector3.one * 0.55f * scale, bone);
        Part(parent, PrimitiveType.Cube, "Jaw", new Vector3(0f, 2.1f * scale, 0.15f * scale), new Vector3(0.35f, 0.12f, 0.25f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "ArmL", new Vector3(-0.55f * scale, 1.3f * scale, 0f), new Vector3(0.18f, 0.55f, 0.18f) * scale, bone);
        Part(parent, PrimitiveType.Capsule, "ArmR", new Vector3(0.55f * scale, 1.3f * scale, 0f), new Vector3(0.18f, 0.55f, 0.18f) * scale, bone);
        Part(parent, PrimitiveType.Capsule, "LegL", new Vector3(-0.2f * scale, 0.4f * scale, 0f), new Vector3(0.18f, 0.4f, 0.18f) * scale, bone);
        Part(parent, PrimitiveType.Capsule, "LegR", new Vector3(0.2f * scale, 0.4f * scale, 0f), new Vector3(0.18f, 0.4f, 0.18f) * scale, bone);
        Part(parent, PrimitiveType.Cube, "Sword", new Vector3(0.7f * scale, 1.2f * scale, 0.3f * scale), new Vector3(0.08f, 0.08f, 1.0f) * scale, Mat(new Color(0.7f, 0.7f, 0.75f)));
    }

    static void BuildWolf(Transform parent, float scale)
    {
        var fur = Mat(new Color(0.45f, 0.38f, 0.32f));
        var dark = Mat(new Color(0.2f, 0.16f, 0.12f));
        var eye = Mat(new Color(1f, 0.85f, 0.2f));
        Part(parent, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.7f * scale, 0f), new Vector3(0.7f, 0.45f, 1.3f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.95f * scale, 0.75f * scale), Vector3.one * 0.5f * scale, fur);
        Part(parent, PrimitiveType.Cube, "Snout", new Vector3(0f, 0.85f * scale, 1.05f * scale), new Vector3(0.28f, 0.22f, 0.35f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "EarL", new Vector3(-0.18f * scale, 1.25f * scale, 0.7f * scale), new Vector3(0.1f, 0.25f, 0.08f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "EarR", new Vector3(0.18f * scale, 1.25f * scale, 0.7f * scale), new Vector3(0.1f, 0.25f, 0.08f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.15f * scale, 1.0f * scale, 0.95f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.15f * scale, 1.0f * scale, 0.95f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Capsule, "LegFL", new Vector3(-0.28f * scale, 0.3f * scale, 0.4f * scale), new Vector3(0.15f, 0.3f, 0.15f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegFR", new Vector3(0.28f * scale, 0.3f * scale, 0.4f * scale), new Vector3(0.15f, 0.3f, 0.15f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegBL", new Vector3(-0.28f * scale, 0.3f * scale, -0.4f * scale), new Vector3(0.15f, 0.3f, 0.15f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegBR", new Vector3(0.28f * scale, 0.3f * scale, -0.4f * scale), new Vector3(0.15f, 0.3f, 0.15f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.75f * scale, -0.85f * scale), new Vector3(0.12f, 0.12f, 0.55f) * scale, fur);
    }

    static void BuildSpider(Transform parent, float scale)
    {
        var body = Mat(new Color(0.25f, 0.12f, 0.1f));
        var accent = Mat(new Color(0.7f, 0.15f, 0.1f));
        Part(parent, PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.55f * scale, -0.35f * scale), new Vector3(1.1f, 0.85f, 1.1f) * scale, body);
        Part(parent, PrimitiveType.Sphere, "Thorax", new Vector3(0f, 0.55f * scale, 0.35f * scale), Vector3.one * 0.7f * scale, body);
        Part(parent, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0.7f * scale, 0.6f * scale), Vector3.one * 0.2f * scale, accent);
        for (int i = 0; i < 4; i++)
        {
            float z = 0.35f - i * 0.2f;
            float spread = 0.55f + (i % 2) * 0.1f;
            Part(parent, PrimitiveType.Capsule, "LegL" + i, new Vector3(-spread * scale, 0.35f * scale, z * scale), new Vector3(0.1f, 0.45f, 0.1f) * scale, body);
            Part(parent, PrimitiveType.Capsule, "LegR" + i, new Vector3(spread * scale, 0.35f * scale, z * scale), new Vector3(0.1f, 0.45f, 0.1f) * scale, body);
        }
    }

    static void BuildBat(Transform parent, float scale)
    {
        var fur = Mat(new Color(0.35f, 0.25f, 0.4f));
        var wing = Mat(new Color(0.45f, 0.2f, 0.55f));
        var eye = Mat(new Color(1f, 0.3f, 0.3f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.7f, 0.6f, 0.7f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.45f * scale, 0.25f * scale), Vector3.one * 0.4f * scale, fur);
        Part(parent, PrimitiveType.Cube, "EarL", new Vector3(-0.15f * scale, 1.7f * scale, 0.2f * scale), new Vector3(0.08f, 0.25f, 0.08f) * scale, fur);
        Part(parent, PrimitiveType.Cube, "EarR", new Vector3(0.15f * scale, 1.7f * scale, 0.2f * scale), new Vector3(0.08f, 0.25f, 0.08f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.12f * scale, 1.5f * scale, 0.4f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.12f * scale, 1.5f * scale, 0.4f * scale), Vector3.one * 0.1f * scale, eye);
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.7f * scale, 1.2f * scale, 0f), new Vector3(0.9f, 0.08f, 0.55f) * scale, wing);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.7f * scale, 1.2f * scale, 0f), new Vector3(0.9f, 0.08f, 0.55f) * scale, wing);
    }

    static void BuildMushroom(Transform parent, float scale)
    {
        var stem = Mat(new Color(0.9f, 0.85f, 0.7f));
        var cap = Mat(new Color(0.85f, 0.25f, 0.3f));
        var spot = Mat(new Color(1f, 0.95f, 0.9f));
        var eye = Mat(Color.black);
        Part(parent, PrimitiveType.Cylinder, "Stem", new Vector3(0f, 0.7f * scale, 0f), new Vector3(0.7f, 0.7f, 0.7f) * scale, stem);
        Part(parent, PrimitiveType.Sphere, "Cap", new Vector3(0f, 1.5f * scale, 0f), new Vector3(1.5f, 0.7f, 1.5f) * scale, cap);
        Part(parent, PrimitiveType.Sphere, "Spot1", new Vector3(-0.35f * scale, 1.65f * scale, 0.35f * scale), Vector3.one * 0.25f * scale, spot);
        Part(parent, PrimitiveType.Sphere, "Spot2", new Vector3(0.4f * scale, 1.55f * scale, -0.2f * scale), Vector3.one * 0.22f * scale, spot);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.2f * scale, 0.95f * scale, 0.35f * scale), Vector3.one * 0.15f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.2f * scale, 0.95f * scale, 0.35f * scale), Vector3.one * 0.15f * scale, eye);
    }

    static void BuildGolem(Transform parent, float scale)
    {
        var stone = Mat(new Color(0.5f, 0.48f, 0.45f));
        var moss = Mat(new Color(0.3f, 0.45f, 0.25f));
        var core = Mat(new Color(0.9f, 0.45f, 0.15f));
        Part(parent, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.2f * scale, 0f), new Vector3(1.1f, 1.2f, 0.7f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "Head", new Vector3(0f, 2.15f * scale, 0f), new Vector3(0.7f, 0.55f, 0.65f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.85f * scale, 1.2f * scale, 0f), new Vector3(0.4f, 1.0f, 0.4f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.85f * scale, 1.2f * scale, 0f), new Vector3(0.4f, 1.0f, 0.4f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "LegL", new Vector3(-0.3f * scale, 0.35f * scale, 0f), new Vector3(0.4f, 0.7f, 0.4f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "LegR", new Vector3(0.3f * scale, 0.35f * scale, 0f), new Vector3(0.4f, 0.7f, 0.4f) * scale, stone);
        Part(parent, PrimitiveType.Cube, "Moss", new Vector3(0.15f * scale, 1.5f * scale, 0.35f * scale), new Vector3(0.5f, 0.25f, 0.15f) * scale, moss);
        Part(parent, PrimitiveType.Sphere, "Core", new Vector3(0f, 1.3f * scale, 0.4f * scale), Vector3.one * 0.28f * scale, core);
    }

    static void BuildGhost(Transform parent, float scale)
    {
        var cloth = Mat(new Color(0.65f, 0.8f, 0.95f, 1f));
        var dark = Mat(new Color(0.25f, 0.35f, 0.5f));
        var eye = Mat(new Color(0.4f, 0.9f, 1f));
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.8f * scale, 0f), Vector3.one * 0.7f * scale, cloth);
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.0f * scale, 0f), new Vector3(0.85f, 0.9f, 0.6f) * scale, cloth);
        Part(parent, PrimitiveType.Cube, "Trail", new Vector3(0f, 0.35f * scale, -0.1f * scale), new Vector3(0.7f, 0.5f, 0.5f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.18f * scale, 1.85f * scale, 0.3f * scale), Vector3.one * 0.14f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.18f * scale, 1.85f * scale, 0.3f * scale), Vector3.one * 0.14f * scale, eye);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.6f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.2f, 0.7f, 0.2f) * scale, cloth);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.6f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.2f, 0.7f, 0.2f) * scale, cloth);
    }

    static void BuildImp(Transform parent, float scale)
    {
        var skin = Mat(new Color(0.75f, 0.2f, 0.15f));
        var dark = Mat(new Color(0.35f, 0.08f, 0.08f));
        var horn = Mat(new Color(0.9f, 0.75f, 0.2f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.7f * scale, 0f), new Vector3(0.55f, 0.55f, 0.5f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.35f * scale, 0f), Vector3.one * 0.45f * scale, skin);
        Part(parent, PrimitiveType.Cube, "HornL", new Vector3(-0.15f * scale, 1.65f * scale, 0f), new Vector3(0.08f, 0.28f, 0.08f) * scale, horn);
        Part(parent, PrimitiveType.Cube, "HornR", new Vector3(0.15f * scale, 1.65f * scale, 0f), new Vector3(0.08f, 0.28f, 0.08f) * scale, horn);
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.45f * scale, 0.95f * scale, -0.1f * scale), new Vector3(0.55f, 0.08f, 0.4f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.45f * scale, 0.95f * scale, -0.1f * scale), new Vector3(0.55f, 0.08f, 0.4f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.5f * scale, -0.45f * scale), new Vector3(0.1f, 0.1f, 0.5f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "TailTip", new Vector3(0f, 0.5f * scale, -0.75f * scale), Vector3.one * 0.18f * scale, horn);
    }

    static void BuildBeetle(Transform parent, float scale)
    {
        var shell = Mat(new Color(0.2f, 0.45f, 0.25f));
        var belly = Mat(new Color(0.35f, 0.3f, 0.15f));
        var horn = Mat(new Color(0.15f, 0.15f, 0.12f));
        Part(parent, PrimitiveType.Sphere, "Shell", new Vector3(0f, 0.55f * scale, 0f), new Vector3(1.3f, 0.9f, 1.5f) * scale, shell);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.5f * scale, 0.7f * scale), Vector3.one * 0.55f * scale, belly);
        Part(parent, PrimitiveType.Cube, "Horn", new Vector3(0f, 0.85f * scale, 0.85f * scale), new Vector3(0.12f, 0.12f, 0.55f) * scale, horn);
        Part(parent, PrimitiveType.Capsule, "LegL1", new Vector3(-0.55f * scale, 0.25f * scale, 0.25f * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, belly);
        Part(parent, PrimitiveType.Capsule, "LegR1", new Vector3(0.55f * scale, 0.25f * scale, 0.25f * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, belly);
        Part(parent, PrimitiveType.Capsule, "LegL2", new Vector3(-0.55f * scale, 0.25f * scale, -0.25f * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, belly);
        Part(parent, PrimitiveType.Capsule, "LegR2", new Vector3(0.55f * scale, 0.25f * scale, -0.25f * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, belly);
    }

    static void BuildTreant(Transform parent, float scale)
    {
        var bark = Mat(new Color(0.35f, 0.22f, 0.12f));
        var leaf = Mat(new Color(0.25f, 0.55f, 0.2f));
        var eye = Mat(new Color(0.9f, 0.85f, 0.3f));
        Part(parent, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.0f * scale, 0f), new Vector3(0.9f, 1.0f, 0.9f) * scale, bark);
        Part(parent, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 2.3f * scale, 0f), new Vector3(1.6f, 1.1f, 1.6f) * scale, leaf);
        Part(parent, PrimitiveType.Cube, "BranchL", new Vector3(-0.8f * scale, 1.6f * scale, 0f), new Vector3(0.9f, 0.2f, 0.2f) * scale, bark);
        Part(parent, PrimitiveType.Cube, "BranchR", new Vector3(0.8f * scale, 1.5f * scale, 0.1f * scale), new Vector3(0.9f, 0.2f, 0.2f) * scale, bark);
        Part(parent, PrimitiveType.Sphere, "LeafL", new Vector3(-1.2f * scale, 1.7f * scale, 0f), Vector3.one * 0.45f * scale, leaf);
        Part(parent, PrimitiveType.Sphere, "LeafR", new Vector3(1.2f * scale, 1.6f * scale, 0.1f * scale), Vector3.one * 0.4f * scale, leaf);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.2f * scale, 1.35f * scale, 0.4f * scale), Vector3.one * 0.18f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.2f * scale, 1.35f * scale, 0.4f * scale), Vector3.one * 0.18f * scale, eye);
        Part(parent, PrimitiveType.Cube, "RootL", new Vector3(-0.35f * scale, 0.2f * scale, 0.2f * scale), new Vector3(0.25f, 0.4f, 0.55f) * scale, bark);
        Part(parent, PrimitiveType.Cube, "RootR", new Vector3(0.35f * scale, 0.2f * scale, -0.15f * scale), new Vector3(0.25f, 0.4f, 0.55f) * scale, bark);
    }

    static void BuildLavaSpawn(Transform parent, float scale)
    {
        var lava = Mat(new Color(1f, 0.35f, 0.08f));
        var crust = Mat(new Color(0.25f, 0.12f, 0.08f));
        var core = Mat(new Color(1f, 0.85f, 0.2f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.8f * scale, 0f), new Vector3(1.3f, 1.2f, 1.3f) * scale, lava);
        Part(parent, PrimitiveType.Cube, "Crust1", new Vector3(-0.35f * scale, 1.0f * scale, 0.35f * scale), new Vector3(0.4f, 0.25f, 0.25f) * scale, crust);
        Part(parent, PrimitiveType.Cube, "Crust2", new Vector3(0.4f * scale, 0.7f * scale, -0.3f * scale), new Vector3(0.35f, 0.3f, 0.3f) * scale, crust);
        Part(parent, PrimitiveType.Sphere, "Core", new Vector3(0f, 0.9f * scale, 0.45f * scale), Vector3.one * 0.35f * scale, core);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.75f * scale, 0.7f * scale, 0f), new Vector3(0.35f, 0.7f, 0.35f) * scale, lava);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.75f * scale, 0.7f * scale, 0f), new Vector3(0.35f, 0.7f, 0.35f) * scale, lava);
    }

    static void BuildCrystal(Transform parent, float scale)
    {
        var crystal = Mat(new Color(0.45f, 0.75f, 1f));
        var dark = Mat(new Color(0.25f, 0.4f, 0.65f));
        var glow = Mat(new Color(0.8f, 0.95f, 1f));
        Part(parent, PrimitiveType.Cube, "Core", new Vector3(0f, 1.0f * scale, 0f), new Vector3(0.7f, 1.3f, 0.7f) * scale, crystal);
        Part(parent, PrimitiveType.Cube, "Spike1", new Vector3(-0.45f * scale, 1.5f * scale, 0f), new Vector3(0.25f, 1.0f, 0.25f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Spike2", new Vector3(0.45f * scale, 1.6f * scale, 0.15f * scale), new Vector3(0.25f, 1.1f, 0.25f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Spike3", new Vector3(0f, 1.9f * scale, -0.3f * scale), new Vector3(0.28f, 0.9f, 0.28f) * scale, glow);
        Part(parent, PrimitiveType.Cube, "Base", new Vector3(0f, 0.25f * scale, 0f), new Vector3(1.0f, 0.4f, 1.0f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "Eye", new Vector3(0f, 1.15f * scale, 0.4f * scale), Vector3.one * 0.25f * scale, glow);
    }

    static void BuildWraith(Transform parent, float scale)
    {
        var robe = Mat(new Color(0.15f, 0.12f, 0.22f));
        var hood = Mat(new Color(0.1f, 0.08f, 0.15f));
        var soul = Mat(new Color(0.55f, 0.3f, 1f));
        Part(parent, PrimitiveType.Capsule, "Robe", new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.9f, 1.0f, 0.7f) * scale, robe);
        Part(parent, PrimitiveType.Cube, "Hood", new Vector3(0f, 2.15f * scale, 0f), new Vector3(0.75f, 0.55f, 0.75f) * scale, hood);
        Part(parent, PrimitiveType.Sphere, "Face", new Vector3(0f, 2.05f * scale, 0.25f * scale), Vector3.one * 0.35f * scale, soul);
        Part(parent, PrimitiveType.Cube, "ScythePole", new Vector3(0.65f * scale, 1.4f * scale, 0f), new Vector3(0.08f, 1.6f, 0.08f) * scale, Mat(new Color(0.3f, 0.25f, 0.2f)));
        Part(parent, PrimitiveType.Cube, "ScytheBlade", new Vector3(0.65f * scale, 2.2f * scale, 0.35f * scale), new Vector3(0.08f, 0.15f, 0.7f) * scale, soul);
        Part(parent, PrimitiveType.Cube, "Arm", new Vector3(0.45f * scale, 1.5f * scale, 0.1f * scale), new Vector3(0.25f, 0.7f, 0.25f) * scale, robe);
    }

    static void BuildDemon(Transform parent, float scale)
    {
        var skin = Mat(new Color(0.45f, 0.08f, 0.12f));
        var armor = Mat(new Color(0.2f, 0.05f, 0.08f));
        var fire = Mat(new Color(1f, 0.45f, 0.1f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.2f * scale, 0f), new Vector3(1.0f, 1.1f, 0.75f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.45f * scale, 0f), Vector3.one * 0.65f * scale, skin);
        Part(parent, PrimitiveType.Cube, "HornL", new Vector3(-0.3f * scale, 2.9f * scale, -0.05f * scale), new Vector3(0.15f, 0.55f, 0.15f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "HornR", new Vector3(0.3f * scale, 2.9f * scale, -0.05f * scale), new Vector3(0.15f, 0.55f, 0.15f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.9f * scale, 1.6f * scale, -0.2f * scale), new Vector3(1.1f, 0.12f, 0.7f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.9f * scale, 1.6f * scale, -0.2f * scale), new Vector3(1.1f, 0.12f, 0.7f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.75f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.35f, 0.9f, 0.35f) * scale, skin);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.75f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.35f, 0.9f, 0.35f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "Flame", new Vector3(0f, 1.4f * scale, 0.45f * scale), Vector3.one * 0.3f * scale, fire);
        Part(parent, PrimitiveType.Cube, "LegL", new Vector3(-0.3f * scale, 0.35f * scale, 0f), new Vector3(0.35f, 0.7f, 0.35f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "LegR", new Vector3(0.3f * scale, 0.35f * scale, 0f), new Vector3(0.35f, 0.7f, 0.35f) * scale, armor);
    }

    static void BuildWorldBoss(Transform parent, float scale)
    {
        int world = AdventureManager.Instance != null ? AdventureManager.Instance.currentWorld : 1;
        int theme = ((world - 1) % 10) + 1;
        Color armorC, darkC, accentC, eyeC;
        switch (theme)
        {
            case 1: armorC = new Color(0.35f, 0.55f, 0.25f); darkC = new Color(0.2f, 0.3f, 0.12f); accentC = new Color(0.9f, 0.8f, 0.3f); eyeC = new Color(1f, 0.9f, 0.2f); break;
            case 2: armorC = new Color(0.2f, 0.45f, 0.18f); darkC = new Color(0.15f, 0.2f, 0.1f); accentC = new Color(0.6f, 0.9f, 0.3f); eyeC = new Color(0.8f, 1f, 0.4f); break;
            case 3: armorC = new Color(0.55f, 0.52f, 0.48f); darkC = new Color(0.25f, 0.22f, 0.2f); accentC = new Color(0.85f, 0.85f, 0.7f); eyeC = new Color(0.9f, 0.2f, 0.2f); break;
            case 4: armorC = new Color(0.7f, 0.25f, 0.1f); darkC = new Color(0.25f, 0.08f, 0.05f); accentC = new Color(1f, 0.6f, 0.15f); eyeC = new Color(1f, 0.4f, 0.1f); break;
            case 5: armorC = new Color(0.4f, 0.12f, 0.5f); darkC = new Color(0.12f, 0.05f, 0.18f); accentC = new Color(0.85f, 0.4f, 1f); eyeC = new Color(1f, 0.3f, 1f); break;
            case 6: armorC = new Color(0.65f, 0.8f, 0.95f); darkC = new Color(0.35f, 0.45f, 0.55f); accentC = new Color(0.9f, 0.95f, 1f); eyeC = new Color(0.4f, 0.8f, 1f); break;
            case 7: armorC = new Color(0.75f, 0.55f, 0.25f); darkC = new Color(0.35f, 0.22f, 0.1f); accentC = new Color(1f, 0.85f, 0.4f); eyeC = new Color(1f, 0.7f, 0.2f); break;
            case 8: armorC = new Color(0.25f, 0.45f, 0.28f); darkC = new Color(0.12f, 0.2f, 0.12f); accentC = new Color(0.5f, 0.9f, 0.35f); eyeC = new Color(0.7f, 1f, 0.3f); break;
            case 9: armorC = new Color(0.7f, 0.8f, 0.95f); darkC = new Color(0.4f, 0.5f, 0.65f); accentC = new Color(1f, 1f, 1f); eyeC = new Color(0.6f, 0.85f, 1f); break;
            default: armorC = new Color(0.35f, 0.08f, 0.4f); darkC = new Color(0.08f, 0.02f, 0.1f); accentC = new Color(0.9f, 0.2f, 1f); eyeC = new Color(1f, 0.15f, 0.5f); break;
        }
        if (world > 10)
        {
            float t = ((world - 11) % 15) / 15f;
            armorC = Color.Lerp(armorC, new Color(armorC.b, armorC.r, armorC.g), 0.2f + t * 0.2f);
            accentC = Color.Lerp(accentC, new Color(accentC.g, accentC.b, accentC.r), 0.15f + t * 0.2f);
        }

        var armor = Mat(armorC);
        var dark = Mat(darkC);
        var gold = Mat(accentC);
        var eye = Mat(eyeC);
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.4f * scale, 0f), new Vector3(1.3f, 1.3f, 1.0f) * scale, armor);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.9f * scale, 0f), Vector3.one * 0.85f * scale, armor);
        Part(parent, PrimitiveType.Cube, "Crown", new Vector3(0f, 3.4f * scale, 0f), new Vector3(0.9f, 0.25f, 0.9f) * scale, gold);
        Part(parent, PrimitiveType.Cube, "Spike1", new Vector3(-0.25f * scale, 3.7f * scale, 0f), new Vector3(0.12f, 0.45f, 0.12f) * scale, gold);
        Part(parent, PrimitiveType.Cube, "Spike2", new Vector3(0.25f * scale, 3.7f * scale, 0f), new Vector3(0.12f, 0.45f, 0.12f) * scale, gold);
        Part(parent, PrimitiveType.Cube, "ShoulderL", new Vector3(-1.0f * scale, 2.0f * scale, 0f), new Vector3(0.7f, 0.5f, 0.6f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "ShoulderR", new Vector3(1.0f * scale, 2.0f * scale, 0f), new Vector3(0.7f, 0.5f, 0.6f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-1.15f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.45f, 1.1f, 0.45f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(1.15f * scale, 1.2f * scale, 0.1f * scale), new Vector3(0.45f, 1.1f, 0.45f) * scale, armor);
        Part(parent, PrimitiveType.Cube, "Mace", new Vector3(1.4f * scale, 0.7f * scale, 0.4f * scale), new Vector3(0.35f, 0.35f, 1.2f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "MaceHead", new Vector3(1.4f * scale, 0.7f * scale, 1.1f * scale), Vector3.one * 0.55f * scale, gold);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.22f * scale, 2.95f * scale, 0.35f * scale), Vector3.one * 0.16f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.22f * scale, 2.95f * scale, 0.35f * scale), Vector3.one * 0.16f * scale, eye);
        Part(parent, PrimitiveType.Cube, "LegL", new Vector3(-0.4f * scale, 0.4f * scale, 0f), new Vector3(0.45f, 0.8f, 0.45f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "LegR", new Vector3(0.4f * scale, 0.4f * scale, 0f), new Vector3(0.45f, 0.8f, 0.45f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Cape", new Vector3(0f, 1.5f * scale, -0.6f * scale), new Vector3(1.4f, 1.6f, 0.12f) * scale, dark);

        // Extra silhouette cue per theme (shared across remixed late worlds)
        switch (theme)
        {
            case 6:
                Part(parent, PrimitiveType.Cube, "IceHornL", new Vector3(-0.4f * scale, 3.5f * scale, 0f), new Vector3(0.15f, 0.7f, 0.15f) * scale, gold);
                Part(parent, PrimitiveType.Cube, "IceHornR", new Vector3(0.4f * scale, 3.5f * scale, 0f), new Vector3(0.15f, 0.7f, 0.15f) * scale, gold);
                break;
            case 7:
                Part(parent, PrimitiveType.Sphere, "SandOrb", new Vector3(0f, 1.6f * scale, 0.55f * scale), Vector3.one * 0.4f * scale, gold);
                break;
            case 9:
                Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-1.3f * scale, 2.2f * scale, -0.2f * scale), new Vector3(1.4f, 0.12f, 0.8f) * scale, gold);
                Part(parent, PrimitiveType.Cube, "WingR", new Vector3(1.3f * scale, 2.2f * scale, -0.2f * scale), new Vector3(1.4f, 0.12f, 0.8f) * scale, gold);
                break;
            case 10:
                Part(parent, PrimitiveType.Sphere, "VoidCore", new Vector3(0f, 1.5f * scale, 0.5f * scale), Vector3.one * 0.45f * scale, gold);
                break;
        }
    }

    static void BuildYeti(Transform parent, float scale)
    {
        var fur = Mat(new Color(0.85f, 0.9f, 0.95f));
        var dark = Mat(new Color(0.45f, 0.5f, 0.55f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.1f * scale, 0f), new Vector3(1.1f, 0.9f, 0.85f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.2f * scale, 0.1f * scale), Vector3.one * 0.7f * scale, fur);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.85f * scale, 1.1f * scale, 0f), new Vector3(0.4f, 0.9f, 0.4f) * scale, fur);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.85f * scale, 1.1f * scale, 0f), new Vector3(0.4f, 0.9f, 0.4f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegL", new Vector3(-0.3f * scale, 0.35f * scale, 0f), new Vector3(0.35f, 0.35f, 0.35f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegR", new Vector3(0.3f * scale, 0.35f * scale, 0f), new Vector3(0.35f, 0.35f, 0.35f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.18f * scale, 2.25f * scale, 0.35f * scale), Vector3.one * 0.12f * scale, Mat(Color.black));
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.18f * scale, 2.25f * scale, 0.35f * scale), Vector3.one * 0.12f * scale, Mat(Color.black));
    }

    static void BuildIceElemental(Transform parent, float scale)
    {
        var ice = Mat(new Color(0.55f, 0.8f, 1f));
        var core = Mat(new Color(0.85f, 0.95f, 1f));
        Part(parent, PrimitiveType.Cube, "Core", new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.9f, 1.4f, 0.9f) * scale, ice);
        Part(parent, PrimitiveType.Cube, "Spike1", new Vector3(-0.5f * scale, 1.8f * scale, 0f), new Vector3(0.25f, 1.0f, 0.25f) * scale, core);
        Part(parent, PrimitiveType.Cube, "Spike2", new Vector3(0.5f * scale, 1.9f * scale, 0.1f * scale), new Vector3(0.25f, 1.1f, 0.25f) * scale, core);
        Part(parent, PrimitiveType.Cube, "Spike3", new Vector3(0f, 2.2f * scale, -0.2f * scale), new Vector3(0.3f, 0.9f, 0.3f) * scale, ice);
        Part(parent, PrimitiveType.Sphere, "Eye", new Vector3(0f, 1.3f * scale, 0.5f * scale), Vector3.one * 0.28f * scale, core);
        Part(parent, PrimitiveType.Cube, "Base", new Vector3(0f, 0.25f * scale, 0f), new Vector3(1.1f, 0.4f, 1.1f) * scale, Mat(new Color(0.4f, 0.55f, 0.7f)));
    }

    static void BuildScorpion(Transform parent, float scale)
    {
        var shell = Mat(new Color(0.65f, 0.4f, 0.18f));
        var dark = Mat(new Color(0.35f, 0.2f, 0.08f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.5f * scale, 0f), new Vector3(1.4f, 0.7f, 1.8f) * scale, shell);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.55f * scale, 0.85f * scale), Vector3.one * 0.55f * scale, shell);
        Part(parent, PrimitiveType.Cube, "Tail", new Vector3(0f, 1.0f * scale, -0.7f * scale), new Vector3(0.2f, 1.0f, 0.2f) * scale, dark);
        Part(parent, PrimitiveType.Sphere, "Stinger", new Vector3(0f, 1.55f * scale, -0.55f * scale), Vector3.one * 0.28f * scale, Mat(new Color(0.9f, 0.2f, 0.15f)));
        Part(parent, PrimitiveType.Cube, "ClawL", new Vector3(-0.7f * scale, 0.45f * scale, 0.7f * scale), new Vector3(0.55f, 0.2f, 0.35f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "ClawR", new Vector3(0.7f * scale, 0.45f * scale, 0.7f * scale), new Vector3(0.55f, 0.2f, 0.35f) * scale, dark);
        for (int i = 0; i < 3; i++)
        {
            float z = 0.4f - i * 0.35f;
            Part(parent, PrimitiveType.Capsule, "LegL" + i, new Vector3(-0.7f * scale, 0.25f * scale, z * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, dark);
            Part(parent, PrimitiveType.Capsule, "LegR" + i, new Vector3(0.7f * scale, 0.25f * scale, z * scale), new Vector3(0.12f, 0.25f, 0.12f) * scale, dark);
        }
    }

    static void BuildMummy(Transform parent, float scale)
    {
        var wrap = Mat(new Color(0.85f, 0.75f, 0.55f));
        var dark = Mat(new Color(0.35f, 0.25f, 0.15f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.75f, 0.95f, 0.55f) * scale, wrap);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.25f * scale, 0f), Vector3.one * 0.5f * scale, wrap);
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.55f * scale, 1.3f * scale, 0.25f * scale), new Vector3(0.22f, 0.7f, 0.22f) * scale, wrap);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.55f * scale, 1.3f * scale, 0.25f * scale), new Vector3(0.22f, 0.7f, 0.22f) * scale, wrap);
        Part(parent, PrimitiveType.Capsule, "LegL", new Vector3(-0.2f * scale, 0.35f * scale, 0f), new Vector3(0.22f, 0.35f, 0.22f) * scale, dark);
        Part(parent, PrimitiveType.Capsule, "LegR", new Vector3(0.2f * scale, 0.35f * scale, 0f), new Vector3(0.22f, 0.35f, 0.22f) * scale, dark);
        Part(parent, PrimitiveType.Cube, "Mask", new Vector3(0f, 2.25f * scale, 0.25f * scale), new Vector3(0.4f, 0.35f, 0.1f) * scale, dark);
    }

    static void BuildSandWorm(Transform parent, float scale)
    {
        var sand = Mat(new Color(0.7f, 0.55f, 0.3f));
        var mouth = Mat(new Color(0.25f, 0.1f, 0.08f));
        Part(parent, PrimitiveType.Sphere, "Seg1", new Vector3(0f, 0.7f * scale, 0f), new Vector3(1.3f, 1.3f, 1.3f) * scale, sand);
        Part(parent, PrimitiveType.Sphere, "Seg2", new Vector3(0f, 1.5f * scale, -0.35f * scale), new Vector3(1.1f, 1.1f, 1.1f) * scale, sand);
        Part(parent, PrimitiveType.Sphere, "Seg3", new Vector3(0f, 2.2f * scale, -0.55f * scale), new Vector3(0.9f, 0.9f, 0.9f) * scale, sand);
        Part(parent, PrimitiveType.Sphere, "Mouth", new Vector3(0f, 2.55f * scale, -0.2f * scale), Vector3.one * 0.55f * scale, mouth);
        Part(parent, PrimitiveType.Cube, "FangL", new Vector3(-0.2f * scale, 2.55f * scale, 0.15f * scale), new Vector3(0.1f, 0.1f, 0.35f) * scale, Mat(Color.white));
        Part(parent, PrimitiveType.Cube, "FangR", new Vector3(0.2f * scale, 2.55f * scale, 0.15f * scale), new Vector3(0.1f, 0.1f, 0.35f) * scale, Mat(Color.white));
    }

    static void BuildToad(Transform parent, float scale)
    {
        var skin = Mat(new Color(0.3f, 0.55f, 0.28f));
        var belly = Mat(new Color(0.55f, 0.65f, 0.35f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.65f * scale, 0f), new Vector3(1.7f, 1.1f, 1.5f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.5f * scale, 0.35f * scale), new Vector3(1.2f, 0.8f, 0.9f) * scale, belly);
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.4f * scale, 1.15f * scale, 0.45f * scale), Vector3.one * 0.35f * scale, Mat(new Color(1f, 0.9f, 0.3f)));
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.4f * scale, 1.15f * scale, 0.45f * scale), Vector3.one * 0.35f * scale, Mat(new Color(1f, 0.9f, 0.3f)));
        Part(parent, PrimitiveType.Sphere, "PupilL", new Vector3(-0.4f * scale, 1.2f * scale, 0.6f * scale), Vector3.one * 0.14f * scale, Mat(Color.black));
        Part(parent, PrimitiveType.Sphere, "PupilR", new Vector3(0.4f * scale, 1.2f * scale, 0.6f * scale), Vector3.one * 0.14f * scale, Mat(Color.black));
        Part(parent, PrimitiveType.Capsule, "LegL", new Vector3(-0.55f * scale, 0.3f * scale, -0.2f * scale), new Vector3(0.35f, 0.3f, 0.55f) * scale, skin);
        Part(parent, PrimitiveType.Capsule, "LegR", new Vector3(0.55f * scale, 0.3f * scale, -0.2f * scale), new Vector3(0.35f, 0.3f, 0.55f) * scale, skin);
    }

    static void BuildLeech(Transform parent, float scale)
    {
        var body = Mat(new Color(0.45f, 0.12f, 0.18f));
        var dark = Mat(new Color(0.2f, 0.05f, 0.08f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.45f * scale, 0f), new Vector3(0.7f, 0.4f, 1.8f) * scale, body);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.5f * scale, 0.85f * scale), Vector3.one * 0.55f * scale, dark);
        Part(parent, PrimitiveType.Sphere, "Suck", new Vector3(0f, 0.5f * scale, 1.15f * scale), Vector3.one * 0.35f * scale, Mat(new Color(0.7f, 0.15f, 0.2f)));
        Part(parent, PrimitiveType.Sphere, "Seg1", new Vector3(0f, 0.4f * scale, -0.5f * scale), Vector3.one * 0.5f * scale, body);
        Part(parent, PrimitiveType.Sphere, "Seg2", new Vector3(0f, 0.35f * scale, -0.95f * scale), Vector3.one * 0.4f * scale, dark);
    }

    static void BuildMosquito(Transform parent, float scale)
    {
        var body = Mat(new Color(0.25f, 0.35f, 0.2f));
        var wing = Mat(new Color(0.7f, 0.85f, 0.75f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 1.0f * scale, 0f), new Vector3(0.55f, 0.45f, 0.9f) * scale, body);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.1f * scale, 0.5f * scale), Vector3.one * 0.35f * scale, body);
        Part(parent, PrimitiveType.Cube, "Proboscis", new Vector3(0f, 1.0f * scale, 0.85f * scale), new Vector3(0.06f, 0.06f, 0.55f) * scale, Mat(new Color(0.15f, 0.15f, 0.12f)));
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.55f * scale, 1.2f * scale, 0f), new Vector3(0.8f, 0.05f, 0.45f) * scale, wing);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.55f * scale, 1.2f * scale, 0f), new Vector3(0.8f, 0.05f, 0.45f) * scale, wing);
        Part(parent, PrimitiveType.Capsule, "LegL1", new Vector3(-0.25f * scale, 0.55f * scale, 0.15f * scale), new Vector3(0.08f, 0.35f, 0.08f) * scale, body);
        Part(parent, PrimitiveType.Capsule, "LegR1", new Vector3(0.25f * scale, 0.55f * scale, 0.15f * scale), new Vector3(0.08f, 0.35f, 0.08f) * scale, body);
    }

    static void BuildHarpy(Transform parent, float scale)
    {
        var skin = Mat(new Color(0.75f, 0.55f, 0.45f));
        var feather = Mat(new Color(0.45f, 0.55f, 0.75f));
        Part(parent, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.65f, 0.7f, 0.5f) * scale, skin);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.95f * scale, 0f), Vector3.one * 0.45f * scale, skin);
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.85f * scale, 1.3f * scale, -0.1f * scale), new Vector3(1.2f, 0.1f, 0.7f) * scale, feather);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.85f * scale, 1.3f * scale, -0.1f * scale), new Vector3(1.2f, 0.1f, 0.7f) * scale, feather);
        Part(parent, PrimitiveType.Cube, "TalonL", new Vector3(-0.25f * scale, 0.35f * scale, 0.1f * scale), new Vector3(0.2f, 0.35f, 0.2f) * scale, Mat(new Color(0.9f, 0.75f, 0.3f)));
        Part(parent, PrimitiveType.Cube, "TalonR", new Vector3(0.25f * scale, 0.35f * scale, 0.1f * scale), new Vector3(0.2f, 0.35f, 0.2f) * scale, Mat(new Color(0.9f, 0.75f, 0.3f)));
        Part(parent, PrimitiveType.Cube, "Hair", new Vector3(0f, 2.25f * scale, -0.05f * scale), new Vector3(0.5f, 0.25f, 0.4f) * scale, feather);
    }

    static void BuildStormSprite(Transform parent, float scale)
    {
        var glow = Mat(new Color(0.7f, 0.85f, 1f));
        var core = Mat(new Color(1f, 1f, 1f));
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 1.2f * scale, 0f), Vector3.one * 0.85f * scale, glow);
        Part(parent, PrimitiveType.Sphere, "Core", new Vector3(0f, 1.2f * scale, 0f), Vector3.one * 0.4f * scale, core);
        Part(parent, PrimitiveType.Cube, "Bolt1", new Vector3(-0.55f * scale, 1.5f * scale, 0f), new Vector3(0.12f, 0.7f, 0.12f) * scale, core);
        Part(parent, PrimitiveType.Cube, "Bolt2", new Vector3(0.55f * scale, 0.9f * scale, 0.1f * scale), new Vector3(0.12f, 0.7f, 0.12f) * scale, core);
        Part(parent, PrimitiveType.Cube, "Bolt3", new Vector3(0f, 1.9f * scale, -0.2f * scale), new Vector3(0.12f, 0.55f, 0.12f) * scale, glow);
        Part(parent, PrimitiveType.Sphere, "Spark", new Vector3(0.3f * scale, 1.7f * scale, 0.35f * scale), Vector3.one * 0.2f * scale, core);
    }

    static void BuildGriffon(Transform parent, float scale)
    {
        var fur = Mat(new Color(0.75f, 0.6f, 0.35f));
        var feather = Mat(new Color(0.85f, 0.85f, 0.9f));
        Part(parent, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.9f * scale, 0f), new Vector3(0.8f, 0.55f, 1.3f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.4f * scale, 0.75f * scale), Vector3.one * 0.5f * scale, feather);
        Part(parent, PrimitiveType.Cube, "Beak", new Vector3(0f, 1.3f * scale, 1.05f * scale), new Vector3(0.2f, 0.15f, 0.3f) * scale, Mat(new Color(0.95f, 0.75f, 0.2f)));
        Part(parent, PrimitiveType.Cube, "WingL", new Vector3(-0.9f * scale, 1.2f * scale, 0f), new Vector3(1.1f, 0.1f, 0.7f) * scale, feather);
        Part(parent, PrimitiveType.Cube, "WingR", new Vector3(0.9f * scale, 1.2f * scale, 0f), new Vector3(1.1f, 0.1f, 0.7f) * scale, feather);
        Part(parent, PrimitiveType.Capsule, "LegFL", new Vector3(-0.3f * scale, 0.35f * scale, 0.4f * scale), new Vector3(0.18f, 0.3f, 0.18f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegFR", new Vector3(0.3f * scale, 0.35f * scale, 0.4f * scale), new Vector3(0.18f, 0.3f, 0.18f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegBL", new Vector3(-0.3f * scale, 0.35f * scale, -0.4f * scale), new Vector3(0.18f, 0.3f, 0.18f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegBR", new Vector3(0.3f * scale, 0.35f * scale, -0.4f * scale), new Vector3(0.18f, 0.3f, 0.18f) * scale, fur);
    }

    static void BuildShadowHound(Transform parent, float scale)
    {
        var fur = Mat(new Color(0.12f, 0.08f, 0.16f));
        var eye = Mat(new Color(0.9f, 0.2f, 1f));
        Part(parent, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.7f * scale, 0f), new Vector3(0.7f, 0.45f, 1.4f) * scale, fur);
        Part(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.95f * scale, 0.8f * scale), Vector3.one * 0.5f * scale, fur);
        Part(parent, PrimitiveType.Cube, "Snout", new Vector3(0f, 0.85f * scale, 1.15f * scale), new Vector3(0.3f, 0.22f, 0.4f) * scale, Mat(new Color(0.08f, 0.05f, 0.1f)));
        Part(parent, PrimitiveType.Sphere, "EyeL", new Vector3(-0.15f * scale, 1.05f * scale, 1.0f * scale), Vector3.one * 0.12f * scale, eye);
        Part(parent, PrimitiveType.Sphere, "EyeR", new Vector3(0.15f * scale, 1.05f * scale, 1.0f * scale), Vector3.one * 0.12f * scale, eye);
        Part(parent, PrimitiveType.Capsule, "LegFL", new Vector3(-0.28f * scale, 0.3f * scale, 0.45f * scale), new Vector3(0.15f, 0.28f, 0.15f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegFR", new Vector3(0.28f * scale, 0.3f * scale, 0.45f * scale), new Vector3(0.15f, 0.28f, 0.15f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegBL", new Vector3(-0.28f * scale, 0.3f * scale, -0.45f * scale), new Vector3(0.15f, 0.28f, 0.15f) * scale, fur);
        Part(parent, PrimitiveType.Capsule, "LegBR", new Vector3(0.28f * scale, 0.3f * scale, -0.45f * scale), new Vector3(0.15f, 0.28f, 0.15f) * scale, fur);
        Part(parent, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.75f * scale, -0.95f * scale), new Vector3(0.12f, 0.12f, 0.6f) * scale, fur);
    }

    static void BuildVoidEye(Transform parent, float scale)
    {
        var shell = Mat(new Color(0.2f, 0.08f, 0.28f));
        var iris = Mat(new Color(0.85f, 0.25f, 1f));
        var pupil = Mat(Color.black);
        Part(parent, PrimitiveType.Sphere, "Body", new Vector3(0f, 1.2f * scale, 0f), Vector3.one * 1.4f * scale, shell);
        Part(parent, PrimitiveType.Sphere, "Iris", new Vector3(0f, 1.2f * scale, 0.55f * scale), Vector3.one * 0.7f * scale, iris);
        Part(parent, PrimitiveType.Sphere, "Pupil", new Vector3(0f, 1.2f * scale, 0.85f * scale), Vector3.one * 0.35f * scale, pupil);
        Part(parent, PrimitiveType.Cube, "Tendril1", new Vector3(-0.7f * scale, 0.5f * scale, 0f), new Vector3(0.15f, 0.9f, 0.15f) * scale, shell);
        Part(parent, PrimitiveType.Cube, "Tendril2", new Vector3(0.7f * scale, 0.5f * scale, 0.1f * scale), new Vector3(0.15f, 0.9f, 0.15f) * scale, shell);
        Part(parent, PrimitiveType.Cube, "Tendril3", new Vector3(0f, 0.4f * scale, -0.6f * scale), new Vector3(0.15f, 0.8f, 0.15f) * scale, shell);
    }

    static void BuildTentacle(Transform parent, float scale)
    {
        var flesh = Mat(new Color(0.45f, 0.15f, 0.35f));
        var tip = Mat(new Color(0.7f, 0.2f, 0.55f));
        Part(parent, PrimitiveType.Capsule, "Base", new Vector3(0f, 0.6f * scale, 0f), new Vector3(0.9f, 0.6f, 0.9f) * scale, flesh);
        Part(parent, PrimitiveType.Capsule, "Mid", new Vector3(0.15f * scale, 1.5f * scale, 0.1f * scale), new Vector3(0.7f, 0.7f, 0.7f) * scale, flesh);
        Part(parent, PrimitiveType.Capsule, "Upper", new Vector3(-0.1f * scale, 2.3f * scale, 0.2f * scale), new Vector3(0.55f, 0.6f, 0.55f) * scale, tip);
        Part(parent, PrimitiveType.Sphere, "Mouth", new Vector3(-0.1f * scale, 2.85f * scale, 0.35f * scale), Vector3.one * 0.4f * scale, Mat(new Color(0.15f, 0.05f, 0.1f)));
        Part(parent, PrimitiveType.Cube, "ArmL", new Vector3(-0.7f * scale, 1.2f * scale, 0.3f * scale), new Vector3(0.25f, 1.0f, 0.25f) * scale, tip);
        Part(parent, PrimitiveType.Cube, "ArmR", new Vector3(0.7f * scale, 1.0f * scale, 0.2f * scale), new Vector3(0.25f, 1.0f, 0.25f) * scale, tip);
    }
}
