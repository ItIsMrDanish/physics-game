using UnityEngine;
using UnityEngine.AI;

public class Enemie : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent agent;

    void Start()
    {
        // Find the player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // Get the NavMeshAgent component
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (player != null && agent != null)
        {
            // Set the agent's destination to the player's position
            agent.SetDestination(player.position);
        }
    }
}
