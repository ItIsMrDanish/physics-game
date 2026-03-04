using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkedPushReceiver : MonoBehaviourPun
{
    // This RPC is intended to be invoked on the owner via targetPv.RPC("RPC_ApplyPush", RpcTarget.Owner, ...)
    // Owner will perform the physics change locally and let the usual Photon synchronization components
    // (PhotonTransformView / PhotonRigidbodyView / your custom sync) propagate results to other clients.
    [PunRPC]
    public void RPC_ApplyPush(Vector3 pushDir, float force, float agentScale, PhotonMessageInfo info = default)
    {
        // For safety ensure only owner executes the authoritative change.
        if (!photonView.IsMine) return;

        // Try rigidbody first
        Rigidbody rb = photonView.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = photonView.GetComponentInChildren<Rigidbody>();
            if (rb == null)
            {
                Collider c = photonView.GetComponentInChildren<Collider>();
                if (c != null) rb = c.attachedRigidbody;
            }
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(pushDir * force, ForceMode.Impulse);
            return;
        }

        // Try NavMeshAgent
        NavMeshAgent agent = photonView.GetComponentInChildren<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            // Move once — owner AI should react to this movement; movement will be visible after sync.
            agent.Move(pushDir * (force * agentScale) * Time.deltaTime);
            return;
        }

        // Fallback: nudge transform slightly if safe
        Transform t = photonView.transform;
        if (t != null)
        {
            t.position += pushDir * (force * 0.05f);
        }
    }
}