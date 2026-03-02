using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Photon.Pun;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseCanvas;

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

        pauseCanvas.SetActive(!pauseCanvas.activeSelf);
    }

    // Public method to hide the pause canvas (can be wired to UI buttons).
    public void HidePauseMenu()
    {
        if (pauseCanvas != null)
        {
            pauseCanvas.SetActive(false);
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
