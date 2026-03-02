using UnityEngine;
using System.Collections;

public class Enemywave : MonoBehaviour
{
    public GameObject enemyPrefab; // Assign your enemy prefab in the Inspector
    public Transform[] spawnPoints; // Assign spawn points in the Inspector
    public int enemiesPerWave = 5;
    public float waveDelay = 5f;

    [Header("Spawn Settings")]
    [Tooltip("Delay in seconds between each enemy spawn within a wave.")]
    public float spawnDelay = 0.5f;

    private int currentWave = 0;
    private float waveTimer = 0f;
    private bool isSpawning = false;

    void Start()
    {
        StartCoroutine(SpawnWave());
    }

    void Update()
    {
        // Check if all enemies are gone and not currently spawning
        if (!isSpawning && GameObject.FindGameObjectsWithTag("Enemy").Length == 0)
        {
            waveTimer += Time.deltaTime;
            if (waveTimer >= waveDelay)
            {
                StartCoroutine(SpawnWave());
                waveTimer = 0f;
            }
        }
    }

    IEnumerator SpawnWave()
    {
        isSpawning = true;
        currentWave++;
        Debug.Log("Wave " + currentWave + " started!");

        for (int i = 0; i < enemiesPerWave; i++)
        {
            // Pick a random spawn point from the array
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

            // Wait before spawning the next enemy
            yield return new WaitForSeconds(spawnDelay);
        }

        isSpawning = false;
    }
}