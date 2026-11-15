using UnityEngine;

/// <summary>
/// Shared utility for generating procedural sprites at runtime.
/// All returned sprites are white so they can be tinted via <see cref="SpriteRenderer.color"/>.
/// </summary>
internal static class ProceduralSprites
{
    /// <summary>
    /// Creates a hollow ring sprite.
    /// PPU of 64 and texSize of 128 produce a sprite that is 2 world units wide at scale 1
    /// (radius = 1 wu), so scaling by <c>radius</c> maps directly to world-space radius.
    /// </summary>
    public static Sprite CreateRing(int texSize = 128, float ringWidthPx = 8f, float ppu = 64f)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear
        };

        float center   = (texSize - 1) / 2f;
        float outerRSq = (texSize / 2f) * (texSize / 2f);
        float innerRSq = (texSize / 2f - ringWidthPx) * (texSize / 2f - ringWidthPx);

        var pixels = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        for (int x = 0; x < texSize; x++)
        {
            float dx  = x - center;
            float dy  = y - center;
            float dSq = dx * dx + dy * dy;
            pixels[y * texSize + x] = (dSq <= outerRSq && dSq >= innerRSq) ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), ppu);
    }

    /// <summary>
    /// Creates a filled circle sprite.
    /// Same PPU/texSize conventions as <see cref="CreateRing"/>.
    /// </summary>
    public static Sprite CreateCircle(int texSize = 128, float ppu = 64f)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear
        };

        float center = (texSize - 1) / 2f;
        float rSq    = (texSize / 2f) * (texSize / 2f);

        var pixels = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        for (int x = 0; x < texSize; x++)
        {
            float dx = x - center;
            float dy = y - center;
            pixels[y * texSize + x] = (dx * dx + dy * dy <= rSq) ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), ppu);
    }
}
