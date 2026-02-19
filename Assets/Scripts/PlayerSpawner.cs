using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Instantiates the local player prefab via Photon when the scene is ready.
/// Attach this to an empty GameObject in the scene (e.g. "PlayerSpawner") or to a persistent manager object.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("Name of the player prefab as in Resources folder used by PhotonNetwork.Instantiate")]
    [SerializeField] private string playerPrefabName = "PlayerPrefab";

    [Tooltip("Optional spawn points. If empty, will spawn at Vector3.zero.")]
    [SerializeField] private Transform[] spawnPoints;

    // Prevent double instantiate in case Start runs more than once
    private static bool s_spawned = false;

    private IEnumerator Start()
    {
        // Wait until Photon is connected and in a room
        yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom);

        if (s_spawned)
        {
            yield break;
        }

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPos = t.position;
            spawnRot = t.rotation;
        }

        // Photon expects the prefab to be in Resources/ and named exactly as playerPrefabName
        PhotonNetwork.Instantiate(playerPrefabName, spawnPos, spawnRot);

        s_spawned = true;
    }
}