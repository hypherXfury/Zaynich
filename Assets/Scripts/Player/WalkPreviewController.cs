using UnityEngine;

/// <summary>
/// Standalone walk animation preview that drives a 12-frame sprite sheet
/// (Walking Animation_0 … _11) with joystick movement.
///
/// Assign the 12 sprites in order in the Inspector and wire up the Joystick reference.
/// Movement is applied via Rigidbody2D; the sprite flips horizontally to match direction.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class WalkPreviewController : MonoBehaviour
{
    private const int TotalFrames = 12;
    private const float DefaultFrameRate = 10f;
    private const float DefaultMoveSpeed = 4f;

    [SerializeField] private Joystick _joystick;

    [Tooltip("All 12 walking frames in sheet order (Walking Animation_0 … _11).")]
    [SerializeField] private Sprite[] _walkFrames = new Sprite[TotalFrames];

    [SerializeField] [Range(1f, 30f)] private float _frameRate = DefaultFrameRate;
    [SerializeField] private float _moveSpeed = DefaultMoveSpeed;

    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D    _rb;

    private int   _frameIndex = 0;
    private float _frameTimer = 0f;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb             = GetComponent<Rigidbody2D>();

        _rb.gravityScale  = 0f;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (_walkFrames == null || _walkFrames.Length != TotalFrames) return;

        bool isMoving = _joystick != null && _joystick.Direction.magnitude > 0.01f;

        if (isMoving)
        {
            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _frameRate;
            if (_frameTimer >= frameDuration)
            {
                _frameTimer  -= frameDuration;
                _frameIndex   = (_frameIndex + 1) % TotalFrames;
            }

            // Flip sprite to face movement direction on the X axis.
            float horizontal = _joystick.Direction.x;
            if (Mathf.Abs(horizontal) > 0.1f)
                _spriteRenderer.flipX = horizontal < 0f;
        }
        else
        {
            _frameIndex = 0;
            _frameTimer = 0f;
        }

        _spriteRenderer.sprite = _walkFrames[_frameIndex];
    }

    private void FixedUpdate()
    {
        if (_joystick == null) return;
        _rb.linearVelocity = _joystick.Direction * _moveSpeed;
    }
}
