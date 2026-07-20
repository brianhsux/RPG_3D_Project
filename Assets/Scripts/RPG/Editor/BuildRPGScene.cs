using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class BuildRPGScene
{
    [MenuItem("RPG/Build Battle Scene")]
    public static void Build()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material MakeMat(string name, Color color)
        {
            string path = "Assets/Materials/RPG/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var m = new Material(lit);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        var groundMat = MakeMat("Ground", new Color(0.32f, 0.52f, 0.25f));
        var stoneMat = MakeMat("Stone", new Color(0.45f, 0.42f, 0.38f));
        var woodMat = MakeMat("Wood", new Color(0.4f, 0.25f, 0.12f));
        var heroBody = MakeMat("HeroBody", new Color(0.15f, 0.35f, 0.75f));
        var heroSkin = MakeMat("HeroSkin", new Color(0.92f, 0.75f, 0.6f));
        var metalMat = MakeMat("Metal", new Color(0.75f, 0.78f, 0.85f));
        var slimeMat = MakeMat("Slime", new Color(0.2f, 0.8f, 0.3f));
        var goblinMat = MakeMat("Goblin", new Color(0.35f, 0.55f, 0.2f));
        var boneMat = MakeMat("Bone", new Color(0.9f, 0.88f, 0.8f));
        var eyeMat = MakeMat("Eye", new Color(0.05f, 0.05f, 0.05f));
        var torchMat = MakeMat("Torch", new Color(1f, 0.4f, 0.1f));
        var dirtMat = MakeMat("Dirt", new Color(0.4f, 0.32f, 0.2f));
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RPGBattle.unity");
        foreach (var root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 55f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 12f, -14f);
        camGO.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        var arena = new GameObject("Arena");
        Prim(PrimitiveType.Cube, "Ground", new Vector3(0, -0.25f, 0), new Vector3(24, 0.5f, 24), groundMat, arena.transform);
        float wallH = 2.5f;
        Prim(PrimitiveType.Cube, "Wall_N", new Vector3(0, wallH / 2f, 12), new Vector3(24, wallH, 0.6f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0, wallH / 2f, -12), new Vector3(24, wallH, 0.6f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(12, wallH / 2f, 0), new Vector3(0.6f, wallH, 24), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-12, wallH / 2f, 0), new Vector3(0.6f, wallH, 24), stoneMat, arena.transform);
        Prim(PrimitiveType.Cylinder, "Pillar_0", new Vector3(11, 1.75f, 11), new Vector3(1.2f, 1.75f, 1.2f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cylinder, "Pillar_1", new Vector3(-11, 1.75f, 11), new Vector3(1.2f, 1.75f, 1.2f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cylinder, "Pillar_2", new Vector3(11, 1.75f, -11), new Vector3(1.2f, 1.75f, 1.2f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cylinder, "Pillar_3", new Vector3(-11, 1.75f, -11), new Vector3(1.2f, 1.75f, 1.2f), stoneMat, arena.transform);

        var props = new GameObject("Props");
        props.transform.SetParent(arena.transform);
        Prim(PrimitiveType.Cube, "Crate_1", new Vector3(-7, 0.6f, 6), new Vector3(1.2f, 1.2f, 1.2f), woodMat, props.transform);
        Prim(PrimitiveType.Cube, "Crate_2", new Vector3(-6.2f, 0.45f, 5.2f), new Vector3(0.9f, 0.9f, 0.9f), woodMat, props.transform);
        Prim(PrimitiveType.Cylinder, "Barrel_1", new Vector3(7, 0.7f, -5), new Vector3(1.1f, 0.7f, 1.1f), woodMat, props.transform);
        Prim(PrimitiveType.Cylinder, "Barrel_2", new Vector3(8, 0.55f, -4.5f), new Vector3(0.9f, 0.55f, 0.9f), woodMat, props.transform);
        Prim(PrimitiveType.Cube, "Rock_1", new Vector3(5, 0.4f, 7), new Vector3(1.8f, 0.8f, 1.4f), stoneMat, props.transform);
        Prim(PrimitiveType.Cube, "Rock_2", new Vector3(-8, 0.35f, -7), new Vector3(1.5f, 0.7f, 1.8f), stoneMat, props.transform);
        Prim(PrimitiveType.Cylinder, "BattleRing", new Vector3(0, 0.02f, 0), new Vector3(8, 0.02f, 8), dirtMat, arena.transform);

        void MakeTorch(Vector3 pos, int index)
        {
            Prim(PrimitiveType.Cylinder, "TorchPost_" + index, pos, new Vector3(0.25f, 1.2f, 0.25f), woodMat, props.transform);
            var flame = Prim(PrimitiveType.Sphere, "Flame_" + index, pos + Vector3.up * 1.4f, Vector3.one * 0.35f, torchMat, props.transform);
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            var plGO = new GameObject("TorchLight_" + index);
            plGO.transform.SetParent(flame.transform, false);
            var pl = plGO.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.55f, 0.2f);
            pl.intensity = 2.5f;
            pl.range = 8f;
        }

        MakeTorch(new Vector3(-10, 1.2f, 10), 0);
        MakeTorch(new Vector3(10, 1.2f, 10), 1);
        MakeTorch(new Vector3(-10, 1.2f, -10), 2);
        MakeTorch(new Vector3(10, 1.2f, -10), 3);

        // Hero
        var hero = new GameObject("Player");
        hero.tag = "Player";
        hero.transform.position = new Vector3(0, 0, -4);
        var body = Prim(PrimitiveType.Capsule, "Body", new Vector3(0, 1, -4), new Vector3(0.9f, 1, 0.9f), heroBody, hero.transform);
        Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
        var head = Prim(PrimitiveType.Sphere, "Head", new Vector3(0, 2.15f, -4), Vector3.one * 0.55f, heroSkin, hero.transform);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        var leftArm = Prim(PrimitiveType.Capsule, "LeftArm", new Vector3(-0.7f, 1.2f, -4), new Vector3(0.28f, 0.45f, 0.28f), heroSkin, hero.transform);
        Object.DestroyImmediate(leftArm.GetComponent<Collider>());
        var rightArm = Prim(PrimitiveType.Capsule, "RightArm", new Vector3(0.7f, 1.2f, -4), new Vector3(0.28f, 0.45f, 0.28f), heroSkin, hero.transform);
        Object.DestroyImmediate(rightArm.GetComponent<Collider>());
        var sword = Prim(PrimitiveType.Cube, "Sword", new Vector3(0.95f, 1.3f, -3.7f), new Vector3(0.12f, 0.12f, 1.4f), metalMat, hero.transform);
        sword.transform.localRotation = Quaternion.Euler(15, 0, 0);
        Object.DestroyImmediate(sword.GetComponent<Collider>());
        var hilt = Prim(PrimitiveType.Cube, "Hilt", new Vector3(0.95f, 1.25f, -4.25f), new Vector3(0.35f, 0.08f, 0.08f), woodMat, hero.transform);
        Object.DestroyImmediate(hilt.GetComponent<Collider>());
        var cc = hero.AddComponent<CharacterController>();
        cc.height = 2.2f;
        cc.radius = 0.45f;
        cc.center = new Vector3(0, 1.1f, 0);

        // Slime
        var slime = new GameObject("Enemy_Slime");
        slime.transform.position = new Vector3(-4, 0, 4);
        var slimeBody = Prim(PrimitiveType.Sphere, "Body", new Vector3(-4, 0.7f, 4), new Vector3(1.6f, 1.2f, 1.6f), slimeMat, slime.transform);
        Object.DestroyImmediate(slimeBody.GetComponent<Collider>());
        var se1 = Prim(PrimitiveType.Sphere, "EyeL", new Vector3(-4.3f, 1.0f, 4.55f), Vector3.one * 0.22f, eyeMat, slime.transform);
        Object.DestroyImmediate(se1.GetComponent<Collider>());
        var se2 = Prim(PrimitiveType.Sphere, "EyeR", new Vector3(-3.7f, 1.0f, 4.55f), Vector3.one * 0.22f, eyeMat, slime.transform);
        Object.DestroyImmediate(se2.GetComponent<Collider>());
        var scc = slime.AddComponent<CharacterController>();
        scc.height = 1.4f;
        scc.radius = 0.7f;
        scc.center = new Vector3(0, 0.7f, 0);

        // Goblin
        var goblin = new GameObject("Enemy_Goblin");
        goblin.transform.position = new Vector3(4, 0, 4);
        var gBody = Prim(PrimitiveType.Capsule, "Body", new Vector3(4, 0.85f, 4), new Vector3(0.8f, 0.85f, 0.8f), goblinMat, goblin.transform);
        Object.DestroyImmediate(gBody.GetComponent<CapsuleCollider>());
        var gHead = Prim(PrimitiveType.Sphere, "Head", new Vector3(4, 1.85f, 4), Vector3.one * 0.5f, goblinMat, goblin.transform);
        Object.DestroyImmediate(gHead.GetComponent<Collider>());
        var earL = Prim(PrimitiveType.Cube, "EarL", new Vector3(3.65f, 2.05f, 4), new Vector3(0.15f, 0.4f, 0.08f), goblinMat, goblin.transform);
        earL.transform.localRotation = Quaternion.Euler(0, 0, 25);
        Object.DestroyImmediate(earL.GetComponent<Collider>());
        var earR = Prim(PrimitiveType.Cube, "EarR", new Vector3(4.35f, 2.05f, 4), new Vector3(0.15f, 0.4f, 0.08f), goblinMat, goblin.transform);
        earR.transform.localRotation = Quaternion.Euler(0, 0, -25);
        Object.DestroyImmediate(earR.GetComponent<Collider>());
        var club = Prim(PrimitiveType.Cylinder, "Club", new Vector3(4.75f, 1.0f, 4.2f), new Vector3(0.25f, 0.7f, 0.25f), woodMat, goblin.transform);
        club.transform.localRotation = Quaternion.Euler(20, 0, 15);
        Object.DestroyImmediate(club.GetComponent<Collider>());
        var gcc = goblin.AddComponent<CharacterController>();
        gcc.height = 2f;
        gcc.radius = 0.4f;
        gcc.center = new Vector3(0, 1f, 0);

        // Skeleton
        var skel = new GameObject("Enemy_Skeleton");
        skel.transform.position = new Vector3(0, 0, 6);
        var sBody = Prim(PrimitiveType.Capsule, "Body", new Vector3(0, 1.2f, 6), new Vector3(0.7f, 1.1f, 0.55f), boneMat, skel.transform);
        Object.DestroyImmediate(sBody.GetComponent<CapsuleCollider>());
        var sHead = Prim(PrimitiveType.Sphere, "Head", new Vector3(0, 2.45f, 6), Vector3.one * 0.45f, boneMat, skel.transform);
        Object.DestroyImmediate(sHead.GetComponent<Collider>());
        var sEyeL = Prim(PrimitiveType.Sphere, "EyeL", new Vector3(-0.12f, 2.5f, 6.18f), Vector3.one * 0.1f, eyeMat, skel.transform);
        Object.DestroyImmediate(sEyeL.GetComponent<Collider>());
        var sEyeR = Prim(PrimitiveType.Sphere, "EyeR", new Vector3(0.12f, 2.5f, 6.18f), Vector3.one * 0.1f, eyeMat, skel.transform);
        Object.DestroyImmediate(sEyeR.GetComponent<Collider>());
        var sArmL = Prim(PrimitiveType.Capsule, "ArmL", new Vector3(-0.65f, 1.4f, 6), new Vector3(0.2f, 0.55f, 0.2f), boneMat, skel.transform);
        Object.DestroyImmediate(sArmL.GetComponent<Collider>());
        var sArmR = Prim(PrimitiveType.Capsule, "ArmR", new Vector3(0.65f, 1.4f, 6), new Vector3(0.2f, 0.55f, 0.2f), boneMat, skel.transform);
        Object.DestroyImmediate(sArmR.GetComponent<Collider>());
        var boneSword = Prim(PrimitiveType.Cube, "BoneSword", new Vector3(0.9f, 1.5f, 6.4f), new Vector3(0.1f, 0.1f, 1.5f), boneMat, skel.transform);
        Object.DestroyImmediate(boneSword.GetComponent<Collider>());
        var skcc = skel.AddComponent<CharacterController>();
        skcc.height = 2.5f;
        skcc.radius = 0.35f;
        skcc.center = new Vector3(0, 1.25f, 0);

        // Attach gameplay scripts if they exist
        void TryAdd(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null && go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        TryAdd(hero, "PlayerController");
        TryAdd(hero, "CharacterStats");
        TryAdd(slime, "EnemyAI");
        TryAdd(slime, "CharacterStats");
        TryAdd(goblin, "EnemyAI");
        TryAdd(goblin, "CharacterStats");
        TryAdd(skel, "EnemyAI");
        TryAdd(skel, "CharacterStats");
        TryAdd(camGO, "CameraFollow");

        var gm = new GameObject("GameManager");
        TryAdd(gm, "GameManager");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("RPG Battle Scene built successfully.");
    }
}
