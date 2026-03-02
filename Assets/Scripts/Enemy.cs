using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent agent;
    public int damagePerSecond = 10;
    private float damageTimer = 0f;
    private IngameHUD ingameHUD;

    [Header("Attack Settings")]
    [Tooltip("Time in seconds between each attack.")]
    public float attackDelay = 1f;

    [Header("Navigation Settings")]
    [Tooltip("How often to update the path to the player (in seconds).")]
    public float pathUpdateRate = 0.25f;

    [Header("Health Settings")]
    [Tooltip("Maximum health of the enemy.")]
    public int maxHealth = 100;

    private int currentHealth;
    private float pathUpdateTimer = 0f;

    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;

        // Find the player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // Get the NavMeshAgent component
        agent = GetComponent<NavMeshAgent>();

        // Find the IngameHUD in the scene
        ingameHUD = FindAnyObjectByType<IngameHUD>();
    }

    void Update()
    {
        if (player != null && agent != null)
        {
            // Only update path periodically instead of every frame
            pathUpdateTimer += Time.deltaTime;
            if (pathUpdateTimer >= pathUpdateRate)
            {
                agent.SetDestination(player.position);
                pathUpdateTimer = 0f;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            damageTimer += Time.deltaTime;
            if (damageTimer >= attackDelay)
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

    // Call this method from weapons/projectiles to damage the enemy
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Get current health (useful for health bars)
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    // Get max health
    public int GetMaxHealth()
    {
        return maxHealth;
    }

    private void Die()
    {
        // Destroy the enemy GameObject
        Destroy(gameObject);
    }
}