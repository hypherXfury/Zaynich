using UnityEngine;

/// <summary>
/// Camera controller with a deadzone boundary.
/// The camera stays completely still while the player is inside the deadzone.
/// Once the player walks outside the boundary, the camera smoothly catches up
/// until the player is back within the bounds — then stops again.
///
/// Two layered noise effects are preserved:
///   - Wobble : slow, gentle Perlin drift that is always active.
///   - Shake  : faster Perlin rumble that scales with the player's current speed.
/// </summary>
public class CameraController : MonoBehaviour
{
    private const float DefaultFollowSmoothTime = 0.12f;
    private const float DefaultWobbleAmplitude  = 0.035f;
    private const float DefaultWobbleFrequency  = 0.6f;
    private const float DefaultShakeAmplitude   = 0.07f;
    private const float DefaultShakeFrequency   = 4.5f;
    private const float DefaultMaxSpeed         = 5f;
    private const float DefaultShakeBlendSpeed  = 5f;

    [Header("Follow")]
    [SerializeField] private Transform _target;
    [SerializeField] private float _followSmoothTime = DefaultFollowSmoothTime;

    [Header("Deadzone")]
    [Tooltip("Half-width of the deadzone in world units. Camera won't move horizontally " +
             "while the player stays within this distance of the camera centre.")]
    [SerializeField] private float _deadzoneX = 1.5f;

    [Tooltip("Half-height of the deadzone in world units. Camera won't move vertically " +
             "while the player stays within this distance of the camera centre.")]
    [SerializeField] private float _deadzoneY = 1.0f;

    [Tooltip("Draw the deadzone rectangle in the Scene view so you can tune it visually.")]
    [SerializeField] private bool _showDeadzoneGizmo = true;

    [Header("Wobble — always active")]
    [SerializeField] private float _wobbleAmplitude = DefaultWobbleAmplitude;
    [SerializeField] private float _wobbleFrequency = DefaultWobbleFrequency;

    [Header("Shake — scales with movement speed")]
    [SerializeField] private float _shakeAmplitude  = DefaultShakeAmplitude;
    [SerializeField] private float _shakeFrequency  = DefaultShakeFrequency;
    [SerializeField] private float _maxSpeed        = DefaultMaxSpeed;
    [SerializeField] private float _shakeBlendSpeed = DefaultShakeBlendSpeed;

    [Header("References")]
    [SerializeField] private TopDownCharacterController _characterController;

    [Header("Map Bounds")]
    [Tooltip("The BoxCollider2D placed around the map. Camera anchor is clamped inside it.")]
    [SerializeField] private BoxCollider2D _mapBounds;

    private Vector3 _followVelocity;
    private float   _noiseTime;
    private float   _seedWobbleX;
    private float   _seedWobbleY;
    private float   _seedShakeX;
    private float   _seedShakeY;
    private float   _currentShakeIntensity;

    // Transient impact shake (triggered by Shake()).
    private float _traumaTimer;
    private float _traumaDuration;
    private float _traumaMagnitude;

    // The camera's logical anchor position (without noise offset applied).
    // We track this separately so noise doesn't accumulate into the follow position.
    private Vector3 _anchorPosition;

    // ── Chamber lock (bounded-follow arenas) ───────────────────────────────────
    private float       _defaultOrthoSize;
    private BoxCollider2D _defaultMapBounds;

    private void Awake()
    {
        _seedWobbleX    = Random.Range(0f,   100f);
        _seedWobbleY    = Random.Range(100f, 200f);
        _seedShakeX     = Random.Range(200f, 300f);
        _seedShakeY     = Random.Range(300f, 400f);

        // Initialise anchor at current camera position
        _anchorPosition = transform.position;

        Camera cam = Camera.main;
        if (cam != null) _defaultOrthoSize = cam.orthographicSize;

        _defaultMapBounds = _mapBounds;
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        if (_focusMode != FocusMode.None) UpdateFocusAnchor();
        else                              UpdateAnchor();

        ApplyNoise();
    }

    // ── Focus API (tutorial camera pans) ──────────────────────────────────────

    private enum FocusMode { None, OnPoint, ReturningToPlayer }

