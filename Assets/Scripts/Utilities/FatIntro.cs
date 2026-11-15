using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an intro card (image panel) the first time this GameObject enters the
/// camera bounds, given the same treatment as the tutorial's nest and chamber
/// intros: the game pauses, the camera pans off the player and onto this fat
/// (the player doesn't move), the object's own sprite flashes to single it out,
/// and the card waits on the Next button. Tapping Next pans back, unpauses, and
/// saves the card so it never replays on a later launch.
///
/// The pan keeps working through the freeze because CameraController drives a
/// focus move on unscaled time; the flash and the arrow bob do the same here.
/// With no Next button assigned the card falls back to dismissing itself after
/// <see cref="_displayDuration"/> seconds.
///
/// The card plays once across every fat, not once per fat: whichever one the
/// player reaches first shows it, and the rest switch themselves off so passing
/// a second fat doesn't replay the same explanation.
///
/// Attach to each "introFat" object.
/// </summary>
public class FatIntro : MonoBehaviour
{
    public GameObject fatIntroPanel;
    public RectTransform arrow;

    [Header("Display")]
    [Tooltip("Fallback only: seconds the card stays up when no Next button is assigned.")]
    [Min(0.1f)] [SerializeField] private float _displayDuration = 4.5f;

    [Tooltip("The panel's Next button. Tapping it pans the camera back and unpauses. " +
             "Leave empty to fall back to a timed card.")]
    [SerializeField] private Button _nextButton;

    [Header("Camera")]
    [Tooltip("Used to pan onto this fat while its card is up. Left empty = found automatically.")]
    [SerializeField] private CameraController _cameraController;

    [Header("Save")]
    [Tooltip("PlayerPrefs key suffix. Leave blank to use this GameObject's name.")]
    [SerializeField] private string _saveKey;

    [Header("Flashing Target")]
    [Tooltip("Sprite to flash while the card is up. Defaults to this object's SpriteRenderer.")]
    [SerializeField] private SpriteRenderer _flashTarget;
    [Tooltip("Flashes per second of the object's sprite.")]
    [SerializeField] private float _flashSpeed = 4f;
    [Tooltip("Minimum alpha the sprite dips to while flashing.")]
    [SerializeField] private float _flashMinAlpha = 0.2f;

    [Header("Trigger")]
    [Tooltip("Viewport margin (0-0.5) the object must be inside before the card fires. " +
             "Higher = object must be further into view (more visible) before triggering.")]
    [SerializeField] [Range(0f, 1f)] private float _viewportMargin = 0.15f;

    [Header("Arrow Motion")]
    [Tooltip("How far (in UI units) the arrow travels from its start position.")]
    [SerializeField] private float _amplitude = 40f;
    [Tooltip("Back-and-forth cycles per second.")]
    [SerializeField] private float _frequency = 2f;
    [Tooltip("Axis the arrow moves along.")]
    [SerializeField] private bool _horizontal = true;

    private bool _playing;
    private bool _shown;      // once per lifetime — don't re-trigger after it plays
    private Camera _cam;
    private bool  _nextPressed;
    private float _savedTimeScale = 1f;   // whatever was running before this card paused it

    // The panel is shared between the fat objects, and so is the card itself: it
    // explains what a fat *is*, so the player only ever needs to see it once. The
    // fats sit on separate routes through the level and either can be met first,
    // which used to mean walking past both showed the same card twice. The first
    // one into view claims the card here; every other fat retires on sight.
    private static FatIntro _activeCard;
    private static bool     _cardClaimed;

