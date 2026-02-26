using UnityEngine;

public class Enemywave : MonoBehaviour
{
    public GameObject enemyPrefab; // Assign your enemy prefab in the Inspector
    public Transform[] spawnPoints; // Assign spawn points in the Inspector
    public int enemiesPerWave = 5;
    public float waveDelay = 5f;

    private int currentWave = 0;
    private float waveTimer = 0f;

    void Start()
    {
        StartWave();
    }

    void Update()
    {
        // Check if all enemies are gone
        if (GameObject.FindGameObjectsWithTag("Enemy").Length == 0)
        {
            waveTimer += Time.deltaTime;
            if (waveTimer >= waveDelay)
            {
                StartWave();
                waveTimer = 0f;
            }
        }
    }

    void StartWave()
    {
        currentWave++;
        for (int i = 0; i < enemiesPerWave; i++)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        }
        Debug.Log("Wave " + currentWave + " started!");
    }
}