    [Header("Focus Pan")]
    [Tooltip("SmoothDamp time for tutorial camera pans — how long it takes to " +
             "settle on a nest/chamber and to come back to the player.")]
    [SerializeField] private float _focusSmoothTime = 0.45f;

    private FocusMode _focusMode = FocusMode.None;
    private Transform _focusTarget;
    private Vector3   _focusVelocity;

    /// <summary>
    /// True while the camera has settled on its focus target (or back on the
    /// player after <see cref="ReturnToPlayer"/>). Coroutines wait on this
    /// before showing a card, so the card lands on a steady shot.
    /// </summary>
    public bool FocusSettled { get; private set; }

    /// <summary>
    /// Pans the camera off the player and onto <paramref name="target"/>, ignoring
    /// the deadzone, and keeps it there until <see cref="ReturnToPlayer"/>. The
    /// player is untouched — they stay exactly where they are, off-screen if need
    /// be. Used to introduce nests (Level 1) and chambers (Level 2).
    ///
    /// The map-bounds clamp still applies, so a target very close to the map edge
    /// frames off-centre rather than dead-centre.
    /// </summary>
    public void FocusOn(Transform target)
    {
        if (target == null) return;

        _focusTarget   = target;
        _focusMode     = FocusMode.OnPoint;
        _focusVelocity = Vector3.zero;
        FocusSettled   = false;
    }

    /// <summary>
    /// Pans back to the player and hands control to the normal deadzone follow
    /// once it arrives. Safe to call when no focus is active.
    /// </summary>
    public void ReturnToPlayer()
    {
        if (_focusMode == FocusMode.None) return;

        _focusTarget   = null;
        _focusMode     = FocusMode.ReturningToPlayer;
        _focusVelocity = Vector3.zero;
        FocusSettled   = false;
    }

    /// <summary>
    /// Drives the anchor straight at the focus point (or back at the player),
    /// bypassing the deadzone entirely — a pan should centre its subject, not
    /// stop as soon as it's nominally in frame.
    /// </summary>
    private void UpdateFocusAnchor()
    {
        Transform goalTransform = _focusMode == FocusMode.OnPoint ? _focusTarget : _target;

        // Focus target destroyed mid-pan (e.g. the nest died) — go home rather
        // than stranding the camera out in the map.
        if (goalTransform == null)
        {
            if (_focusMode == FocusMode.OnPoint) { ReturnToPlayer(); return; }
            _focusMode = FocusMode.None;
            return;
        }

        Vector3 goal = new Vector3(goalTransform.position.x, goalTransform.position.y, _anchorPosition.z);

        // Unscaled: the tutorial freezes the game (timeScale 0) for nest and
        // chamber intros so nothing moves while the player reads — but the pan
        // itself is the point of those intros, so it has to keep running.
        _anchorPosition = Vector3.SmoothDamp(
            _anchorPosition, goal, ref _focusVelocity, _focusSmoothTime,
            Mathf.Infinity, Time.unscaledDeltaTime);

        ClampAnchorToBounds();

        transform.position = new Vector3(
            _anchorPosition.x,
            _anchorPosition.y,
            transform.position.z);

        // "Arrived" is measured against the clamped goal, so a target pressed up
        // against the map edge still counts as settled instead of hanging forever.
        Vector3 clampedGoal = goal;
        ClampToBounds(ref clampedGoal);

        if ((_anchorPosition - clampedGoal).sqrMagnitude < 0.01f)
        {
            FocusSettled = true;

            // Back on the player — resume normal deadzone following. The follow
            // velocity is zeroed so the handover doesn't inherit the pan's speed.
            if (_focusMode == FocusMode.ReturningToPlayer)
            {
                _focusMode      = FocusMode.None;
                _followVelocity = Vector3.zero;
            }
        }
    }

    // ── Chamber lock API ──────────────────────────────────────────────────────

    /// <summary>
    /// Swaps the camera's clamp bounds to a chamber's arena collider and applies
    /// the chamber's framing size — used by Chamber for its arena fights. The
    /// camera keeps following the player with the normal deadzone behaviour, it
    /// just can't leave the arena until <see cref="Unlock"/> is called.
    /// </summary>
    public void EnterChamberBounds(BoxCollider2D arenaBounds, float orthographicSize)
    {
        _mapBounds = arenaBounds;

        Camera cam = Camera.main;
        if (cam == null) return;

        cam.orthographicSize = FitSizeToWidth(arenaBounds, orthographicSize, cam.aspect);
    }

