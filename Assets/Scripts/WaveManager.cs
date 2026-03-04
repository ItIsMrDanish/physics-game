using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    public GameObject enemyPrefab; // Assign your enemy prefab in the Inspector

    [Header("NavMesh Spawn Settings")]
    [Tooltip("Radius (meters) around this GameObject within which enemies will be spawned on the NavMesh.")]
    [HideInInspector] public float navMeshSpawnRadius = 20f;
    [Tooltip("Maximum distance used by NavMesh.SamplePosition to snap a random point to the NavMesh.")]
    [HideInInspector] public float navMeshSampleMaxDistance = 2f;
    [Tooltip("How many attempts to try to find a valid NavMesh point before giving up.")]
    [HideInInspector] public int navMeshSampleAttempts = 30;

    [Header("Spawn Interval (seconds)")]
    public float minSpawnInterval = 3f;
    public float maxSpawnInterval = 5f;

    [Header("Enemies per Batch")]
    public int minEnemiesPerSpawn = 1;
    public int maxEnemiesPerSpawn = 3;

    private int spawnGroupCount = 0;
    private bool isRunning = true;

    void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("WaveManager: enemyPrefab is not assigned. Disabling WaveManager.");
            enabled = false;
            return;
        }

        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (isRunning)
        {
            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(wait);

            int toSpawn = Random.Range(minEnemiesPerSpawn, maxEnemiesPerSpawn + 1);
            spawnGroupCount++;
            Debug.Log("Spawn group " + spawnGroupCount + ": spawning " + toSpawn + " enemies.");

            for (int i = 0; i < toSpawn; i++)
            {
                Vector3 spawnPos;
                if (TryGetRandomNavMeshPosition(out spawnPos))
                {
                    Quaternion spawnRot = Quaternion.identity;
                    Instantiate(enemyPrefab, spawnPos, spawnRot);
                }
                else
                {
                    // Fallback: spawn at WaveManager position if NavMesh sample fails
                    Debug.LogWarning("WaveManager: Failed to find NavMesh spawn point after multiple attempts. Spawning at manager position as fallback.");
                    Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                }
            }
        }
    }

    bool TryGetRandomNavMeshPosition(out Vector3 result)
    {
        for (int attempt = 0; attempt < navMeshSampleAttempts; attempt++)
        {
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * navMeshSpawnRadius;
            // Keep sample height near WaveManager to improve sampling reliability
            randomPoint.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    // Optional controls
    public void StopSpawning() => isRunning = false;

    public void StartSpawning()
    {
        if (!isRunning)
        {
            isRunning = true;
            StartCoroutine(SpawnLoop());
        }
    }
}