using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot "what is this?" card for Level 2's urine hazard, built the same way
/// as <see cref="FatIntro"/>: the game pauses, the camera pans off the player and
/// onto the urine (the player doesn't move), the card waits on its Next button,
/// and tapping it pans back, unpauses, and saves the card so it never replays.
///
/// The one real difference is what it points at. A fat is a fixed piece of scenery
/// that can be watched for directly; urine is spawned on the fly by UrineEffect —
/// droplets fly a spline path and are destroyed shortly after. So this watches for
/// the first <see cref="UrineDroplet"/> to come into view and focuses on that,
/// which is the hazard the player is actually looking at. The pause keeps it there:
/// timed Destroy calls and droplet movement are both scaled by timeScale, so
/// nothing moves or despawns while the card is up.
///
/// SETUP:
///   1. Put this anywhere in the Level 2 scene (the UrineEffect GameObject is the
///      obvious home) and assign _urineIntroPanel and its Next button.
///   2. Leave _cameraController empty to have it found automatically.
///   3. Optionally assign _focusOverride to pin the camera on a fixed spot — a
///      chamber spawn point, say — instead of whichever droplet is seen first.
/// </summary>
public class UrineIntro : MonoBehaviour
{
    [Header("Card")]
    [Tooltip("The urine intro panel — enabled while the card is up.")]
    [SerializeField] private GameObject _urineIntroPanel;
    [Tooltip("Optional bobbing arrow on the panel.")]
    [SerializeField] private RectTransform _arrow;

    [Header("Display")]
    [Tooltip("Fallback only: seconds the card stays up when no Next button is assigned.")]
    [Min(0.1f)] [SerializeField] private float _displayDuration = 4.5f;

    [Tooltip("The panel's Next button. Tapping it pans the camera back and unpauses. " +
             "Leave empty to fall back to a timed card.")]
    [SerializeField] private Button _nextButton;

    [Header("Camera")]
    [Tooltip("Used to pan onto the urine while the card is up. Left empty = found automatically.")]
    [SerializeField] private CameraController _cameraController;

    [Header("Save")]
    [Tooltip("PlayerPrefs key suffix. Leave blank to use this GameObject's name.")]
    [SerializeField] private string _saveKey;

    [Header("Trigger")]
    [Tooltip("Pin the camera on this instead of whichever droplet is spotted first. " +
             "Leave empty to follow the live urine.")]
    [SerializeField] private Transform _focusOverride;

    [Tooltip("Viewport margin (0-0.5) the urine must be inside before the card fires. " +
             "Higher = it has to be further into view.")]
    [SerializeField] [Range(0f, 1f)] private float _viewportMargin = 0.15f;

    [Tooltip("Seconds between checks for urine on screen. This scans the scene, so it's " +
             "deliberately not every frame.")]
    [Min(0.05f)] [SerializeField] private float _pollInterval = 0.25f;

    [Header("Flashing Target")]
    [Tooltip("Flashes the focused urine's sprite while the card is up, when it has one.")]
    [SerializeField] private bool _flashTarget = true;
    [SerializeField] private float _flashSpeed = 4f;
    [SerializeField] private float _flashMinAlpha = 0.2f;

    [Header("Arrow Motion")]
    [Tooltip("How far (in UI units) the arrow travels from its start position.")]
    [SerializeField] private float _amplitude = 40f;
    [Tooltip("Back-and-forth cycles per second.")]
    [SerializeField] private float _frequency = 2f;
    [Tooltip("Axis the arrow moves along.")]
    [SerializeField] private bool _horizontal = true;

    private bool    _playing;
    private bool    _shown;
    private Camera  _cam;
    private bool    _nextPressed;
    private float   _savedTimeScale = 1f;
    private float   _nextPollTime;

    private string SaveKey => "UrineIntro_" + (string.IsNullOrEmpty(_saveKey) ? gameObject.name : _saveKey);

    private void Start()
    {
        if (_urineIntroPanel != null) _urineIntroPanel.SetActive(false);
        _cam = Camera.main;
        if (_cameraController == null) _cameraController = FindFirstObjectByType<CameraController>();

        if (_nextButton != null)
        {
            _nextButton.onClick.AddListener(() => _nextPressed = true);
            _nextButton.gameObject.SetActive(false);   // only while a card is up
        }

        if (PlayerPrefs.HasKey(SaveKey)) _shown = true;
    }

