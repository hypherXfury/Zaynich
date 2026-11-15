using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy that spreads into a slot around the player (same static slot registry
/// idea as SpitterEnemy, so several instances take up different angles instead
/// of stacking), then periodically rushes the player in a straight-line charge
/// (same mechanic as ChargerEnemy) and drops a Biofilm puddle on the attack
/// (same as OXAAcinetobacterEnemy). Because each instance holds its own slot
/// before charging, several Stenotrophomonas rushing at once converge on the
/// player from different directions — surrounding rather than clumping.
/// Inherits base stats (health, shields, contact damage) from Enemy.
///
/// This component owns movement in every state (positioning and charging),
/// not just during the charge — so Enemy.FixedUpdate has an unconditional
/// guard for it, same as it does for ChargerEnemy/Boss/OXAAcinetobacterEnemy.
///
/// SETUP:
///   1. Add this component alongside Enemy on the Stenotrophomonas prefab.
///   2. Assign _animator. Its Controller must have states named exactly
///      "idle" and "attack" (same convention as every other enemy —
///      Animator.Play looks up states by exact name; a mismatch fails
///      silently with "GotoState: State could not be found" in the console).
///   3. Assign _biofilmPrefab (the Biofilm prefab).
///   4. On the attack clip, add an Animation Event (at the frame the puddle
///      should appear — windup or landing, your call) calling the public
///      method "SpawnBiofilm".
///   5. If _animator is left unassigned, biofilm drops automatically at the
///      end of each charge instead, so it's testable before animations are
///      wired up.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class StenotrophomonasEnemy : MonoBehaviour
{
    private enum State
    {
        Positioning,
        Charging
    }

    // ── Slot registry (static — shared across all StenotrophomonasEnemy instances) ─

    /// <summary>Maximum number of simultaneous surrounding positions around the player.</summary>
    private const int MaxSlots = 12;

    private static readonly bool[] s_slotOccupied = new bool[MaxSlots];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSlots()
    {
        for (int i = 0; i < MaxSlots; i++) s_slotOccupied[i] = false;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Positioning")]
    [Tooltip("Distance from the player this enemy holds while waiting to charge.")]
    [SerializeField] private float _holdDistance = 4f;

    [Tooltip("Once settled into a slot, it holds that spot instead of following the " +
             "player. It only re-anchors once the player has moved further than this " +
             "distance away from the held spot.")]
    [SerializeField] private float _repositionTriggerDistance = 6f;

    [Tooltip("Minimum distance kept between neighbouring Stenotrophomonas (world units).")]
    [SerializeField] private float _separationDistance = 1.8f;

    [Tooltip("How strongly this enemy steers away from neighbours. Higher = snappier separation.")]
    [SerializeField] private float _separationStrength = 3f;

    [SerializeField] private float _positioningSpeed = 2f;

    [Header("Charge Attack")]
    [SerializeField] private float _minChargeCooldown = 3f;
    [SerializeField] private float _maxChargeCooldown = 6f;
    [SerializeField] private float _chargeSpeed = 10f;
    [Tooltip("One-time damage dealt if the charge connects with the player.")]
    [SerializeField] private float _chargeDamage = 20f;
    [Tooltip("Safety timeout — if the player is dodged, the charge gives up and the enemy re-positions.")]
    [SerializeField] private float _chargeMaxDuration = 1.5f;

    [Header("Animation")]
    [Tooltip("Animator holding the idle/attack states. Leave unassigned to test without animations.")]
    [SerializeField] private Animator _animator;

    [Header("Biofilm")]
    [SerializeField] private GameObject _biofilmPrefab;
    [Tooltip("Optional spawn point. Falls back to this enemy's own position.")]
    [SerializeField] private Transform _biofilmSpawnPoint;

    private const string IdleStateName   = "idle";
    private const string AttackStateName = "attack";

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Transform   _player;
    private int          _slotIndex = -1;
    private Vector2      _targetPosition;
    private bool          _hasSlot;
    private bool          _hasHitPlayerThisCharge;
    private State         _state = State.Positioning;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        ClaimSlot();

        if (_hasSlot && _player != null)
            _targetPosition = SlotPositionAroundPlayer();

        PlayIdle();
        StartCoroutine(ChargeLoop());
    }

    private void OnDestroy() => ReleaseSlot();

    private void Update()
    {
        if (_player == null || !_hasSlot || _state != State.Positioning) return;

        float distToHeldSpot = Vector2.Distance(_player.position, _targetPosition);
        if (distToHeldSpot > _repositionTriggerDistance)
            _targetPosition = SlotPositionAroundPlayer();
    }

    private void FixedUpdate()
    {
        if (_state == State.Positioning)
            TickPositioning();
        // Charging is driven entirely by DoCharge's own velocity assignment.
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void TickPositioning()
    {
        if (_player == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 destination = _hasSlot ? _targetPosition : (Vector2)_player.position;
        Vector2 toDestination = destination - _rb.position;
        float   dist          = toDestination.magnitude;

        Vector2 separation = GetSeparationForce();

        _rb.linearVelocity = dist <= 0.15f
            ? separation
            : toDestination.normalized * _positioningSpeed + separation;
    }

    private Vector2 SlotPositionAroundPlayer()
    {
        float angle = _slotIndex * (360f / MaxSlots) * Mathf.Deg2Rad;
        return (Vector2)_player.position
             + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _holdDistance;
    }

    private Vector2 GetSeparationForce()
    {
        Vector2 separation = Vector2.zero;
        var allEnemies = Enemy.All;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            Enemy other = allEnemies[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (!other.TryGetComponent<StenotrophomonasEnemy>(out _)) continue;

            Vector2 away = _rb.position - (Vector2)other.transform.position;
            float   dist = away.magnitude;

            if (dist < _separationDistance && dist > 0.001f)
                separation += away.normalized * (_separationDistance - dist) / _separationDistance;
        }

        return separation * _separationStrength;
    }

    // ── Slot management ───────────────────────────────────────────────────────

    private void ClaimSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (!s_slotOccupied[i])
            {
                s_slotOccupied[i] = true;
                _slotIndex        = i;
                _hasSlot          = true;
                return;
            }
        }

        _hasSlot = false;
        Debug.LogWarning("[StenotrophomonasEnemy] All positioning slots are full. " +
                         "Increase MaxSlots if you spawn more than " + MaxSlots + " at once.");
    }

    private void ReleaseSlot()
    {
        if (_slotIndex >= 0 && _slotIndex < MaxSlots)
            s_slotOccupied[_slotIndex] = false;
    }

    // ── Charge attack ─────────────────────────────────────────────────────────

    private IEnumerator ChargeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(_minChargeCooldown, _maxChargeCooldown));

            if (_player == null || _state != State.Positioning) continue;

            yield return DoCharge();
        }
    }

    private IEnumerator DoCharge()
    {
        _state = State.Charging;
        _hasHitPlayerThisCharge = false;
        PlayAttack();

        Vector2 direction = ((Vector2)_player.position - _rb.position).normalized;
        float   elapsed   = 0f;

        while (elapsed < _chargeMaxDuration && !_hasHitPlayerThisCharge)
        {
            elapsed += Time.deltaTime;
            _rb.linearVelocity = direction * _chargeSpeed;
            yield return null;
        }

        _rb.linearVelocity = Vector2.zero;

        if (_animator == null)
            SpawnBiofilm();

        _state = State.Positioning;
        PlayIdle();
    }

    // ── Charge hit detection ─────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other) => TryHitPlayer(other);
    private void OnTriggerStay2D(Collider2D other)  => TryHitPlayer(other);

    private void TryHitPlayer(Collider2D other)
    {
        if (_state != State.Charging || _hasHitPlayerThisCharge) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        _hasHitPlayerThisCharge = true;
        playerHealth.TakeDamage(_chargeDamage);
    }

    // ── Animation ────────────────────────────────────────────────────────────

    private void PlayIdle()
    {
        if (_animator != null) _animator.Play(IdleStateName);
    }

    private void PlayAttack()
    {
        if (_animator != null) _animator.Play(AttackStateName, 0, 0f);
    }

    // Public — invoked directly as an Animation Event on the attack clip.
    public void SpawnBiofilm()
    {
        if (_biofilmPrefab == null)
        {
            Debug.LogWarning($"[StenotrophomonasEnemy] biofilmPrefab is not assigned on {name}.", this);
            return;
        }

        Vector3 spawnPos = _biofilmSpawnPoint != null ? _biofilmSpawnPoint.position : transform.position;
        Instantiate(_biofilmPrefab, spawnPos, Quaternion.identity);
    }
}
