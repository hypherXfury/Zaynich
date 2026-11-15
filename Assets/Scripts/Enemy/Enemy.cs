using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core enemy behaviour: chases the player, deals contact damage, and dies when health is depleted.
/// Call <see cref="Initialize"/> immediately after Instantiate to inject data and player reference.
/// Supports layered shields, shield regeneration, and variable-chance shields.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
public class Enemy : MonoBehaviour
{
    /// <summary>Raised when this enemy's health reaches zero, just before the GameObject is destroyed.</summary>
    public event Action<Enemy> OnDeath;

    /// <summary>Raised on death if this enemy awards bonus pills. Arg: pill count.</summary>
    public event Action<int> OnPillBonus;

    // ── Static registry ──────────────────────────────────────────────────────
    private static readonly List<Enemy> s_all = new();

    /// <summary>All currently active enemies. Read-only. Used by abilities for target acquisition.</summary>
    public static IReadOnlyList<Enemy> All => s_all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => s_all.Clear();
    // ─────────────────────────────────────────────────────────────────────────

    private static Sprite s_cachedSprite;

    // ── Knockback ─────────────────────────────────────────────────────────────
    private const float KnockbackForce    = 2.5f;   // small push, not a launch
    private const float KnockbackDuration = 0.15f;

    private Vector2 _knockbackVelocity;
    private float   _knockbackTimer;

    private EnemyData   _data;
    private Transform   _player;
    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private float _currentHealth;
    private float _damageCooldown;

    // Shield state
    private int   _shieldLayersRemaining;
    private int   _pulsesOnCurrentLayer;
    private int   _boltHitsOnCurrentLayer;
    private float _shieldRegenTimer;

    /// <summary>True while at least one shield layer is intact. Shields block all incoming damage.</summary>
    public bool HasShield { get; private set; }

    /// <summary>Number of shield layers still active on this enemy.</summary>
    public int ShieldLayersRemaining => _shieldLayersRemaining;

    /// <summary>Current health, 0..MaxHealth. Read-only — TakeDamage/Kill are the only way to change it.</summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>Max health from this enemy's EnemyData. 0 before Initialize() runs.</summary>
    public float MaxHealth => _data != null ? _data.maxHealth : 0f;
    public GameObject bloodFX;
    public AudioSource audioSource;
    public AudioClip deathSfx;

    // ── Initialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Injects runtime configuration. Must be called immediately after Instantiate.
    /// </summary>
    public void Initialize(EnemyData data, Transform player)
    {
        _data   = data;
        _player = player;

        _currentHealth = data.maxHealth;

        // Variable-chance shield roll
        bool shieldRolled = data.hasShield &&
                            (data.shieldChance >= 1f || UnityEngine.Random.value < data.shieldChance);
        HasShield              = shieldRolled;
        _shieldLayersRemaining = shieldRolled ? data.shieldLayers : 0;
        _pulsesOnCurrentLayer  = 0;
        _boltHitsOnCurrentLayer = 0;
        _shieldRegenTimer      = 0f;

        transform.localScale = Vector3.one * data.size;
        //_sr.sprite           = GetOrCreateSprite();
        UpdateVisuals();

        // Biofilm puddle
        if (data.spawnsBiofilm && data.biofilmPrefab != null)
            Instantiate(data.biofilmPrefab, transform.position, Quaternion.identity);
    }

    // ── Shield API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Applies one pulse hit to the shield.
    /// Returns <c>true</c> when all shield layers have been removed.
    /// Returns <c>false</c> when the shield is still active after the hit (more layers or pulses required).
    /// Has no effect and returns <c>false</c> when the shield is already broken.
    /// </summary>
    public bool BreakShield()
    {
        if (!HasShield) return false;

        _pulsesOnCurrentLayer++;

        if (_pulsesOnCurrentLayer < _data.pulsesPerLayer)
        {
            // Layer not yet stripped — update visual to show damage progress
            UpdateShieldDamageVisual();
            return false;
        }

        return StripCurrentLayer();
    }

    /// <summary>
    /// Applies one Cefepime Bolt hit to the shield. Bolts strip shield layers on
    /// their own counter (<see cref="EnemyData.boltHitsPerLayer"/>), independent
    /// of pulse progress, so players can peel shields with normal fire.
    /// Returns <c>true</c> when all shield layers have been removed.
    /// </summary>
    public bool HitShieldWithBolt()
    {
        if (!HasShield) return false;

        _boltHitsOnCurrentLayer++;

        if (_boltHitsOnCurrentLayer < _data.boltHitsPerLayer)
        {
            UpdateShieldDamageVisual();
            return false;
        }

        return StripCurrentLayer();
    }

