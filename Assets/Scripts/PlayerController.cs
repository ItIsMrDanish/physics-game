using System;
using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Mouse / Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int startHealth = 100;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Rigidbody _rb;
    private Vector2 _moveInput;
    private bool _jumpPressed;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;

    private float _yaw;
    private float _pitch;

    // Network smoothing (kept minimal)
    private Vector3 _networkPosition;
    private Quaternion _networkRotation;
    private Vector3 _networkVelocity;
    [SerializeField] private float networkLerpRate = 10f;

    // Health runtime
    private int _currentHealth;
    private bool _isEliminated;

    // Event invoked when this player becomes eliminated (health reaches 0).
    public event Action<PlayerController> OnEliminated;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsEliminated => _isEliminated;
    public Camera PlayerCamera => playerCamera;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 1f;
        }

        // Input actions (WASD, mouse look, space)
        _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _moveAction.AddBinding("<Gamepad>/leftStick");

        _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        _lookAction.AddBinding("<Mouse>/delta");
        _lookAction.AddBinding("<Gamepad>/rightStick");

        _jumpAction = new InputAction("Jump", InputActionType.Button, expectedControlType: "Button");
        _jumpAction.AddBinding("<Keyboard>/space");
        _jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        _moveAction.Enable();
        _lookAction.Enable();
        _jumpAction.Enable();

        if (photonView != null && photonView.IsMine)
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            if (playerCamera != null)
            {
                _pitch = playerCamera.transform.localEulerAngles.x;
                if (_pitch > 180f) _pitch -= 360f;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] OnEnable - isMine={photonView.IsMine} name={gameObject.name} root={transform.root.name}");
        }
    }

    private void OnDisable()
    {
        _moveAction.Disable();
        _lookAction.Disable();
        _jumpAction.Disable();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        _moveAction.Dispose();
        _lookAction.Dispose();
        _jumpAction.Dispose();
    }

    private void Start()
    {
        _currentHealth = Mathf.Clamp(startHealth, 0, Mathf.Max(1, maxHealth));
        _isEliminated = false;

        if (photonView.IsMine)
        {
            _rb.isKinematic = false;
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null) audio.enabled = true;
            }
        }
        else
        {
            _rb.isKinematic = true;
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null) audio.enabled = false;
            }

            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
            _networkVelocity = Vector3.zero;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] Start - isMine={photonView.IsMine}, isKinematic={_rb.isKinematic}, constraints={_rb.constraints}");
        }
    }

    private void Update()
    {
        if (_isEliminated) return;

        if (photonView.IsMine)
        {
            Vector2 mv = _moveAction.ReadValue<Vector2>();
            _moveInput = Vector2.ClampMagnitude(mv, 1f);

            _jumpPressed = _jumpPressed || _jumpAction.triggered;

            Vector2 look = _lookAction.ReadValue<Vector2>();
            if (look != Vector2.zero)
            {
                _yaw += look.x * mouseSensitivity;
                _pitch -= look.y * mouseSensitivity; // invert Y here by subtracting
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (playerCamera != null)
                {
                    playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerController] Update - moveInput={_moveInput} jumpPressed={_jumpPressed} yaw={_yaw} pitch={_pitch}");
            }
        }
        else
        {
            // smoothing for remote instances
            transform.position = Vector3.Lerp(transform.position, _networkPosition, networkLerpRate * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _networkRotation, networkLerpRate * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (_isEliminated) return;
        if (!photonView.IsMine) return;

        // horizontal movement (preserve vertical velocity)
        Vector3 localMove = new Vector3(_moveInput.x, 0f, _moveInput.y) * moveSpeed;
        Vector3 worldMove = transform.TransformDirection(localMove) * Time.fixedDeltaTime;
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] FixedUpdate - isKinematic={_rb.isKinematic} constraints={_rb.constraints} localMove={localMove} worldMove={worldMove} rb.velocity={_rb.linearVelocity}");
        }

        _rb.MovePosition(_rb.position + worldMove);

        bool grounded = IsGrounded();
        if (_jumpPressed && grounded)
        {
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerController] Jump applied - new velocity={_rb.linearVelocity}");
            }
        }

        _jumpPressed = false;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Change health by the given delta. When health reaches zero, the player is eliminated and the event is fired.
    /// </summary>
    /// <param name="delta">Positive to heal, negative to damage.</param>
    public void ModifyHealth(int delta)
    {
        if (_isEliminated) return;

        int newHealth = Mathf.Clamp(_currentHealth + delta, 0, Mathf.Max(1, maxHealth));
        _currentHealth = newHealth;

        if (_currentHealth == 0)
        {
            Eliminate();
        }
    }

    private void Eliminate()
    {
        if (_isEliminated) return;
        _isEliminated = true;

        // disable local player control
        enabled = false;

        // disable physics interactions for eliminated players to prevent interference
        if (_rb != null)
        {
            _rb.isKinematic = true;
        }

        // disable camera/audio for this player's own camera; GameManager will activate a spectator camera
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            var audio = playerCamera.GetComponent<AudioListener>();
            if (audio != null) audio.enabled = false;
        }

        OnEliminated?.Invoke(this);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(_rb.linearVelocity);
            stream.SendNext(_currentHealth);
        }
        else
        {
            _networkPosition = (Vector3)stream.ReceiveNext();
            _networkRotation = (Quaternion)stream.ReceiveNext();
            _networkVelocity = (Vector3)stream.ReceiveNext();
            _currentHealth = (int)stream.ReceiveNext();

            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            _networkPosition += _networkVelocity * lag;
        }
    }
}