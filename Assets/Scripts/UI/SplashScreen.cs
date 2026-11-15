using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Splash screen — fades in a logo, holds, fades out, then hands off:
///
///   FIRST TIME : Splash → UserDetailsPanel → Main Menu
///   RETURNING  : Splash → Main Menu (details already saved)
///
/// HIERARCHY SETUP:
///   Canvas (Screen Space — Overlay)
///     └── SplashPanel              ← Image (solid black background) + this script
///           └── LogoImage          ← Image (your logo sprite)
///
/// SETUP:
///   1. Create a Canvas, add a full-screen Image (black) as SplashPanel.
///   2. Add a child Image for your logo, assign to _logoImage.
///   3. Add this script to SplashPanel, assign _splashPanel and _logoImage.
///   4. Assign _mainMenu and _userDetailsPanel (both start inactive).
///   5. Hit Play — splash runs automatically on Start.
/// </summary>
public class SplashScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup      _splashPanel;      // full-screen black bg
    [SerializeField] private Image            _logoImage;        // your logo
    [SerializeField] private MainMenu         _mainMenu;         // shows after splash (returning user)
    [SerializeField] private UserDetailsPanel _userDetailsPanel; // shows after splash (first-time user)

    [Header("Timing (seconds)")]
    [SerializeField] private float _fadeInDuration  = 1.2f;
    [SerializeField] private float _holdDuration    = 1.8f;
    [SerializeField] private float _fadeOutDuration = 1.0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Start with logo invisible, panel fully opaque (black screen)
        if (_splashPanel != null) _splashPanel.alpha = 1f;
        if (_logoImage   != null)
        {
            Color c = _logoImage.color;
            c.a = 0f;
            _logoImage.color = c;
        }

        StartCoroutine(RunSplash());
    }

    // ── Splash sequence ───────────────────────────────────────────────────────

    private IEnumerator RunSplash()
    {
        // 1 — Fade logo IN
        yield return StartCoroutine(FadeLogo(0f, 1f, _fadeInDuration));

        // 2 — Hold
        yield return new WaitForSeconds(_holdDuration);

        // 3 — Fade logo OUT
        yield return StartCoroutine(FadeLogo(1f, 0f, _fadeOutDuration));

        // 4 — Hand off: first-time users fill in their details before the
        //     main menu; returning users go straight to the menu.
        gameObject.SetActive(false);

        if (!UserDetailsPanel.HasSavedData && _userDetailsPanel != null)
            _userDetailsPanel.Show();
        else if (_mainMenu != null)
            _mainMenu.Show();
    }

    private IEnumerator FadeLogo(float from, float to, float duration)
    {
        if (_logoImage == null) yield break;

        float elapsed = 0f;
        Color c       = _logoImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a              = Mathf.Lerp(from, to, elapsed / duration);
            _logoImage.color = c;
            yield return null;
        }

        c.a              = to;
        _logoImage.color = c;
    }
}