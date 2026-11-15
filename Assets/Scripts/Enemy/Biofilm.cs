using UnityEngine;

/// <summary>
/// Sticky biofilm puddle spawned by OXA Acinetobacter enemies.
/// Slows any player that walks through the zone, then fades out after its lifetime expires.
/// The <see cref="TopDownCharacterController"/> on the player must be present for the slow to apply.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Biofilm : MonoBehaviour
{
    private const float DefaultRadius        = 2.0f;
    private const float DefaultLifetime      = 15f;
    private const float DefaultSlowMultiplier = 0.35f;
    private const float StartAlpha           = 1f;

    [SerializeField] private float _radius        = DefaultRadius;
    [SerializeField] private float _lifetime      = DefaultLifetime;

    /// <summary>Multiplier applied to the player's move speed while inside the zone (e.g., 0.35 = 65% slow).</summary>
    [SerializeField] [Range(0.05f, 0.95f)]
    private float _slowMultiplier = DefaultSlowMultiplier;

    private static Sprite s_cachedSprite;

    private SpriteRenderer                _sr;
    private float                         _elapsed;
    private FourDirPreviewController    _slowedPlayer;

    private void Awake()
    {
        _sr        = GetComponent<SpriteRenderer>();
        //_sr.sprite = GetOrCreateSprite();
        _sr.color  = new Color(0.15f, 0.55f, 0.1f, StartAlpha);
        _sr.sortingOrder = -1; // render below enemies and player

        // Scale the GameObject so the circle sprite matches _radius world units
        transform.localScale = Vector3.one * _radius;

        var col       = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 1f; // world radius = localRadius * scale = 1 * _radius
    }

    private void OnDestroy()
    {
        // Always clean up the speed modifier in case the player is still inside
        if (_slowedPlayer != null)
            _slowedPlayer.SetMoveSpeed(2.5f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        // Fade alpha over lifetime
        float t = Mathf.Clamp01(_elapsed / _lifetime);
        Color c = _sr.color;
        c.a     = Mathf.Lerp(StartAlpha, 0f, t);
        _sr.color = c;

        if (_elapsed >= _lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_slowedPlayer != null) return; // only track one player at a time

        var ctrl = other.GetComponent<FourDirPreviewController>();
        if (ctrl == null) return;

        _slowedPlayer = ctrl;
        ctrl.SetMoveSpeed(_slowMultiplier);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var ctrl = other.GetComponent<FourDirPreviewController>();
        if (ctrl == null || ctrl != _slowedPlayer) return;

        ctrl.SetMoveSpeed(2.5f); // Reset to normal speed
        _slowedPlayer = null;
    }

    private static Sprite GetOrCreateSprite()
    {
        // CreateCircle ppu=64, texSize=128 → sprite is 2wu diameter at scale 1 → radius = 1wu at scale 1
        return s_cachedSprite ??= ProceduralSprites.CreateCircle(128, 64f);
    }
}
