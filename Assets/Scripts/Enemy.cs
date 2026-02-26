using UnityEngine;
using UnityEngine.AI;

public class Enemie : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent agent;
    public int damagePerSecond = 10;
    private float damageTimer = 0f;
    private IngameHUD ingameHUD;

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

        // Find the IngameHUD in the scene
        ingameHUD = FindObjectOfType<IngameHUD>();
    }

    void Update()
    {
        if (player != null && agent != null)
        {
            // Set the agent's destination to the player's position
            agent.SetDestination(player.position);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            damageTimer += Time.deltaTime;
            if (damageTimer >= 1f)
            {
                if (ingameHUD != null)
                {
                    ingameHUD.ModifyHealth(-damagePerSecond);
                }
                damageTimer = 0f;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            damageTimer = 0f;
        }
    }
}