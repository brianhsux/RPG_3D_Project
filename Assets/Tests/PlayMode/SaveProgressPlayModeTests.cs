using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// A pure client applies the host snapshot through AdventureManager, then NetBootstrap.Shutdown
/// restores the personal record and the account is loaded again. Host and client cannot keep
/// two AdventureManagers in one process, so this is the client side of that join.
/// </summary>
public class SaveProgressPlayModeTests
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        return PlayModeWorld.Begin(PlayModeWorld.SaveAccount);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        return PlayModeWorld.End();
    }

    [UnityTest]
    [Timeout(60000)]
    public IEnumerator HigherClientProgress_SurvivesNewerHost_AfterReload()
    {
        var adventure = AdventureManager.Instance;
        Assert.IsNotNull(adventure);

        var personal = new AccountSaveData
        {
            currentWorld = 0,
            currentMap = 1,
            highestWorldUnlocked = 8,
            clearedBossKeys = new[] { "W3M4", "W5M1" },
            level = 1,
            xp = 0
        };
        adventure.ApplySave(personal);
        Assert.AreEqual(8, adventure.highestWorldUnlocked);
        Assert.IsTrue(adventure.IsBossCleared(3, 4));
        Assert.IsTrue(adventure.IsBossCleared(5, 1));
        Assert.IsFalse(adventure.IsBossCleared(1, 4));

        adventure.ApplyNetworkSnapshot(1, 1, highest: 2, clearedCsv: "W1M4");
        Assert.AreEqual(2, adventure.highestWorldUnlocked, "While joined, the visible unlock should follow the host.");
        Assert.IsTrue(adventure.IsBossCleared(1, 4));
        Assert.IsFalse(adventure.IsBossCleared(3, 4), "Host snapshot replaces the session key set.");
        Assert.AreEqual(1, adventure.currentWorld);

        NetBootstrap.Instance.Shutdown();

        Assert.AreEqual(8, adventure.highestWorldUnlocked, "Disconnect should restore the higher personal unlock.");
        Assert.IsTrue(adventure.IsBossCleared(1, 4), "A boss the host had cleared stays in the union.");
        Assert.IsTrue(adventure.IsBossCleared(3, 4));
        Assert.IsTrue(adventure.IsBossCleared(5, 1));
        Assert.IsFalse(adventure.IsBossCleared(2, 2));

        AccountManager.Instance.SaveActive(silent: true);
        var onDisk = PlayModeWorld.ReadActiveSave();
        Assert.AreEqual(8, onDisk.highestWorldUnlocked);
        Assert.IsNotNull(onDisk.clearedBossKeys);
        Assert.That(onDisk.clearedBossKeys, Does.Contain("W1M4"));
        Assert.That(onDisk.clearedBossKeys, Does.Contain("W3M4"));
        Assert.That(onDisk.clearedBossKeys, Does.Contain("W5M1"));

        PlayModeWorld.SuppressSaves();
        yield return PlayModeWorld.LoadBattleScene();
        PlayModeWorld.PlayAccount(PlayModeWorld.SaveAccount);

        var reloaded = AdventureManager.Instance;
        Assert.AreEqual(8, reloaded.highestWorldUnlocked);
        Assert.IsTrue(reloaded.IsBossCleared(1, 4));
        Assert.IsTrue(reloaded.IsBossCleared(3, 4));
        Assert.IsTrue(reloaded.IsBossCleared(5, 1));
        Assert.IsFalse(reloaded.IsBossCleared(2, 2));

        var reread = PlayModeWorld.ReadActiveSave();
        Assert.AreEqual(8, reread.highestWorldUnlocked);
        Assert.That(reread.clearedBossKeys, Does.Contain("W3M4"));
        Assert.That(reread.clearedBossKeys, Does.Contain("W5M1"));
        Assert.That(reread.clearedBossKeys, Does.Contain("W1M4"));
    }
}
