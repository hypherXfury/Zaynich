using UnityEngine;

/// <summary>
/// Enemy that periodically spits a bolt toward the player.
/// Inherits all base behaviour from Enemy (movement, shields, health).
///
/// SPREADING:
/// A static slot registry divides the circle around the player into evenly-spaced
/// angle slots. Each SpitterEnemy claims one slot on Start and releases it on Destroy.
/// Enemy.FixedUpdate steers toward the slot position instead of straight at the player,
/// so multiple spitters naturally spread out around the player rather than stacking.
///
/// SETUP:
///   1. Add this component alongside Enemy on your prefab.
///   2. Assign boltPrefab and (optionally) mouthTransform in the Inspector.
///   3. Add an Animation Event on the mouth-open frame pointing to "FireBolt".
/// </summary>
[RequireComponent(typeof(Enemy))]
public class SpitterEnemy : MonoBehaviour
{
    // ── Slot registry (static — shared across all SpitterEnemy instances) ─────

    /// <summary>
    /// Maximum number of simultaneous spitter positions around the player.
    /// Increase if you ever spawn more than this many spitters at once.
    /// </summary>
    private const int MaxSlots = 12;

    private static readonly bool[] s_slotOccupied = new bool[MaxSlots];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSlots()
    {
        for (int i = 0; i < MaxSlots; i++) s_slotOccupied[i] = false;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Positioning")]
    [Tooltip("Spitter stops and shoots from this distance from the player.")]
    [SerializeField] private float _stopDistance = 4f;

    [Tooltip("Once the spitter has settled into a spot, it holds that spot instead of " +
             "following the player. It only picks a new spot once the player has moved " +
             "further than this distance away from the held spot.")]
    [SerializeField] private float _repositionTriggerDistance = 6f;

    [Tooltip("Minimum distance spitters keep between each other (world units). " +
             "Increase if they still feel too close together.")]
    [SerializeField] private float _separationDistance = 1.8f;

    [Tooltip("How strongly the spitter steers away from neighbouring spitters. " +
             "Higher = snappier separation.")]
    [SerializeField] private float _separationStrength = 3f;

    [Header("Bolt Settings")]
    [SerializeField] private GameObject _boltPrefab;
    [SerializeField] private Transform  _mouthTransform;
    [SerializeField] private float      _boltSpeed    = 7f;
    [SerializeField] private float      _boltDamage   = 8f;
    [SerializeField] private float      _boltLifetime = 4f;

    // ── Public API read by Enemy.FixedUpdate ──────────────────────────────────

    /// <summary>Enemy stops moving when closer than this to its target position.</summary>
    public float StopDistance => _stopDistance;

    /// <summary>World-space position this spitter should move toward (its slot position).</summary>
    public Vector2 TargetPosition { get; private set; }

    /// <summary>True once a slot has been assigned and TargetPosition is valid.</summary>
    public bool HasSlot { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────

    private Transform _player;
    private int       _slotIndex = -1;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        ClaimSlot();

        // Settle into an initial spot right away instead of waiting a frame.
        if (HasSlot && _player != null)
            TargetPosition = SlotPositionAroundPlayer();
    }

    private void OnDestroy() => ReleaseSlot();

    private void Update()
    {
        if (_player == null || !HasSlot) return;

        // Hold the current spot. Only recompute (re-anchor around the player's
        // new position) once the player has wandered far enough away from it.
        float distToHeldSpot = Vector2.Distance(_player.position, TargetPosition);
        if (distToHeldSpot > _repositionTriggerDistance)
            TargetPosition = SlotPositionAroundPlayer();
    }

    private Vector2 SlotPositionAroundPlayer()
    {
        float angle = _slotIndex * (360f / MaxSlots) * Mathf.Deg2Rad;
        return (Vector2)_player.position
             + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _stopDistance;
    }

    // ── Slot management ───────────────────────────────────────────────────────

    private void ClaimSlot()
    {
        // Find the first free slot
        for (int i = 0; i < MaxSlots; i++)
        {
            if (!s_slotOccupied[i])
            {
                s_slotOccupied[i] = true;
                _slotIndex        = i;
                HasSlot           = true;
                return;
            }
        }

        // All slots taken — fall back to no slot (will just stop at stopDistance)
        HasSlot = false;
        Debug.LogWarning("[SpitterEnemy] All positioning slots are full. " +
                         "Increase MaxSlots if you spawn more than " + MaxSlots + " spitters.");
    }

    private void ReleaseSlot()
    {
        if (_slotIndex >= 0 && _slotIndex < MaxSlots)
            s_slotOccupied[_slotIndex] = false;
    }

    // ── Separation from other spitters ────────────────────────────────────────

    /// <summary>
    /// Returns a steering vector that pushes this spitter away from any
    /// neighbouring SpitterEnemy that is too close. Called by Enemy.FixedUpdate.
    /// </summary>
    public Vector2 GetSeparationForce()
    {
        Vector2 separation = Vector2.zero;
        var allEnemies = Enemy.All;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            Enemy other = allEnemies[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (!other.TryGetComponent<SpitterEnemy>(out _)) continue;

            Vector2 away = _rb.position - (Vector2)other.transform.position;
            float   dist = away.magnitude;

            if (dist < _separationDistance && dist > 0.001f)
                separation += away.normalized * (_separationDistance - dist) / _separationDistance;
        }

        return separation * _separationStrength;
    }

    private Rigidbody2D _rb;
    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    // ── Animation Event ───────────────────────────────────────────────────────

    public void FireBolt()
    {
        if (_boltPrefab == null)
        {
            Debug.LogWarning($"[SpitterEnemy] boltPrefab is not assigned on {name}.", this);
            return;
        }
        if (_player == null) return;

        // Spitting a bolt counts as the enemy "acting" — worsens Infection
        if (VitalsManager.Instance != null && TryGetComponent<Enemy>(out var enemyComp))
        {
            // EnemyData isn't directly exposed on Enemy, so we report with null
            // and let VitalsManager fall back to its default infection value.
            // (If you want spitter-specific infection values, expose EnemyData
            // publicly on Enemy and pass it here instead.)
            VitalsManager.Instance.ReportEnemyAction(null);
        }

        Vector3 spawnPos = _mouthTransform != null ? _mouthTransform.position : transform.position;
        Vector2 dir      = ((Vector2)_player.position - (Vector2)spawnPos).normalized;

        GameObject boltGO = Instantiate(_boltPrefab, spawnPos, Quaternion.identity);
        float angle       = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        boltGO.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        if (boltGO.TryGetComponent<EnemyBolt>(out var bolt))
            bolt.Launch(dir, _boltSpeed, _boltDamage, _boltLifetime);
        else if (boltGO.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.gravityScale   = 0f;
            rb.linearVelocity = dir * _boltSpeed;
            Destroy(boltGO, _boltLifetime);
        }
    }
}