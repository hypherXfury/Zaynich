using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Intro/description panel for an ability (e.g. Zidebactam Pulse), shown once
/// when its unlock moment arrives. Pauses the game and stays up until the
/// player presses the ability button for the first time — that first press
/// dismisses the panel and resumes the game.
///
/// SETUP:
///   1. Put this on the panel GameObject (with a CanvasGroup).
///   2. Assign _canvasGroup.
///   3. Assign _dismissButton = the Zidebactam Pulse ability button. Its first
///      click both fires the pulse (its own onClick) and dismisses this panel.
///   4. LevelProgression calls Show() when nest 3's first enemy appears.
/// </summary>
public class AbilityIntroPanel : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeInDuration  = 0.4f;
    [SerializeField] private float _fadeOutDuration = 0.3f;

    [Header("Dismiss")]
    [Tooltip("The ability button (Zidebactam Pulse). Pressing it while this panel is open dismisses the panel and resumes the game.")]
    [SerializeField] private Button _dismissButton;
    [Tooltip("Optional safety net: auto-dismiss after this many seconds. 0 = wait for the button press only.")]
    [SerializeField] private float _autoDismissSeconds = 0f;

    private Coroutine _autoDismissCoroutine;
    private bool _shown;
    private bool _visible;

    private void Awake()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        if (_dismissButton != null) _dismissButton.onClick.AddListener(Dismiss);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        if (_shown) return;   // once per level run
        _shown   = true;
        _visible = true;

        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeIn());

        if (_autoDismissSeconds > 0f)
            _autoDismissCoroutine = StartCoroutine(AutoDismiss());
    }

    public void Dismiss()
    {
        // The ability button keeps calling this on every press for the rest of
        // the game — only the press that happens while the panel is open counts.
        if (!_visible) return;
        _visible = false;

        if (_autoDismissCoroutine != null) StopCoroutine(_autoDismissCoroutine);
        StartCoroutine(FadeOutAndHide());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;
        float elapsed = 0f;
        Time.timeScale = 0f;   // pause game while panel shows
        while (elapsed < _fadeInDuration)
        {
            elapsed            += Time.unscaledDeltaTime;
            _canvasGroup.alpha  = Mathf.Clamp01(elapsed / _fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAndHide()
    {
        if (_canvasGroup == null) { Finish(); yield break; }
        float elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            elapsed            += Time.unscaledDeltaTime;
            _canvasGroup.alpha  = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
            yield return null;
        }
        _canvasGroup.alpha = 0f;
        Finish();
    }

    private IEnumerator AutoDismiss()
    {
        yield return new WaitForSecondsRealtime(_autoDismissSeconds);
        Dismiss();
    }

    private void Finish()
    {
        Time.timeScale = 1f;   // resume game
        gameObject.SetActive(false);
    }
}
