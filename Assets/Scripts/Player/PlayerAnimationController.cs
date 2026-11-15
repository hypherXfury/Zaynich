using UnityEngine;

/// <summary>
/// Script-driven 8-directional walk animation using the AxulArt small character sheet.
///
/// Sprite layout (character 1, sprites 8-31):
///   Row 0 (indices  8-15): walk frame 0 — columns [N, NE, E, SE, S, SW, W, NW] (clockwise from North)
///   Row 1 (indices 16-23): walk frame 1
///   Row 2 (indices 24-31): walk frame 2
///
/// _sprites must hold 24 sprites ordered as: frame * 8 + direction (populated by the setup tool).
/// When idle the animation holds on frame 0 for the last facing direction.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(TopDownCharacterController))]
public class PlayerAnimationController : MonoBehaviour
{
    // Column → direction constants matching AxulArt sheet (clockwise from North)
    public const int DirNorth     = 0;
    public const int DirNorthEast = 1;
    public const int DirEast      = 2;
    public const int DirSouthEast = 3;
    public const int DirSouth     = 4;
    public const int DirSouthWest = 5;
    public const int DirWest      = 6;
    public const int DirNorthWest = 7;

    private const int DirectionCount = 8;
    private const int WalkFrameCount = 3;

    // Maps atan2 sector (0=E, 1=NE, 2=N … CCW) → sprite-sheet column
    private static readonly int[] SectorToColumn =
    {
        DirEast, DirNorthEast, DirNorth, DirNorthWest,
        DirWest, DirSouthWest, DirSouth, DirSouthEast
    };

    /// <summary>
    /// 24 sprites ordered as [frame * 8 + direction]. Populated by the setup tool.
    /// Frame 0 is the resting pose; frames 1-2 are walk steps.
    /// </summary>
    [SerializeField] private Sprite[] _sprites;
    [SerializeField] [Range(1f, 24f)] private float _frameRate = 8f;

    private SpriteRenderer             _spriteRenderer;
    private TopDownCharacterController _controller;

    private int   _currentDirection = DirSouth;
    private int   _currentFrame     = 0;
    private float _animTimer        = 0f;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _controller     = GetComponent<TopDownCharacterController>();
    }

    private void Update()
    {
        float   magnitude = _controller.MoveMagnitude;
        Vector2 dir       = _controller.MoveDirection;

        // Keep the last facing direction when the player stops.
        if (magnitude > 0.01f)
            _currentDirection = DirectionFromVector(dir);

        if (magnitude > 0.01f)
        {
            _animTimer += Time.deltaTime;
            if (_animTimer >= 1f / _frameRate)
            {
                _animTimer   -= 1f / _frameRate;
                _currentFrame = (_currentFrame + 1) % WalkFrameCount;
            }
        }
        else
        {
            _currentFrame = 0;
            _animTimer    = 0f;
        }

        ApplySprite();
    }

    private void ApplySprite()
    {
        if (_sprites == null || _sprites.Length != WalkFrameCount * DirectionCount) return;
        _spriteRenderer.sprite = _sprites[_currentFrame * DirectionCount + _currentDirection];
    }

    /// <summary>Converts a movement vector to one of 8 sprite-sheet direction columns.</summary>
    private static int DirectionFromVector(Vector2 dir)
    {
        float angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle        = (angle % 360f + 360f) % 360f;
        int   sector = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;
        return SectorToColumn[sector];
    }
}
