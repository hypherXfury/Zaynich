using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable tooltip bubble. Shows instantly at full opacity — no fade in/out
/// so text is always readable the moment it appears.
///
/// Cards come in two flavours, chosen by the showNext argument:
///
///   INFO cards (showNext: true)   — a Next button, and the card blocks raycasts
///     so a stray tap can't hit the joystick behind it. The caller advances with
///     <see cref="WaitForNext"/>.
///
///   ACTION cards (showNext: false) — no button. These stay up while the player
///     does the thing being taught (move, shoot, collect, pulse), so the card
///     MUST let input through to the joystick and ability buttons underneath —
///     hence blocksRaycasts is switched off for them. The caller dismisses the
///     card itself once its gameplay condition is met.
///
/// The game is no longer paused behind either kind (see TutorialManager), so
/// action cards are readable while the world keeps moving.
/// </summary>
public class TutorialTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup      _canvasGroup;
    [SerializeField] private TextMeshProUGUI  _messageText;
    [SerializeField] private Image            _imageDisplay;
    [SerializeField] private RectTransform    _arrow;

    [Header("Next Button")]
    [SerializeField] private Button _nextButton;

    [Header("Glow Pulse")]
    [SerializeField] private Image  _glowImage;
    [SerializeField] private float  _glowPulseSpeed = 2f;

    private Coroutine _glowCoroutine;
    private bool      _nextPressed;

    private void Awake()
    {
        // Ensure fully visible from the start — no accidental alpha = 0
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;   // the Next button must be clickable
        }

        if (_nextButton != null)
            _nextButton.onClick.AddListener(() => _nextPressed = true);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Blocks until the player taps Next. Uses WaitUntil so it keeps polling
    /// while the game is paused at timeScale 0.
    /// </summary>
    public IEnumerator WaitForNext()
    {
        _nextPressed = false;
        yield return new WaitUntil(() => _nextPressed);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show tooltip instantly at full opacity near anchorScreenPos.</summary>
    public void Show(string message, Vector2 anchorScreenPos, bool showNext = true, bool showGlow = false)
    {
        // Set text first
        if (_messageText != null)
        {
            _messageText.text = message;
            _messageText.gameObject.SetActive(true);
        }
        if (_imageDisplay != null) _imageDisplay.gameObject.SetActive(false);

        // Snap to full opacity before activating so there is zero fade
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        // Position near anchor
        //PositionNear(anchorScreenPos);

        SetNextVisible(showNext);

        // Activate
        gameObject.SetActive(true);

        // Stop any running glow coroutine before starting a new one
        if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);

        if (showGlow && _glowImage != null)
        {
            _glowImage.gameObject.SetActive(true);
            _glowCoroutine = StartCoroutine(PulseGlow());
        }
        else if (_glowImage != null)
        {
            _glowImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Show a tutorial image instantly at full opacity near anchorScreenPos.
    /// Pass showNext: false for an action card — the Next button is hidden and
    /// the card stops blocking raycasts so the player can still drive the
    /// joystick and ability buttons through it.
    /// </summary>
    public void ShowImage(Sprite image, Vector2 anchorScreenPos, bool showNext = true, bool showGlow = false)
    {
        // Swap to image mode: hide text, show sprite
        if (_messageText != null) _messageText.gameObject.SetActive(false);
        if (_imageDisplay != null)
        {
            _imageDisplay.sprite = image;
            _imageDisplay.gameObject.SetActive(true);
        }

        // Snap to full opacity before activating so there is zero fade
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        // Position near anchor
        //PositionNear(anchorScreenPos);

        SetNextVisible(showNext);

        // Activate
        gameObject.SetActive(true);

        // Stop any running glow coroutine before starting a new one
        if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);

        if (showGlow && _glowImage != null)
        {
            _glowImage.gameObject.SetActive(true);
            _glowCoroutine = StartCoroutine(PulseGlow());
        }
        else if (_glowImage != null)
        {
            _glowImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Shows/hides the Next button and matches the card's raycast blocking to it:
    /// an info card swallows taps (nothing behind it should be reachable), an
    /// action card must let every tap through to the gameplay UI underneath.
    /// </summary>
    private void SetNextVisible(bool visible)
    {
        _nextPressed = false;
        if (_nextButton != null) _nextButton.gameObject.SetActive(visible);

        if (_canvasGroup != null)
        {
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    /// <summary>
    /// Starts/stops the glow pulse on a card that's already up. TutorialManager
    /// uses this as a nudge when the player has been sitting on an action card
    /// without doing the action.
    /// </summary>
    public void SetGlow(bool on)
    {
        if (_glowImage == null) return;

        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        _glowImage.gameObject.SetActive(on);
        if (on) _glowCoroutine = StartCoroutine(PulseGlow());
    }

    /// <summary>Hide the tooltip instantly.</summary>
    public void Hide(bool instant = true)
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }
        if (_glowImage != null) _glowImage.gameObject.SetActive(false);

        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void PositionNear(Vector2 screenPos)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // Place above or below based on screen position
        float offsetY = screenPos.y > Screen.height * 0.65f ? -130f : 130f;
        rt.position   = new Vector3(
            Mathf.Clamp(screenPos.x, 160f, Screen.width  - 160f),
            Mathf.Clamp(screenPos.y + offsetY, 80f, Screen.height - 80f),
            0f);

        // Flip arrow to point toward anchor
        if (_arrow != null)
            _arrow.localScale = new Vector3(1f, offsetY > 0 ? -1f : 1f, 1f);
    }

    // ── Glow pulse ────────────────────────────────────────────────────────────

    private IEnumerator PulseGlow()
    {
        if (_glowImage == null) yield break;
        while (true)
        {
            float t          = (Mathf.Sin(Time.unscaledTime * _glowPulseSpeed) + 1f) * 0.5f;
            Color c          = _glowImage.color;
            c.a              = Mathf.Lerp(0.2f, 0.9f, t);
            _glowImage.color = c;
            yield return null;
        }
    }
}