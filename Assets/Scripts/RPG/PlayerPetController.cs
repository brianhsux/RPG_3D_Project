using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns pet collection, active pet, spawn/despawn follower, save/load.
/// Combat-focused: pets deal % of player ATK automatically.
/// </summary>
public class PlayerPetController : MonoBehaviour
{
    public static PlayerPetController Instance { get; private set; }

    public const int MaxPets = 16;

    [System.NonSerialized] public List<PetInstance> owned = new List<PetInstance>();
    [System.NonSerialized] public string activePetId;

    PetFollower _follower;
    CharacterStats _stats;

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
        // Ensure follower exists after load/starter
        RefreshFollower();
    }

    public void ResetToStarter()
    {
        owned = new List<PetInstance>();
        var starter = PetInstance.Create(PetKind.EmberFox, 1);
        owned.Add(starter);
        activePetId = starter.id;
        RefreshFollower();
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

        // Validate active
        if (ActivePet == null)
            activePetId = owned.Count > 0 ? owned[0].id : null;

        if (owned.Count == 0)
            ResetToStarter();
        else
            RefreshFollower();
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
        // If already own this kind, level it up instead
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].kind == kind)
            {
                owned[i].level = Mathf.Min(40, owned[i].level + Mathf.Max(1, level / 4));
                added = owned[i];
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.ShowLoot($"{added.displayName} grew to Lv {added.level}!");
                RefreshFollower();
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
        RefreshFollower();
        if (PlayerInventory.Instance != null && ActivePet != null)
            PlayerInventory.Instance.ShowLoot($"Deployed {ActivePet.displayName}");
        if (AccountManager.Instance != null)
            AccountManager.Instance.SaveActive(silent: true);
    }

    public void GrantXp(int amount)
    {
        var pet = ActivePet;
        if (pet == null || amount <= 0) return;
        pet.xp += amount;
        bool leveled = false;
        while (pet.level < 40 && pet.xp >= PetCatalog.XpToNext(pet.level))
        {
            pet.xp -= PetCatalog.XpToNext(pet.level);
            pet.level++;
            leveled = true;
        }
        if (_follower != null)
            _follower.SetLevel(pet.level);
        if (leveled && PlayerInventory.Instance != null)
            PlayerInventory.Instance.ShowLoot($"{pet.displayName} → Lv {pet.level}!");
    }

    public void OnEnemyKilledNearby(Vector3 enemyPos, bool wasBoss)
    {
        var pet = ActivePet;
        if (pet == null || _follower == null) return;
        if (Vector3.Distance(_follower.transform.position, enemyPos) > 14f) return;
        GrantXp(wasBoss ? 28 : 8);
    }

    public void RefreshFollower()
    {
        var pet = ActivePet;
        if (pet == null)
        {
            DespawnFollower();
            return;
        }

        if (_follower == null)
        {
            var go = new GameObject("ActivePet");
            go.transform.position = transform.position - transform.forward * 2f + transform.right * 0.6f;
            _follower = go.AddComponent<PetFollower>();
        }

        if (_stats == null) _stats = GetComponent<CharacterStats>();
        _follower.Bind(transform, _stats, pet);
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
        // Slight theme bias by world, still all 3 available
        float r = Random.value;
        if (world >= 8) return r < 0.45f ? PetKind.StormHawk : (r < 0.75f ? PetKind.ShadowWolf : PetKind.EmberFox);
        if (world >= 4) return r < 0.4f ? PetKind.ShadowWolf : (r < 0.7f ? PetKind.EmberFox : PetKind.StormHawk);
        return r < 0.5f ? PetKind.EmberFox : (r < 0.8f ? PetKind.ShadowWolf : PetKind.StormHawk);
    }
}
