using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;

public class WeaponSystem : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("Max distance of the swing.")]
    [SerializeField] private float range = 3f;

    [Tooltip("Full cone angle in degrees (e.g. 90 = 45° each side).")]
    [SerializeField] private float coneAngle = 90f;

    [Tooltip("Impulse strength applied to rigidbodies.")]
    [SerializeField] private float pushForce = 6f;

    [Tooltip("Scaling applied to NavMeshAgents when pushed (agents use Move).")]
    [SerializeField] private float agentPushScale = 1.5f;

    [Tooltip("Seconds between swings.")]
    [SerializeField] private float cooldown = 1f;

    [Tooltip("Which layers can be affected by the swing.")]
    [SerializeField] private LayerMask hitMask = ~0;

    // Optional: visualize cone in editor
    [SerializeField] private bool drawGizmos = true;

    [Header("Weapon Model (visual)")]
    [Tooltip("Assign a child transform that contains the weapon model. If empty a default GameObject will be created.")]
    [SerializeField] private Transform weaponModel;

    private float _lastAttackTime = -999f;
    private PhotonView _photonView;

    // new input system action for attack
    private InputAction _attackAction;

    private void Awake()
    {
        // Photon is present in this project; respect ownership if attached
        _photonView = GetComponent<PhotonView>();

        // Ensure there is a weaponModel transform so designers can assign a visual model easily.
        if (weaponModel == null)
        {
            GameObject go = new GameObject("WeaponModel");
            go.transform.SetParent(transform, false);

            // place it a bit in front so designer can see it in editor; adjust as needed.
            go.transform.localPosition = Vector3.forward * 0.6f;
            go.transform.localRotation = Quaternion.identity;

            weaponModel = go.transform;
        }

        // Configure new Input System action for attack (left mouse + gamepad right trigger)
        _attackAction = new InputAction("Attack", InputActionType.Button);
        _attackAction.AddBinding("<Mouse>/leftButton");
        _attackAction.AddBinding("<Gamepad>/rightTrigger");
        _attackAction.AddBinding("<Gamepad>/buttonSouth"); // optional fallback (A/X)
    }

    private void OnEnable()
    {
        if (_attackAction != null)
        {
            _attackAction.performed += OnAttackPerformed;
            _attackAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_attackAction != null)
        {
            _attackAction.performed -= OnAttackPerformed;
            _attackAction.Disable();
        }
    }

    private void OnDestroy()
    {
        if (_attackAction != null)
        {
            _attackAction.performed -= OnAttackPerformed;
            _attackAction.Dispose();
            _attackAction = null;
        }
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        // If PhotonView exists, only local owner should perform attacks
        if (_photonView != null && !_photonView.IsMine) return;

        // enforce cooldown
        if (Time.time - _lastAttackTime < cooldown) return;

        Attack();
    }

    private void Update()
    {
        // No animation/visual swing — input handled by _attackAction callbacks.
    }

    private void Attack()
    {
        _lastAttackTime = Time.time;

        // Collect candidates within spherical range first (cheaper broadphase)
        Vector3 origin = transform.position;
        Collider[] hits = Physics.OverlapSphere(origin, range, hitMask, QueryTriggerInteraction.Collide);

        float halfCone = coneAngle * 0.5f;

        foreach (var col in hits)
        {
            if (col == null) continue;

            // Don't affect self (player)
            if (col.transform.IsChildOf(transform)) continue;
            if (col.transform.root == transform.root) continue;

            // Direction from attacker to target
            Vector3 toTarget = col.transform.position - origin;
            // zero-length guard
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            // Angle check (cone)
            float angleToTarget = Vector3.Angle(transform.forward, toTarget);
            if (angleToTarget > halfCone) continue;

            Vector3 pushDir = toTarget.normalized;

            // Prefer Rigidbody push
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                PhotonView targetPv = col.GetComponentInParent<PhotonView>();
                if (targetPv != null)
                {
                    // If the target has a receiver component, ask the owner to apply the impulse.
                    // This keeps physics authoritative on the object's owner and avoids multiple clients applying forces.
                    if (targetPv.GetComponent<NetworkedPushReceiver>() != null)
                    {
                        // Invoke the RPC on the target's PhotonView and send it to the owner only.
                        targetPv.RPC("RPC_ApplyPush", targetPv.Owner, pushDir, pushForce, agentPushScale);
                    }
                    else if (_photonView != null)
                    {
                        // Fallback: broadcast to all if receiver not present (original behavior).
                        _photonView.RPC(nameof(RPC_ApplyPush), RpcTarget.All, targetPv.ViewID, pushDir, pushForce, agentPushScale);
                    }
                    else
                    {
                        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                    }
                }
                else
                {
                    // Non-networked rigidbody: apply locally only
                    rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                }

                continue;
            }

            // If no rigidbody, try NavMeshAgent (common for AI)
            NavMeshAgent agent = col.GetComponentInParent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                PhotonView targetPv = col.GetComponentInParent<PhotonView>();
                if (targetPv != null)
                {
                    if (targetPv.GetComponent<NetworkedPushReceiver>() != null)
                    {
                        targetPv.RPC("RPC_ApplyPush", targetPv.Owner, pushDir, pushForce, agentPushScale);
                    }
                    else if (_photonView != null)
                    {
                        _photonView.RPC(nameof(RPC_ApplyPush), RpcTarget.All, targetPv.ViewID, pushDir, pushForce, agentPushScale);
                    }
                    else
                    {
                        // Fallback local move
                        agent.Move(pushDir * (pushForce * agentPushScale) * Time.deltaTime);
                    }
                }
                else
                {
                    agent.Move(pushDir * (pushForce * agentPushScale) * Time.deltaTime);
                }

                continue;
            }

            // As a fallback, if the object has a transform only, nudge its position (non-physics)
            if (col.attachedRigidbody == null && col.transform != transform)
            {
                PhotonView targetPv = col.GetComponentInParent<PhotonView>();
                if (targetPv != null)
                {
                    if (targetPv.GetComponent<NetworkedPushReceiver>() != null)
                    {
                        targetPv.RPC("RPC_ApplyPush", targetPv.Owner, pushDir, pushForce, agentPushScale);
                    }
                    else if (_photonView != null)
                    {
                        _photonView.RPC(nameof(RPC_ApplyPush), RpcTarget.All, targetPv.ViewID, pushDir, pushForce, agentPushScale);
                    }
                    else
                    {
                        col.transform.position += pushDir * (pushForce * 0.05f);
                    }
                }
                else
                {
                    col.transform.position += pushDir * (pushForce * 0.05f);
                }
            }
        }
    }

    /// <summary>
    /// RPC that applies a push to a networked object identified by PhotonView id.
    /// Executed on all clients so the physics/visuals are consistent.
    /// This is kept as a fallback for networked objects that don't have a dedicated receiver component.
    /// </summary>
    [PunRPC]
    private void RPC_ApplyPush(int targetViewId, Vector3 pushDir, float force, float agentScale, PhotonMessageInfo info = default)
    {
        PhotonView targetPv = PhotonView.Find(targetViewId);
        if (targetPv == null) return;

        // Try rigidbody first
        Rigidbody rb = targetPv.GetComponent<Rigidbody>();
        if (rb == null)
        {
            // try children
            rb = targetPv.GetComponentInChildren<Rigidbody>();
            if (rb == null)
            {
                // try attachedRigidbody on any collider in root
                Collider c = targetPv.GetComponentInChildren<Collider>();
                if (c != null) rb = c.attachedRigidbody;
            }
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(pushDir * force, ForceMode.Impulse);
            return;
        }

        // try NavMeshAgent
        NavMeshAgent agent = targetPv.GetComponentInChildren<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Move(pushDir * (force * agentScale) * Time.deltaTime);
            return;
        }

        // fallback: nudge transform if present and safe
        Transform t = targetPv.transform;
        if (t != null)
        {
            t.position += pushDir * (force * 0.05f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        Gizmos.DrawSphere(transform.position, range);

        // draw cone outline
        int steps = 12;
        float halfCone = coneAngle * 0.5f;
        Gizmos.color = Color.red;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float angle = -halfCone + t * coneAngle;
            Quaternion rot = Quaternion.AngleAxis(angle, transform.up);
            Vector3 dir = rot * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + dir.normalized * range);
        }

        // draw weapon model pivot in editor
        if (weaponModel != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(weaponModel.position, 0.03f);
        }
    }
}