using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy that continuously roams around the player inside a Chamber and
/// periodically pauses to attack by dropping a Biofilm puddle at its own
/// position. Inherits base stats (health, shields, contact damage) from
/// Enemy — this component owns movement and the idle/attack cycle instead
/// of Enemy's default straight-line chase. Built for OXAAcinetobacter.
///
/// Like ChargerEnemy, this component owns movement in EVERY state (roam and
/// attack), not just during a brief special move — so Enemy.FixedUpdate has
/// an unconditional guard for it, the same way it does for Boss/ChargerEnemy.
///
/// SETUP:
///   1. Add this component alongside Enemy on the OXAAcinetobacter prefab.
///   2. Assign _animator. Its Controller must have states named exactly
///      "idle" and "attack" (same convention as every other enemy —
///      Animator.Play looks up states by exact name; a mismatch fails
///      silently with "GotoState: State could not be found" in the console).
///   3. Assign _biofilmPrefab (the Biofilm prefab).
///   4. On the attack clip, add an Animation Event at the release frame
///      calling the public method "SpawnBiofilm".
///   5. If _animator is left unassigned, the attack fires immediately and
///      waits _noAnimatorAttackDuration instead, so it's testable before
///      animations are wired up.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class OXAAcinetobacterEnemy : MonoBehaviour
{
    [Header("Roaming")]
    [SerializeField] private float _roamSpeed = 1.8f;
    [Tooltip("Roam targets are picked within this radius of the player.")]
    [SerializeField] private float _roamRadius = 3f;
    [Tooltip("Seconds before picking a new roam target, even if the current one hasn't been reached.")]
    [SerializeField] private float _roamPointInterval = 2.5f;
    [SerializeField] private float _roamArriveThreshold = 0.2f;

    [Header("Animation")]
    [Tooltip("Animator holding the idle/attack states. Leave unassigned to test without animations.")]
    [SerializeField] private Animator _animator;

    [Header("Attack")]
    [Tooltip("Seconds spent roaming between biofilm attacks.")]
    [SerializeField] private float _attackCooldown = 4f;

    [Tooltip("Used only when no Animator is assigned — how long the attack 'holds' before resuming roam.")]
    [SerializeField] private float _noAnimatorAttackDuration = 0.5f;

    [Header("Biofilm")]
    [SerializeField] private GameObject _biofilmPrefab;

    [Tooltip("Optional spawn point. Falls back to this enemy's own position.")]
    [SerializeField] private Transform _biofilmSpawnPoint;

    private const string IdleStateName   = "idle";
    private const string AttackStateName = "attack";

    private Rigidbody2D _rb;
    private Transform   _player;
    private Vector2      _roamTarget;
    private float        _roamTimer;
    private bool          _isAttacking;

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        PickNewRoamTarget();
        PlayIdle();

        StartCoroutine(AttackLoop());
    }

    private void FixedUpdate()
    {
        if (_isAttacking || _player == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        TickRoam();
    }

    // ── Roaming ───────────────────────────────────────────────────────────────

    private void TickRoam()
    {
        _roamTimer += Time.fixedDeltaTime;

        bool reachedTarget = Vector2.Distance(_rb.position, _roamTarget) <= _roamArriveThreshold;
        if (_roamTimer >= _roamPointInterval || reachedTarget)
        {
            PickNewRoamTarget();
            _roamTimer = 0f;
        }

        Vector2 toTarget = _roamTarget - _rb.position;
        _rb.linearVelocity = toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized * _roamSpeed
            : Vector2.zero;
    }

    private void PickNewRoamTarget()
    {
        if (_player == null) return;
        _roamTarget = (Vector2)_player.position + Random.insideUnitCircle * _roamRadius;
    }

    // ── Attack loop ──────────────────────────────────────────────────────────

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(_attackCooldown);
            if (_player == null) continue;
            yield return DoAttack();
        }
    }

    private IEnumerator DoAttack()
    {
        _isAttacking = true;
        PlayAttack();

        if (_animator != null)
        {
            yield return null; // let the Animator register the new state this frame
            float length = _animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(length);
        }
        else
        {
            SpawnBiofilm();
            yield return new WaitForSeconds(_noAnimatorAttackDuration);
        }

        _isAttacking = false;
        PlayIdle();
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
            Debug.LogWarning($"[OXAAcinetobacterEnemy] biofilmPrefab is not assigned on {name}.", this);
            return;
        }

        Vector3 spawnPos = _biofilmSpawnPoint != null ? _biofilmSpawnPoint.position : transform.position;
        Instantiate(_biofilmPrefab, spawnPos, Quaternion.identity);
    }
}