    private void Update()
    {
        if (_shown || _playing) return;

        // Don't stack this on top of someone else's paused card (a fat intro, a
        // nest/chamber intro, the pause menu). _shown stays false, so the urine
        // still gets its card once the screen is clear.
        if (Time.timeScale == 0f) return;

        // Same courtesy the fat card gives the tutorial: stay quiet until the
        // player has been handed control.
        if (TutorialManager.Instance != null && !TutorialManager.Instance.IsComplete) return;

        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + _pollInterval;

        Transform subject = FindVisibleUrine();
        if (subject == null) return;

        _shown = true;
        StartCoroutine(PlayIntro(subject));
    }

    /// <summary>
    /// The urine to point at: the override if one is assigned and on screen,
    /// otherwise the first droplet far enough into view. Null when there's
    /// nothing worth showing yet.
    /// </summary>
    private Transform FindVisibleUrine()
    {
        if (_focusOverride != null)
            return IsInView(_focusOverride.position) ? _focusOverride : null;

        foreach (UrineDroplet droplet in FindObjectsByType<UrineDroplet>(FindObjectsSortMode.None))
        {
            if (droplet == null) continue;
            if (IsInView(droplet.transform.position)) return droplet.transform;
        }
        return null;
    }

    private bool IsInView(Vector3 worldPos)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        Vector3 vp = _cam.WorldToViewportPoint(worldPos);
        float m = _viewportMargin;

        return vp.z > 0f
            && vp.x >= m && vp.x <= 1f - m
            && vp.y >= m && vp.y <= 1f - m;
    }

    private IEnumerator PlayIntro(Transform subject)
    {
        _playing = true;

        // Remember rather than assume 1: something else could already own the
        // pause, and clobbering its value would resume a game it wants frozen.
        _savedTimeScale = Time.timeScale;
        Time.timeScale  = 0f;

        // Pan first, card second, so it lands on a steady shot of the urine.
        if (_cameraController != null && subject != null)
        {
            _cameraController.FocusOn(subject);
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        if (_urineIntroPanel != null) _urineIntroPanel.SetActive(true);

        _nextPressed = false;
        if (_nextButton != null) _nextButton.gameObject.SetActive(true);

        SpriteRenderer flash = _flashTarget && subject != null
            ? subject.GetComponentInChildren<SpriteRenderer>()
            : null;
        Color flashOriginal = flash != null ? flash.color : Color.white;

        Vector2 startPos = _arrow != null ? _arrow.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        // Unscaled throughout: this card pauses the game itself, so scaled time
        // would freeze the arrow and the flash along with everything else — and
        // the fallback timer would never elapse.
        while (!IsDismissed(elapsed))
        {
            elapsed += Time.unscaledDeltaTime;

            if (_arrow != null)
            {
                float offset = Mathf.Sin(elapsed * _frequency * Mathf.PI * 2f) * _amplitude;
                _arrow.anchoredPosition = _horizontal
                    ? startPos + new Vector2(offset, 0f)
                    : startPos + new Vector2(0f, offset);
            }

            if (flash != null)
            {
                float t = (Mathf.Sin(elapsed * _flashSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                Color c = flashOriginal;
                c.a = Mathf.Lerp(_flashMinAlpha, flashOriginal.a, t);
                flash.color = c;
            }

            yield return null;
        }

        if (_arrow != null) _arrow.anchoredPosition = startPos;
        if (flash != null) flash.color = flashOriginal;

        if (_urineIntroPanel != null) _urineIntroPanel.SetActive(false);
        if (_nextButton      != null) _nextButton.gameObject.SetActive(false);

        // Only hand control back once the camera is home, so nothing gets a free
        // move on the player while the view is still out here.
        if (_cameraController != null)
        {
            _cameraController.ReturnToPlayer();
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        Time.timeScale = _savedTimeScale;
        _playing = false;

        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Next ends the card when there's a button to press; without one the card
    /// falls back to its timer, which is the only way it could ever be dismissed.
    /// </summary>
    private bool IsDismissed(float elapsed) =>
        _nextButton != null ? _nextPressed : elapsed >= _displayDuration;

    /// <summary>
    /// Scene change or teardown mid-card would otherwise strand the game paused
    /// with the camera parked on a droplet that no longer exists.
    /// </summary>
    private void OnDisable()
    {
        if (!_playing) return;

        Time.timeScale = _savedTimeScale;
        _cameraController?.ReturnToPlayer();
        _playing = false;

        if (_urineIntroPanel != null) _urineIntroPanel.SetActive(false);
        if (_nextButton      != null) _nextButton.gameObject.SetActive(false);
    }
}
