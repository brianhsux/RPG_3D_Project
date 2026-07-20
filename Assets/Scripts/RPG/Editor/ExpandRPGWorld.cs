using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExpandRPGWorld
{
    [MenuItem("RPG/Expand World Map")]
    public static void Expand()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material LoadOrCreate(string name, Color color)
        {
            string path = "Assets/Materials/RPG/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(lit);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        Material Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RPG/" + name + ".mat");

        GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        var groundMat = Load("Ground");
        var stoneMat = Load("Stone");
        var woodMat = Load("Wood");
        var leafMat = LoadOrCreate("Leaf", new Color(0.15f, 0.55f, 0.18f));
        var forestMat = LoadOrCreate("ForestFloor", new Color(0.18f, 0.38f, 0.16f));
        var ruinMat = LoadOrCreate("RuinFloor", new Color(0.42f, 0.4f, 0.36f));
        var villageMat = LoadOrCreate("VillageFloor", new Color(0.55f, 0.48f, 0.32f));
        var pathMat = LoadOrCreate("Path", new Color(0.62f, 0.55f, 0.38f));
        AssetDatabase.SaveAssets();

        var arena = GameObject.Find("Arena");
        if (arena == null) arena = new GameObject("Arena");

        var oldGround = arena.transform.Find("Ground");
        if (oldGround != null)
        {
            oldGround.position = new Vector3(0f, -0.25f, 0f);
            oldGround.localScale = new Vector3(80f, 0.5f, 80f);
        }
        else
        {
            Prim(PrimitiveType.Cube, "Ground", new Vector3(0, -0.25f, 0), new Vector3(80, 0.5f, 80), groundMat, arena.transform);
        }

        foreach (var wallName in new[] { "Wall_N", "Wall_S", "Wall_E", "Wall_W" })
        {
            var wt = arena.transform.Find(wallName);
            if (wt != null) Object.DestroyImmediate(wt.gameObject);
        }

        float wallH = 3f;
        float half = 40f;
        Prim(PrimitiveType.Cube, "Wall_N", new Vector3(0, wallH / 2f, half), new Vector3(80, wallH, 0.8f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0, wallH / 2f, -half), new Vector3(80, wallH, 0.8f), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(half, wallH / 2f, 0), new Vector3(0.8f, wallH, 80), stoneMat, arena.transform);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-half, wallH / 2f, 0), new Vector3(0.8f, wallH, 80), stoneMat, arena.transform);

        var oldZones = GameObject.Find("Zones");
        if (oldZones != null) Object.DestroyImmediate(oldZones);
        var zones = new GameObject("Zones");
        zones.transform.SetParent(arena.transform, false);

        Prim(PrimitiveType.Cube, "Zone_Forest", new Vector3(26, 0.03f, 0), new Vector3(22, 0.06f, 22), forestMat, zones.transform);
        Prim(PrimitiveType.Cube, "Zone_Ruins", new Vector3(0, 0.03f, 26), new Vector3(22, 0.06f, 22), ruinMat, zones.transform);
        Prim(PrimitiveType.Cube, "Zone_Village", new Vector3(-26, 0.03f, 0), new Vector3(22, 0.06f, 22), villageMat, zones.transform);
        Prim(PrimitiveType.Cube, "Path_East", new Vector3(13, 0.04f, 0), new Vector3(10, 0.05f, 4), pathMat, zones.transform);
        Prim(PrimitiveType.Cube, "Path_North", new Vector3(0, 0.04f, 13), new Vector3(4, 0.05f, 10), pathMat, zones.transform);
        Prim(PrimitiveType.Cube, "Path_West", new Vector3(-13, 0.04f, 0), new Vector3(10, 0.05f, 4), pathMat, zones.transform);

        var forest = new GameObject("ForestProps");
        forest.transform.SetParent(zones.transform, false);
        var treePos = new[]
        {
            new Vector3(22, 0, 6), new Vector3(28, 0, -4), new Vector3(32, 0, 8), new Vector3(20, 0, -8),
            new Vector3(30, 0, 2), new Vector3(24, 0, 10), new Vector3(34, 0, -6), new Vector3(26, 0, -10)
        };
        for (int t = 0; t < treePos.Length; t++)
        {
            var p = treePos[t];
            Prim(PrimitiveType.Cylinder, "Trunk_" + t, p + new Vector3(0, 1.2f, 0), new Vector3(0.5f, 1.2f, 0.5f), woodMat, forest.transform);
            var canopy = Prim(PrimitiveType.Sphere, "Canopy_" + t, p + new Vector3(0, 2.8f, 0), Vector3.one * 2.2f, leafMat, forest.transform);
            Object.DestroyImmediate(canopy.GetComponent<Collider>());
        }

        var ruins = new GameObject("RuinProps");
        ruins.transform.SetParent(zones.transform, false);
        Prim(PrimitiveType.Cube, "RuinWall_1", new Vector3(-4, 1.2f, 28), new Vector3(6, 2.4f, 0.6f), stoneMat, ruins.transform);
        Prim(PrimitiveType.Cube, "RuinWall_2", new Vector3(5, 0.9f, 32), new Vector3(0.6f, 1.8f, 5), stoneMat, ruins.transform);
        Prim(PrimitiveType.Cube, "RuinPillar", new Vector3(0, 1.6f, 24), new Vector3(1.1f, 3.2f, 1.1f), stoneMat, ruins.transform);
        Prim(PrimitiveType.Cube, "Rubble_1", new Vector3(3, 0.35f, 26), new Vector3(2.2f, 0.7f, 1.4f), stoneMat, ruins.transform);
        Prim(PrimitiveType.Cube, "Rubble_2", new Vector3(-6, 0.3f, 30), new Vector3(1.8f, 0.6f, 2f), stoneMat, ruins.transform);

        var village = new GameObject("VillageProps");
        village.transform.SetParent(zones.transform, false);
        void MakeHouse(Vector3 pos, string name)
        {
            Prim(PrimitiveType.Cube, name + "_Base", pos + new Vector3(0, 1.1f, 0), new Vector3(3.5f, 2.2f, 3f), woodMat, village.transform);
            Prim(PrimitiveType.Cube, name + "_Roof", pos + new Vector3(0, 2.6f, 0), new Vector3(4f, 0.6f, 3.5f), stoneMat, village.transform);
        }
        MakeHouse(new Vector3(-28, 0, 4), "HouseA");
        MakeHouse(new Vector3(-24, 0, -5), "HouseB");
        MakeHouse(new Vector3(-32, 0, -2), "HouseC");
        Prim(PrimitiveType.Cube, "MarketStall", new Vector3(-22, 0.7f, 2), new Vector3(2.5f, 1.4f, 1.5f), woodMat, village.transform);

        var oldExtras = GameObject.Find("ExtraEnemies");
        if (oldExtras != null) Object.DestroyImmediate(oldExtras);
        var extras = new GameObject("ExtraEnemies");

        GameObject CloneEnemy(GameObject src, Vector3 pos, string newName)
        {
            if (src == null) return null;
            var clone = Object.Instantiate(src);
            clone.name = newName;
            clone.transform.position = pos;
            clone.transform.SetParent(extras.transform, true);
            var ai = clone.GetComponent<EnemyAI>();
            if (ai != null) ai.SetSpawnPosition(pos);
            var st = clone.GetComponent<CharacterStats>();
            if (st != null)
            {
                st.isDead = false;
                st.currentHealth = st.maxHealth;
            }
            var cc = clone.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            return clone;
        }

        var slime = GameObject.Find("Enemy_Slime");
        var goblin = GameObject.Find("Enemy_Goblin");
        var skel = GameObject.Find("Enemy_Skeleton");

        if (slime != null)
        {
            var sai = slime.GetComponent<EnemyAI>();
            if (sai != null)
            {
                sai.enemyType = "Slime";
                sai.SetSpawnPosition(slime.transform.position);
            }
            CloneEnemy(slime, new Vector3(24, 0, 4), "Enemy_Slime_2");
            CloneEnemy(slime, new Vector3(30, 0, -3), "Enemy_Slime_3");
            CloneEnemy(slime, new Vector3(22, 0, -6), "Enemy_Slime_4");
        }

        if (goblin != null)
        {
            var gai = goblin.GetComponent<EnemyAI>();
            if (gai != null)
            {
                gai.enemyType = "Goblin";
                gai.SetSpawnPosition(goblin.transform.position);
            }
            CloneEnemy(goblin, new Vector3(-24, 0, 3), "Enemy_Goblin_2");
            CloneEnemy(goblin, new Vector3(-30, 0, -4), "Enemy_Goblin_3");
            CloneEnemy(goblin, new Vector3(-26, 0, -8), "Enemy_Goblin_4");
        }

        if (skel != null)
        {
            var kai = skel.GetComponent<EnemyAI>();
            if (kai != null)
            {
                kai.enemyType = "Skeleton";
                kai.SetSpawnPosition(skel.transform.position);
            }
            CloneEnemy(skel, new Vector3(-3, 0, 28), "Enemy_Skeleton_2");
            CloneEnemy(skel, new Vector3(4, 0, 30), "Enemy_Skeleton_3");
            CloneEnemy(skel, new Vector3(0, 0, 34), "Enemy_Skeleton_4");
        }

        var camFollow = Object.FindFirstObjectByType<CameraFollow>();
        if (camFollow != null) camFollow.offset = new Vector3(0f, 14f, -16f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("RPG world map expanded.");
    }
}