    /// <summary>
    /// Removes the current shield layer and resets per-layer progress (both pulse
    /// and bolt counters). Returns true when that was the last layer.
    /// </summary>
    private bool StripCurrentLayer()
    {
        _pulsesOnCurrentLayer   = 0;
        _boltHitsOnCurrentLayer = 0;
        _shieldLayersRemaining--;

        if (_shieldLayersRemaining > 0)
        {
            // More layers remain — update to next layer's colour
            UpdateVisuals();
            return false;
        }

        // All layers gone
        HasShield         = false;
        _shieldRegenTimer = _data.shieldRegenerateTime > 0f ? _data.shieldRegenerateTime : 0f;
        UpdateVisuals();
        return true;
    }

    /// <summary>
    /// Immediately removes all shield layers, bypassing pulse requirements.
    /// Used by powerful abilities such as Emergency Dose.
    /// </summary>
    public void ForceBreakShield()
    {
        if (!HasShield) return;
        HasShield              = false;
        _shieldLayersRemaining = 0;
        _pulsesOnCurrentLayer  = 0;
        _boltHitsOnCurrentLayer = 0;
        _shieldRegenTimer      = _data != null && _data.shieldRegenerateTime > 0f
            ? _data.shieldRegenerateTime
            : 0f;
        UpdateVisuals();
    }

    // ── Damage & Death ───────────────────────────────────────────────────────

    /// <summary>Applies damage to this enemy. Blocked entirely while a shield is active. Triggers death when health reaches zero.</summary>
    public void TakeDamage(float amount)
    {
        if (HasShield) return;

        _currentHealth -= amount;
        if (_currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// Applies damage plus a small knockback push in <paramref name="knockbackDirection"/>.
    /// Used by direct projectile hits (Bolt/Lance) so hit enemies get a brief, gentle shove back.
    /// </summary>
    public void TakeDamage(float amount, Vector2 knockbackDirection)
    {
        if (HasShield) return;

        TakeDamage(amount);
        ApplyKnockback(knockbackDirection);
    }

    /// <summary>
    /// Kills this enemy outright, bypassing shields. Used by EnemyNest when the
    /// nest is destroyed — every enemy it spawned dies with it.
    /// </summary>
    public void Kill()
    {
        _currentHealth = 0f;
        Die();
    }

    /// <summary>
    /// Removes this enemy without granting any kill rewards (no loot, XP, pill
    /// bonus, death VFX/SFX, or Infection reduction). Used when a CannonEnemy
    /// flies out of the arena bounds instead of being killed by the player.
    /// </summary>
    public void DespawnSilently()
    {
        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    private void ApplyKnockback(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        // Per-type scaling — Boss_Data zeroes this so the boss holds its ground.
        float multiplier = _data != null ? _data.knockbackMultiplier : 1f;
        if (multiplier <= 0f) return;

        _knockbackVelocity = direction.normalized * KnockbackForce * multiplier;
        _knockbackTimer    = KnockbackDuration;
    }

    private void Die()
    {
        if (_data != null && _data.extraPillsOnDeath > 0)
            OnPillBonus?.Invoke(_data.extraPillsOnDeath);

        // Killing this enemy reduces Infection, scaled to its toughness
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.ReportEnemyKilled(_data);

        if (_data != null)
            FindFirstObjectByType<PlayerProgression>().AddXP(_data.xpReward);
        OnDeath?.Invoke(this);
        Instantiate(bloodFX, transform.position, Quaternion.identity);
        audioSource.PlayOneShot(deathSfx);
        float rand =  UnityEngine.Random.Range(0f,1f);
        UtillitySpawner.Instance.Spawn(rand, this.transform);
        Destroy(gameObject);
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _sr = GetComponent<SpriteRenderer>();

        var col       = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
    }

    private void OnEnable()  => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    private void FixedUpdate()
    {
        if (_player == null || _data == null) return;

        // ── Knockback — brief override of normal movement ─────────────────────
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = _knockbackVelocity;
            return;
        }

        // ── JumperEnemy — coroutine owns movement while airborne ──────────────
        if (TryGetComponent<JumperEnemy>(out var jumper) && jumper.IsJumping) return;

        // ── CannonEnemy — fired in a straight line, never chases ──────────────
        if (TryGetComponent<CannonEnemy>(out var cannon) && cannon.IsLaunched) return;

        // ── Boss — holds its ground and patrols left/right on its own ─────────
        if (TryGetComponent<Boss>(out _)) return;

        // ── ChargerEnemy — owns movement in every state (roam/charge/retreat) ─
        if (TryGetComponent<ChargerEnemy>(out _)) return;

        // ── OXAAcinetobacterEnemy — owns movement in every state (roam/attack) ─
        if (TryGetComponent<OXAAcinetobacterEnemy>(out _)) return;

        // ── StenotrophomonasEnemy — owns movement in every state (position/charge) ─
        if (TryGetComponent<StenotrophomonasEnemy>(out _)) return;

        // ── SpitterEnemy — steer toward reserved slot, stop when close enough ─
        if (TryGetComponent<SpitterEnemy>(out var spitter))
        {
            Vector2 destination = spitter.HasSlot
                ? spitter.TargetPosition
                : (Vector2)_player.position;   // fallback if all slots full

            Vector2 toDestination = destination - _rb.position;
            float   dist          = toDestination.magnitude;

            if (dist <= 0.15f)
            {
                // Arrived at slot — apply only separation so spitters nudge apart
                _rb.linearVelocity = spitter.GetSeparationForce();
            }
            else
            {
                // Move toward slot + push away from neighbouring spitters
                Vector2 steer = toDestination.normalized * _data.moveSpeed
                              + spitter.GetSeparationForce();
                _rb.linearVelocity = steer;
            }
            return;
        }

        // ── Default — chase player directly ───────────────────────────────────
        Vector2 toPlayer   = (Vector2)_player.position - _rb.position;
        _rb.linearVelocity = toPlayer.normalized * _data.moveSpeed;
    }

    private void Update()
    {
        if (_damageCooldown > 0f)
            _damageCooldown -= Time.deltaTime;

        TickShieldRegen();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_data == null || _damageCooldown > 0f) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(_data.contactDamage);
        _damageCooldown = _data.damageInterval;

        // Enemy "acting" (dealing contact damage) worsens Infection
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.ReportEnemyAction(_data);
    }

    // ── Shield helpers ────────────────────────────────────────────────────────

    private void TickShieldRegen()
    {
        if (HasShield || _data == null || _data.shieldRegenerateTime <= 0f) return;
        if (_shieldRegenTimer <= 0f) return;

        _shieldRegenTimer -= Time.deltaTime;
        if (_shieldRegenTimer <= 0f)
            RegenerateShield();
    }

    private void RegenerateShield()
    {
        HasShield              = true;
        _shieldLayersRemaining = _data.shieldLayers;
        _pulsesOnCurrentLayer  = 0;
        _boltHitsOnCurrentLayer = 0;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_data == null || _sr == null) return;
        _sr.color = HasShield ? GetCurrentLayerColor() : _data.color;
    }

