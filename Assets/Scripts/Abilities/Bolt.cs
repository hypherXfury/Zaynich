using UnityEngine;

/// <summary>
/// Cefepime Bolt projectile.
/// Moves in a straight line, deals damage to enemies on contact, supports piercing and splitting.
/// Spawned and configured by <see cref="CefepiimeBolt"/>. Never place this directly in the scene.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Bolt : MonoBehaviour
{
    private const float ColliderRadius        = 0.12f;
    private const float SplitLifetime         = 1.5f;
    private const float SplitDamageMultiplier = 0.7f;
    private const float HomingTurnRateDegPerSec = 720f;
    private const float HomingSearchRadius      = 20f;

    private static readonly Color BoltColor = new Color(0.35f, 0.9f, 1.0f, 1f);
    private static Sprite s_cachedSprite;

    private Bolt    _prefabRef;
    private float   _damage;
    private int     _piercingCount;
    private bool    _canSplit;
    private int     _splitCount;
    private float   _splitAngle;
    private float   _speed;
    private Vector2 _direction;
    private int     _pierceHits;
    private Rigidbody2D _rb;
    private Transform   _homingTarget;

    /// <summary>
    /// Configures and launches the bolt. Call immediately after Instantiate.
    /// </summary>
    /// <param name="prefabRef">The bolt prefab used to spawn split projectiles.</param>
    /// <param name="isSplitBolt">Split bolts never split again.</param>
    public void Initialize(
        float   damage,
        int     piercingCount,
        bool    canSplit,
        int     splitCount,
        float   splitAngle,
        Vector2 direction,
        float   speed,
        float   lifetime,
        Bolt    prefabRef,
        bool    isSplitBolt = false)
    {
        _damage        = damage;
        _piercingCount = piercingCount;
        _canSplit      = canSplit && !isSplitBolt;
        _splitCount    = splitCount;
        _splitAngle    = splitAngle;
        _direction     = direction.normalized;
        _speed         = speed;
        _prefabRef     = prefabRef;

        _rb.linearVelocity = _direction * speed;

        // Rotate the sprite so it visually faces the travel direction
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);

        if (!isSplitBolt)
            _homingTarget = FindNearestEnemy();

        Destroy(gameObject, lifetime);
    }

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col       = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = ColliderRadius;

        var sr    = GetComponent<SpriteRenderer>();
        //sr.sprite = GetOrCreateSprite();
        //sr.color  = BoltColor;
        sr.sortingOrder = 2;
    }

    private void FixedUpdate()
    {
        if (_homingTarget == null || !_homingTarget.gameObject.activeInHierarchy)
        {
            _homingTarget = FindNearestEnemy();
            if (_homingTarget == null) return;
        }

        Vector2 toTarget  = (Vector2)_homingTarget.position - _rb.position;
        float   angleTo   = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float   angleCur  = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        float   angleNew  = Mathf.MoveTowardsAngle(angleCur, angleTo, HomingTurnRateDegPerSec * Time.fixedDeltaTime);

        _direction = new Vector2(Mathf.Cos(angleNew * Mathf.Deg2Rad), Mathf.Sin(angleNew * Mathf.Deg2Rad));
        _rb.linearVelocity  = _direction * _speed;
        transform.rotation  = Quaternion.Euler(0f, 0f, angleNew);
    }

    private Transform FindNearestEnemy()
    {
        Transform nearest      = null;
        float     bestSqrDist  = HomingSearchRadius * HomingSearchRadius;

        var enemies = Enemy.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            float sqrDist = ((Vector2)enemies[i].transform.position - _rb.position).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                nearest     = enemies[i].transform;
            }
        }

        var nests = EnemyNest.All;
        for (int i = 0; i < nests.Count; i++)
        {
            if (nests[i] == null) continue;
            float sqrDist = ((Vector2)nests[i].transform.position - _rb.position).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                nearest     = nests[i].transform;
            }
        }

        return nearest;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check enemies first
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            // A shielded enemy takes no health damage — the bolt instead chips
            // away at the shield (boltHitsPerLayer hits strip one layer).
            if (enemy.HasShield)
                enemy.HitShieldWithBolt();
            else
                enemy.TakeDamage(_damage, _direction);

            _pierceHits++;

            if (_pierceHits > _piercingCount)
                Destroy(gameObject);
            return;
        }

        // Also check destroyable enemy nests
        var nest = other.GetComponent<EnemyNest>();
        if (nest != null)
        {
            nest.TakeDamage(_damage);
            _pierceHits++;

            if (_pierceHits > _piercingCount)
                Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying || !_canSplit || _prefabRef == null) return;
        SpawnSplitBolts();
    }

    private void SpawnSplitBolts()
    {
        float baseAngle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        for (int i = 0; i < _splitCount; i++)
        {
            float offset = _splitCount > 1
                ? Mathf.Lerp(-_splitAngle * 0.5f, _splitAngle * 0.5f, (float)i / (_splitCount - 1))
                : 0f;

            float   totalAngle = baseAngle + offset;
            Vector2 dir        = new Vector2(
                Mathf.Cos(totalAngle * Mathf.Deg2Rad),
                Mathf.Sin(totalAngle * Mathf.Deg2Rad));

            Bolt split = Instantiate(_prefabRef, transform.position, Quaternion.identity);
            split.Initialize(
                _damage * SplitDamageMultiplier,
                _piercingCount,
                false,
                0,
                0f,
                dir,
                _speed,
                SplitLifetime,
                _prefabRef,
                isSplitBolt: true);
        }
    }

    /// <summary>
    /// Landscape 16×5 white-pixel sprite cached across all bolt instances.
    /// Wider than tall so the sprite visually points right at zero rotation.
    /// </summary>
    private static Sprite GetOrCreateSprite()
    {
        if (s_cachedSprite != null) return s_cachedSprite;

        const int   w   = 16, h = 5;
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