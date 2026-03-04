using System.Linq;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("HUD / Timer")]
    [Tooltip("Assign the IngameHUD in the scene so GameManager can StopTimer() when the match ends.")]
    public IngameHUD ingameHUD;

    [Header("Elimination UI")]
    [Tooltip("UI GameObject that shows a text indicating the player is eliminated.")]
    public Canvas deadTextCanvas;

    [Tooltip("Game Over Canvas shown when last player is eliminated.")]
    public Canvas gameOverCanvas;

    [Header("Spectator")]
    [Tooltip("Offset applied when switching to a 3rd-person view of an alive player.")]
    public Vector3 spectatorOffset = new Vector3(0f, 2f, -4f);

    // tracked players
    private PlayerController[] _players;

    void Start()
    {
        Debug.Log("[GameManager] Start - initializing.");

        if (deadTextCanvas != null)
        {
            Debug.Log($"[GameManager] deadTextCanvas assigned: {deadTextCanvas.name}. Disabling at Start.");
            deadTextCanvas.gameObject.SetActive(false);
            deadTextCanvas.enabled = false;
            var cg = deadTextCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] deadTextCanvas is NOT assigned in the inspector.");
        }

        if (gameOverCanvas != null)
        {
            Debug.Log($"[GameManager] gameOverCanvas assigned: {gameOverCanvas.name}. Disabling at Start.");
            gameOverCanvas.gameObject.SetActive(false);
            gameOverCanvas.enabled = false;
            var cg = gameOverCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] gameOverCanvas is NOT assigned in the inspector.");
        }

        RefreshPlayerListAndSubscribe();
    }

    void Update()
    {
        // If players spawn later, refresh subscriptions automatically.
        var found = FindObjectsOfType<PlayerController>();
        if (_players == null || found.Length != _players.Length)
        {
            Debug.Log($"[GameManager] Player count changed (previous={_players?.Length ?? 0}, found={found.Length}) - refreshing subscriptions.");
            RefreshPlayerListAndSubscribe();
        }
    }

    void RefreshPlayerListAndSubscribe()
    {
        // find existing players and subscribe to their elimination events
        _players = FindObjectsOfType<PlayerController>();
        Debug.Log($"[GameManager] RefreshPlayerListAndSubscribe found {_players.Length} PlayerController(s).");

        foreach (var p in _players)
        {
            if (p == null)
                continue;

            // avoid double-subscription
            p.OnEliminated -= OnPlayerEliminated;
            p.OnEliminated += OnPlayerEliminated;

            Debug.Log($"[GameManager] Subscribed to OnEliminated for player '{p.name}'. IsEliminated={p.IsEliminated}");
        }
    }

    private void OnPlayerEliminated(PlayerController eliminated)
    {
        Debug.Log($"[GameManager] OnPlayerEliminated called for '{(eliminated != null ? eliminated.name : "NULL")}'.");

        // show dead-player canvas only (do NOT change any text)
        if (deadTextCanvas != null)
        {
            Debug.Log("[GameManager] Enabling deadTextCanvas (only).");
            // Ensure game over canvas is hidden when showing dead text
            if (gameOverCanvas != null && gameOverCanvas.gameObject.activeSelf)
            {
                gameOverCanvas.gameObject.SetActive(false);
                gameOverCanvas.enabled = false;
                var gcg = gameOverCanvas.GetComponent<CanvasGroup>();
                if (gcg != null)
                {
                    gcg.alpha = 0f;
                    gcg.interactable = false;
                    gcg.blocksRaycasts = false;
                }
            }

            deadTextCanvas.gameObject.SetActive(true);
            deadTextCanvas.enabled = true;
            var cg = deadTextCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            // IMPORTANT: do not modify any text components as requested.
        }
        else
        {
            Debug.LogWarning("[GameManager] deadTextCanvas is null - cannot show eliminated UI.");
        }

        // attempt to switch camera to another alive player (prefer not eliminated)
        var alive = FindObjectsOfType<PlayerController>().Where(x => !x.IsEliminated).ToArray();
        Debug.Log($"[GameManager] Alive players count after elimination: {alive.Length}");

        if (alive.Length > 0)
        {
            // pick the first alive player (could improve selection logic)
            var target = alive[0];
            Debug.Log($"[GameManager] Switching to spectator camera for '{target.name}'.");
            SwitchToSpectatorCamera(target);
        }
        else
        {
            // no players left alive -> game over
            Debug.Log("[GameManager] No players alive - Game Over.");

            // hide dead text canvas when showing game over
            if (deadTextCanvas != null && deadTextCanvas.gameObject.activeSelf)
            {
                deadTextCanvas.gameObject.SetActive(false);
                deadTextCanvas.enabled = false;
                var dcg = deadTextCanvas.GetComponent<CanvasGroup>();
                if (dcg != null)
                {
                    dcg.alpha = 0f;
                    dcg.interactable = false;
                    dcg.blocksRaycasts = false;
                }
            }

            if (ingameHUD != null)
            {
                Debug.Log("[GameManager] Stopping match timer via IngameHUD.StopTimer().");
                ingameHUD.StopTimer();
            }
            else
            {
                Debug.LogWarning("[GameManager] ingameHUD is null - cannot StopTimer().");
            }

            if (gameOverCanvas != null)
            {
                Debug.Log("[GameManager] Enabling gameOverCanvas.");
                gameOverCanvas.gameObject.SetActive(true);
                gameOverCanvas.enabled = true;
                var cg = gameOverCanvas.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] gameOverCanvas is null - cannot show Game Over.");
            }
        }
    }

    private void SwitchToSpectatorCamera(PlayerController target)
    {
        if (target == null)
        {
            Debug.LogWarning("[GameManager] SwitchToSpectatorCamera called with null target.");
            return;
        }

        // disable all player cameras first
        var allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p.PlayerCamera != null)
            {
                p.PlayerCamera.enabled = false;
                var audio = p.PlayerCamera.GetComponent<AudioListener>();
                if (audio != null) audio.enabled = false;
                Debug.Log($"[GameManager] Disabled camera for player '{p.name}'.");
            }
        }

        // enable target camera and re-parent/set offset to create a simple 3rd-person view
        Camera cam = target.PlayerCamera;
        if (cam == null)
        {
            // if the target doesn't have a camera, create a temporary one
            var go = new GameObject($"SpectatorCam_{target.name}");
            cam = go.AddComponent<Camera>();
            Debug.Log($"[GameManager] Created temporary spectator camera '{go.name}'.");
        }

        cam.enabled = true;
        var listener = cam.GetComponent<AudioListener>();
        if (listener == null)
            cam.gameObject.AddComponent<AudioListener>();
        else
            listener.enabled = true;

        // parent camera to the target player and set offset for 3rd-person
        cam.transform.SetParent(target.transform, false);
        cam.transform.localPosition = spectatorOffset;
        cam.transform.localRotation = Quaternion.identity;

        Debug.Log($"[GameManager] Spectator camera set on '{target.name}' at localPosition {spectatorOffset}.");

        // if the local player had cursor locked, unlock it (spectator)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}