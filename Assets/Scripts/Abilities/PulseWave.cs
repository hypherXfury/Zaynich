using UnityEngine;

/// <summary>
/// Expanding ring visual for the Zidebactam Pulse.
/// Scales from zero to the target radius while fading to transparent, then self-destructs.
/// Spawned at runtime by <see cref="ZidebactamPulse"/>. Never place in the scene manually.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PulseWave : MonoBehaviour
{
    private static Sprite s_cachedRingSprite;

    private float          _maxRadius;
    private float          _duration;
    private float          _elapsed;
    private Color          _baseColor;
    private SpriteRenderer _sr;

    /// <summary>Configures the wave immediately after Instantiate.</summary>
    public void Initialize(float maxRadius, float duration, Color color)
    {
        _maxRadius = maxRadius;
        _duration  = duration;
        _baseColor = color;

        _sr        = GetComponent<SpriteRenderer>();
        _sr.sprite = GetOrCreateRingSprite();
        _sr.color  = color;

        transform.localScale = Vector3.zero;
        Destroy(gameObject, duration + 0.05f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // Expand outward
        float scale = Mathf.Lerp(0f, _maxRadius, t);
        transform.localScale = Vector3.one * scale;

        // Fade out
        Color c = _baseColor;
        c.a     = 1f - t;
        _sr.color = c;
    }

    /// <summary>
    /// White ring sprite (128×128, transparent centre) cached across all wave instances.
    /// PPU = 64 → sprite is 2 world units wide at scale 1, giving radius = 1 world unit.
    /// Scaling by <c>maxRadius</c> maps directly to world-space radius.
    /// </summary>
    private static Sprite GetOrCreateRingSprite()
    {
        if (s_cachedRingSprite != null) return s_cachedRingSprite;

        const int   texSize    = 128;
        const float ppu        = texSize / 2f;  // radius = 1 wu at scale 1
        const float ringWidth  = 10f;           // pixel width of the ring

        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear
        };

        float center   = (texSize - 1) / 2f;
        float outerRSq = (texSize / 2f) * (texSize / 2f);
        float innerRSq = (texSize / 2f - ringWidth) * (texSize / 2f - ringWidth);

        var pixels = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx  = x - center;
                float dy  = y - center;
                float dSq = dx * dx + dy * dy;
                pixels[y * texSize + x] = (dSq <= outerRSq && dSq >= innerRSq)
                    ? Color.white
                    : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        s_cachedRingSprite = Sprite.Create(
            tex,
            new Rect(0, 0, texSize, texSize),
            new Vector2(0.5f, 0.5f),
            ppu);

        return s_cachedRingSprite;
    }
}
