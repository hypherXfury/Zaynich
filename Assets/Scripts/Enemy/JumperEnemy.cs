using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy that periodically leaps toward the player and, on landing,
/// releases an expanding shockwave ring that damages the player if caught in it.
///
/// FIXES over v1:
///   • Base Enemy.FixedUpdate was setting linearVelocity every frame, fighting MovePosition.
///     Solution: we zero the velocity each FixedUpdate while jumping, and disable Enemy
///     movement by checking IsJumping via a shared flag.
///   • Target is now re-sampled mid-air every frame up to a configurable lock-in point
///     (trackingCutoffNormalised), so the jump actively chases the player for the first
///     portion of the arc, then commits to the locked position — feels both threatening
///     and fair.
///   • _baseScale is now captured from Enemy.Initialize size AFTER the Enemy component
///     has scaled the transform, not in Awake (which runs before Initialize).
///
/// SETUP:
///   1. Add this component alongside Enemy on your prefab.
///   2. Assign shockwavePrefab — a prefab with a ShockwaveRing component.
///   3. In Enemy's FixedUpdate, add this guard at the top:
///         var jumper = GetComponent<JumperEnemy>();
///         if (jumper != null && jumper.IsJumping) return;
///      (or see the alternative approach in the summary comment at the bottom)
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class JumperEnemy : MonoBehaviour
{
    [Header("Jump Settings")]
    [Tooltip("How high (world units) the enemy arcs at the peak.")]
    [SerializeField] private float _jumpHeight = 3f;

    [Tooltip("Total seconds from takeoff to landing.")]
    [SerializeField] private float _jumpDuration = 0.75f;

    [Tooltip("Seconds on the ground before next jump.")]
    [SerializeField] private float _jumpCooldown = 1.6f;

    [Tooltip("Normalised time [0–1] in the jump arc where the target position locks in. " +
             "0 = locks immediately at takeoff (old behaviour). " +
             "0.4 = tracks player for the first 40 % of the arc then commits. " +
             "1 = tracks all the way to landing (homing).")]
    [Range(0f, 1f)]
    [SerializeField] private float _trackingCutoff = 0.38f;

    [Tooltip("Maximum world-unit distance covered by a single hop. The enemy " +
             "advances toward the player in hops of at most this length instead " +
             "of leaping the whole distance at once. Only when the player is " +
             "within one hop does it land beside them.")]
    [SerializeField] private float _maxHopDistance = 2.5f;

    [Tooltip("World-unit offset from the player's centre where the enemy lands. " +
             "Prevents the enemy from overlapping the player's centre. " +
             "Set this to roughly (enemy radius + player radius).")]
    [SerializeField] private float _landingOffset = 0.35f;

    [Tooltip("Show a warning shadow on the ground where the enemy will land, " +
             "starting from the moment the target locks in.")]
    [SerializeField] private bool  _showLandingShadow = true;
    [SerializeField] private float _shadowRadius      = 0.45f;

    [Header("Shockwave Settings")]
    [SerializeField] private GameObject _shockwavePrefab;
    [SerializeField] private float _shockwaveRadius    = 3.5f;
    [SerializeField] private float _shockwaveThickness = 0.8f;
    [SerializeField] private float _shockwaveDamage    = 15f;
    [SerializeField] private float _shockwaveDuration  = 0.5f;

    // ── State ─────────────────────────────────────────────────────────────────

    private Rigidbody2D    _rb;
    private Transform      _player;
    private Vector3        _baseScale;
    private bool           _isJumping;

    // Landing shadow (simple circle drawn via a child SpriteRenderer)
    private Transform      _shadowTransform;

    /// <summary>True while the enemy is airborne. Read by the base Enemy to suppress movement.</summary>
    public bool IsJumping => _isJumping;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Capture scale AFTER Enemy.Initialize has already applied EnemyData.size
        _baseScale = transform.localScale;

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        // We manage all movement ourselves — kill base velocity each physics step
        // The guard in Enemy.FixedUpdate also needs to check IsJumping (see summary)
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (_showLandingShadow)
            BuildShadow();

        StartCoroutine(JumpLoop());
    }

    private void FixedUpdate()
    {
        // While not jumping, let the base Enemy drive movement normally.
        // While jumping, zero out any residual velocity so Enemy.FixedUpdate
        // (which still runs) cannot overwrite our MovePosition.
        if (_isJumping)
            _rb.linearVelocity = Vector2.zero;
    }

    // ── Jump loop ─────────────────────────────────────────────────────────────

    private IEnumerator JumpLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(_jumpCooldown);
            if (_player == null) continue;
            yield return StartCoroutine(DoJump());
        }
    }

    private IEnumerator DoJump()
    {
        _isJumping = true;

        Vector2 startPos   = _rb.position;
        Vector2 targetPos  = _player.position;   // initial aim
        bool    targetLocked = false;

        SetShadowVisible(false);

        float elapsed = 0f;

        while (elapsed < _jumpDuration)
        {
            elapsed     += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / _jumpDuration);

            // ── Target tracking ──────────────────────────────────────────────
            if (!targetLocked)
            {
                if (t < _trackingCutoff)
                {
                    // Still tracking — update destination to player's live position
                    targetPos = _player.position;
                }
                else
                {
                    // Commit to a landing spot. Hop toward the player but cover at
                    // most _maxHopDistance so the enemy advances in hops rather
                    // than leaping the entire gap onto the player.
                    Vector2 playerPos = _player.position;
                    Vector2 toPlayer  = playerPos - startPos;
                    float   dist      = toPlayer.magnitude;

                    if (dist <= _maxHopDistance + _landingOffset)
                    {
                        // Close enough to finish beside the player — land on a
                        // random point on a small circle around them.
                        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle.normalized * _landingOffset;
                        targetPos = playerPos + randomOffset;
                    }
                    else
                    {
                        // Too far — hop _maxHopDistance toward the player.
                        Vector2 dir = dist > 0.0001f ? toPlayer / dist : UnityEngine.Random.insideUnitCircle.normalized;
                        targetPos   = startPos + dir * _maxHopDistance;
                    }

                    targetLocked = true;
                    SetShadowPosition(targetPos);
                    SetShadowVisible(true);
                }
            }

            // ── Arc movement ─────────────────────────────────────────────────
            // Remap t so horizontal position reflects only the committed portion
            // of the journey (from start → locked target).
            float tFlat = _trackingCutoff > 0f
                ? Mathf.Clamp01(t / 1f)   // simple 0→1
                : t;

            Vector2 flatPos = Vector2.Lerp(startPos, targetPos, tFlat);
            float   arc     = Mathf.Sin(t * Mathf.PI) * _jumpHeight;

            _rb.MovePosition(flatPos + Vector2.up * arc);

            // ── Squash & stretch ─────────────────────────────────────────────
            float sY = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;
            float sX = 1f - Mathf.Sin(t * Mathf.PI) * 0.15f;
            transform.localScale = new Vector3(
                _baseScale.x * sX,
                _baseScale.y * sY,
                _baseScale.z);

            yield return null;
        }

        // ── Landing ───────────────────────────────────────────────────────────
        _rb.MovePosition(targetPos);
        transform.localScale = _baseScale;
        SetShadowVisible(false);

        _isJumping = false;

        SpawnShockwave(targetPos);
        yield return StartCoroutine(LandingSquash());
    }

    private IEnumerator LandingSquash()
    {
        const float squashTime = 0.13f;
        float elapsed = 0f;
        while (elapsed < squashTime)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / squashTime;
            transform.localScale = new Vector3(
                _baseScale.x * Mathf.Lerp(1.4f, 1f, t),
                _baseScale.y * Mathf.Lerp(0.62f, 1f, t),
                _baseScale.z);
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    // ── Shockwave ─────────────────────────────────────────────────────────────

    private void SpawnShockwave(Vector2 position)
    {
        if (_shockwavePrefab == null)
        {
            Debug.LogWarning($"[JumperEnemy] shockwavePrefab not assigned on {name}.", this);
            return;
        }

        var go = Instantiate(_shockwavePrefab, position, Quaternion.identity);
        if (go.TryGetComponent<ShockwaveRing>(out var ring))
            ring.Launch(_shockwaveRadius, _shockwaveThickness, _shockwaveDamage, _shockwaveDuration, _player);
    }

    // ── Landing shadow ────────────────────────────────────────────────────────

    private void BuildShadow()
    {
        var go  = new GameObject("LandingShadow");
        go.transform.SetParent(null);   // world space — doesn't move with enemy

        var sr           = go.AddComponent<SpriteRenderer>();
        sr.sprite        = CreateCircleSprite();
        sr.color         = new Color(1f, 0.15f, 0.05f, 0.45f);
        sr.sortingOrder  = -1;

        go.transform.localScale = Vector3.one * _shadowRadius * 2f;
        go.SetActive(false);

        _shadowTransform = go.transform;

        // Destroy shadow when this enemy is destroyed
        var cleanup = go.AddComponent<ShadowCleanup>();
        cleanup.owner = this;
    }

    private void SetShadowPosition(Vector2 pos)
    {
        if (_shadowTransform != null)
            _shadowTransform.position = new Vector3(pos.x, pos.y, 0f);
    }

    private void SetShadowVisible(bool visible)
    {
        if (_shadowTransform != null)
            _shadowTransform.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (_shadowTransform != null)
            Destroy(_shadowTransform.gameObject);
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        float center = size * 0.5f;
        float r = center - 1f;
        for (int i = 0; i < pixels.Length; i++)
        {
            float x = i % size - center;
            float y = i / size - center;
            float dist = Mathf.Sqrt(x * x + y * y);
            float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, dist - r));
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

/// <summary>
/// Tiny helper that destroys the shadow GO if the owner Enemy is destroyed
/// while the shadow is detached in world space.
/// </summary>
internal class ShadowCleanup : MonoBehaviour
{
    public JumperEnemy owner;
    private void Update()
    {
        if (owner == null) Destroy(gameObject);
    }
}

/*
 * ── HOW TO SUPPRESS BASE ENEMY MOVEMENT WHILE JUMPING ───────────────────────
 *
 * The simplest approach is a one-line guard in Enemy.FixedUpdate():
 *
 *     private void FixedUpdate()
 *     {
 *         if (_player == null || _data == null) return;
 *
 *         // NEW: don't fight the JumperEnemy coroutine while airborne
 *         if (TryGetComponent<JumperEnemy>(out var j) && j.IsJumping) return;
 *
 *         Vector2 direction  = ((Vector2)_player.position - _rb.position).normalized;
 *         _rb.linearVelocity = direction * _data.moveSpeed;
 *     }
 *
 * Alternatively, if you prefer not to touch Enemy.cs, the JumperEnemy.FixedUpdate
 * already zeroes linearVelocity every physics frame while _isJumping is true,
 * which overrides whatever Enemy.FixedUpdate writes immediately after.
 * Both approaches work — the guard in Enemy.cs is cleaner.
 * ─────────────────────────────────────────────────────────────────────────────
 */