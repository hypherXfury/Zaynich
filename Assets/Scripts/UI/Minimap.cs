using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimap with two modes driven by a second orthographic camera → RenderTexture → RawImage.
///
/// COLLAPSED (default): camera follows the player and shows a small local area.
///                       Player dot is always centred.
/// EXPANDED  (button) : camera pulls back to show the entire world.
///                       Player dot moves to its real position on the full map.
///
/// ── HIERARCHY SETUP ──────────────────────────────────────────────────────────
///
/// Scene:
///   MinimapCamera          ← Camera component + this script lives here
///
/// Canvas (Screen Space — Overlay):
///   └── MinimapRoot        ← RectTransform, CanvasGroup
///         ├── MapDisplay   ← RawImage  (shows the RenderTexture)
///         ├── PlayerDot    ← Image     (circle, cyan)
///         └── ExpandButton ← Button
///               └── Label  ← Text ("+" / "−")
///
/// ── QUICK START ──────────────────────────────────────────────────────────────
///   1. Create an empty GameObject in the scene, name it "MinimapCamera".
///   2. Add a Camera component to it. Set:
///        - Clear Flags    = Solid Color
///        - Background     = black (or your map bg colour)
///        - Projection     = Orthographic
///        - Culling Mask   = Everything (or a dedicated Minimap layer)
///        - Depth          = -2  (renders before main camera)
///   3. Add this script to "MinimapCamera".
///   4. Build the Canvas hierarchy above.
///   5. Assign all references in the Inspector.
///   6. Set _mapWorldWidth / _mapWorldHeight to your world size.
///   7. Hit Play.
/// </summary>
[RequireComponent(typeof(Camera))]
public class Minimap : MonoBehaviour
{
    // ── Inspector — World ─────────────────────────────────────────────────────

    [Header("World Bounds")]
    [Tooltip("Total width of the playable world in Unity units.")]
    [SerializeField] private float _mapWorldWidth = 100f;
    [Tooltip("Total height of the playable world in Unity units.")]
    [SerializeField] private float _mapWorldHeight = 100f;
    [Tooltip("World-space centre of the map. Usually (0,0).")]
    [SerializeField] private Vector2 _mapWorldCenter = Vector2.zero;

    [Tooltip("How many world units the minimap shows around the player when collapsed.\n" +
             "This is the orthographic half-size of the local view (e.g. 8 = 16 unit wide area).")]
    [SerializeField] private float _localViewSize = 8f;

    // ── Inspector — References ────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Transform _player;
    [SerializeField] private RectTransform _minimapRoot;   // the UI panel
    [SerializeField] private RawImage _mapDisplay;       // shows RenderTexture
    [SerializeField] private Image _playerDot;
    [SerializeField] private Button _expandButton;
    [SerializeField] private TextMeshProUGUI _expandLabel;

    // ── Inspector — Sizes ─────────────────────────────────────────────────────

    [Header("UI Size — pixels")]
    [SerializeField] private float _collapsedSize = 150f;
    [SerializeField] private float _expandedSize = 380f;

    [Header("Render Texture Resolution")]
    [Tooltip("Higher = sharper minimap, more GPU cost. 256 is fine for most mobile games.")]
    [SerializeField] private int _rtResolution = 256;

    [Header("Animation")]
    [SerializeField] private float _animDuration = 0.2f;

    // ── Inspector — Style ─────────────────────────────────────────────────────

    [Header("Style")]
    [SerializeField] private Color _playerDotColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private float _dotSize = 10f;
    [SerializeField] private float _dotSizeExpanded = 16f;
    [SerializeField] private Color _borderColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    [SerializeField] private float _borderThickness = 2f;

    [Header("Map Bounds")]
    [Tooltip("The BoxCollider2D placed around the map. Camera anchor is clamped inside it.")]
    [SerializeField] private BoxCollider2D _mapBounds;

    // ── Private state ─────────────────────────────────────────────────────────

    private Camera _minimapCam;
    private RenderTexture _rt;
    private RectTransform _dotRect;
    private RectTransform _displayRect;

    private bool _expanded;
    private float _animTime = 1f;
    private float _fromSize;
    private float _toSize;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _minimapCam = GetComponent<Camera>();
        _dotRect = _playerDot.GetComponent<RectTransform>();
        _displayRect = _mapDisplay.GetComponent<RectTransform>();

        SetupRenderTexture();
        SetupMinimapCamera();
        SetupUI();

