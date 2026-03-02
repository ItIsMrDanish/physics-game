using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

/// Lobby UI manager:
/// - StartGame: switches to the Photon-synced "GameScene".
/// - LeaveLobby: leaves the Photon room and returns to "LobbyCreateJoin".
/// - Player list view: keeps a UI list in sync with players that joined via Photon.
public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [Tooltip("Parent transform where player entry prefabs will be instantiated (e.g. a VerticalLayoutGroup content).")]
    public Transform playersContent;

    [Tooltip("Prefab representing a single player row. Should contain a TMP_Text or legacy Text component to show the player's name.")]
    public GameObject playerEntryPrefab;

    // cache active player entry GameObjects by actor number
    private readonly Dictionary<int, GameObject> _playerEntries = new Dictionary<int, GameObject>();

    void Start()
    {
        // ensure UI references are sane
        if (playersContent == null)
        {
            Debug.LogWarning("LobbyManager: playersContent is not assigned.");
        }

        if (playerEntryPrefab == null)
        {
            Debug.LogWarning("LobbyManager: playerEntryPrefab is not assigned.");
        }

        // Ensure scene loads are synchronized when the master client changes scenes.
        // Setting this here makes the behavior robust even if it wasn't configured elsewhere.
        PhotonNetwork.AutomaticallySyncScene = true;

        // populate if we are already in a room (e.g. reloaded scene)
        if (PhotonNetwork.InRoom)
        {
            PopulatePlayerList();
        }
    }

    /// Start game. Uses PhotonNetwork.LoadLevel so all connected clients can synchronize scene load.
    /// Only the master client will actually trigger the synchronized load. Non-master clients will request the master.
    public void StartGame()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("StartGame called but not in a room.");
            return;
        }

        // If this client is the master, start the run and sync the load for everyone.
        if (PhotonNetwork.IsMasterClient)
        {
            // close the room to prevent late joiners
            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
            }

            PhotonNetwork.LoadLevel("GameScene");
        }
        else
        {
            // Ask the master client to start the game. The master will call LoadLevel for everyone.
            photonView.RPC(nameof(RPC_RequestStartGame), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    /// Leave the Photon room. OnLeftRoom callback will handle scene change.
    public void LeaveLobby()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            // Do not immediately change scene here — wait for OnLeftRoom to ensure Photon cleaned up.
        }
        else
        {
            // If not in a room, just go back to the create/join scene immediately.
            SceneManager.LoadScene("LobbyCreateJoin");
        }
    }

    /// Populate the player UI list from current Photon player list.
    private void PopulatePlayerList()
    {
        ClearPlayerList();

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            AddPlayerEntry(p);
        }
    }

    /// Instantiate and configure a UI entry for a player.
    /// Expects the prefab to contain a TMP_Text or Text component to display name.
    private void AddPlayerEntry(Player player)
    {
        if (player == null || playerEntryPrefab == null || playersContent == null)
        {
            return;
        }

        if (_playerEntries.ContainsKey(player.ActorNumber))
        {
            // already present (rejoin scenario)
            return;
        }

        GameObject entry = Instantiate(playerEntryPrefab, playersContent);
        entry.transform.localScale = Vector3.one;

        // Determine display name
        string displayName = string.IsNullOrEmpty(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName;

        // Try TMP_Text first, then fallback to legacy Text
        TMP_Text tmp = entry.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = displayName;
        }
        else
        {
            Text uiText = entry.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                uiText.text = displayName;
            }
            else
            {
                // nothing to show - log to help debugging
                Debug.LogWarning("LobbyManager: playerEntryPrefab has no TMP_Text or Text component in children.");
            }
        }

        // Optional: mark master client / local player if prefab contains specific child named "MasterFlag" or "LocalFlag"
        Transform masterFlag = entry.transform.Find("MasterFlag");
        if (masterFlag != null)
        {
            masterFlag.gameObject.SetActive(player.IsMasterClient);
        }

        Transform localFlag = entry.transform.Find("LocalFlag");
        if (localFlag != null)
        {
            localFlag.gameObject.SetActive(player.IsLocal);
        }

        _playerEntries[player.ActorNumber] = entry;
    }

    private void RemovePlayerEntry(int actorNumber)
    {
        if (_playerEntries.TryGetValue(actorNumber, out GameObject entry))
        {
            Destroy(entry);
            _playerEntries.Remove(actorNumber);
        }
    }

    private void ClearPlayerList()
    {
        foreach (var kv in _playerEntries)
        {
            if (kv.Value != null)
            {
                Destroy(kv.Value);
            }
        }

        _playerEntries.Clear();
    }

    #region Photon callbacks to keep UI in sync

    public override void OnJoinedRoom()
    {
        PopulatePlayerList();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AddPlayerEntry(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer != null)
        {
            RemovePlayerEntry(otherPlayer.ActorNumber);
        }
    }

    public override void OnLeftRoom()
    {
        ClearPlayerList();
        // After leaving the room, go back to the Create/Join lobby scene.
        SceneManager.LoadScene("LobbyCreateJoin");
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Update master flags if entries expose a "MasterFlag" child.
        foreach (var kv in _playerEntries)
        {
            GameObject entry = kv.Value;
            if (entry == null) continue;

            Transform masterFlag = entry.transform.Find("MasterFlag");
            if (masterFlag != null)
            {
                // find corresponding player by actor number (kv.Key)
                bool isMaster = (newMasterClient != null && newMasterClient.ActorNumber == kv.Key);
                masterFlag.gameObject.SetActive(isMaster);
            }
        }
    }

    #endregion

    /// RPC received by the master client when a non-master requests the game start.
    [PunRPC]
    private void RPC_RequestStartGame(int requesterActorNumber, PhotonMessageInfo info)
    {
        // Only the master should process requests to start the game.
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // Optionally you can validate the requester here (e.g. check permissions or readiness).

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        PhotonNetwork.LoadLevel("GameScene");
    }
}