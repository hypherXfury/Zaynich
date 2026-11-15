using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Synergy Field — a passive damaging aura that surrounds Zaynich.
/// Represents the cefepime + zidebactam synergy. Always active while equipped.
/// Deals tick-based damage to all unshielded enemies within its radius.
/// Displays a gently pulsing ring visual that scales with the current level radius.
/// Attach to the Player GameObject. Wire <see cref="_data"/> in the Inspector.
/// Call <see cref="LevelUp"/> to advance through the five levels.
/// </summary>
public class SynergyField : MonoBehaviour
{
    private static readonly Color FieldColor = new Color(0.25f, 1f, 0.55f, 0.22f);
    private const float PulseSpeed = 2.2f;

    [SerializeField] private SynergyFieldData _data;

    [Header("Debug")]
    [Tooltip("Draws the exact damage radius as a wire circle. Always in the Scene view; " +
             "in the Game view too once its Gizmos toggle is on.")]
    [SerializeField] private bool _drawRadiusGizmo = true;
    [Tooltip("Renders the in-game ring at full opacity instead of the subtle 22% tint, " +
             "so the AoE edge is obvious while testing.")]
    [SerializeField] private bool _highlightRing;

    private int   _level = 1;
    private float _tickTimer;
    private float _pulseTime;

    // Visual ring child
    private Transform      _ringTransform;
    private SpriteRenderer _ringSr;
    private static Sprite  s_cachedRing;

    public int Level    => _level;
    public int MaxLevel => _data != null ? _data.Length : 5;

    /// <summary>Advances the ability by one level, up to the maximum.</summary>
    public void LevelUp()
    {
        if (_data == null || _level >= MaxLevel) return;
        _level++;
        Debug.Log($"[SynergyField] Level {_level}");
    }

    private void OnEnable()
    {
        EnsureRing();
        _ringSr.enabled = true;
        _ringSr.color   = BaseFieldColor;
        RefreshVisualScale();
    }

    private void OnDisable()
    {
        if (_ringSr != null) _ringSr.enabled = false;
    }

    /// <summary>
    /// Builds the ring child on first enable rather than in Awake: Unity runs
    /// Awake even on a disabled component, so creating it there left a locked
    /// Synergy Field drawing a static aura it wasn't actually projecting
    /// (Update doesn't run while disabled, so it never even pulsed).
    /// </summary>
    private void EnsureRing()
    {
        if (_ringSr != null) return;

        // Dedicated child so the ring doesn't interfere with the player's own renderer
        var ringObj = new GameObject("SynergyFieldVFX")
        {
            transform = { parent = transform, localPosition = Vector3.zero }
        };

        _ringSr = ringObj.AddComponent<SpriteRenderer>();
        _ringSr.sprite       = GetOrCreateRing();
        _ringSr.sortingOrder = -1;

        _ringTransform = ringObj.transform;
    }

    /// <summary>Ring tint before the pulse is applied — opaque when the debug highlight is on.</summary>
    private Color BaseFieldColor
    {
        get
        {
            Color c = FieldColor;
            if (_highlightRing) c.a = 0.85f;
            return c;
        }
    }

    private void Update()
    {
        if (_data == null) return;

        SynergyFieldLevelConfig cfg = _data.GetLevel(_level);

        // Damage tick
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= cfg.tickInterval)
        {
            _tickTimer = 0f;
            ApplyFieldDamage(cfg);
        }

        // Gentle ring pulse
        _pulseTime += Time.deltaTime * PulseSpeed;
        float pulse = 1f + Mathf.Sin(_pulseTime) * 0.04f;
        _ringTransform.localScale = Vector3.one * GetVisualScale(cfg) * pulse;

        Color c = BaseFieldColor;
        c.a     = Mathf.Clamp01(c.a + Mathf.Sin(_pulseTime) * 0.05f);
        _ringSr.color = c;
    }

    /// <summary>
    /// The pulsing ring sprite is only a visual approximation of the field —
    /// this draws the exact radius ApplyFieldDamage() tests against, so the two
    /// can be compared while tuning. Dimmed while the ability is locked.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_drawRadiusGizmo || _data == null) return;

        // _level stays 1 outside play mode, so this previews the Lv 1 radius in the editor.
        float radius = _data.GetLevel(_level).radius;
        Gizmos.color = enabled ? new Color(0.25f, 1f, 0.55f, 0.9f) : new Color(1f, 1f, 1f, 0.3f);

        const int segments = 64;
        Vector3 center = transform.position;
        Vector3 prev   = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle  = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void ApplyFieldDamage(SynergyFieldLevelConfig cfg)
    {
        float   damageThisTick = cfg.damagePerSecond * cfg.tickInterval;
        float   radiusSq       = cfg.radius * cfg.radius;
        Vector3 origin         = transform.position;

        // Snapshot to avoid collection mutation during iteration
        var snapshot = new List<Enemy>(Enemy.All);
        foreach (Enemy enemy in snapshot)
        {
            if (enemy == null) continue;
            if ((enemy.transform.position - origin).sqrMagnitude > radiusSq) continue;
            // TakeDamage already guards against shielded enemies internally
            enemy.TakeDamage(damageThisTick);
        }
    }

    private void RefreshVisualScale()
    {
        if (_data == null || _ringTransform == null) return;
        _ringTransform.localScale = Vector3.one * GetVisualScale(_data.GetLevel(_level));
    }

    /// <summary>
    /// The ring sprite is 2wu wide at scale 1 (ppu=64, texSize=128 → radius = 1wu),
    /// so <c>radius</c> alone would map straight to the world-space field radius —
    /// except the ring is a child of the player, whose own transform is scaled
    /// (0.2 in the level scenes). localScale is relative to that, so the parent
    /// scale has to be divided back out; otherwise the drawn ring comes out at a
    /// fraction of the area actually being damaged.
    /// </summary>
    private float GetVisualScale(SynergyFieldLevelConfig cfg)
    {
        float parentScale = transform.lossyScale.x;
        return Mathf.Approximately(parentScale, 0f) ? cfg.radius : cfg.radius / parentScale;
    }

    private static Sprite GetOrCreateRing() =>
        s_cachedRing ??= ProceduralSprites.CreateRing(128, 8f, 64f);
}
