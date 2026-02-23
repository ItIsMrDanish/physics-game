using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0; // default: everything

    [Header("Mouse / Camera")]
    [SerializeField] private float mouseSensitivity = 1.5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Networking")]
    [SerializeField] private float networkLerpRate = 10f;

    [Header("References (optional)")]
    [SerializeField] private Camera playerCamera; // assign a child camera for local player if you have one

    private Rigidbody _rb;
    private Vector3 _inputMove;
    private bool _wantJump;

    // New Input System actions
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _lookAction;

    // camera rotation state
    private float _yaw;
    private float _pitch;

    // Network smoothing targets (for remote instances)
    private Vector3 _networkPosition;
    private Quaternion _networkRotation;
    private Vector3 _networkVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 1f;
        }

        // Create simple input actions (no InputActionAsset required)
        _moveAction = new InputAction(
            name: "Move",
            type: InputActionType.Value,
            expectedControlType: "Vector2");
        // WASD / arrow keys composite
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        // Gamepad left stick
        _moveAction.AddBinding("<Gamepad>/leftStick");

        _jumpAction = new InputAction(
            name: "Jump",
            type: InputActionType.Button,
            expectedControlType: "Button");
        _jumpAction.AddBinding("<Keyboard>/space");
        _jumpAction.AddBinding("<Gamepad>/buttonSouth");

        _lookAction = new InputAction(
            name: "Look",
            type: InputActionType.Value,
            expectedControlType: "Vector2");
        _lookAction.AddBinding("<Mouse>/delta");
        _lookAction.AddBinding("<Gamepad>/rightStick");
    }

    private void OnEnable()
    {
        // Enable inputs only for the local player to avoid every networked instance reading input
        if (photonView != null && photonView.IsMine)
        {
            _moveAction.Enable();
            _jumpAction.Enable();
            _lookAction.Enable();

            // Initialize camera rotation state from current transforms
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            if (playerCamera != null)
            {
                _pitch = playerCamera.transform.localEulerAngles.x;
                // convert 0..360 to -180..180 to allow proper clamping
                if (_pitch > 180f) _pitch -= 360f;
            }
            else
            {
                _pitch = 0f;
            }

            // Optional: lock cursor for local player
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        _moveAction.Disable();
        _jumpAction.Disable();
        _lookAction.Disable();

        // restore cursor when disabled
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        _moveAction.Dispose();
        _jumpAction.Dispose();
        _lookAction.Dispose();
    }

    private void Start()
    {
        // Owner-controlled object: enable physics simulation and local camera
        if (photonView.IsMine)
        {
            _rb.isKinematic = false;

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null)
                {
                    audio.enabled = true;
                }
            }
        }
        else
        {
            // Remote objects: physics will be driven by network updates
            _rb.isKinematic = true;

            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null)
                {
                    audio.enabled = false;
                }
            }

            // Initialize smoothing targets
            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
            _networkVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        // Only read input for the local player
        if (photonView.IsMine)
        {
            Vector2 move = _moveAction.ReadValue<Vector2>();
            // map keyboard/gamepad axes (x -> strafe, y -> forward)
            _inputMove = new Vector3(move.x, 0f, move.y);
            _inputMove = Vector3.ClampMagnitude(_inputMove, 1f);

            // jump was pressed this frame
            _wantJump = _wantJump || _jumpAction.triggered;

            // look (mouse/gamepad)
            Vector2 look = _lookAction.ReadValue<Vector2>();
            if (look != Vector2.zero)
            {
                float invert = invertY ? 1f : -1f;
                _yaw += look.x * mouseSensitivity;
                _pitch += look.y * mouseSensitivity * invert;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                // apply yaw to player transform, pitch to camera local rotation
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (playerCamera != null)
                {
                    playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                }
            }
        }
        else
        {
            // Smooth remote transform to network target
            transform.position = Vector3.Lerp(transform.position, _networkPosition, networkLerpRate * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _networkRotation, networkLerpRate * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            // Move relative to the player's orientation
            Vector3 localMove = _inputMove * moveSpeed;
            Vector3 moveWorld = transform.TransformDirection(localMove);
            Vector3 newPos = _rb.position + moveWorld * Time.fixedDeltaTime;
            _rb.MovePosition(newPos);

            // Simple grounded check
            bool grounded = IsGrounded();

            if (_wantJump && grounded)
            {
                // clear existing vertical velocity then add jump impulse
                Vector3 vel = _rb.linearVelocity;
                vel.y = 0f;
                _rb.linearVelocity = vel;
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            _wantJump = false;
        }
        else
        {
            // For remote objects we could apply received velocity if desired; currently smoothing position/rotation only.
        }
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore);
    }

    // IPunObservable implementation: serialize position/rotation/velocity for remote clients
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send position, rotation and velocity
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(_rb.linearVelocity);
        }
        else
        {
            // Remote player: receive networked state
            _networkPosition = (Vector3)stream.ReceiveNext();
            _networkRotation = (Quaternion)stream.ReceiveNext();
            _networkVelocity = (Vector3)stream.ReceiveNext();

            // Optionally extrapolate position based on network time latency:
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            _networkPosition += _networkVelocity * lag;
        }
    }
}
