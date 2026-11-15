using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes a UI panel behave like a normal Android form when the soft keyboard is
/// up: the panel slides up out of the keyboard's way and settles back down when
/// it closes, instead of the keyboard covering the field being typed into.
///
/// Two separate things are fixed here, and both matter on Android:
///
///   1. THE FLOATING INPUT BAR. By default Unity puts a native text box above the
///      soft keyboard and mirrors typing into it, which reads as a stray floating
///      bar over the game. Setting shouldHideMobileInput on every TMP_InputField
///      turns that off, so text goes straight into the in-game field.
///
///   2. THE COVERED FIELD. Unity does not reflow the canvas for the keyboard, so
///      a field near the bottom just disappears behind it. This shifts the panel
///      up by however much is needed to clear the keyboard (plus _padding), then
///      eases it back on close.
///
/// The shift is measured against the FOCUSED field when there is one, so the row
/// being typed into is what's guaranteed to be visible — the same behaviour a
/// native form has. With nothing focused it falls back to the panel's own bottom
/// edge, and it never lifts the panel so far that its top leaves the screen.
///
/// SETUP: add to the panel and leave everything empty — the target rect, canvas
/// and input fields all resolve themselves. UserDetailsPanel adds and configures
/// one automatically, so that panel needs nothing at all.
/// </summary>
public class KeyboardAwarePanel : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The rect that slides up. Left empty = this GameObject's own RectTransform. " +
             "Point this at the group holding the WHOLE form — fields, labels and icons " +
             "together — rather than at a root that also holds full-screen art. Too high " +
             "and the shift drags a background off the screen; too low and only part of " +
             "the form moves, leaving the rows out of line with their labels.")]
    [SerializeField] private RectTransform _target;

    [Tooltip("Canvas the target lives under. Left empty = found in the parents.")]
    [SerializeField] private Canvas _canvas;

    [Header("Behaviour")]
    [Tooltip("Extra gap (screen pixels) left between the keyboard and the focused field.")]
    [SerializeField] private float _padding = 48f;

    [Tooltip("Higher = snappier slide. This is an exponential ease, not a duration.")]
    [SerializeField] private float _slideSpeed = 14f;

    [Tooltip("Used only when the device reports the keyboard as open but gives no size: " +
             "assume it covers this fraction of the screen.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float _fallbackKeyboardFraction = 0.45f;

    [Header("Input Fields")]
    [Tooltip("Fields to strip Unity's floating native input bar from. Left empty = every " +
             "TMP_InputField under this panel, including inactive ones.")]
    [SerializeField] private TMP_InputField[] _inputFields;

    [Tooltip("Turn off only if you actually want Unity's native input bar above the keyboard.")]
    [SerializeField] private bool _hideNativeInputBar = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private Vector2       _restAnchoredPos;
    private RectTransform _restTarget;   // the rect _restAnchoredPos was read from
    private float         _currentOffset; // canvas units currently applied
    private Camera        _uiCamera;

    private readonly Vector3[] _corners = new Vector3[4];

    // JNI is only consulted when TouchScreenKeyboard.area comes back empty, and
    // then only a few times a second — it allocates, and this runs every frame.
    private const float NativeQueryInterval = 0.2f;
    private float _nextNativeQuery;
    private float _cachedNativeHeight;

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the panel from code, for callers that add this component at
    /// runtime. Safe to call after Awake — AddComponent runs Awake immediately,
    /// so this re-resolves everything with the values just handed in.
    /// </summary>
    public void Configure(RectTransform target, TMP_InputField[] inputFields)
    {
        _target      = target;
        _inputFields = inputFields;
        Initialize();

        // The rest position belongs to whichever rect we're moving. AddComponent
        // runs Awake AND OnEnable before this call, so one was already captured
        // from the fallback target (this GameObject's own rect). Keeping it would
        // mean writing the root's position onto the form box — which reads as the
        // box snapping to the canvas origin.
        CaptureRest();
    }

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (_target == null) _target = GetComponent<RectTransform>();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        if (_inputFields == null || _inputFields.Length == 0)
            _inputFields = GetComponentsInChildren<TMP_InputField>(true);

        if (_canvas != null)
            _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        ApplyNativeInputBarSetting();
    }

    private void OnEnable()
    {
        // Captured here rather than in Awake so a panel that's repositioned while
        // hidden still slides from wherever it actually rests when shown.
        CaptureRest();
    }

    private void OnDisable()
    {
        // Don't leave the panel parked mid-slide for the next time it's shown.
        // Only when we actually moved it, and only when the stored position came
        // from the rect we're about to write to — never restore a position
        // captured from some other rect.
        if (_target != null && _restTarget == _target && _currentOffset != 0f)
            _target.anchoredPosition = _restAnchoredPos;

        _currentOffset = 0f;
    }

    /// <summary>
    /// Records where the current target rests, so every shift is an offset from
    /// the position the designer authored rather than an absolute move.
    /// </summary>
    private void CaptureRest()
    {
        if (_target == null) return;

        _restTarget      = _target;
        _restAnchoredPos = _target.anchoredPosition;
        _currentOffset   = 0f;
    }

    private void ApplyNativeInputBarSetting()
    {
        if (!_hideNativeInputBar || _inputFields == null) return;

        foreach (TMP_InputField field in _inputFields)
        {
            if (field == null) continue;

            // Hides the native overlay text box only — the soft keyboard itself
            // still opens, which is the whole point.
            field.shouldHideMobileInput = true;
        }
    }

    // ── Slide ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_target == null || _canvas == null) return;

        // Guards against moving a rect whose rest position we never recorded —
        // that would treat the stored offset as absolute and teleport it.
        if (_restTarget != _target) CaptureRest();

        float targetOffset = CalculateTargetOffset();

        // Frame-rate independent ease, on unscaled time so it still animates if
        // the game is paused behind this panel.
        float t = 1f - Mathf.Exp(-_slideSpeed * Time.unscaledDeltaTime);
        _currentOffset = Mathf.Lerp(_currentOffset, targetOffset, t);

        if (Mathf.Abs(_currentOffset - targetOffset) < 0.5f) _currentOffset = targetOffset;

        _target.anchoredPosition = _restAnchoredPos + Vector2.up * _currentOffset;
    }

    /// <summary>
    /// How far (in canvas units) the panel has to rise for the focused field to
    /// clear the keyboard. Zero when the keyboard is closed or already clear.
    /// </summary>
    private float CalculateTargetOffset()
    {
        float keyboardTop = GetKeyboardHeightPixels();
        if (keyboardTop <= 0f) return 0f;

        RectTransform focus = GetFocusedRect();

        // The panel is measured at rest, not where it currently sits — otherwise
        // each frame's shift would feed into the next one and the panel would
        // climb off the top of the screen.
        float bottomAtRest = GetBottomScreenY(focus) - PixelsFromUnits(_currentOffset);

        float needed = (keyboardTop + _padding) - bottomAtRest;
        if (needed <= 0f) return 0f;

        // Never lift the focused row off the top of the screen. Measured on the
        // row rather than the whole target on purpose: the target is usually a
        // group rect stretched to the full canvas, whose top edge already sits at
        // the top of the screen — clamping against that would compute zero
        // headroom every frame and the panel would never move at all.
        float topAtRest  = GetTopScreenY(focus) - PixelsFromUnits(_currentOffset);
        float headroom   = Mathf.Max(0f, Screen.height - topAtRest);

        return UnitsFromPixels(Mathf.Min(needed, headroom));
    }

    /// <summary>
    /// The field the player is typing into, when it belongs to this panel — the
    /// row that actually has to stay visible. Falls back to the whole panel.
    /// </summary>
    private RectTransform GetFocusedRect()
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        if (selected == null) return _target;

        TMP_InputField field = selected.GetComponent<TMP_InputField>();
        if (field == null) return _target;

        return field.transform.IsChildOf(_target) ? (RectTransform)field.transform : _target;
    }

    private float GetBottomScreenY(RectTransform rect)
    {
        rect.GetWorldCorners(_corners);
        return RectTransformUtility.WorldToScreenPoint(_uiCamera, _corners[0]).y;   // bottom-left
    }

    private float GetTopScreenY(RectTransform rect)
    {
        rect.GetWorldCorners(_corners);
        return RectTransformUtility.WorldToScreenPoint(_uiCamera, _corners[1]).y;   // top-left
    }

    private float PixelsFromUnits(float units)  => units * _canvas.scaleFactor;
    private float UnitsFromPixels(float pixels) => pixels / Mathf.Max(_canvas.scaleFactor, 0.0001f);

    // ── Keyboard measurement ──────────────────────────────────────────────────

    /// <summary>
    /// Height of the soft keyboard in screen pixels, 0 when it's closed. Prefers
    /// TouchScreenKeyboard.area; some Android devices report that as empty even
    /// with the keyboard up, so a native window measurement backs it up, and a
    /// flat fraction of the screen backs THAT up.
    /// </summary>
    private float GetKeyboardHeightPixels()
    {
        if (!TouchScreenKeyboard.visible) return 0f;

        float area = TouchScreenKeyboard.area.height;
        if (area > 1f) return area;

        float native = GetNativeKeyboardHeight();
        if (native > 1f) return native;

        return Screen.height * _fallbackKeyboardFraction;
    }

    private float GetNativeKeyboardHeight()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Time.unscaledTime < _nextNativeQuery) return _cachedNativeHeight;
        _nextNativeQuery = Time.unscaledTime + NativeQueryInterval;

        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var view = activity.Call<AndroidJavaObject>("findViewById", 0x01020002))   // android.R.id.content
            using (var rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                view.Call("getWindowVisibleDisplayFrame", rect);
                int visibleHeight = rect.Call<int>("height");
                _cachedNativeHeight = Mathf.Max(0f, Screen.height - visibleHeight);
            }
        }
        catch (System.Exception e)
        {
            // Activity shape differs between Unity's Android backends; if the
            // view can't be reached we simply stay on the fraction fallback.
            Debug.LogWarning($"[KeyboardAwarePanel] Native keyboard height unavailable: {e.Message}");
            _cachedNativeHeight = 0f;
        }

        return _cachedNativeHeight;
#else
        return 0f;
#endif
    }
}
