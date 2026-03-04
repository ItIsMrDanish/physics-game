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
        // Prefer a scene HUD (not one that lives under a Player prefab). If inspector didn't set it,
        // attempt to find the best candidate automatically.
        if (ingameHUD == null)
        {
            ingameHUD = FindBestIngameHUD();
            Debug.Log(ingameHUD != null
                ? $"[GameManager] Selected IngameHUD: {ingameHUD.gameObject.name}"
                : "[GameManager] No IngameHUD found in scene.");
        }
        else
        {
            // warn if assigned HUD appears to be parented to a player (likely a prefab instance)
            if (ingameHUD.GetComponentInParent<PlayerController>() != null)
            {
                Debug.LogWarning("[GameManager] Assigned ingameHUD appears to be parented under a PlayerController. This may be a HUD on the player prefab. GameManager will prefer a scene HUD when stopping timers.");
            }
        }

        if (deadTextCanvas != null)
        {
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

        if (gameOverCanvas != null)
        {
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
        // Hide the eliminated player's GameObject so they are no longer visible
        if (eliminated != null)
        {
            eliminated.gameObject.SetActive(false);
        }

        // show dead-player canvas
        if (deadTextCanvas != null)
        {
            deadTextCanvas.gameObject.SetActive(true);
            deadTextCanvas.enabled = true;
            var cg = deadTextCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        // attempt to switch camera to another alive player (prefer not eliminated)
        var alive = FindObjectsOfType<PlayerController>().Where(x => !x.IsEliminated && x.gameObject.activeSelf).ToArray();
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

            // Stop timer: prefer the scene HUD, but make sure any running HUD timers are stopped.
            if (ingameHUD != null && ingameHUD.GetComponentInParent<PlayerController>() == null)
            {
                Debug.Log("[GameManager] Stopping timer on selected scene IngameHUD.");
                ingameHUD.StopTimer();
            }
            else
            {
                Debug.Log("[GameManager] Selected ingameHUD is missing or looks like a player HUD. Stopping timers on all IngameHUD instances found.");
                StopTimerOnAllHUDs();
            }

            if (gameOverCanvas != null)
            {
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

    // Finds a scene-level IngameHUD (prefer not parented under any PlayerController).
    private IngameHUD FindBestIngameHUD()
    {
        var all = FindObjectsOfType<IngameHUD>();
        // Prefer active HUD not parented to a PlayerController
        var sceneHud = all.FirstOrDefault(h => h.gameObject.activeInHierarchy && h.GetComponentInParent<PlayerController>() == null);
        if (sceneHud != null) return sceneHud;
        // Fallback to any active HUD
        var activeHud = all.FirstOrDefault(h => h.gameObject.activeInHierarchy);
        if (activeHud != null) return activeHud;
        // Last resort: return first found
        return all.FirstOrDefault();
    }

    // Stops timers on all IngameHUD instances (useful when prefab spawns create extra HUDs).
    private void StopTimerOnAllHUDs()
    {
        var all = FindObjectsOfType<IngameHUD>();
        Debug.Log($"[GameManager] StopTimerOnAllHUDs - found {all.Length} IngameHUD instance(s).");
        foreach (var hud in all)
        {
            if (hud == null) continue;
            Debug.Log($"[GameManager] Stopping timer on '{hud.gameObject.name}' (activeInHierarchy={hud.gameObject.activeInHierarchy}).");
            try
            {
                hud.StopTimer();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameManager] Exception stopping timer on '{hud.gameObject.name}': {ex.Message}");
            }
        }
    }
}