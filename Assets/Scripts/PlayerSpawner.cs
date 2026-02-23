using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Instantiates the local player prefab via Photon when the scene is ready.
/// Attach this to an empty GameObject in the scene (e.g. "PlayerSpawner") or to a persistent manager object.
/// Drag your player prefab into the inspector (the prefab's name must match a prefab located in a Resources folder for PhotonNetwork.Instantiate to work).
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("Drag the player prefab here. The prefab's name must match a prefab in a Resources folder for PhotonNetwork.Instantiate.")]
    [SerializeField] private GameObject playerPrefab;

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

        if (playerPrefab == null)
        {
            Debug.LogError($"{nameof(PlayerSpawner)}: playerPrefab is not assigned in the inspector. Aborting spawn.");
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

        // PhotonNetwork.Instantiate requires the prefab to be located in a Resources folder.
        // We use the assigned prefab's name so you can drag & drop it in the inspector.
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPos, spawnRot);

        s_spawned = true;
    }
}