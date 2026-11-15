using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy that loosely roams around the player, then periodically locks onto
/// the player's current position and charges in a straight line at high
/// speed. Deals a large one-time hit on contact during the charge (separate
/// from the base Enemy's regular contact-damage tick, which stays active the
/// whole time — see JumperEnemy's shockwave doc comment for the same split).
/// After the charge connects or times out, it retreats back to its spawn
/// position before resuming roam behaviour. Built for KPCKlebsiella (Level 2).
///
/// Unlike SpitterEnemy/JumperEnemy, this component owns movement in EVERY
/// state (roam/charge/retreat), not just during a brief special move — so
/// Enemy.FixedUpdate has an unconditional guard for it, the same way it does
/// for Boss.
///
/// SETUP:
///   1. Add this component alongside Enemy on the prefab.
///   2. Assign _animator. Its Controller must have states named exactly
///      "idle" and "attack" (matches KPCKlebsiella's EColi_3.controller —
///      Animator.Play looks up states by exact name; a mismatch fails
///      silently with "GotoState: State could not be found" in the console).
///   3. No Animation Events needed — the charge/retreat timing is driven
///      entirely by this script, not by clip frames.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class ChargerEnemy : MonoBehaviour
{
    private enum State
    {
        Roaming,
        Charging,
        Retreating
    }

    [Header("Roaming")]
    [SerializeField] private float _roamSpeed = 1.5f;
    [Tooltip("Roam targets are picked within this radius of the player.")]
    [SerializeField] private float _roamRadius = 3f;
    [Tooltip("Seconds before picking a new roam target, even if the current one hasn't been reached.")]
    [SerializeField] private float _roamPointInterval = 2.5f;
    [SerializeField] private float _roamArriveThreshold = 0.2f;

    [Header("Charge Attack")]
    [SerializeField] private float _minChargeCooldown = 3f;
    [SerializeField] private float _maxChargeCooldown = 6f;
    [SerializeField] private float _chargeSpeed = 10f;
    [Tooltip("One-time damage dealt if the charge connects with the player.")]
    [SerializeField] private float _chargeDamage = 25f;
    [Tooltip("Safety timeout — if the player is dodged, the charge gives up and retreats after this long.")]
    [SerializeField] private float _chargeMaxDuration = 1.5f;

    [Header("Retreat")]
    [SerializeField] private float _retreatSpeed = 3f;
    [SerializeField] private float _retreatArriveThreshold = 0.2f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    private const string IdleStateName   = "idle";
    private const string AttackStateName = "attack";

    private Rigidbody2D _rb;
    private Transform _player;
    private Vector2 _homePosition;
    private Vector2 _roamTarget;
    private float _roamTimer;
    private bool _hasHitPlayerThisCharge;
    private State _state = State.Roaming;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        _homePosition = _rb.position;
        PickNewRoamTarget();
        PlayIdle();

        StartCoroutine(ChargeLoop());
    }

    private void FixedUpdate()
    {
        if (_player == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (_state)
        {
            case State.Roaming:    TickRoam();    break;
            case State.Retreating: TickRetreat(); break;
            case State.Charging:   break; // driven entirely by DoCharge's own velocity assignment
        }
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

    // ── Charge attack ─────────────────────────────────────────────────────────

    private IEnumerator ChargeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(_minChargeCooldown, _maxChargeCooldown));

            if (_player == null || _state != State.Roaming) continue;

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
        _state = State.Retreating;
        PlayIdle();
    }

    // ── Retreat ───────────────────────────────────────────────────────────────

    private void TickRetreat()
    {
        Vector2 toHome = _homePosition - _rb.position;

        if (toHome.magnitude <= _retreatArriveThreshold)
        {
            _rb.linearVelocity = Vector2.zero;
            _state = State.Roaming;
            PickNewRoamTarget();
            return;
        }

        _rb.linearVelocity = toHome.normalized * _retreatSpeed;
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
        if (_animator != null) _animator.Play(AttackStateName);
    }
}
