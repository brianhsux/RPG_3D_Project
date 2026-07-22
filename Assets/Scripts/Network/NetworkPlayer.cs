using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Root network avatar. Owner gets local camera focus; everyone sees a simple body.
/// Armor style/rarity syncs as a tint so peers can tell gear tiers apart.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] Color ownerColor = new Color(0.35f, 0.75f, 0.95f);
    [SerializeField] Color remoteColor = new Color(0.85f, 0.45f, 0.35f);

    public NetworkVariable<int> ArmorStyle = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> ArmorRarity = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    Renderer _bodyRenderer;
    CameraFollow _cameraFollow;
    float _nextVisualPush;

    public override void OnNetworkSpawn()
    {
        gameObject.tag = "Player";

        EnsureVisual();
        ArmorStyle.OnValueChanged += OnArmorChanged;
        ArmorRarity.OnValueChanged += OnArmorChanged;
        ApplyVisualFromSync();

        if (IsOwner)
        {
            AttachCamera();
            PushArmorFromInventory();
            if (PlayerPetController.Instance != null)
                PlayerPetController.Instance.RefreshFollower();
        }

        Debug.Log($"[NGO] NetworkPlayer spawn owner={OwnerClientId} localOwner={IsOwner}");
    }

    public override void OnNetworkDespawn()
    {
        ArmorStyle.OnValueChanged -= OnArmorChanged;
        ArmorRarity.OnValueChanged -= OnArmorChanged;
        if (IsOwner && _cameraFollow != null && _cameraFollow.target == transform)
            _cameraFollow.target = FindSoloPlayerTransform();
        if (IsOwner && PlayerPetController.Instance != null)
            PlayerPetController.Instance.RefreshFollower();
    }

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        if (Time.time < _nextVisualPush) return;
        _nextVisualPush = Time.time + 0.75f;
        PushArmorFromInventory();
    }

    void OnArmorChanged(int previous, int current) => ApplyVisualFromSync();

    void PushArmorFromInventory()
    {
        if (!IsOwner) return;
        var inv = PlayerInventory.Instance;
        var armor = inv != null ? inv.equippedArmor : null;
        int style = armor != null ? ArmorVisualUtil.StyleIndex(armor.itemLevel) : 0;
        int rarity = armor != null ? (int)armor.rarity : 0;
        if (ArmorStyle.Value != style) ArmorStyle.Value = style;
        if (ArmorRarity.Value != rarity) ArmorRarity.Value = rarity;
    }

    void ApplyVisualFromSync()
    {
        EnsureVisual();
        if (_bodyRenderer == null) return;

        Color baseTint = IsOwner ? ownerColor : remoteColor;
        Color plate = ArmorVisualUtil.BodyColor(ArmorStyle.Value, (ItemRarity)ArmorRarity.Value);
        Color c = Color.Lerp(baseTint, plate, 0.72f);

        var combat = GetComponent<NetworkPlayerCombat>();
        if (combat != null && !combat.IsAlive)
            c = new Color(0.35f, 0.35f, 0.38f);

        var mat = _bodyRenderer.material;
        mat.color = c;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
    }

    void EnsureVisual()
    {
        var body = transform.Find("Body");
        if (body == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Body";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            body = go.transform;
        }
        _bodyRenderer = body.GetComponent<Renderer>();
    }

    void AttachCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        _cameraFollow = cam.GetComponent<CameraFollow>();
        if (_cameraFollow == null)
            _cameraFollow = cam.gameObject.AddComponent<CameraFollow>();
        _cameraFollow.target = transform;
    }

    static Transform FindSoloPlayerTransform()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            if (players[i].GetComponent<NetworkPlayer>() != null) continue;
            return players[i].transform;
        }
        return null;
    }
}
