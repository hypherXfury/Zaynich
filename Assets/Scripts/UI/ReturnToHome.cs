using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to your pause menu "Return to Home" button.
/// Fades to black, resets timeScale, then loads the main menu scene.
/// The main menu will show the splash screen first then the main panel.
/// </summary>
public class ReturnToHome : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button _returnButton;
    [SerializeField] private Image  _blackOverlay;   // full-screen black Image in your pause UI

    [Header("Scene")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("Timing")]
    [SerializeField] private float _fadeOutDuration = 0.5f;

    private void Awake()
    {
        if (_returnButton != null)
            _returnButton.onClick.AddListener(OnReturnClicked);

        SetOverlayAlpha(0f);
    }

    private void OnReturnClicked()
    {
        if (_returnButton != null) _returnButton.interactable = false;
        StartCoroutine(FadeAndReturn());
    }

    private IEnumerator FadeAndReturn()
    {
        float elapsed = 0f;
        if (_blackOverlay != null) _blackOverlay.raycastTarget = true;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;   // works even while game is paused
            SetOverlayAlpha(Mathf.Clamp01(elapsed / _fadeOutDuration));
            yield return null;
        }

        // Always restore timeScale before loading a new scene
        Time.timeScale = 1f;
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void SetOverlayAlpha(float a)
    {
        if (_blackOverlay == null) return;
        Color c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }
}