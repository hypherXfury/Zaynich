using UnityEngine;

/// <summary>
/// Generates a plain white square sprite at runtime when no sprite is assigned to the SpriteRenderer.
/// Replace this component with a real sprite once your art is ready.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class PlaceholderSprite : MonoBehaviour
{
    private const int TextureSize = 64;
    private const float PixelsPerUnit = 64f;

    private void OnEnable()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr.sprite != null) return;

        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point
        };

        var pixels = new Color[TextureSize * TextureSize];
        System.Array.Fill(pixels, Color.white);
        tex.SetPixels(pixels);
        tex.Apply();

        sr.sprite = Sprite.Create(
            tex,
            new Rect(0, 0, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
    }
}
