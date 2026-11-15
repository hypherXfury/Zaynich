using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mission brief panel shown after the cutscene ends or is skipped. CutScenePlayer
/// enables this panel over a fully-black overlay; it fades in to reveal the brief,
/// then fades back to black and loads the game scene when Start is pressed.
/// </summary>
public class MissionBrief : MonoBehaviour
{
    public Image _blackOverlay;
    public Button startGame;

    [Header("Fade")]
    [SerializeField] private string _gameSceneName = "Game";
    [SerializeField] private float _fadeInDuration  = 0.5f;
    [SerializeField] private float _fadeOutDuration = 0.5f;

    private bool _starting;

    void Start()
    {
        startGame.onClick.AddListener(StartGame);
    }

    // Panel is activated by CutScenePlayer — fade in from the black overlay.
    void OnEnable()
    {
        Time.timeScale = 1f;
        SetOverlayAlpha(1f);
        StartCoroutine(FadeOverlay(1f, 0f, _fadeInDuration));
    }

    private void StartGame()
    {
        if (_starting) return;
        _starting = true;
        startGame.interactable = false;
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        yield return StartCoroutine(FadeOverlay(0f, 1f, _fadeOutDuration));
        SceneManager.LoadScene(_gameSceneName);
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float a)
    {
        if (_blackOverlay == null) return;
        Color c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }
}
