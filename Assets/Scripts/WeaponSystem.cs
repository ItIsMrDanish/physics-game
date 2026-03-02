using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Photon.Pun;

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

    [Tooltip("Local axis to rotate the model around when swinging (in local space).")]
    [SerializeField] private Vector3 swingAxis = Vector3.up;

    [Tooltip("Use weapon's local axis when swinging. If false, axis will be interpreted in world space (model.TransformDirection used).")]
    [SerializeField] private bool useLocalSwingAxis = true;

    [Tooltip("Degrees the model sweeps during the visual swing.")]
    [SerializeField] private float visualSwingAngle = 100f;

    [Tooltip("Duration of the visual swing in seconds.")]
    [SerializeField] private float visualSwingDuration = 0.22f;

    [Tooltip("Optional animation curve for the swing (0..1).")]
    [SerializeField] private AnimationCurve visualSwingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Disable Animator on the weaponModel during the swing if present.")]
    [SerializeField] private bool disableAnimatorWhileSwinging = true;

    [Tooltip("Enable debug logs for the swing (prints warnings if animation is overridden).")]
    [SerializeField] private bool logDebug = false;

    private float _lastAttackTime = -999f;
    private PhotonView _photonView;

    // new input system action for attack
    private InputAction _attackAction;

    // visual state
    private bool _isSwinging;
    private Quaternion _weaponInitialLocalRotation;
    private Quaternion _weaponInitialWorldRotation;
    private Animator _weaponAnimator;

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

        _weaponInitialLocalRotation = weaponModel.localRotation;
        _weaponInitialWorldRotation = weaponModel.rotation;

        // detect Animator that may override localRotation
        _weaponAnimator = weaponModel.GetComponentInChildren<Animator>();
        if (_weaponAnimator != null && logDebug)
        {
            Debug.Log($"[WeaponSystem] Found Animator on weaponModel ({_weaponAnimator.gameObject.name}). It may override rotation. disableAnimatorWhileSwinging={disableAnimatorWhileSwinging}");
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
        // Nothing input-related here any more; input handled by _attackAction callbacks.
    }

    private void Attack()
    {
        _lastAttackTime = Time.time;

        // Trigger visual swing (non-blocking)
        if (!_isSwinging)
        {
            StartCoroutine(PerformVisualSwing());
        }

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
                // Use impulse so effect is immediate
                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                continue;
            }

            // If no rigidbody, try NavMeshAgent (common for AI)
            NavMeshAgent agent = col.GetComponentInParent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                // Move the agent a short distance in push direction.
                // Move is applied immediately and is frame-local; scale to feel like a push.
                agent.Move(pushDir * (pushForce * agentPushScale) * Time.deltaTime);
                continue;
            }

            // As a fallback, if the object has a transform only, nudge its position (non-physics)
            // This avoids teleporting root objects: only apply small displacement
            if (col.attachedRigidbody == null && col.transform != transform)
            {
                col.transform.position += pushDir * (pushForce * 0.05f);
            }
        }
    }

    private IEnumerator PerformVisualSwing()
    {
        _isSwinging = true;

        // capture both local and world baseline rotations (in case model changed)
        _weaponInitialLocalRotation = weaponModel.localRotation;
        _weaponInitialWorldRotation = weaponModel.rotation;

        // If Animator present and configured, disable it during swing so it doesn't override rotation
        if (_weaponAnimator != null && disableAnimatorWhileSwinging)
        {
            _weaponAnimator.enabled = false;
        }

        float elapsed = 0f;
        float half = visualSwingAngle * 0.5f;
        Vector3 axis = swingAxis.normalized;

        // Start from -half to +half so sweep crosses forward direction
        while (elapsed < visualSwingDuration)
        {
            float t = Mathf.Clamp01(elapsed / visualSwingDuration);
            float curved = visualSwingCurve.Evaluate(t);
            float angle = Mathf.Lerp(-half, half, curved);

            if (useLocalSwingAxis)
            {
                // local rotation: pivot around weaponModel local axis
                weaponModel.localRotation = _weaponInitialLocalRotation * Quaternion.AngleAxis(angle, axis);
            }
            else
            {
                // world-based: rotate around the weaponModel's transformed axis
                Vector3 worldAxis = weaponModel.TransformDirection(axis);
                weaponModel.rotation = _weaponInitialWorldRotation * Quaternion.AngleAxis(angle, worldAxis);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ensure exact final rotation
        if (useLocalSwingAxis)
            weaponModel.localRotation = _weaponInitialLocalRotation * Quaternion.AngleAxis(half, axis);
        else
            weaponModel.rotation = _weaponInitialWorldRotation * Quaternion.AngleAxis(half, weaponModel.TransformDirection(axis));

        // short restore
        float restoreTime = 0.06f;
        elapsed = 0f;

        Quaternion startLocal = weaponModel.localRotation;
        Quaternion startWorld = weaponModel.rotation;

        while (elapsed < restoreTime)
        {
            float t = elapsed / restoreTime;
            if (useLocalSwingAxis)
                weaponModel.localRotation = Quaternion.Slerp(startLocal, _weaponInitialLocalRotation, t);
            else
                weaponModel.rotation = Quaternion.Slerp(startWorld, _weaponInitialWorldRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // restore final
        if (useLocalSwingAxis)
            weaponModel.localRotation = _weaponInitialLocalRotation;
        else
            weaponModel.rotation = _weaponInitialWorldRotation;

        // re-enable animator if we disabled it
        if (_weaponAnimator != null && disableAnimatorWhileSwinging)
        {
            _weaponAnimator.enabled = true;
        }

        _isSwinging = false;
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

        // draw weapon model pivot and swing arc in editor
        if (weaponModel != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(weaponModel.position, 0.03f);

            // draw visual sweep arc (in local space)
            Vector3 axis = swingAxis.normalized;
            int arcSteps = 10;
            float half = visualSwingAngle * 0.5f;
            for (int i = 0; i <= arcSteps; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / arcSteps);
                Quaternion rot = Quaternion.AngleAxis(a, weaponModel.TransformDirection(axis));
                Vector3 dir = rot * weaponModel.forward;
                Gizmos.DrawLine(weaponModel.position, weaponModel.position + dir.normalized * 0.5f);
            }
        }
    }
}