using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// One editor process, two NetworkManagers: the scene Host and a second Client on localhost.
/// NGO only promotes the first manager to the singleton, which matches how this project looks
/// up NetPlay. The client still owns its player object and receives replicated HP.
/// The client manager is built the way NGO's in-process tests do: inactive until NetworkConfig
/// exists. Play Mode does not call Reset(), so AddComponent leaves that field null.
/// </summary>
public class NetworkCombatPlayModeTests
{
    NetworkManager _client;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _client = null;
        return PlayModeWorld.Begin(PlayModeWorld.NetAccount);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _client = null;
        return PlayModeWorld.End();
    }

    [UnityTest]
    [Timeout(90000)]
    public IEnumerator HostAndClient_CombatModsReplicateAndMatchSoloMath()
    {
        var hero = PlayModeWorld.RequireSoloHero().GetComponent<CharacterStats>();
        var bootstrap = NetBootstrap.Instance;
        if (NetHud.Instance != null)
            NetHud.Instance.showPanel = false;
        var host = NetworkManager.Singleton;
        Assert.IsNotNull(host, "Scene NetworkManager was not registered.");
        Assert.IsFalse(host.IsListening, "Test must start from an offline scene.");

        PlayModeWorld.RelaxTransport(host.GetComponent<UnityTransport>());
        host.NetworkConfig.EnableSceneManagement = false;
        host.NetworkConfig.ForceSamePrefabs = false;

        ushort port = 0;
        bool hosting = false;
        for (ushort candidate = 40177; candidate < 40190 && !hosting; candidate++)
        {
            bootstrap.connectAddress = "127.0.0.1";
            bootstrap.connectPort = candidate;
            hosting = bootstrap.StartHost();
            port = candidate;
            if (!hosting)
            {
                bootstrap.Shutdown();
                yield return null;
            }
        }
        Assert.IsTrue(hosting, "StartHost failed on ports 40177-40189. " + bootstrap.LastMessage);

        yield return PlayModeWorld.WaitUntil(
            () => host.IsHost && HostCombat() != null,
            8f,
            "Host player did not spawn. " + bootstrap.LastMessage);

        var hostMotion = HostCombat().GetComponent<NetworkPlayerMovement>();
        if (hostMotion != null)
            hostMotion.enabled = false;

        yield return new WaitForSecondsRealtime(0.35f);

        Assert.IsTrue(host != null && host.IsHost && host.IsListening, "Host dropped local client 0 before the peer was created. " + bootstrap.LastMessage);
        Assert.IsNotNull(HostCombat(), "Host player despawned before the peer was created.");

        _client = CreateClient(host, port);
        bool clientStarted = _client.StartClient();
        Assert.IsTrue(clientStarted, "In-process client failed to start.");

        yield return PlayModeWorld.WaitUntil(
            () => _client.IsConnectedClient && host.ConnectedClientsIds.Count >= 2,
            12f,
            "Client did not join the host. " + Describe(host, _client, bootstrap));

        Assert.IsFalse(_client.IsServer, "The second manager must stay a pure client.");
        DisableClientOwnedCombat();

        hero.critChance = 1f;
        hero.critDamageBonus = 0.8f;
        hero.fireBonus = 6f;
        hero.iceBonus = 2f;
        hero.poisonBonus = 1f;
        hero.lifesteal = 0.2f;
        hero.defense = 0f;
        hero.damageReduction = 0f;
        hero.blockChance = 0f;
        hero.reflectPercent = 0f;

        var hostCombat = HostCombat();
        hostCombat.PushLocalStatsToServer();
        yield return PlayModeWorld.WaitUntil(
            () => Mathf.Approximately(hostCombat.CritChance.Value, 1f)
                  && Mathf.Approximately(hostCombat.FireBonus.Value, 6f)
                  && Mathf.Approximately(hostCombat.Lifesteal.Value, 0.2f)
                  && hostCombat.AttackPower.Value > 1f,
            4f,
            "Host did not accept the solo hero's crit, element, and lifesteal.");

        float expectedHit = CombatMath.OutgoingDamage(
            hostCombat.AttackPower.Value,
            1f,
            hostCombat.CritDamageBonus.Value,
            0f,
            hostCombat.FireBonus.Value,
            hostCombat.IceBonus.Value,
            hostCombat.PoisonBonus.Value);
        float soloHit = CombatMath.OutgoingDamage(
            hero.attackDamage, 1f, 0.8f, 0f, 6f, 2f, 1f);
        Assert.AreEqual(soloHit, expectedHit, 0.05f, "Network attack power diverged from the solo hero.");

        var session = bootstrap.GetComponent<NetCombatSession>();
        Assert.IsNotNull(session);
        Vector3 spawnAt = hostCombat.transform.position + hostCombat.transform.forward * 2f;
        var spawned = session.SpawnAdventureEnemy(
            "PlayModeNetEnemy",
            spawnAt,
            spawnAt,
            1,
            false,
            EnemyKind.Slime,
            1,
            0f,
            30f,
            false);
        Assert.IsNotNull(spawned, "Server did not spawn a NetworkEnemy.");
        var enemy = spawned.GetComponent<NetworkEnemy>();
        enemy.MaxHp.Value = 5000f;
        enemy.CurrentHp.Value = 5000f;
        Stick(enemy, hostCombat.transform, 2f);

        hostCombat.CurrentHp.Value = hostCombat.MaxHp.Value * 0.4f;
        float playerBefore = hostCombat.CurrentHp.Value;
        float enemyBefore = enemy.CurrentHp.Value;

        yield return PlayModeWorld.WaitUntil(
            () =>
            {
                DisableClientOwnedCombat();
                Stick(enemy, hostCombat.transform, 2f);
                return enemy.CurrentHp.Value < enemyBefore - 1f;
            },
            4f,
            "Host did not hit the networked enemy. range=" + hostCombat.AttackRangeNv.Value
            + " alive=" + enemy.IsAlive);

        float dealt = enemyBefore - enemy.CurrentHp.Value;
        float swings = Mathf.Round(dealt / expectedHit);
        Assert.GreaterOrEqual(swings, 1f);
        Assert.AreEqual(expectedHit * swings, dealt, 0.3f, "Networked crit/element damage should match solo CombatMath.");

        float heal = CombatMath.LifestealHeal(dealt, 0.2f);
        Assert.AreEqual(
            Mathf.Min(hostCombat.MaxHp.Value, playerBefore + heal),
            hostCombat.CurrentHp.Value,
            0.3f,
            "Lifesteal should heal the attacking network player.");

        yield return PlayModeWorld.WaitUntil(
            () =>
            {
                var remote = ClientEnemy();
                return remote != null && Mathf.Abs(remote.CurrentHp.Value - enemy.CurrentHp.Value) < 0.2f;
            },
            6f,
            "Client never saw the enemy HP. " + Describe(host, _client, bootstrap));

        FreezeAttacks(hostCombat);
        hero.defense = 30f;
        hero.damageReduction = 0.1f;
        hero.blockChance = 0f;
        hero.reflectPercent = 0.5f;
        hero.maxHealth = 40f;
        hero.currentHealth = 40f;
        hostCombat.PushLocalStatsToServer();
        yield return null;
        yield return null;

        float raw = ProgressionFormulas.MobAttack(1, false);
        float expectedMitigated = CombatMath.MitigatePlayerIncoming(raw, 30f, 0.1f, blocked: false);
        float expectedReflect = CombatMath.ReflectDamage(expectedMitigated, 0.5f, defenderIsPlayer: true);
        Assert.Greater(expectedReflect, 0.5f);

        hostCombat.CurrentHp.Value = hostCombat.MaxHp.Value;
        float hpBeforeSwing = hostCombat.CurrentHp.Value;
        float enemyBeforeSwing = enemy.CurrentHp.Value;
        yield return PlayModeWorld.WaitUntil(
            () =>
            {
                Stick(enemy, hostCombat.transform, 1.2f);
                return hostCombat.CurrentHp.Value < hpBeforeSwing - 0.2f;
            },
            4f,
            "NetworkEnemy did not swing at the host player.");

        float lost = hpBeforeSwing - hostCombat.CurrentHp.Value;
        Assert.AreEqual(expectedMitigated, lost, 0.2f, "Network defense and damage reduction diverged from solo mitigation.");
        Assert.AreEqual(
            expectedReflect,
            enemyBeforeSwing - enemy.CurrentHp.Value,
            0.2f,
            "Reflect from the network player should damage the enemy.");

        hostCombat.BlockChance.Value = 1f;
        float blocked = hostCombat.MitigateIncomingServer(100f);
        float soloBlocked = CombatMath.MitigatePlayerIncoming(100f, hostCombat.Defense.Value, hostCombat.DamageReduction.Value, blocked: true);
        Assert.AreEqual(soloBlocked, blocked, 0.05f, "A sure block should use the solo mitigation order.");

        float beforeChip = hostCombat.CurrentHp.Value;
        hostCombat.ApplyDamageServer(0.25f);
        Assert.AreEqual(
            CombatMath.SubtractHp(beforeChip, 0.25f),
            hostCombat.CurrentHp.Value,
            0.0001f,
            "Network hits under 0.5 must still reduce HP.");
    }

    static NetworkManager CreateClient(NetworkManager host, ushort port)
    {
        // Stay inactive until NetworkConfig is assigned. Awake initializes prefabs,
        // and enabling early would also run while the host is already the singleton.
        var clientObject = new GameObject("PlayModeClient");
        clientObject.SetActive(false);
        var client = clientObject.AddComponent<NetworkManager>();
        var transport = clientObject.AddComponent<UnityTransport>();
        PlayModeWorld.RelaxTransport(transport);
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");

        client.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ForceSamePrefabs = false,
            ConnectionApproval = false,
            TickRate = host.NetworkConfig.TickRate,
            ProtocolVersion = host.NetworkConfig.ProtocolVersion,
            PlayerPrefab = host.NetworkConfig.PlayerPrefab,
            NetworkTopology = host.NetworkConfig.NetworkTopology,
            AutoSpawnPlayerPrefabClientSide = host.NetworkConfig.AutoSpawnPlayerPrefabClientSide,
            SpawnTimeout = host.NetworkConfig.SpawnTimeout
        };
        client.RunInBackground = true;

        client.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
        var lists = host.NetworkConfig.Prefabs.NetworkPrefabsLists;
        for (int i = 0; i < lists.Count; i++)
        {
            if (lists[i] != null)
                client.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lists[i]);
        }

        var registered = host.NetworkConfig.Prefabs.Prefabs;
        for (int i = 0; i < registered.Count; i++)
        {
            var entry = registered[i];
            if (entry == null || entry.Prefab == null)
                continue;
            if (Listed(client, entry.Prefab))
                continue;
            client.AddNetworkPrefab(entry.Prefab);
        }

        clientObject.SetActive(true);
        Assert.AreNotSame(host, client);
        Assert.AreSame(host, NetworkManager.Singleton, "Creating the client replaced the host singleton.");
        return client;
    }

    static bool Listed(NetworkManager manager, GameObject prefab)
    {
        var lists = manager.NetworkConfig.Prefabs.NetworkPrefabsLists;
        for (int i = 0; i < lists.Count; i++)
        {
            if (lists[i] != null && lists[i].Contains(prefab))
                return true;
        }
        return false;
    }

    static void Stick(NetworkEnemy enemy, Transform player, float distance)
    {
        if (enemy == null || player == null)
            return;
        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        enemy.transform.position = player.position + forward.normalized * distance;
    }

    static NetworkPlayerCombat HostCombat()
    {
        var combats = Object.FindObjectsByType<NetworkPlayerCombat>(FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            var combat = combats[i];
            if (combat == null || !combat.IsSpawned || !combat.IsOwner)
                continue;
            if (combat.NetworkManager != null && combat.NetworkManager.IsServer)
                return combat;
        }
        return null;
    }

    static NetworkEnemy ClientEnemy()
    {
        var enemies = Object.FindObjectsByType<NetworkEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.IsSpawned || enemy.NetworkManager == null)
                continue;
            if (enemy.NetworkManager.IsClient && !enemy.NetworkManager.IsServer)
                return enemy;
        }
        return null;
    }

    static void DisableClientOwnedCombat()
    {
        var combats = Object.FindObjectsByType<NetworkPlayerCombat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < combats.Length; i++)
        {
            var combat = combats[i];
            if (combat == null || combat.NetworkManager == null)
                continue;
            if (combat.NetworkManager.IsClient && !combat.NetworkManager.IsServer && combat.IsOwner)
                combat.enabled = false;
        }
    }

    static void FreezeAttacks(NetworkPlayerCombat combat)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var server = combat.GetType().GetField("_serverNextAttackTime", flags);
        var client = combat.GetType().GetField("_clientNextAttackTime", flags);
        Assert.IsNotNull(server, "NetworkPlayerCombat attack cooldown field was renamed.");
        Assert.IsNotNull(client, "NetworkPlayerCombat attack cooldown field was renamed.");
        server.SetValue(combat, float.PositiveInfinity);
        client.SetValue(combat, float.PositiveInfinity);
    }

    static string Describe(NetworkManager host, NetworkManager client, NetBootstrap bootstrap)
    {
        int peers = host != null ? host.ConnectedClientsIds.Count : -1;
        bool clientUp = client != null && client.IsConnectedClient;
        string message = bootstrap != null ? bootstrap.LastMessage : "";
        return "hostListening=" + (host != null && host.IsListening)
            + " peers=" + peers
            + " clientConnected=" + clientUp
            + " status=" + message;
    }
}
