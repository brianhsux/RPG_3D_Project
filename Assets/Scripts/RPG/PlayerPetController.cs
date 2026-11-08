using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns pet collection, active pet, spawn/despawn follower, bond training, save/load.
/// Active pet deals damage and grants owner bond buffs.
/// </summary>
public class PlayerPetController : MonoBehaviour
{
    public static PlayerPetController Instance { get; private set; }

    public const int MaxPets = 16;
    public const float BaseMoveSpeed = 6.5f;

    [System.NonSerialized] public List<PetInstance> owned = new List<PetInstance>();
    [System.NonSerialized] public string activePetId;

    PetFollower _follower;
    CharacterStats _stats;
    float _regenTimer;
    int _killsTowardBondPoint;

    public PetInstance ActivePet
    {
        get
        {
            if (string.IsNullOrEmpty(activePetId) || owned == null) return null;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null && owned[i].id == activePetId)
                    return owned[i];
            return null;
        }
    }

    public PetOwnerBuffs ActiveOwnerBuffs => PetCatalog.ComputeOwnerBuffs(ActivePet);

    void Awake()
    {
        Instance = this;
        _stats = GetComponent<CharacterStats>();
        if (owned == null) owned = new List<PetInstance>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DespawnFollower();
    }

    void Start()
    {
        RefreshFollower();
        RefreshOwnerStats();
    }

    void Update()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.IsReady) return;
        if (_stats == null || !_stats.isPlayer || _stats.isDead) return;

        var buffs = ActiveOwnerBuffs;
        if (buffs.regenPercentPerTick <= 0.0001f) return;

        _regenTimer += Time.deltaTime;
        if (_regenTimer < PetCatalog.RegenIntervalSeconds) return;
        _regenTimer = 0f;

        float heal = _stats.maxHealth * buffs.regenPercentPerTick;
        if (heal > 0.05f)
        {
            _stats.Heal(heal);
            if (NetPlay.IsNetworking)
                NetworkPlayerCombat.RequestOwnerHeal(heal);
        }
    }

    public void ResetToStarter()
    {
        owned = new List<PetInstance>();
        var starter = PetInstance.Create(PetKind.EmberFox, 1);
        owned.Add(starter);
        activePetId = starter.id;
        RefreshFollower();
        RefreshOwnerStats();
    }

    public void ApplySave(AccountSaveData data)
    {
        owned = new List<PetInstance>();
        activePetId = null;
        if (data?.pets != null)
        {
            foreach (var entry in data.pets)
            {
                if (entry == null) continue;
                var pet = entry.ToPet();
                if (pet == null) continue;
                if (owned.Count >= MaxPets) break;
                owned.Add(pet);
            }
        }
        if (!string.IsNullOrEmpty(data?.activePetId))
            activePetId = data.activePetId;

        if (ActivePet == null)
            activePetId = owned.Count > 0 ? owned[0].id : null;

        if (owned.Count == 0)
            ResetToStarter();
        else
        {
            RefreshFollower();
            RefreshOwnerStats();
        }
    }

    public void WriteToSave(AccountSaveData data)
    {
        if (data == null) return;
        data.activePetId = activePetId;
        if (owned == null || owned.Count == 0)
        {
            data.pets = System.Array.Empty<PetSaveData>();
            return;
        }
        var list = new List<PetSaveData>();
        for (int i = 0; i < owned.Count; i++)
        {
            var s = PetSaveData.From(owned[i]);
            if (s != null) list.Add(s);
        }
        data.pets = list.ToArray();
    }

    public bool TryAddPet(PetKind kind, int level, out PetInstance added)
    {
        added = null;
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].kind == kind)
            {
                int before = owned[i].level;
                owned[i].level = Mathf.Min(ProgressionFormulas.MaxPetLevel,
                    owned[i].level + Mathf.Max(1, level / 4));
                int gained = owned[i].level - before;
                if (gained > 0)
                {
                    PetCatalog.EnsureBonds(owned[i]);
                    owned[i].bondPoints += gained;
                }
                added = owned[i];
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.ShowLoot($"{added.displayName} grew to Lv {added.level}! (+{gained} bond)");
                RefreshFollower();
                RefreshOwnerStats();
                return true;
            }
        }

        if (owned.Count >= MaxPets)
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.ShowLoot("Pet roster full!");
            return false;
        }

        added = PetInstance.Create(kind, level);
        owned.Add(added);
        if (string.IsNullOrEmpty(activePetId))
            activePetId = added.id;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"New pet: {added.displayName}!");
        RefreshFollower();
        RefreshOwnerStats();
        return true;
    }

    public void SetActive(string petId)
    {
        if (string.IsNullOrEmpty(petId)) return;
        bool found = false;
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].id == petId)
            {
                found = true;
                break;
            }
        }
        if (!found) return;
        activePetId = petId;
        _regenTimer = 0f;
        RefreshFollower();
        RefreshOwnerStats();
        if (PlayerInventory.Instance != null && ActivePet != null)
        {
            var buffs = ActiveOwnerBuffs;
            PlayerInventory.Instance.ShowLoot(
                $"Deployed {ActivePet.displayName} — {PetCatalog.FormatOwnerBuffs(buffs)}");
        }
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public bool TryTrain(string petId, PetBond bond)
    {
        PetInstance pet = null;
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].id == petId)
            {
                pet = owned[i];
                break;
            }
        }
        if (pet == null) return false;
        if (!PetCatalog.TryTrain(pet, bond)) return false;

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot(
                $"{pet.displayName}: {PetCatalog.BondLabel(bond)} → {PetCatalog.BondEffectLine(pet, bond)}");

        if (pet.id == activePetId)
            RefreshOwnerStats();

        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
        return true;
    }

    public void GrantXp(int amount)
    {
        var pet = ActivePet;
        if (pet == null || amount <= 0) return;
        PetCatalog.EnsureBonds(pet);
        pet.xp += amount;
        bool leveled = false;
        int levelsGained = 0;
        while (pet.level < ProgressionFormulas.MaxPetLevel && pet.xp >= PetCatalog.XpToNext(pet.level))
        {
            pet.xp -= PetCatalog.XpToNext(pet.level);
            pet.level++;
            pet.bondPoints++;
            levelsGained++;
            leveled = true;
        }
        if (_follower != null)
            _follower.SetLevel(pet.level);
        if (leveled)
        {
            RefreshOwnerStats();
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.ShowLoot(
                    $"{pet.displayName} → Lv {pet.level}! (+{levelsGained} bond pt)");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
    }

    public void OnEnemyKilledNearby(Vector3 enemyPos, bool wasBoss)
    {
        var pet = ActivePet;
        if (pet == null || _follower == null) return;
        if (Vector3.Distance(_follower.transform.position, enemyPos) > 14f) return;
        GrantXp(wasBoss ? 28 : 8);

        PetCatalog.EnsureBonds(pet);
        if (wasBoss)
        {
            pet.bondPoints += 3;
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.ShowLoot($"{pet.displayName} gained 3 bond points!");
            if (AccountManager.Instance != null)
                AccountManager.Instance.SaveActive(silent: true);
        }
        else
        {
            _killsTowardBondPoint++;
            if (_killsTowardBondPoint >= 5)
            {
                _killsTowardBondPoint = 0;
                pet.bondPoints += 1;
                if (AccountManager.Instance != null)
                    AccountManager.Instance.SaveActive(silent: true);
            }
        }
    }

    public void RefreshOwnerStats()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ApplyEquipmentStats(false);
        else
            ApplyMoveSpeedOnly();
    }

    public void ApplyMoveSpeedOnly()
    {
        var pc = GetComponent<PlayerController>();
        if (pc == null) return;
        float mul = 1f + ActiveOwnerBuffs.moveSpeedPercent;
        pc.moveSpeed = BaseMoveSpeed * mul;
    }

    public void RefreshFollower()
    {
        var pet = ActivePet;
        if (pet == null)
        {
            DespawnFollower();
            return;
        }

        Transform follow = transform;
        if (NetPlay.IsNetworking)
        {
            var players = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsOwner)
                {
                    follow = players[i].transform;
                    break;
                }
            }
        }

        if (_follower == null)
        {
            var go = new GameObject("ActivePet");
            go.transform.position = follow.position - follow.forward * 2f + follow.right * 0.6f;
            _follower = go.AddComponent<PetFollower>();
        }

        if (_stats == null) _stats = GetComponent<CharacterStats>();
        _follower.Bind(follow, _stats, pet);
    }

    void DespawnFollower()
    {
        if (_follower != null)
        {
            Destroy(_follower.gameObject);
            _follower = null;
        }
    }

    public static PetKind RollBossPetKind(int world)
    {
        float r = Random.value;
        if (world >= 20) return r < 0.5f ? PetKind.StormHawk : (r < 0.8f ? PetKind.ShadowWolf : PetKind.EmberFox);
        if (world >= 12) return r < 0.45f ? PetKind.StormHawk : (r < 0.75f ? PetKind.ShadowWolf : PetKind.EmberFox);
        if (world >= 8) return r < 0.45f ? PetKind.StormHawk : (r < 0.75f ? PetKind.ShadowWolf : PetKind.EmberFox);
        if (world >= 4) return r < 0.4f ? PetKind.ShadowWolf : (r < 0.7f ? PetKind.EmberFox : PetKind.StormHawk);
        return r < 0.5f ? PetKind.EmberFox : (r < 0.8f ? PetKind.ShadowWolf : PetKind.StormHawk);
    }
}
