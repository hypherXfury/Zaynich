using System.Collections;
using UnityEngine;

/// <summary>
/// Expanding shockwave ring spawned by JumperEnemy on landing.
/// Uses a LineRenderer for visuals (fully visible in Game camera).
///
/// DAMAGE MODEL — two separate sources, both configurable:
///
///   1. RING SWEEP DAMAGE  — deals damage once when the expanding ring band
///      passes through the player. Reliable at any expansion speed because we
///      track the radius from the previous frame and check if the player was
///      "crossed over" rather than relying on the band thickness window.
///
///   2. DIRECT LANDING DAMAGE — the JumperEnemy itself deals contact damage
///      via the base Enemy component (OnTriggerStay2D). That is separate and
///      always active; this script does not control it.
///
/// PREFAB SETUP:
///   • Empty GameObject + this component. Nothing else needed.
///   • The LineRenderer is created at runtime automatically.
/// </summary>
public class ShockwaveRing : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Color _ringColor    = new Color(1f, 0.55f, 0.05f, 1f);
    [SerializeField] private float _lineWidth    = 0.18f;
    [SerializeField] private int   _segments     = 48;
    [SerializeField] private int   _sortingOrder = 10;
    [SerializeField] private Material _lineMaterial = null;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player when the ring sweeps over them. Set to 0 to disable ring damage.")]
    [SerializeField] private float _ringDamage = 15f;

    [Tooltip("Extra world-unit tolerance added to the sweep check. Increase if the ring is\n" +
             "visually passing through the player but no damage is registered.")]
    [SerializeField] private float _sweepTolerance = 0.25f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private float     _maxRadius;
    private float     _duration;
    private Transform _player;

    private LineRenderer _lr;
    private float        _elapsed;
    private bool         _hasDealtDamage;
    private float        _currentRadius;
    private float        _previousRadius;   // radius last frame — used for sweep check

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by JumperEnemy immediately after Instantiate.
    /// thickness parameter kept for API compatibility but detection no longer
    /// depends on it — sweep-crossing is used instead.
    /// </summary>
    public void Launch(float maxRadius, float thickness, float damage, float duration, Transform player)
    {
        _maxRadius = maxRadius;
        _ringDamage = damage;
        _duration  = duration;
        _player    = player;

        BuildLineRenderer();
        StartCoroutine(Expand());
    }

    // ── LineRenderer setup ────────────────────────────────────────────────────

    private void BuildLineRenderer()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.material      = _lineMaterial != null
            ? _lineMaterial
            : new Material(Shader.Find("Sprites/Default"));
        _lr.startColor    = _ringColor;
        _lr.endColor      = _ringColor;
        _lr.startWidth    = _lineWidth;
        _lr.endWidth      = _lineWidth;
        _lr.positionCount = _segments + 1;
        _lr.useWorldSpace = true;
        _lr.sortingOrder  = _sortingOrder;
        UpdateLinePositions(0f);
    }

    private void UpdateLinePositions(float radius)
    {
        if (_lr == null) return;
        Vector3 origin = transform.position;
        for (int i = 0; i <= _segments; i++)
        {
            float angle = (float)i / _segments * Mathf.PI * 2f;
            _lr.SetPosition(i, new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                origin.y + Mathf.Sin(angle) * radius,
                origin.z));
        }
    }

    // ── Expansion coroutine ───────────────────────────────────────────────────

    private IEnumerator Expand()
    {
        _previousRadius = 0f;
        _currentRadius  = 0f;

        while (_elapsed < _duration)
        {
            _elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(_elapsed / _duration);

            _previousRadius = _currentRadius;
            _currentRadius  = Mathf.Lerp(0f, _maxRadius, t);

            float alpha    = Mathf.Lerp(1f, 0f, t);
            Color c        = _ringColor;
            c.a            = alpha;
            _lr.startColor = c;
            _lr.endColor   = c;

            float w        = Mathf.Lerp(_lineWidth, _lineWidth * 0.3f, t);
            _lr.startWidth = w;
            _lr.endWidth   = w;

            UpdateLinePositions(_currentRadius);
            CheckSweepDamage();

            yield return null;
        }

        Destroy(gameObject);
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sweep-crossing check: did the ring radius cross over the player's distance
    /// from the origin between last frame and this frame?
    ///
    /// This is frame-rate independent — it doesn't matter how fast the ring
    /// expands or how thin the visual band is. If the player's distance falls
    /// anywhere in [previousRadius - tolerance, currentRadius + tolerance]
    /// the hit registers.
    /// </summary>
    private void CheckSweepDamage()
    {
        if (_hasDealtDamage || _player == null || _ringDamage <= 0f) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        float bandInner = _previousRadius - _sweepTolerance;
        float bandOuter = _currentRadius  + _sweepTolerance;

        if (dist >= bandInner && dist <= bandOuter)
        {
            _hasDealtDamage = true;
            _player.GetComponent<PlayerHealth>()?.TakeDamage(_ringDamage);
        }
    }
}