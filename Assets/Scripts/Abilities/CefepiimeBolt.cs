using UnityEngine;

/// <summary>
/// Cefepime Bolt — fires bolts in the player's current facing direction.
/// ONLY fires when SetCanShoot(true) — which FourDirPreviewController calls
/// only when the player is IDLE and an enemy is within attack radius.
/// This is the Archero mechanic: stop moving → auto-aim → shoot.
/// </summary>
public class CefepiimeBolt : MonoBehaviour
{
    private const float BoltSpeed   = 9f;
    private const float BoltLifetime = 4f;

    [SerializeField] private BoltData _data;
    [SerializeField] private Bolt     _boltPrefab;

    [Header("Gun Tip Positions (8 directions)")]
    [Tooltip("8 transforms, one per facing direction.\n" +
             "Order: Down / Left / Right / Up / UpRight / DownRight / UpLeft / DownLeft")]
    [SerializeField] private Transform[] gunTipPos;

    // ── State ─────────────────────────────────────────────────────────────────

    private int     _level       = 1;
    private float   _fireCooldown;
    private Vector2 _facing      = Vector2.up;
    private bool    _canShoot    = false;

    private FourDirPreviewController _player;

    public int Level    => _level;
    public int MaxLevel => _data != null ? _data.Length : 5;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        _player = GetComponent<FourDirPreviewController>();
    }

    private void Update()
    {
        if (_data == null || _boltPrefab == null) return;
        if (!_canShoot) return;   // moving or no enemy in range — don't fire

        _fireCooldown -= Time.deltaTime;
        if (_fireCooldown > 0f) return;

        Fire();

        BoltLevelConfig cfg = _data.GetLevel(_level);
        _fireCooldown = 1f / cfg.fireRate;
    }

    /// <summary>
    /// Raised every time a bolt actually leaves the barrel. The tutorial listens
    /// for the first one: since firing only happens while the player is idle with
    /// an enemy in range, it's proof they've understood "stop moving to shoot"
    /// rather than just proof the enemy died somehow.
    /// </summary>
    public event System.Action OnBoltFired;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by FourDirPreviewController.
    /// True = idle + enemy in range → shoot.
    /// False = moving or no enemy → don't shoot.
    /// </summary>
    public void SetCanShoot(bool canShoot) => _canShoot = canShoot;

    /// <summary>Called by FourDirPreviewController when facing changes.</summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            _facing = direction.normalized;
    }

    public void LevelUp()
    {
        if (_data == null || _level >= MaxLevel) return;
        _level++;
        Debug.Log($"[CefepiimeBolt] Level {_level}");
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    private void Fire()
    {
        BoltLevelConfig cfg = _data.GetLevel(_level);

        // Spawn at the correct gun tip for the current facing direction
        int       tipIndex = _player != null ? _player.GetGunTipPosIndex() : 3;
        Transform tip      = (gunTipPos != null && tipIndex < gunTipPos.Length)
                             ? gunTipPos[tipIndex]
                             : transform;

        Bolt bolt = Instantiate(_boltPrefab, tip.position, Quaternion.identity);
        bolt.Initialize(
            cfg.damage,
            cfg.piercingCount,
            cfg.splits,
            cfg.splitCount,
            cfg.splitAngle,
            _facing,
            BoltSpeed,
            BoltLifetime,
            _boltPrefab,
            false);

        OnBoltFired?.Invoke();
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, (Vector3)_facing * 3f);

        float halfRad    = 45f * Mathf.Deg2Rad;
        float facingAngle = Mathf.Atan2(_facing.y, _facing.x);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position,
            new Vector3(Mathf.Cos(facingAngle + halfRad), Mathf.Sin(facingAngle + halfRad)) * 3f);
        Gizmos.DrawRay(transform.position,
            new Vector3(Mathf.Cos(facingAngle - halfRad), Mathf.Sin(facingAngle - halfRad)) * 3f);
    }
}