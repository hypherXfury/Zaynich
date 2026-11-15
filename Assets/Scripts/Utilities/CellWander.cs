using UnityEngine;

/// <summary>
/// Moves a decorative blood cell downward continuously within camera bounds.
/// When the cell exits the bottom of the camera it wraps to the top,
/// but only at an X position that falls inside a registered SpawnZone.
/// Supports BloodFlowManager surge mechanic.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CellWander : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private float _baseSpeed    = 0.5f;
    [SerializeField] private float _randomSpeedMin = 0.6f;
    [SerializeField] private float _randomSpeedMax = 1.4f;
    [SerializeField] private float _maxSpeed     = 4f;

    [Header("Wrap")]
    [Tooltip("Extra units beyond camera edge before the wrap triggers.")]
    [SerializeField] private float _wrapBuffer   = 0.5f;
    [Tooltip("Max attempts to find a zone-valid X position on wrap.")]
    [SerializeField] private int   _wrapAttempts = 20;

    [Header("Subtle Wander")]
    [SerializeField] private float _wanderStrength = 0.04f;
    [SerializeField] private float _wanderInterval  = 2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Rigidbody2D  _rb;
    private Camera       _cam;
    private SpawnZone[]  _zones;
    private float        _individualMultiplier;
    private float        _surgeMultiplier = 1f;
    private float        _wanderTimer;
    private float        _wanderX;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
    }

    private void Start()
    {
        _cam                  = Camera.main;
        _zones                = Object.FindObjectsByType<SpawnZone>(FindObjectsSortMode.None);
        _individualMultiplier = Random.Range(_randomSpeedMin, _randomSpeedMax);
        _wanderTimer          = Random.Range(0f, _wanderInterval);
    }

    public void SetSurging(bool surging, float multiplier)
        => _surgeMultiplier = surging ? multiplier : 1f;

    private void FixedUpdate()
    {
        // Wander nudge
        _wanderTimer -= Time.fixedDeltaTime;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = _wanderInterval + Random.Range(-0.5f, 0.5f);
            _wanderX     = Random.Range(-_wanderStrength, _wanderStrength);
        }

        // Velocity
        float speed        = Mathf.Min(_baseSpeed * _individualMultiplier * _surgeMultiplier, _maxSpeed);
        _rb.linearVelocity = new Vector2(_wanderX, -speed);

        // Wrap
        WrapIfOffscreen();
    }

    // ── Wrap ──────────────────────────────────────────────────────────────────

    private void WrapIfOffscreen()
    {
        if (_cam == null) return;

        // Camera edges this frame — recomputed every call so the wrap follows
        // the camera no matter which direction the player moves it.
        float halfH     = _cam.orthographicSize;
        float halfW     = halfH * _cam.aspect;
        float camX      = _cam.transform.position.x;
        float camY      = _cam.transform.position.y;
        float camBottom = camY - halfH;
        float camTop    = camY + halfH;
        float camLeft   = camX - halfW;
        float camRight  = camX + halfW;

        Vector2 pos = _rb.position;

        // ── Vertical wrap (primary flow axis) ────────────────────────────────
        // Wrap on BOTH edges: the cell can leave the top when the camera moves
        // down faster than the flow, or the bottom in normal downward flow.
        if (pos.y < camBottom - _wrapBuffer)
        {
            pos.y = camTop + _wrapBuffer;
            pos.x = GetZoneValidX(camLeft, camRight, pos.y);
            _rb.position = pos;
            return;
        }
        if (pos.y > camTop + _wrapBuffer)
        {
            pos.y = camBottom - _wrapBuffer;
            pos.x = GetZoneValidX(camLeft, camRight, pos.y);
            _rb.position = pos;
            return;
        }

        // ── Horizontal wrap (handles sideways camera movement) ───────────────
        if (pos.x < camLeft - _wrapBuffer)
        {
            pos.x = camRight + _wrapBuffer;
            pos.y = GetZoneValidY(camBottom, camTop, pos.x);
            _rb.position = pos;
            return;
        }
        if (pos.x > camRight + _wrapBuffer)
        {
            pos.x = camLeft - _wrapBuffer;
            pos.y = GetZoneValidY(camBottom, camTop, pos.x);
            _rb.position = pos;
        }
    }

    /// <summary>
    /// Picks a random X inside the camera width that also lies within at least
    /// one SpawnZone at the given Y. Falls back to a random camera X if no zone
    /// match is found (e.g. the camera has moved off all zones).
    /// </summary>
    private float GetZoneValidX(float camLeft, float camRight, float atY)
    {
        if (_zones == null || _zones.Length == 0)
            return Random.Range(camLeft, camRight);

        for (int i = 0; i < _wrapAttempts; i++)
        {
            float x         = Random.Range(camLeft, camRight);
            Vector2 testPos = new Vector2(x, atY);

            foreach (var zone in _zones)
            {
                if (zone != null && zone.ContainsWorldPoint(testPos))
                    return x;
            }
        }

        // Fallback — no zone overlap found, use random camera X
        return Random.Range(camLeft, camRight);
    }

    /// <summary>
    /// Picks a random Y inside the camera height that also lies within at least
    /// one SpawnZone at the given X. Falls back to a random camera Y if no zone
    /// match is found.
    /// </summary>
    private float GetZoneValidY(float camBottom, float camTop, float atX)
    {
        if (_zones == null || _zones.Length == 0)
            return Random.Range(camBottom, camTop);

        for (int i = 0; i < _wrapAttempts; i++)
        {
            float y         = Random.Range(camBottom, camTop);
            Vector2 testPos = new Vector2(atX, y);

            foreach (var zone in _zones)
            {
                if (zone != null && zone.ContainsWorldPoint(testPos))
                    return y;
            }
        }

        // Fallback — no zone overlap found, use random camera Y
        return Random.Range(camBottom, camTop);
    }
}