using UnityEngine;

/// <summary>
/// Archero-style 8-directional player controller.
///
/// MOVING  : Walk animation plays in movement direction. No shooting.
/// IDLE    : Finds nearest enemy within _attackRadius. Snaps facing toward it
///           (nearest of 8 directions). Plays that direction's walk animation.
///           Notifies CefepiimeBolt and TissueLance to shoot in that direction.
///
/// Sprite sheet: 64 sprites, 8 rows × 8 frames.
/// Row order configurable via Inspector (default: Down/Left/Right/Up/UpRight/DownRight/UpLeft/DownLeft).
/// </summary>

public enum FacingDirection
{
    Down      = 0,
    Left      = 1,
    Right     = 2,
    Up        = 3,
    UpRight   = 4,
    DownRight = 5,
    UpLeft    = 6,
    DownLeft  = 7,
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class FourDirPreviewController : UnityEngine.MonoBehaviour
{
    private const int FramesPerDirection = 8;
    private const int TotalSprites       = 64;

    // ── Inspector ─────────────────────────────────────────────────────────────

    [UnityEngine.SerializeField] private Joystick _joystick;

    [UnityEngine.Tooltip("All 64 sprites in sheet order (_0…_63). 8 rows × 8 frames each.")]
    [UnityEngine.SerializeField] private UnityEngine.Sprite[] _sprites = new UnityEngine.Sprite[TotalSprites];

    [UnityEngine.Header("Direction Row Start Indices")]
    [UnityEngine.SerializeField] private int _downRowStart      = 0;
    [UnityEngine.SerializeField] private int _leftRowStart      = 8;
    [UnityEngine.SerializeField] private int _rightRowStart     = 16;
    [UnityEngine.SerializeField] private int _upRowStart        = 24;
    [UnityEngine.SerializeField] private int _upRightRowStart   = 32;
    [UnityEngine.SerializeField] private int _downRightRowStart = 40;
    [UnityEngine.SerializeField] private int _upLeftRowStart    = 48;
    [UnityEngine.SerializeField] private int _downLeftRowStart  = 56;

    [UnityEngine.Header("Movement")]
    [UnityEngine.SerializeField][UnityEngine.Range(1f, 30f)] private float _frameRate = 10f;
    [UnityEngine.SerializeField] private float _moveSpeed = 4f;

    [UnityEngine.Header("Attack Radius")]
    [UnityEngine.Tooltip("Player will auto-aim and shoot at the nearest enemy within this radius when idle.")]
    [UnityEngine.SerializeField] private float _attackRadius = 10f;

    [UnityEngine.Header("Idle Sprite (no enemies in range)")]
    [UnityEngine.Tooltip("Sprite shown when idle and no enemy is in range. Leave null to keep last walk frame.")]
    [UnityEngine.SerializeField] private UnityEngine.Sprite _idleSprite;

    [UnityEngine.Header("Oxygen Speed Penalty")]
    [UnityEngine.SerializeField] private float _oxygenPenaltyThreshold = 50f;
    [UnityEngine.SerializeField] private float _penaltyPerBand          = 0.9f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip footSteps;
    [UnityEngine.Tooltip("Footstep playback pitch when moving at full (unpenalized) speed.")]
    [SerializeField] private float _footstepBasePitch = 1f;

    // ── Private refs ──────────────────────────────────────────────────────────

    private UnityEngine.SpriteRenderer _spriteRenderer;
    private UnityEngine.Rigidbody2D    _rb;
    private CefepiimeBolt              _cefepiimeBolt;
    private TissueLance                _tissueLance;

    private FacingDirection _facing              = FacingDirection.Up;
    private int             _frameIndex          = 0;
    private float           _frameTimer          = 0f;
    private float           _currentSpeedMul     = 1f;

    // ── Properties ────────────────────────────────────────────────────────────

    public float          MoveSpeed       => _moveSpeed;
    public float          AttackRadius    => _attackRadius;
    public FacingDirection CurrentFacing  => _facing;

    /// <summary>True while the joystick is being pushed.</summary>
    public bool IsMoving { get; private set; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _spriteRenderer            = GetComponent<UnityEngine.SpriteRenderer>();
        _rb                        = GetComponent<UnityEngine.Rigidbody2D>();
        _rb.gravityScale           = 0f;
        _rb.freezeRotation         = true;
        _rb.interpolation          = UnityEngine.RigidbodyInterpolation2D.Interpolate;

        _cefepiimeBolt = GetComponent<CefepiimeBolt>();
        _tissueLance   = GetComponent<TissueLance>();

        if (VitalsManager.Instance != null)
            VitalsManager.Instance.OnOxygenChanged += OnOxygenChanged;
        else
            _currentSpeedMul = 1f;
    }

    private void Start()
    {
        if (VitalsManager.Instance != null)
        {
            VitalsManager.Instance.OnOxygenChanged -= OnOxygenChanged;
            VitalsManager.Instance.OnOxygenChanged += OnOxygenChanged;
            RecalculateSpeedMultiplier(VitalsManager.Instance.Oxygen);
        }
    }

    private void OnDestroy()
    {
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.OnOxygenChanged -= OnOxygenChanged;
    }

    private void Update()
    {
        if (_sprites == null || _sprites.Length != TotalSprites) return;

        UnityEngine.Vector2 input = GetMovementInput();
        IsMoving = input.magnitude > 0.01f;

        if (IsMoving)
            HandleMoving(input);
        else
            HandleIdle();
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = GetMovementInput() * (_moveSpeed * _currentSpeedMul);
    }

    /// <summary>Keyboard (WASD/arrow keys) takes priority when pressed, otherwise falls back to the joystick.</summary>
    private UnityEngine.Vector2 GetMovementInput()
    {
        UnityEngine.Vector2 keyboard = new UnityEngine.Vector2(
            UnityEngine.Input.GetAxisRaw("Horizontal"),
            UnityEngine.Input.GetAxisRaw("Vertical"));

        if (keyboard.sqrMagnitude > 0.01f)
        {
            if (keyboard.sqrMagnitude > 1f) keyboard.Normalize();
            return keyboard;
        }

        return _joystick != null ? _joystick.Direction : UnityEngine.Vector2.zero;
    }

    // ── Moving state ──────────────────────────────────────────────────────────

    private void HandleMoving(UnityEngine.Vector2 input)
    {
        
        // Facing follows movement direction
        FacingDirection moveFacing = DirectionFromInput(input);
        if (moveFacing != _facing)
        {
            _facing = moveFacing;
            // While moving, weapons are disabled — but update facing for when idle resumes
            _cefepiimeBolt?.SetFacing(FacingToVector(_facing));
            _tissueLance?.SetFacing(FacingToVector(_facing));
        }

        // Advance walk animation
        _frameTimer += UnityEngine.Time.deltaTime;
        if (_frameTimer >= 1f / _frameRate)
        {
            _frameTimer -= 1f / _frameRate;
            _frameIndex  = (_frameIndex + 1) % FramesPerDirection;
            
        }

        ApplySprite();

        UpdateFootstepAudio();

        // Tell weapons NOT to shoot while moving
        _cefepiimeBolt?.SetCanShoot(false);
        //_tissueLance?.SetCanShoot(false);
    }

    // ── Footstep audio ────────────────────────────────────────────────────────

    private void UpdateFootstepAudio()
    {
        if (audioSource == null) return;

        if (footSteps != null && audioSource.clip != footSteps)
            audioSource.clip = footSteps;

        // Faster player → higher pitch; scales with the oxygen speed multiplier.
        audioSource.pitch = _footstepBasePitch * _currentSpeedMul;

        audioSource.loop = true;
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void StopFootstepAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // ── Idle state ────────────────────────────────────────────────────────────

    private void HandleIdle()
    {
        StopFootstepAudio();

        // Find nearest enemy (or enemy nest) within attack radius
        UnityEngine.Transform nearest = FindNearestTargetInRadius();

        if (nearest != null)
        {
            // Snap facing toward the nearest target (8-direction snap)
            UnityEngine.Vector2 toEnemy = (UnityEngine.Vector2)(nearest.position - transform.position);
            FacingDirection aimDir      = DirectionFromInput(toEnemy);

            if (aimDir != _facing)
            {
                _facing = aimDir;
                UnityEngine.Vector2 facingVec = FacingToVector(_facing);
                _cefepiimeBolt?.SetFacing(facingVec);
                _tissueLance?.SetFacing(facingVec);
            }

            // Play the aim direction's walk animation while shooting
            _frameTimer += UnityEngine.Time.deltaTime;
            if (_frameTimer >= 1f / _frameRate)
            {
                _frameTimer -= 1f / _frameRate;
                _frameIndex  = (_frameIndex + 1) % FramesPerDirection;
            }
            ApplySprite();

            // Allow weapons to shoot
            _cefepiimeBolt?.SetCanShoot(true);
            //_tissueLance?.SetCanShoot(true);
        }
        else
        {
            // No enemy in range — show idle sprite, stop shooting
            _frameIndex = 0;
            _frameTimer = 0f;

            if (_idleSprite != null)
                _spriteRenderer.sprite = _idleSprite;
            else
                ApplySprite();   // keep last walk frame

            _cefepiimeBolt?.SetCanShoot(false);
            //_tissueLance?.SetCanShoot(false);
        }
    }

    // ── Enemy search ──────────────────────────────────────────────────────────

    private UnityEngine.Transform FindNearestTargetInRadius()
    {
        UnityEngine.Transform best  = null;
        float                 minSq = _attackRadius * _attackRadius;
        UnityEngine.Vector3   myPos = transform.position;

        var enemies = Enemy.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            float sq = (enemies[i].transform.position - myPos).sqrMagnitude;
            if (sq < minSq)
            {
                minSq = sq;
                best  = enemies[i].transform;
            }
        }

        var nests = EnemyNest.All;
        for (int i = 0; i < nests.Count; i++)
        {
            if (nests[i] == null) continue;
            float sq = (nests[i].transform.position - myPos).sqrMagnitude;
            if (sq < minSq)
            {
                minSq = sq;
                best  = nests[i].transform;
            }
        }

        return best;
    }

    // ── Sprite ────────────────────────────────────────────────────────────────

    private void ApplySprite()
    {
        int rowStart = _facing switch
        {
            FacingDirection.Down      => _downRowStart,
            FacingDirection.Left      => _leftRowStart,
            FacingDirection.Right     => _rightRowStart,
            FacingDirection.Up        => _upRowStart,
            FacingDirection.UpRight   => _upRightRowStart,
            FacingDirection.DownRight => _downRightRowStart,
            FacingDirection.UpLeft    => _upLeftRowStart,
            FacingDirection.DownLeft  => _downLeftRowStart,
            _                         => _downRowStart
        };

        int index = rowStart + _frameIndex;
        if (index < _sprites.Length && _sprites[index] != null)
            _spriteRenderer.sprite = _sprites[index];
    }

    // ── Gun tip index (used by CefepiimeBolt) ────────────────────────────────

    public int GetGunTipPosIndex() => _facing switch
    {
        FacingDirection.Down      => 0,
        FacingDirection.Left      => 1,
        FacingDirection.Right     => 2,
        FacingDirection.Up        => 3,
        FacingDirection.UpRight   => 4,
        FacingDirection.DownRight => 5,
        FacingDirection.UpLeft    => 6,
        FacingDirection.DownLeft  => 7,
        _                         => 3
    };

    // ── Oxygen speed penalty ──────────────────────────────────────────────────

    public void SetMoveSpeed(float s) => _moveSpeed = s;

    private void OnOxygenChanged(float o) => RecalculateSpeedMultiplier(o);

    private void RecalculateSpeedMultiplier(float oxygen)
    {
        if (oxygen >= _oxygenPenaltyThreshold) { _currentSpeedMul = 1f; return; }
        int bands      = UnityEngine.Mathf.FloorToInt((_oxygenPenaltyThreshold - oxygen) / 10f);
        _currentSpeedMul = UnityEngine.Mathf.Pow(_penaltyPerBand, bands);
    }

    // ── Direction helpers ─────────────────────────────────────────────────────

    private static readonly float D = 1f / UnityEngine.Mathf.Sqrt(2f);

    public static UnityEngine.Vector2 FacingToVector(FacingDirection dir) => dir switch
    {
        FacingDirection.Right     => UnityEngine.Vector2.right,
        FacingDirection.Left      => UnityEngine.Vector2.left,
        FacingDirection.Up        => UnityEngine.Vector2.up,
        FacingDirection.Down      => UnityEngine.Vector2.down,
        FacingDirection.UpRight   => new UnityEngine.Vector2( D,  D),
        FacingDirection.DownRight => new UnityEngine.Vector2( D, -D),
        FacingDirection.UpLeft    => new UnityEngine.Vector2(-D,  D),
        FacingDirection.DownLeft  => new UnityEngine.Vector2(-D, -D),
        _                         => UnityEngine.Vector2.up
    };

    private static FacingDirection DirectionFromInput(UnityEngine.Vector2 input)
    {
        float ax   = UnityEngine.Mathf.Abs(input.x);
        float ay   = UnityEngine.Mathf.Abs(input.y);
        bool  diag = ax > 0.38f && ay > 0.38f;

        if (diag)
        {
            if (input.x > 0f && input.y > 0f) return FacingDirection.UpRight;
            if (input.x > 0f && input.y < 0f) return FacingDirection.DownRight;
            if (input.x < 0f && input.y > 0f) return FacingDirection.UpLeft;
            return FacingDirection.DownLeft;
        }

        if (ax >= ay) return input.x > 0f ? FacingDirection.Right : FacingDirection.Left;
        return input.y > 0f ? FacingDirection.Up : FacingDirection.Down;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        UnityEngine.Gizmos.color = new UnityEngine.Color(0.2f, 1f, 0.4f, 0.3f);
        UnityEngine.Gizmos.DrawWireSphere(transform.position, _attackRadius);
    }
}