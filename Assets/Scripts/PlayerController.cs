using UnityEngine;
using Photon.Pun;

public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundMask = ~0; // default: everything

    [Header("Networking")]
    [SerializeField] private float networkLerpRate = 10f;

    [Header("References (optional)")]
    [SerializeField] private Camera playerCamera; // assign a child camera for local player if you have one

    private Rigidbody _rb;
    private Vector3 _inputMove;
    private bool _wantJump;

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
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _inputMove = new Vector3(h, 0f, v).normalized;
            _wantJump = _wantJump || Input.GetButtonDown("Jump"); // record jump intent until FixedUpdate handles it
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
            // Move relative to the player's orientation (optional: world-space)
            Vector3 moveWorld = transform.TransformDirection(_inputMove) * moveSpeed;
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
            // If you prefer physically applying velocity instead of kinematic interpolation, set rb.isKinematic = false in Start for remote objects and apply _networkVelocity here.
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
