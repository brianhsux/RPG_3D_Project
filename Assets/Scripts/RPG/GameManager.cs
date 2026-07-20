using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles enemy respawn so players can grind XP and loot.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Seconds until a dead enemy respawns")]
    public float enemyRespawnDelay = 6f;

    readonly Dictionary<EnemyAI, Coroutine> _respawnJobs = new Dictionary<EnemyAI, Coroutine>();

    void Awake()
    {
        Instance = this;
        if (AccountManager.Instance == null)
        {
            var go = new GameObject("AccountManager");
            go.AddComponent<AccountManager>();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void NotifyEnemyDied(EnemyAI enemy)
    {
        if (enemy == null || enemy.disableAutoRespawn) return;
        if (_respawnJobs.ContainsKey(enemy)) return;
        _respawnJobs[enemy] = StartCoroutine(RespawnAfterDelay(enemy));
    }

    IEnumerator RespawnAfterDelay(EnemyAI enemy)
    {
        // Brief corpse time, then hide
        yield return new WaitForSeconds(1.0f);
        if (enemy != null)
            enemy.gameObject.SetActive(false);

        yield return new WaitForSeconds(Mathf.Max(0.1f, enemyRespawnDelay - 1.0f));

        if (enemy != null)
        {
            enemy.gameObject.SetActive(true);
            var stats = enemy.GetComponent<CharacterStats>();
            if (stats != null)
                stats.Respawn(enemy.SpawnPosition);
            var anim = enemy.GetComponent<ProceduralBodyAnim>();
            if (anim != null)
            {
                anim.SetDead(false);
                anim.Recache();
            }
        }

        _respawnJobs.Remove(enemy);
    }
}
