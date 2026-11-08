using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads RPGBattle in the editor play mode loop and keeps probe accounts out of the
/// real save folder. Waits use realtime so batchmode (-nographics) is not stuck on
/// scaled time or end-of-frame rendering.
/// </summary>
public static class PlayModeWorld
{
    public const string ScenePath = "Assets/Scenes/RPGBattle.unity";
    public const string SoloAccount = "PlayMode Solo";
    public const string NetAccount = "PlayMode Net";
    public const string SaveAccount = "PlayMode Save";

    static readonly string[] ProbeNames = { SoloAccount, NetAccount, SaveAccount };

    public static IEnumerator Begin(string accountName)
    {
        DeleteAccountNamed(accountName);
        yield return LoadBattleScene();
        CreateAccount(accountName);
        Time.timeScale = 1f;
        SilenceLocalEnemies();
        var player = RequireSoloHero();
        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;
    }

    public static IEnumerator End()
    {
        SuppressSaves();
        yield return DestroySessionObjects();
        foreach (var name in ProbeNames)
            DeleteAccountNamed(name);
    }

    public static IEnumerator LoadBattleScene()
    {
        SuppressSaves();
        yield return DestroySessionObjects();

        var load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        Assert.IsNotNull(load, "Could not start loading " + ScenePath);
        while (!load.isDone)
            yield return null;

        yield return null;
        Time.timeScale = 1f;

        Assert.AreEqual("RPGBattle", SceneManager.GetActiveScene().name);
        Assert.IsNotNull(GameObject.Find("Player"), "RPGBattle is missing its Player.");
        Assert.IsNotNull(AdventureManager.Instance, "AdventureManager did not start with the scene.");
        Assert.IsNotNull(AccountManager.Instance, "AccountManager did not start with the scene.");
        Assert.IsNotNull(NetBootstrap.Instance, "NetBootstrap did not start with the scene.");
    }

    public static GameObject RequireSoloHero()
    {
        var player = PlayerLookup.FindSoloHero();
        Assert.IsNotNull(player, "Solo hero was not tagged and marked isPlayer.");
        Assert.IsNotNull(player.GetComponent<CharacterStats>());
        Assert.IsNotNull(player.GetComponent<PlayerInventory>());
        Assert.IsNotNull(PlayerProgression.Instance);
        return player;
    }

    public static void CreateAccount(string accountName)
    {
        var accounts = AccountManager.Instance;
        Assert.IsNotNull(accounts);
        Invoke(accounts, "CreateNewAccount", accountName);
        if (!accounts.IsReady)
            Assert.Fail("Could not create account '" + accountName + "': " + Field<string>(accounts, "_status"));
    }

    public static void PlayAccount(string accountName)
    {
        var accounts = AccountManager.Instance;
        Assert.IsNotNull(accounts);
        var list = Field<List<AccountIndexEntry>>(accounts, "_accounts");
        Assert.IsNotNull(list, "Account index was not loaded.");

        int index = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].accountName == accountName)
            {
                index = i;
                break;
            }
        }
        Assert.GreaterOrEqual(index, 0, "Saved probe account was not in the index: " + accountName);
        SetField(accounts, "_selectedIndex", index);
        Invoke(accounts, "PlaySelectedAccount");
        if (!accounts.IsReady)
            Assert.Fail("Could not reload account '" + accountName + "': " + Field<string>(accounts, "_status"));
    }

    public static AccountSaveData ReadActiveSave()
    {
        var accounts = AccountManager.Instance;
        Assert.IsNotNull(accounts);
        Assert.IsFalse(string.IsNullOrEmpty(accounts.ActiveAccountId));
        string path = Path.Combine(SaveRoot(), accounts.ActiveAccountId + ".json");
        Assert.IsTrue(File.Exists(path), "Save file missing: " + path);
        var data = JsonUtility.FromJson<AccountSaveData>(File.ReadAllText(path));
        Assert.IsNotNull(data);
        return data;
    }

    public static void SilenceLocalEnemies()
    {
        var ais = UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < ais.Length; i++)
        {
            if (ais[i] == null)
                continue;
            ais[i].enabled = false;
            var stats = ais[i].GetComponent<CharacterStats>();
            if (stats != null)
                stats.isDead = true;
        }
    }

    public static CharacterStats SpawnSoloEnemy(int contentLevel, bool boss, Vector3 position)
    {
        var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObject.name = boss ? "PlayModeBoss" : "PlayModeEnemy";
        enemyObject.transform.position = position;
        var stats = enemyObject.AddComponent<CharacterStats>();
        stats.ConfigureAsEnemy(contentLevel, boss, EnemyKind.Slime);
        return stats;
    }

    public static void SuppressSaves()
    {
        var setter = typeof(AccountManager).GetProperty("IsReady")?.GetSetMethod(true);
        Assert.IsNotNull(setter, "AccountManager.IsReady has no setter.");
        var managers = UnityEngine.Object.FindObjectsByType<AccountManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] == null)
                continue;
            setter.Invoke(managers[i], new object[] { false });
        }
    }

    public static IEnumerator DestroySessionObjects()
    {
        var managers = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager == null)
                continue;
            try
            {
                if (manager.IsListening || manager.IsClient)
                    manager.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PlayMode shutdown: " + exception.Message);
            }
        }

        yield return null;

        var roots = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null || root.transform.parent != null)
                continue;
            if (root.scene.name != "DontDestroyOnLoad")
                continue;
            UnityEngine.Object.Destroy(root);
        }

        yield return null;
    }

    public static IEnumerator WaitUntil(Func<bool> ready, float seconds, string message)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            if (ready())
                yield break;
            yield return null;
        }
        Assert.Fail(message + " (waited " + seconds.ToString("0.0") + "s)");
    }

    public static void RelaxTransport(UnityTransport transport)
    {
        if (transport == null)
            return;
        transport.HeartbeatTimeoutMS = 15000;
        transport.ConnectTimeoutMS = 2000;
        transport.MaxConnectAttempts = 30;
        transport.DisconnectTimeoutMS = 30000;
    }

    public static void DeleteAccountNamed(string accountName)
    {
        string root = SaveRoot();
        if (!Directory.Exists(root))
            return;

        string indexPath = Path.Combine(root, "index.json");
        if (!File.Exists(indexPath))
            return;

        AccountIndexFile file;
        try
        {
            file = JsonUtility.FromJson<AccountIndexFile>(File.ReadAllText(indexPath));
        }
        catch
        {
            return;
        }
        if (file?.accounts == null)
            return;

        var keep = new List<AccountIndexEntry>();
        for (int i = 0; i < file.accounts.Length; i++)
        {
            var entry = file.accounts[i];
            if (entry == null)
                continue;
            if (entry.accountName == accountName)
            {
                string save = Path.Combine(root, entry.accountId + ".json");
                if (File.Exists(save))
                    File.Delete(save);
                continue;
            }
            keep.Add(entry);
        }

        file.accounts = keep.ToArray();
        File.WriteAllText(indexPath, JsonUtility.ToJson(file, true));
    }

    static string SaveRoot() => Path.Combine(Application.persistentDataPath, "rpg_accounts");

    static void Invoke(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing " + target.GetType().Name + "." + methodName);
        method.Invoke(target, args);
    }

    static T Field<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing " + target.GetType().Name + "." + fieldName);
        return (T)field.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing " + target.GetType().Name + "." + fieldName);
        field.SetValue(target, value);
    }
}
