using UnityEngine;

/// <summary>
/// Tissue Penetration Lance projectile.
/// Travels in a straight line and pierces every enemy it hits with no pierce limit.
/// Spawned and configured by <see cref="TissueLance"/>. Never place this directly in the scene.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Lance : MonoBehaviour
{
    private const float LanceSpeed   = 12f;
    private const float LanceLifetime = 3f;

    // Collider half-extents in local space (lance points along X before rotation)
    private const float ColliderHalfLength = 0.28f;
    private const float ColliderHalfWidth  = 0.06f;

    private static readonly Color LanceColor = new Color(1f, 0.45f, 0.1f, 1f); // deep orange
    private static Sprite s_cachedSprite;

    private float   _damage;
    private Vector2 _direction;

    /// <summary>Configures and launches the lance. Call immediately after Instantiate.</summary>
    public void Initialize(float damage, Vector2 direction)
    {
        _damage    = damage;
        _direction = direction.normalized;

        var rb            = GetComponent<Rigidbody2D>();
        rb.linearVelocity = direction.normalized * LanceSpeed;

        // Rotate sprite/collider so the long axis faces the travel direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, LanceLifetime);
    }

    private void Awake()
    {
        var rb            = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.constraints    = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation  = RigidbodyInterpolation2D.Interpolate;

        var col           = GetComponent<BoxCollider2D>();
        col.isTrigger     = true;
        col.size          = new Vector2(ColliderHalfLength * 2f, ColliderHalfWidth * 2f);

        var sr            = GetComponent<SpriteRenderer>();
        sr.sprite         = GetOrCreateSprite();
        sr.color          = LanceColor;
        sr.sortingOrder   = 2;
    }

    /// <summary>Lance pierces every enemy and nest it touches — no pierce limit.</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage, _direction);
            return;
        }

        var nest = other.GetComponent<EnemyNest>();
        if (nest != null)
            nest.TakeDamage(_damage);
    }

    /// <summary>Elongated white rectangle, wider than tall so it faces right at zero rotation.</summary>
    private static Sprite GetOrCreateSprite()
    {
        if (s_cachedSprite != null) return s_cachedSprite;

        const int   w   = 32;
        const int   h   = 6;
        const float ppu = 16f;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point
        };

        var pixels = new Color[w * h];
        System.Array.Fill(pixels, Color.white);
        tex.SetPixels(pixels);
        tex.Apply();

        s_cachedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            ppu);

        return s_cachedSprite;
    }
}