    // Entering play mode resets these even when domain reload is off, so a fresh
    // session never starts with the card already claimed by a dead object.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeCard   = null;
        _cardClaimed  = false;
    }

    private string SaveKey => "FatIntro_" + (string.IsNullOrEmpty(_saveKey) ? gameObject.name : _saveKey);

    /// <summary>
    /// Written by whichever fat actually plays the card. Shared by every fat, so a
    /// later launch skips the card no matter which one the player met — the
    /// per-object <see cref="SaveKey"/> alone would let the other fat still fire.
    /// </summary>
    private const string GroupSaveKey = "FatIntro_Shown";

    void Start()
    {
        fatIntroPanel.SetActive(false);
        _cam = Camera.main;
        if (_flashTarget == null) _flashTarget = GetComponent<SpriteRenderer>();
        if (_cameraController == null) _cameraController = FindFirstObjectByType<CameraController>();

        if (_nextButton != null)
        {
            _nextButton.onClick.AddListener(() => _nextPressed = true);
            _nextButton.gameObject.SetActive(false);   // only while a card is up
        }

        if (PlayerPrefs.HasKey(SaveKey) || PlayerPrefs.HasKey(GroupSaveKey)) Retire();
    }

    void Update()
    {
        if (_shown || _playing) return;

        // Another fat is showing the card, or already has. One is all the player
        // needs, so this trigger is done — switch it off rather than queue behind.
        if (_cardClaimed)
        {
            Retire();
            return;
        }

        // The fat can sit close enough to the spawn point to drift into view
        // mid-tutorial, stacking this card on top of a tutorial card. Hold off
        // until the tutorial hands over control — _shown stays false, so the fat
        // still gets its card the moment it's back in view afterwards.
        // Returning players clear this on the first frame.
        if (TutorialManager.Instance != null && !TutorialManager.Instance.IsComplete) return;

        // A paused card owned by something else entirely (a urine intro,
        // a nest/chamber intro, the pause menu) — don't stack on top of it.
        if (Time.timeScale == 0f) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 vp = _cam.WorldToViewportPoint(transform.position);
        float m = _viewportMargin;
        bool inBounds = vp.z > 0f
            && vp.x >= m && vp.x <= 1f - m
            && vp.y >= m && vp.y <= 1f - m;

        if (inBounds)
        {
            _shown       = true;
            _cardClaimed = true;
            _activeCard  = this;
            StartCoroutine(PlayIntro());
        }
    }

    /// <summary>
    /// This trigger will never show a card: another fat has it, or a save says the
    /// player has already seen one. Switching the component off (never the
    /// GameObject — that's the fat itself, sprite and all) stops the view checks.
    /// </summary>
    private void Retire()
    {
        _shown  = true;
        enabled = false;
    }

    private IEnumerator PlayIntro()
    {
        _playing = true;

        // Remember rather than assume 1: something else could already own the
        // pause, and clobbering its value would resume a game it wants frozen.
        _savedTimeScale = Time.timeScale;
        Time.timeScale  = 0f;

        // Pan first, card second, so it lands on a steady shot of the fat.
        if (_cameraController != null)
        {
            _cameraController.FocusOn(transform);
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        fatIntroPanel.SetActive(true);

        _nextPressed = false;
        if (_nextButton != null) _nextButton.gameObject.SetActive(true);

        Vector2 startPos = arrow != null ? arrow.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        // Unscaled throughout: this card pauses the game itself, so scaled time
        // would freeze the arrow and the flash along with everything else — and
        // the fallback timer would never elapse.
        while (!IsDismissed(elapsed))
        {
            elapsed += Time.unscaledDeltaTime;

            if (arrow != null)
            {
                float offset = Mathf.Sin(elapsed * _frequency * Mathf.PI * 2f) * _amplitude;
                arrow.anchoredPosition = _horizontal
                    ? startPos + new Vector2(offset, 0f)
                    : startPos + new Vector2(0f, offset);
            }

            if (_flashTarget != null)
            {
                float t = (Mathf.Sin(elapsed * _flashSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                Color c = _flashTarget.color;
                c.a = Mathf.Lerp(_flashMinAlpha, 1f, t);
                _flashTarget.color = c;
            }

            yield return null;
        }

        // Restore original position, full opacity, and close the panel
        if (arrow != null) arrow.anchoredPosition = startPos;
        if (_flashTarget != null)
        {
            Color c = _flashTarget.color;
            c.a = 1f;
            _flashTarget.color = c;
        }
        fatIntroPanel.SetActive(false);
        if (_nextButton != null) _nextButton.gameObject.SetActive(false);

        // Only hand control back once the camera is home, so nothing gets a free
        // move on the player while the view is still out here.
        if (_cameraController != null)
        {
            _cameraController.ReturnToPlayer();
            yield return new WaitUntil(() => _cameraController == null || _cameraController.FocusSettled);
        }

        Time.timeScale = _savedTimeScale;

        _playing = false;
        if (_activeCard == this) _activeCard = null;

        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.SetInt(GroupSaveKey, 1);   // retires the other fats on a later launch
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Next ends the card when there's a button to press; without one the card
    /// falls back to its timer, which is the only way it could ever be dismissed.
    /// </summary>
    private bool IsDismissed(float elapsed) =>
        _nextButton != null ? _nextPressed : elapsed >= _displayDuration;

    /// <summary>
    /// The fat can be disabled or destroyed mid-card (scene change, pooling).
    /// Without this the static slot would stay claimed by a dead card and every
    /// later fat would silently never show one — and now that the card pauses the
    /// game and steals the camera, dying mid-card would also strand both.
    /// </summary>
    private void OnDisable()
    {
        if (_activeCard != this) return;

        if (_playing)
        {
            Time.timeScale = _savedTimeScale;
            _cameraController?.ReturnToPlayer();

            if (_flashTarget != null)
            {
                Color c = _flashTarget.color;
                c.a = 1f;
                _flashTarget.color = c;
            }
        }

        // Nothing was saved, so hand the claim back: after a scene reload a fat
        // should be able to show the card the player never got to read.
        if (_playing) _cardClaimed = false;

        _activeCard = null;
        _playing    = false;

        if (fatIntroPanel != null) fatIntroPanel.SetActive(false);
        if (_nextButton   != null) _nextButton.gameObject.SetActive(false);
    }
}
