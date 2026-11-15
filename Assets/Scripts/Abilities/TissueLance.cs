using UnityEngine;

/// <summary>
/// Tissue Penetration Lance — automatically fires piercing lances in the player's facing direction.
/// Represents Zaynich reaching deep tissue. At level 5 a three-lance trident volley is fired.
/// Attach to the Player GameObject alongside FourDirPreviewController.
/// Wire <see cref="_data"/> and <see cref="_lancePrefab"/> in the Inspector.
/// Call <see cref="LevelUp"/> to advance through the five levels.
/// FourDirPreviewController calls <see cref="SetFacing"/> automatically — no manual wiring needed.
/// </summary>
public class TissueLance : MonoBehaviour
{
    [SerializeField] private LanceData _data;
    [SerializeField] private Lance     _lancePrefab;

    [Header("Facing-Direction Targeting")]
    [Tooltip("Half-angle of the forward cone in degrees.\n" +
             "90 = full front hemisphere. 45 = tight forward cone.")]
    [Range(10f, 180f)]
    [SerializeField] private float _targetingConeHalfAngle = 90f;

    // ── State ─────────────────────────────────────────────────────────────────

    private int     _level = 1;
    private float   _fireCooldown;
    private Vector2 _facing = Vector2.right;

    public int Level    => _level;
    public int MaxLevel => _data != null ? _data.Length : 5;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by FourDirPreviewController whenever the player changes direction.
    /// </summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            _facing = direction.normalized;
    }

    /// <summary>Advances the ability by one level, up to the maximum.</summary>
    public void LevelUp()
    {
        if (_data == null || _level >= MaxLevel) return;
        _level++;
        Debug.Log($"[TissueLance] Level {_level}");
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        if (_data == null || _lancePrefab == null) return;

        _fireCooldown -= Time.deltaTime;
        if (_fireCooldown > 0f) return;

        // Only fire if at least one enemy is inside the forward cone
        Transform target = FindNearestInCone();
        if (target == null) return;

        FireLances(target.position);

        LanceLevelConfig cfg = _data.GetLevel(_level);
        _fireCooldown = 1f / cfg.fireRate;
    }

    // ── Targeting ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nearest enemy inside the forward cone, or null if none found.
    /// </summary>
    private Transform FindNearestInCone()
    {
        var       enemies      = Enemy.All;
        float     cosThreshold = Mathf.Cos(_targetingConeHalfAngle * Mathf.Deg2Rad);
        Vector3   myPos        = transform.position;
        Transform best         = null;
        float     minSq        = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            Vector2 toEnemy = (Vector2)(enemies[i].transform.position - myPos);
            if (Vector2.Dot(_facing, toEnemy.normalized) < cosThreshold) continue;
            float sq = toEnemy.sqrMagnitude;
            if (sq < minSq) { minSq = sq; best = enemies[i].transform; }
        }
        return best;
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires lances spread around the direction toward the nearest in-cone enemy.
    /// The center lance aims directly at the enemy; side lances spread around that angle.
    /// All lances are guaranteed to be within the forward cone since the target is in-cone.
    /// </summary>
    private void FireLances(Vector3 targetPosition)
    {
        LanceLevelConfig cfg = _data.GetLevel(_level);

        // Aim the center lance directly at the nearest in-cone enemy
        Vector2 toTarget  = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        float   baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

        for (int i = 0; i < cfg.lanceCount; i++)
        {
            float offset = cfg.lanceCount > 1
                ? Mathf.Lerp(-cfg.lanceSpread * 0.5f, cfg.lanceSpread * 0.5f,
                             (float)i / (cfg.lanceCount - 1))
                : 0f;

            float   rad = (baseAngle + offset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Lance lance = Instantiate(_lancePrefab, transform.position, Quaternion.identity);
            lance.Initialize(cfg.damage, dir);
        }
    }

    // ── Debug visualisation ───────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float halfRad    = _targetingConeHalfAngle * Mathf.Deg2Rad;
        float facingAngle = Mathf.Atan2(_facing.y, _facing.x);
        Vector3 origin   = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, (Vector3)_facing * 3f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, new Vector3(Mathf.Cos(facingAngle + halfRad), Mathf.Sin(facingAngle + halfRad)) * 3f);
        Gizmos.DrawRay(origin, new Vector3(Mathf.Cos(facingAngle - halfRad), Mathf.Sin(facingAngle - halfRad)) * 3f);

        var enemies = Enemy.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            Vector2 toEnemy = (Vector2)(enemies[i].transform.position - origin);
            float dot = Vector2.Dot(_facing, toEnemy.normalized);
            Gizmos.color = dot >= Mathf.Cos(halfRad) ? Color.green : Color.red;
            Gizmos.DrawWireSphere(enemies[i].transform.position, 0.2f);
        }
    }
}