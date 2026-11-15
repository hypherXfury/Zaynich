using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu. Panel flow in this scene:
///
///   FIRST TIME : Splash → UserDetailsPanel → Main Menu → Cutscene → Mission Brief
///   RETURNING  : Splash → Main Menu → Cutscene → Mission Brief
///
/// The user details panel is handled BEFORE this menu (by SplashScreen), so
/// Start always goes straight to the cutscene.
///
/// The black screen bug on return was caused by the previous scene's black
/// overlay persisting. Fixed by always fading IN from black on Show() so the
/// menu is always clearly visible regardless of how we got here.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup      _canvasGroup;
    [SerializeField] private Button           _startButton;
    [Tooltip("Optional. Shown only when a cleared level is saved (see SaveSystem). If left unassigned, a plain fallback button is created next to Start at runtime.")]
    [SerializeField] private Button           _continueButton;
    [SerializeField] private Image            _blackOverlay;
    [SerializeField] private CutscenePlayer   _cutscenePlayer;

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration  = 0.8f;
    [SerializeField] private float _fadeOutDuration = 0.5f;

    [Header("Scene")]
    [SerializeField] private string _gameSceneName = "Level1";

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Start invisible — Show() always fades us in fresh
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }

        // Black overlay starts fully opaque — we fade it away on Show()
        SetOverlayAlpha(1f);
        if (_blackOverlay != null) _blackOverlay.raycastTarget = false;

        if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);

        bool canContinue = SaveSystem.HasContinue;
        if (_continueButton == null && canContinue) _continueButton = CreateFallbackContinueButton();
        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
            _continueButton.gameObject.SetActive(canContinue);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// StartBtn has no text child (it's a plain graphic), so it can't be cloned
    /// as a readable "Continue" button. This builds a minimal functional one
    /// instead — swap in a proper styled Button in the Inspector's
    /// _continueButton field whenever art is ready; this fallback then never runs.
    /// </summary>
    private Button CreateFallbackContinueButton()
    {
        if (_startButton == null) return null;
        var startRect = _startButton.transform as RectTransform;
        if (startRect == null) return null;

        var go = new GameObject("ContinueBtn (auto)", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(startRect.parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = startRect.anchorMin;
        rect.anchorMax = startRect.anchorMax;
        rect.pivot = startRect.pivot;
        rect.sizeDelta = startRect.sizeDelta;
        rect.localScale = startRect.localScale;
        rect.anchoredPosition = startRect.anchoredPosition +
            new Vector2(0f, startRect.sizeDelta.y * startRect.localScale.y + 40f);

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.9f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "CONTINUE";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 72f;
        label.color = Color.black;
        label.font = TMP_Settings.defaultFontAsset;

        return button;
    }

    // ── Public API — called by SplashScreen ──────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }

    // ── Button ────────────────────────────────────────────────────────────────

    private void OnStartClicked()
    {
        if (_startButton != null) _startButton.interactable = false;
        if (_continueButton != null) _continueButton.interactable = false;

        // New Game wipes the level checkpoint, any saved ability upgrades, and
        // the run clock — Continue is the only path that keeps them.
        InfectionState.ResetForNewGame();
        GameTimer.ResetForNewGame();
        SaveSystem.ResetForNewGame();

        // User details were already collected right after the splash (first
        // time only) — Start always goes straight to the cutscene.
        StartCoroutine(FadeOutThenCutscene());
    }

    private void OnContinueClicked()
    {
        if (_startButton != null) _startButton.interactable = false;
        if (_continueButton != null) _continueButton.interactable = false;

        StartCoroutine(FadeOutThenContinue());
    }

    /// <summary>Skips the cutscene/mission-brief entirely and drops the player straight into the saved level.</summary>
    private IEnumerator FadeOutThenContinue()
    {
        float elapsed = 0f;
        SetOverlayAlpha(0f);
        if (_blackOverlay != null) _blackOverlay.raycastTarget = true;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Clamp01(elapsed / _fadeOutDuration));
            yield return null;
        }
        SetOverlayAlpha(1f);

        SceneManager.LoadScene(SaveSystem.GetContinueSceneName());
    }

    private IEnumerator FadeOutThenCutscene()
    {
        // Fade main menu to black
        float elapsed = 0f;
        SetOverlayAlpha(0f);
        if (_blackOverlay != null) _blackOverlay.raycastTarget = true;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Clamp01(elapsed / _fadeOutDuration));
            yield return null;
        }
        SetOverlayAlpha(1f);

        // Hand off to cutscene
        if (_cutscenePlayer != null)
        {
            gameObject.SetActive(false);
            _cutscenePlayer.Play();
        }
        else
        {
            // Cutscene not assigned — go straight to game
            SceneManager.LoadScene(_gameSceneName);
        }
    }

    // ── Fades ─────────────────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        // First fade out the black overlay so background becomes visible
        float elapsed = 0f;
        while (elapsed < _fadeInDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(1f, 0f, elapsed / (_fadeInDuration * 0.5f)));
            yield return null;
        }
        SetOverlayAlpha(0f);

        // Then fade in the menu UI
        elapsed = 0f;
        while (elapsed < _fadeInDuration * 0.5f)
        {
            elapsed            += Time.deltaTime;
            _canvasGroup.alpha  = Mathf.Clamp01(elapsed / (_fadeInDuration * 0.5f));
            yield return null;
        }

        _canvasGroup.alpha          = 1f;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        if (_startButton    != null) _startButton.interactable = true;
        if (_continueButton != null) _continueButton.interactable = true;
    }

    private void SetOverlayAlpha(float a)
    {
        if (_blackOverlay == null) return;
        Color c = _blackOverlay.color;
        c.a = a;
        _blackOverlay.color = c;
    }
}