using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject _pauseMenuPanel;
    public GameObject _settingsPanel;

    void Start()
    {
        _pauseMenuPanel.SetActive(false);
        _settingsPanel.SetActive(false);
    }
    public void PauseGame()
    {
        Time.timeScale = 0f;
        _pauseMenuPanel.SetActive(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        _pauseMenuPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        _pauseMenuPanel.SetActive(false);
        _settingsPanel.SetActive(true);
    }

    public void BackToPause()
    {
        _settingsPanel.SetActive(false);
        _pauseMenuPanel.SetActive(true);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Closes the game. Wire a Quit button's onClick here — works from the pause
    /// menu or the boss win panel, both of which sit at timeScale 0, which
    /// doesn't affect quitting.
    ///
    /// Application.Quit() is a no-op inside the editor, so play mode is stopped
    /// explicitly there — otherwise the button looks broken while testing.
    /// </summary>
    public void QuitGame()
    {
        Time.timeScale = 1f;   // leave a sane state behind for the next play session

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