    /// <summary>
    /// The authored framing size, tightened just enough that the camera never sees
    /// wider than the arena.
    ///
    /// The authored size assumes a screen shape. A 20:9 phone sees ~25% wider than
    /// a 16:9 one at the same orthographic size, which is enough to overshoot an
    /// arena that frames perfectly in the editor — and once the view is wider than
    /// its own clamp bounds, there is no valid camera position left (see
    /// <see cref="ClampAxis"/>). Zooming in by that margin keeps the arena filling
    /// the screen on tall phones instead of showing the void around its edges.
    /// </summary>
    private static float FitSizeToWidth(BoxCollider2D bounds, float orthographicSize, float aspect)
    {
        if (bounds == null || aspect <= 0f) return orthographicSize;

        // Measured from the collider's own size rather than its world bounds:
        // Chamber activates the arena and calls this in the same frame, and a
        // collider's bounds aren't refreshed until the next physics step — a zero
        // read there would set the camera's size to zero and blank the screen.
        float width = bounds.size.x * Mathf.Abs(bounds.transform.lossyScale.x);
        if (width <= 0.01f) return orthographicSize;

        return Mathf.Min(orthographicSize, width / (2f * aspect));
    }

    /// <summary>
    /// Restores the normal map bounds and framing size after a chamber fight.
    /// </summary>
    public void Unlock()
    {
        _mapBounds = _defaultMapBounds;

        Camera cam = Camera.main;
        if (cam != null) cam.orthographicSize = _defaultOrthoSize;
    }

    // ── Deadzone follow ───────────────────────────────────────────────────────

    private void UpdateAnchor()
    {
        // Offset of the player relative to the camera anchor (ignore Z)
        float offsetX = _target.position.x - _anchorPosition.x;
        float offsetY = _target.position.y - _anchorPosition.y;

        // How far outside the deadzone is the player on each axis?
        // Zero while inside, positive when outside.
        float pushX = 0f;
        float pushY = 0f;

        if (offsetX >  _deadzoneX) pushX = offsetX - _deadzoneX;
        if (offsetX < -_deadzoneX) pushX = offsetX + _deadzoneX;
        if (offsetY >  _deadzoneY) pushY = offsetY - _deadzoneY;
        if (offsetY < -_deadzoneY) pushY = offsetY + _deadzoneY;

        // Only move the anchor when the player is outside the deadzone
        if (Mathf.Abs(pushX) > 0f || Mathf.Abs(pushY) > 0f)
        {
            Vector3 goal = _anchorPosition + new Vector3(pushX, pushY, 0f);
            _anchorPosition = Vector3.SmoothDamp(
                _anchorPosition, goal, ref _followVelocity, _followSmoothTime);
        }
        else
        {
            // Inside deadzone — bleed follow velocity to zero so there's no drift
            // when the player stops just after re-entering the boundary.
            _followVelocity = Vector3.MoveTowards(
                _followVelocity, Vector3.zero, Time.deltaTime * 20f);
        }

        // Clamp anchor inside map bounds so camera never shows outside the level
        ClampAnchorToBounds();

        // Write anchor back (preserve camera Z)
        transform.position = new Vector3(
            _anchorPosition.x,
            _anchorPosition.y,
            transform.position.z);
    }

    private void ClampAnchorToBounds()
    {
        Vector3 clamped = _anchorPosition;
        ClampToBounds(ref clamped);
        _anchorPosition = clamped;
    }

    /// <summary>
    /// Keeps a camera anchor inside the map bounds shrunk by the camera's own
    /// half-extents, so the view never shows outside the level. Shared by the
    /// deadzone follow and the focus pan.
    /// </summary>
    private void ClampToBounds(ref Vector3 position)
    {
        if (_mapBounds == null) return;

        // Calculate how much of the world the camera sees on each axis
        Camera cam   = Camera.main;
        float  halfH = cam != null ? cam.orthographicSize             : 0f;
        float  halfW = cam != null ? cam.orthographicSize * cam.aspect : 0f;

        Bounds b = _mapBounds.bounds;

        position.x = ClampAxis(position.x, b.min.x + halfW, b.max.x - halfW, b.center.x);
        position.y = ClampAxis(position.y, b.min.y + halfH, b.max.y - halfH, b.center.y);
    }

