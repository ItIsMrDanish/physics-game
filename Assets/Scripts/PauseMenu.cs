using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Photon.Pun;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseCanvas;

    private PlayerController _localPlayerController;

    void Start()
    {
        // Ensure pause canvas starts hidden if assigned.
        if (pauseCanvas != null)
        {
            pauseCanvas.SetActive(false);
        }
    }

    void Update()
    {
        // Using the new Input System: toggle when Esc is pressed.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePauseCanvas();
        }
    }

    // Toggle the pause canvas visibility.
    private void TogglePauseCanvas()
    {
        if (pauseCanvas == null) return;

        bool willShow = !pauseCanvas.activeSelf;
        pauseCanvas.SetActive(willShow);

        // Ensure we have the local PlayerController reference.
        if (_localPlayerController == null)
        {
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc != null && pc.photonView != null && pc.photonView.IsMine)
                {
                    _localPlayerController = pc;
                    break;
                }
            }
        }

        // If we found the local controller, disable it while paused so all input/looking stops.
        if (_localPlayerController != null)
        {
            _localPlayerController.enabled = !willShow;
        }
        else
        {
            // Fallback: ensure cursor state still becomes usable for UI when paused.
            Cursor.lockState = willShow ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = willShow;
        }
    }

    // Public method to hide the pause canvas (can be wired to UI buttons).
    public void HidePauseMenu()
    {
        if (pauseCanvas != null)
        {
            pauseCanvas.SetActive(false);
        }

        if (_localPlayerController == null)
        {
            // Try to locate controller if not cached
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc != null && pc.photonView != null && pc.photonView.IsMine)
                {
                    _localPlayerController = pc;
                    break;
                }
            }
        }

        if (_localPlayerController != null)
        {
            _localPlayerController.enabled = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Disconnect from Photon (if connected) and go to MainMenu scene.
    public void DisconnectAndGoToMainMenu()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        SceneManager.LoadScene("MainMenu");
    }
}