    /// <summary>Dims the current shield colour towards the base colour based on the furthest-along strip progress within the layer (pulse or bolt).</summary>
    private void UpdateShieldDamageVisual()
    {
        if (_data == null || _sr == null || !HasShield) return;

        float pulseProgress = _data.pulsesPerLayer   > 0 ? (float)_pulsesOnCurrentLayer   / _data.pulsesPerLayer   : 0f;
        float boltProgress  = _data.boltHitsPerLayer > 0 ? (float)_boltHitsOnCurrentLayer / _data.boltHitsPerLayer : 0f;
        float progress      = Mathf.Max(pulseProgress, boltProgress);

        Color dimmed   = Color.Lerp(GetCurrentLayerColor(), _data.color, progress * 0.55f);
        _sr.color      = dimmed;
    }

    private Color GetCurrentLayerColor()
    {
        if (_data.shieldLayerColors != null && _data.shieldLayerColors.Length > 0)
        {
            int colorIndex = Mathf.Clamp(
                _data.shieldLayers - _shieldLayersRemaining,
                0,
                _data.shieldLayerColors.Length - 1);
            return _data.shieldLayerColors[colorIndex];
        }
        return _data.shieldColor;
    }

    // ── Sprite generation ─────────────────────────────────────────────────────

    private static Sprite GetOrCreateSprite()
    {
        if (s_cachedSprite != null) return s_cachedSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point
        };

        var pixels = new Color[size * size];
        System.Array.Fill(pixels, Color.white);
        tex.SetPixels(pixels);
        tex.Apply();

        s_cachedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        return s_cachedSprite;
    }
}