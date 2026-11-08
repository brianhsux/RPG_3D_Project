using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 14f, -16f);
    public float smooth = 5f;
    public float lookHeight = 1.2f;

    void Start()
    {
        if (target == null)
        {
            var player = PlayerLookup.FindSoloHero();
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
