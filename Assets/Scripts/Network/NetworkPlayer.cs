using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Root network avatar. Owner gets local camera focus; everyone sees a simple body.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] Color ownerColor = new Color(0.35f, 0.75f, 0.95f);
    [SerializeField] Color remoteColor = new Color(0.85f, 0.45f, 0.35f);

    Renderer _bodyRenderer;
    CameraFollow _cameraFollow;

    public override void OnNetworkSpawn()
    {
        EnsureVisual();
        ApplyTint(IsOwner ? ownerColor : remoteColor);

        if (IsOwner)
            AttachCamera();

        Debug.Log($"[NGO] NetworkPlayer spawn owner={OwnerClientId} localOwner={IsOwner}");
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && _cameraFollow != null && _cameraFollow.target == transform)
            _cameraFollow.target = FindSoloPlayerTransform();
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

    void ApplyTint(Color c)
    {
        if (_bodyRenderer == null) return;
        var mat = _bodyRenderer.material;
        mat.color = c;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
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
        var solo = GameObject.FindGameObjectWithTag("Player");
        return solo != null ? solo.transform : null;
    }
}
