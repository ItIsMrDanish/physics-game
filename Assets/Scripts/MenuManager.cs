using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class MenuManager : MonoBehaviour
{
    [Header("UI Canvases")]
    public GameObject mainMenuCanvas;
    public GameObject unlocksCanvas;
    public GameObject highscoreCanvas;

    void Start()
    {
        // Ensures main menu is visible on start
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(true);
        }

        if (unlocksCanvas != null)
        {
            unlocksCanvas.SetActive(false);
        }

        if (highscoreCanvas != null)
        {
            highscoreCanvas.SetActive(false);
        }
    }

    // Change to Loading scene which continues to Create and Join in Lobby.
    public void PlayGame()
    {
        SceneManager.LoadScene("Loading");
    }

    // Show Unlocks UI and hide main menu.
    public void ShowUnlocks()
    {
        SetActiveCanvas(unlocksCanvas);
    }

    // Show Highscore UI and hide main menu.
    public void ShowHighscore()
    {
        SetActiveCanvas(highscoreCanvas);
    }

    // Exit application.
    public void ExitGame()
    {
        Application.Quit();
    }

    public void GoToMainMenuScene()
    {
        // Disconnect from Photon before returning to the main menu.
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        SceneManager.LoadScene("MainMenu");
    }

    // Generic back function: hides the provided current canvas and shows the main menu.
    public void Back(GameObject currentCanvas)
    {
        if (currentCanvas != null)
        {
            currentCanvas.SetActive(false);
        }

        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(true);
        }
    }

    // Convenience back method with no parameters: hides known sub-panels and shows main menu.
    public void Back()
    {
        if (unlocksCanvas != null) unlocksCanvas.SetActive(false);
        if (highscoreCanvas != null) highscoreCanvas.SetActive(false);

        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
    }

    // Internal helper to turn off all known canvases and enable the requested one.
    private void SetActiveCanvas(GameObject canvasToShow)
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        if (unlocksCanvas != null) unlocksCanvas.SetActive(false);
        if (highscoreCanvas != null) highscoreCanvas.SetActive(false);

        if (canvasToShow != null) canvasToShow.SetActive(true);
    }
}