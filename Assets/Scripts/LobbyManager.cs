using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    public GameObject lobbyCanvas;
    [Tooltip("Parent transform where player entries will be instantiated")]
    public Transform playersContent;
    [Tooltip("Prefab for a single player row. Should contain a Text or TMP_Text component.")]
    public GameObject playerEntryPrefab;

    [Header("Scenes")]
    [Tooltip("Scene name to load when leaving the lobby (CreateAndJoin scene).")]
    public string createAndJoinSceneName = "CreateAndJoin";

    // cache active player entry GameObjects by actor number
    private readonly Dictionary<int, GameObject> _playerEntries = new Dictionary<int, GameObject>();

    void Start()
    {
        // If already in a room when this opens, populate the list
        if (PhotonNetwork.InRoom)
        {
            PopulatePlayerList();
        }
    }

    /// <summary>
    /// Hides the lobby UI. Call this when starting the game from the lobby.
    /// </summary>
    public void StartGame()
    {
        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Leaves the current room and switches to the CreateAndJoin scene.
    /// </summary>
    public void LeaveLobby()
    {
        // request leaving the room (Photon will handle callbacks). Also change scene as requested.
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        if (!string.IsNullOrEmpty(createAndJoinSceneName))
        {
            SceneManager.LoadScene(createAndJoinSceneName);
        }
    }

    /// <summary>
    /// Populate the UI list from current Photon player list.
    /// </summary>
    private void PopulatePlayerList()
    {
        ClearPlayerList();

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            AddPlayerEntry(p);
        }
    }

    /// <summary>
    /// Add a UI row for the given player.
    /// </summary>
    private void AddPlayerEntry(Player player)
    {
        if (playerEntryPrefab == null || playersContent == null || player == null)
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

        // try to set text on TMP_Text first, then fallback to legacy Text
        string displayName = string.IsNullOrEmpty(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName;

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
        }

        _playerEntries[player.ActorNumber] = entry;
    }

    /// <summary>
    /// Remove and destroy the UI row for the given actor number.
    /// </summary>
    private void RemovePlayerEntry(int actorNumber)
    {
        GameObject entry;
        if (_playerEntries.TryGetValue(actorNumber, out entry))
        {
            Destroy(entry);
            _playerEntries.Remove(actorNumber);
        }
    }

    /// <summary>
    /// Clear the whole player UI list.
    /// </summary>
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

    // Photon callbacks to keep UI in sync:

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
    }
}