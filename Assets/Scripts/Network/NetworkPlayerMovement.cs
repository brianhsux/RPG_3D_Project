using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner-authoritative movement. Local owner drives CharacterController;
/// NetworkTransform (Owner authority) replicates pose to others.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 6.5f;
    public float rotateSpeed = 12f;
    public float gravity = -20f;
    public float jumpHeight = 1.75f;

    CharacterController _controller;
    NetworkTransform _netTransform;
    Vector3 _velocity;
    bool _jumpQueued;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        ConfigureAuthority();

        // Remote proxies: CharacterController can fight NetworkTransform — disable it.
        if (!IsOwner && _controller != null)
            _controller.enabled = false;
    }

    void ConfigureAuthority()
    {
        if (_netTransform == null) return;
        // NGO 2.x owner authority so the owning client can move freely.
        _netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
    }

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        if (_controller == null || !_controller.enabled) return;

        // Don't steal input while character panel is open (same as solo).
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.showPanel)
            return;

        Vector2 move = ReadMoveInput();
        Vector3 input = new Vector3(move.x, 0f, move.y);
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (WasJumpPressed())
            _jumpQueued = true;

        if (input.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            _controller.Move(input * moveSpeed * Time.deltaTime);
        }

        bool grounded = _controller.isGrounded;
        if (grounded && _jumpQueued)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
        }
        else if (grounded)
        {
            _jumpQueued = false;
            if (_velocity.y < 0f) _velocity.y = -2f;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    static Vector2 ReadMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }

    static bool WasJumpPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;
        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            return true;
        return false;
    }
}