        _expandButton.onClick.AddListener(ToggleExpand);
        UpdateExpandLabel();
        SetSize(_collapsedSize, animated: false);
    }

    private void LateUpdate()
    {
        UpdateMinimapCamera();
        UpdatePlayerDot();
        TickAnimation();
    }

    // ── Render Texture ────────────────────────────────────────────────────────

    private void SetupRenderTexture()
    {
        _rt = new RenderTexture(_rtResolution, _rtResolution, 16)
        {
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1
        };
        _rt.Create();

        _mapDisplay.texture = _rt;
    }

    // ── Minimap camera ────────────────────────────────────────────────────────

    private void SetupMinimapCamera()
    {
        _minimapCam.targetTexture = _rt;
        _minimapCam.orthographic = true;
        _minimapCam.depth = -2;   // renders before main camera

        // Start following the player in local view mode
        if (_player != null)
            transform.position = new Vector3(_player.position.x, _player.position.y, -10f);
        else
            transform.position = new Vector3(_mapWorldCenter.x, _mapWorldCenter.y, -10f);

        // Start at local view size — UpdateMinimapCamera animates it from here
        _minimapCam.orthographicSize = _localViewSize;
    }

    /// <summary>
    /// Collapsed — follow player with local view size.
    /// Expanded  — fix at world centre with full map size.
    /// Smoothly interpolates orthographic size during the animation.
    /// </summary>
    private void UpdateMinimapCamera()
    {
        if (_player == null) return;

        if (_expanded)
        {
            // Show full world — fixed at centre
            transform.position = new Vector3(_mapWorldCenter.x, _mapWorldCenter.y, -10f);

            // Smoothly animate ortho size toward full-map size
            float fullSize = Mathf.Max(_mapWorldWidth, _mapWorldHeight) * 0.5f;
            _minimapCam.orthographicSize = Mathf.Lerp(
                _minimapCam.orthographicSize, fullSize,
                Time.unscaledDeltaTime * 8f);
        }
        else
        {
            // Follow player — start from player position
            float posX = _player.position.x;
            float posY = _player.position.y;

            // Clamp within map bounds using the MINIMAP camera's own half-extents
            // (not the main camera's), so the minimap view never shows outside the level
            if (_mapBounds != null)
            {
                float halfH = _minimapCam.orthographicSize;
                float halfW = _minimapCam.orthographicSize * _minimapCam.aspect;

                Bounds b   = _mapBounds.bounds;
                float minX = b.min.x + halfW;
                float maxX = b.max.x - halfW;
                float minY = b.min.y + halfH;
                float maxY = b.max.y - halfH;

                posX = Mathf.Clamp(posX, minX, maxX);
                posY = Mathf.Clamp(posY, minY, maxY);
            }

            transform.position = new Vector3(posX, posY, -10f);

            // Smoothly animate ortho size toward local view size
            _minimapCam.orthographicSize = Mathf.Lerp(
                _minimapCam.orthographicSize, _localViewSize,
                Time.unscaledDeltaTime * 8f);
        }
    }

    // ── Player dot ────────────────────────────────────────────────────────────

    private void UpdatePlayerDot()
    {
        if (_player == null) return;

        float currentSize = _minimapRoot.sizeDelta.x;
        float padding = _borderThickness + 1f;
        float innerSize = currentSize - padding * 2f;

        float nx, ny;

        if (_expanded)
        {
            // Expanded — dot moves to real world position on the full map
            nx = (_player.position.x - (_mapWorldCenter.x - _mapWorldWidth * 0.5f)) / _mapWorldWidth;
            ny = (_player.position.y - (_mapWorldCenter.y - _mapWorldHeight * 0.5f)) / _mapWorldHeight;
            nx = Mathf.Clamp01(nx);
            ny = Mathf.Clamp01(ny);
        }
        else
        {
            // Collapsed — camera follows player so dot is always at the centre
            nx = 0.5f;
            ny = 0.5f;
        }

        _dotRect.anchorMin = Vector2.zero;
        _dotRect.anchorMax = Vector2.zero;
        _dotRect.pivot = new Vector2(0.5f, 0.5f);
        _dotRect.anchoredPosition = new Vector2(
            padding + nx * innerSize,
            padding + ny * innerSize);

        float ds = _expanded ? _dotSizeExpanded : _dotSize;
        _dotRect.sizeDelta = new Vector2(ds, ds);
    }

    // ── Expand / collapse ─────────────────────────────────────────────────────

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        _fromSize = _minimapRoot.sizeDelta.x;
        _toSize = _expanded ? _expandedSize : _collapsedSize;
        _animTime = 0f;
        UpdateExpandLabel();
    }

    private void TickAnimation()
    {
        if (_animTime >= 1f) return;

        _animTime += Time.unscaledDeltaTime / Mathf.Max(_animDuration, 0.001f);
        float t = Mathf.Clamp01(_animTime);
        float ease = t * t * (3f - 2f * t);   // smooth-step
        SetSize(Mathf.Lerp(_fromSize, _toSize, ease), animated: true);
    }

    private void SetSize(float size, bool animated)
    {
        _minimapRoot.sizeDelta = new Vector2(size, size);

        // MapDisplay fills the root entirely
        _displayRect.anchorMin = Vector2.zero;
        _displayRect.anchorMax = Vector2.one;
        _displayRect.offsetMin = Vector2.zero;
        _displayRect.offsetMax = Vector2.zero;

        // Button stays in top-right corner
        var btnRect = _expandButton.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = Vector2.zero;
        btnRect.sizeDelta = new Vector2(28f, 28f);
    }

    private void UpdateExpandLabel()
    {
        if (_expandLabel != null)
            _expandLabel.text = _expanded ? "−" : "+";
    }

    // ── UI setup ─────────────────────────────────────────────────────────────

    private void SetupUI()
    {
        // Anchor root to top-left
        _minimapRoot.anchorMin = new Vector2(0f, 1f);
        _minimapRoot.anchorMax = new Vector2(0f, 1f);
        _minimapRoot.pivot = new Vector2(0f, 1f);
        _minimapRoot.anchoredPosition = new Vector2(10f, -10f);

        // Add an outline Image as border (sits on top of MapDisplay)
        var border = _mapDisplay.gameObject.AddComponent<Outline>();
        border.effectColor = _borderColor;
        border.effectDistance = new Vector2(_borderThickness, -_borderThickness);

        // Player dot style
        _playerDot.color = _playerDotColor;
        _playerDot.sprite = CreateCircleSprite();
        _playerDot.GetComponent<RectTransform>().SetAsLastSibling(); // always on top
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _minimapCam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
        }
    }

    // ── Circle sprite ─────────────────────────────────────────────────────────

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        float c = size * 0.5f;

        for (int i = 0; i < pixels.Length; i++)
        {
            float x = i % size - c;
            float y = i / size - c;
            float a = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Sqrt(x * x + y * y) - (c - 1f)));
            pixels[i] = new Color(1f, 1f, 1f, a);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}