    /// <summary>
    /// Clamps one axis between the shrunk bounds, centring instead when the camera
    /// sees more than the bounds contain.
    ///
    /// That case is not hypothetical: halfW scales with the screen's aspect ratio,
    /// so an arena that comfortably contains the view on a 16:9 phone is narrower
    /// than it on a 20:9 one. There is then no position satisfying both edges, and
    /// min lands above max. Mathf.Clamp handles that by returning min for anything
    /// below min and max for anything above — so the anchor flips between the two
    /// edges from frame to frame, which reads on screen as the camera shaking.
    /// Centring shows the whole area evenly and holds still, which is what a view
    /// wider than its bounds should do.
    /// </summary>
    private static float ClampAxis(float value, float min, float max, float centre)
    {
        const float Inset = 0.05f;

        min += Inset;
        max -= Inset;

        if (min >= max) return centre;

        return Mathf.Clamp(value, min, max);
    }

    // ── Noise (wobble + shake) ────────────────────────────────────────────────

    private void ApplyNoise()
    {
        // A focus pan runs through a paused game, where Time.deltaTime is 0 —
        // without unscaled time the wobble would freeze solid and the intro shot
        // would look like a screenshot rather than a live camera.
        float dt = _focusMode != FocusMode.None ? Time.unscaledDeltaTime : Time.deltaTime;

        _noiseTime += dt;

        float speedNormalised = _characterController != null
            ? Mathf.Clamp01(_characterController.Speed / _maxSpeed)
            : 0f;

        _currentShakeIntensity = Mathf.MoveTowards(
            _currentShakeIntensity, speedNormalised, _shakeBlendSpeed * dt);

        float wx = Noise(_noiseTime * _wobbleFrequency + _seedWobbleX, _seedWobbleX) * _wobbleAmplitude;
        float wy = Noise(_seedWobbleY, _noiseTime * _wobbleFrequency + _seedWobbleY) * _wobbleAmplitude;

        float sx = Noise(_noiseTime * _shakeFrequency + _seedShakeX, _seedShakeX) * _shakeAmplitude * _currentShakeIntensity;
        float sy = Noise(_seedShakeY, _noiseTime * _shakeFrequency + _seedShakeY) * _shakeAmplitude * _currentShakeIntensity;

        // Add noise on top of the anchor — never into _anchorPosition itself
        transform.position += new Vector3(wx + sx, wy + sy, 0f);

        ApplyImpactShake();
    }

    // ── Impact shake (player damage) ──────────────────────────────────────────

    /// <summary>Triggers a transient camera shake, e.g. when the player takes damage.</summary>
    public void Shake(float magnitude = 0.25f, float duration = 0.2f)
    {
        // Restart with the stronger of the current and incoming shake so rapid hits stack sensibly.
        _traumaMagnitude = Mathf.Max(_traumaMagnitude, magnitude);
        _traumaDuration  = Mathf.Max(duration, 0.0001f);
        _traumaTimer     = _traumaDuration;
    }

    private void ApplyImpactShake()
    {
        if (_traumaTimer <= 0f) return;

        _traumaTimer -= Time.deltaTime;
        // Quadratic falloff feels punchier than linear.
        float falloff = Mathf.Clamp01(_traumaTimer / _traumaDuration);
        float amount  = _traumaMagnitude * falloff * falloff;

        float ox = (Random.value - 0.5f) * 2f * amount;
        float oy = (Random.value - 0.5f) * 2f * amount;
        transform.position += new Vector3(ox, oy, 0f);

        if (_traumaTimer <= 0f)
            _traumaMagnitude = 0f;
    }

    private static float Noise(float x, float y) => (Mathf.PerlinNoise(x, y) - 0.5f) * 2f;

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!_showDeadzoneGizmo) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        // Draw the deadzone rectangle centred on the camera anchor
        Vector3 centre = Application.isPlaying ? _anchorPosition : transform.position;
        centre.z = 0f;
        Gizmos.DrawWireCube(centre, new Vector3(_deadzoneX * 2f, _deadzoneY * 2f, 0f));
    }
}