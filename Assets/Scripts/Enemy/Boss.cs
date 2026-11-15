using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-projectile-type configuration for the Boss's ranged attack. Each entry
/// carries its own prefab plus its own speed/damage/lifetime, so different
/// bolt types (e.g. a fast weak bolt vs. a slow heavy one) can be tuned
/// independently. Mirrors the EnemySpawnConfig pattern used by EnemySpawner.
/// </summary>
[System.Serializable]
public class BossProjectileConfig
{
    public GameObject prefab;
    public float speed    = 7f;
    public float damage   = 10f;
    public float lifetime = 5f;
}

/// <summary>The three mutually-exclusive actions the boss picks between.</summary>
public enum BossAttackType
{
    Projectile,
    Summon,
    Pulse
}

/// <summary>
/// Level 3 boss. Combines two responsibilities on top of the base Enemy
/// (health/shield/contact damage all come from Boss_Data, an EnemyData asset):
///   1. Spawns other enemy types, reusing the same EnemySpawnConfig list used
///      by EnemySpawner.
///   2. Fires projectiles or a circular pulse wave at the player.
///
/// Every _actionCooldown seconds, the boss randomly picks ONE of three
/// attacks — Projectile, Summon, or Pulse — plays that attack's Animator
/// state, and returns to idle afterwards. Only one attack runs at a
/// time; they never overlap.
///
/// The boss does NOT chase the player — it holds its ground and only shuffles
/// left/right around its spawn position (paused while an attack animation is
/// playing). Enemy.FixedUpdate has a guard for Boss (see the "Boss" check
/// next to the JumperEnemy/CannonEnemy checks) so the base chase movement
/// never overrides this component's patrol.
///
/// ANIMATION SETUP (required — mirrors SpitterEnemy.FireBolt's pattern):
///   1. Assign _animator to the Boss's Animator component. Its Controller
///      must have states named exactly: "idle", "Attack", "Summon", "Pulse"
///      (matches Boss.controller's current state names — Animator.Play looks
///      up states by exact name, so a mismatch here fails silently at
///      runtime with "GotoState: State could not be found" in the console).
///   2. On the Attack/Summon/Pulse clips, add an Animation Event at the frame
///      the effect should actually happen (throw/release/peak frame) calling:
///        Attack clip → FireProjectile
///        Summon clip → SummonWave
///        Pulse  clip → FirePulse
///      These must be called as PUBLIC methods — Unity's Animation Event
///      system cannot invoke private ones (it fails silently, or logs
///      "AnimationEvent 'X' has no receiver!"). Boss.cs only plays the
///      animation and waits for its length — it never fires the effect
///      itself, so without a correctly-wired event nothing happens.
///   3. If _animator is left unassigned, Boss falls back to firing the effect
///      immediately and waiting _noAnimatorAttackDuration, so it's still
///      testable before animations are wired up.
///
/// OTHER SETUP:
///   • Assign _projectiles — each entry needs its own prefab plus its own
///     speed/damage/lifetime (each just needs an EnemyBolt component, or any
///     Rigidbody2D-based projectile — see FireProjectile fallback).
///   • Assign _shockwavePrefab — a prefab with a ShockwaveRing component
///     (same one JumperEnemy uses) — for the Pulse attack.
///   • Fill in _spawnConfigs the same way as on EnemySpawner: enemy prefab +
///     enemy data + group size per enemy type. startTime/endTime still gate
///     which types are eligible to summon at the current fight time;
///     spawnInterval is unused here since Summon is now boss-triggered, not
///     on its own timer.
///   • Optionally assign _spawnPoints; if left empty, spawns happen in a ring
///     around the boss's starting position instead.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class Boss : MonoBehaviour
{
    [Header("Enemy Spawning")]
    [SerializeField] private List<EnemySpawnConfig> _spawnConfigs = new();

    [Tooltip("Optional fixed points enemies spawn at. If empty, spawns happen in a ring around the boss.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("Radius used to place spawns around the boss when no spawn points are assigned.")]
    [SerializeField] private float _spawnRingRadius = 6f;

    [SerializeField] private int _maxSpawnedEnemies = 20;

    [Header("Projectile Attack")]
    [Tooltip("Different projectile types the boss can fire, each with its own speed/damage/lifetime. One is picked at random each shot.")]
    [SerializeField] private List<BossProjectileConfig> _projectiles = new();

    [Tooltip("Optional muzzle point. Falls back to the boss's own position.")]
    [SerializeField] private Transform _firePoint;

    [Header("Pulse Attack")]
    [Tooltip("Prefab with a ShockwaveRing component — the same one JumperEnemy uses for its landing shockwave.")]
    [SerializeField] private GameObject _shockwavePrefab;
    [SerializeField] private float _pulseRadius    = 4f;
    [SerializeField] private float _pulseThickness = 0.8f;
    [SerializeField] private float _pulseDamage    = 15f;
    [SerializeField] private float _pulseDuration  = 0.6f;

    [Header("Boss Behaviour")]
    [Tooltip("Animator holding the idle/Attack/Summon/Pulse states. Leave unassigned to test without animations.")]
    [SerializeField] private Animator _animator;

    [Tooltip("Seconds spent idling/patrolling between attacks.")]
    [SerializeField] private float _actionCooldown = 3f;

    [Tooltip("Used only when no Animator is assigned — how long the boss 'holds' an attack before returning to patrol.")]
    [SerializeField] private float _noAnimatorAttackDuration = 0.5f;

    [Header("Patrol Movement")]
    [Tooltip("How far the boss shuffles left/right from its starting position.")]
    [SerializeField] private float _patrolDistance = 4f;
    [SerializeField] private float _patrolSpeed    = 2f;

    [Header("Boss Own Data")]
    [Tooltip("Unlike other enemies, the Boss is hand-placed in the scene rather than " +
             "spawned by EnemySpawner/Chamber/EnemyNest, so nothing else calls " +
             "Enemy.Initialize() for it. Assign Boss_Data here and Boss initializes " +
             "itself on Start.")]
    [SerializeField] private EnemyData _bossData;

    private const string IdleStateName   = "idle";
    private const string AttackStateName = "Attack";
    private const string SummonStateName = "Summon";
    private const string PulseStateName  = "Pulse";

    private Enemy       _enemy;
    private Rigidbody2D _rb;
    private Transform   _player;
    private Vector2     _homePosition;
    private float       _patrolDirection = 1f;
    private bool        _isAttacking;

    private float _runTime;
    private int   _totalSpawnedCount;

    /// <summary>False once the boss dies — freezes the whole-run clock at the winning time.</summary>
    private bool _timerRunning = true;

    /// <summary>Every enemy summoned by this boss that's still alive — all killed when the boss dies.</summary>
    private readonly List<Enemy> _aliveSpawned = new();

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _rb    = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        if (_bossData == null)
            Debug.LogWarning($"[Boss] No Boss_Data assigned on {name}. Falling back to prefab defaults.", this);
        else
            _enemy.Initialize(_bossData, _player);

        _enemy.OnDeath += HandleBossDeath;

        _homePosition = _rb.position;

        // The Boss scene has no LevelProgression, so this component is what keeps
        // the whole-run clock going through the final fight.
        GameTimer.Restore(SaveSystem.Data.elapsedSeconds);

        StartCoroutine(BossActionLoop());
    }

    private void Update()
    {
        _runTime += Time.deltaTime;

        if (_timerRunning) GameTimer.Tick(Time.deltaTime);
    }

    /// <summary>
    /// The Boss level has no LevelProgression/nests — the boss kill IS the
    /// level's only objective, so this is what finally drops the persistent
    /// Bloodstream Infection stat to 0 (from whatever Level 1/2 left it at)
    /// and stops the whole-run clock.
    /// </summary>
    private void HandleBossDeath(Enemy enemy)
    {
        InfectionState.Value = 0f;
        if (VitalsManager.Instance != null)
            VitalsManager.Instance.SetInfectionPercent(0f);

        KillSpawnedEnemies();

        // Freeze and bank the final time — GameTimer.Formatted is what the win
        // panel displays, and what the leaderboard will submit later.
        _timerRunning = false;
        SaveSystem.SetElapsedTime(GameTimer.Elapsed);

        SaveSystem.MarkLevelCleared("Boss");

        Debug.Log($"[Boss] Run complete in {GameTimer.Formatted}.");
    }

    private void FixedUpdate()
    {
        Patrol();
    }

    // ── Patrol movement ──────────────────────────────────────────────────────

    private void Patrol()
    {
        if (_isAttacking)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        float offsetFromHome = _rb.position.x - _homePosition.x;

        if (offsetFromHome >= _patrolDistance) _patrolDirection = -1f;
        else if (offsetFromHome <= -_patrolDistance) _patrolDirection = 1f;

        _rb.linearVelocity = new Vector2(_patrolDirection * _patrolSpeed, 0f);
    }

    // ── Boss action loop — picks and plays one attack at a time ────────────────

    private IEnumerator BossActionLoop()
    {
        while (true)
        {
            PlayIdle();
            yield return new WaitForSeconds(_actionCooldown);

            var attack = (BossAttackType)Random.Range(0, 3);
            yield return PerformAttack(attack);
        }
    }

    private void PlayIdle()
    {
        if (_animator != null) _animator.Play(IdleStateName);
    }

    private IEnumerator PerformAttack(BossAttackType attack)
    {
        _isAttacking = true;

        if (_animator != null)
        {
            _animator.Play(GetStateName(attack), 0, 0f);
            yield return null; // let the Animator register the new state this frame
            float length = _animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(length);
        }
        else
        {
            ExecuteAttackEffect(attack);
            yield return new WaitForSeconds(_noAnimatorAttackDuration);
        }

        _isAttacking = false;
    }

    private static string GetStateName(BossAttackType attack) => attack switch
    {
        BossAttackType.Projectile => AttackStateName,
        BossAttackType.Summon     => SummonStateName,
        BossAttackType.Pulse      => PulseStateName,
        _                         => AttackStateName
    };

    private void ExecuteAttackEffect(BossAttackType attack)
    {
        switch (attack)
        {
            case BossAttackType.Projectile: FireProjectile(); break;
            case BossAttackType.Summon:     SummonWave();      break;
            case BossAttackType.Pulse:      FirePulse();       break;
        }
    }

    // ── Enemy spawning ───────────────────────────────────────────────────────

    // Public — invoked directly as an Animation Event on Summon.anim.
    public void SummonWave()
    {
        if (_totalSpawnedCount >= _maxSpawnedEnemies)
        {
            Debug.LogWarning($"[Boss] SummonWave skipped — already at the {_maxSpawnedEnemies} live-enemy cap.", this);
            return;
        }

        int spawned = 0;
        for (int c = 0; c < _spawnConfigs.Count; c++)
        {
            EnemySpawnConfig cfg = _spawnConfigs[c];

            if (_runTime < cfg.startTime || _runTime > cfg.endTime)
            {
                Debug.Log($"[Boss] Config {c} skipped — runTime {_runTime:0.0}s is outside its {cfg.startTime}-{cfg.endTime}s window.", this);
                continue;
            }

            spawned += SpawnGroup(c, cfg);
        }

        Debug.Log($"[Boss] SummonWave spawned {spawned} enemies from {_spawnConfigs.Count} configs ({_totalSpawnedCount}/{_maxSpawnedEnemies} live).", this);
    }

    /// <summary>Spawns one config's group and returns how many actually spawned.</summary>
    private int SpawnGroup(int configIndex, EnemySpawnConfig cfg)
    {
        if (cfg.enemyPrefab == null || cfg.enemyData == null || _player == null)
        {
            Debug.LogWarning($"[Boss] Config {configIndex} spawned nothing — " +
                             $"enemyPrefab={(cfg.enemyPrefab == null ? "NULL" : cfg.enemyPrefab.name)}, " +
                             $"enemyData={(cfg.enemyData == null ? "NULL" : cfg.enemyData.name)}, " +
                             $"player={(_player == null ? "NULL" : _player.name)}.", this);
            return 0;
        }

        int count = Mathf.Max(1, cfg.spawnGroupSize);
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (_totalSpawnedCount >= _maxSpawnedEnemies)
            {
                Debug.LogWarning($"[Boss] Config {configIndex} cut short at {spawned}/{count} — hit the {_maxSpawnedEnemies} live-enemy cap.", this);
                break;
            }

            Vector3 spawnPos = GetSpawnPosition(cfg, i);
            Enemy enemy = Instantiate(cfg.enemyPrefab, spawnPos, Quaternion.identity);
            enemy.Initialize(cfg.enemyData, _player);
            enemy.OnDeath += dead => HandleEnemyDeath(cfg, dead);
            _aliveSpawned.Add(enemy);
            cfg.activeCount++;
            _totalSpawnedCount++;
            spawned++;
        }

        return spawned;
    }

    private Vector3 GetSpawnPosition(EnemySpawnConfig cfg, int indexInGroup)
    {
        Vector3 basePos;

        // An empty slot in _spawnPoints used to throw here, and because the slot is
        // picked at random the throw would escape mid-SummonWave and silently drop
        // every remaining config — falling back to the ring keeps the wave intact.
        Transform point = PickSpawnPoint();

        if (point != null)
        {
            basePos = point.position;
        }
        else
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            basePos = _homePosition + dir * _spawnRingRadius;
        }

        if (indexInGroup <= 0 || cfg.groupSpreadRadius <= 0f) return basePos;

        return basePos + (Vector3)(Random.insideUnitCircle * cfg.groupSpreadRadius);
    }

    /// <summary>A random assigned spawn point, or null if none are usable.</summary>
    private Transform PickSpawnPoint()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0) return null;

        Transform point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        if (point != null) return point;

        // That slot was empty — fall back to any assigned one before giving up.
        foreach (Transform candidate in _spawnPoints)
            if (candidate != null) return candidate;

        return null;
    }

    private void HandleEnemyDeath(EnemySpawnConfig cfg, Enemy enemy)
    {
        cfg.activeCount--;
        _totalSpawnedCount--;
        _aliveSpawned.Remove(enemy);
    }

    /// <summary>
    /// Wipes out everything the boss summoned, the same way destroying an
    /// EnemyNest kills its brood. Each Kill() fires OnDeath → HandleEnemyDeath,
    /// which mutates _aliveSpawned, so iterate over a snapshot.
    /// </summary>
    private void KillSpawnedEnemies()
    {
        Enemy[] toKill = _aliveSpawned.ToArray();
        foreach (Enemy enemy in toKill)
            if (enemy != null) enemy.Kill();

        _aliveSpawned.Clear();
        Debug.Log($"[Boss] Boss died — killed {toKill.Length} summoned enemies.", this);
    }

    // ── Projectile attack ────────────────────────────────────────────────────

    // Public — invoked directly as an Animation Event on Attack.anim.
    public void FireProjectile()
    {
        if (_player == null) return;

        if (_projectiles == null || _projectiles.Count == 0)
        {
            Debug.LogWarning($"[Boss] No projectiles assigned on {name}.", this);
            return;
        }

        BossProjectileConfig cfg = _projectiles[Random.Range(0, _projectiles.Count)];
        if (cfg == null || cfg.prefab == null) return;

        Vector3 spawnPos = _firePoint != null ? _firePoint.position : transform.position;
        Vector2 dir      = ((Vector2)_player.position - (Vector2)spawnPos).normalized;

        GameObject projGO = Instantiate(cfg.prefab, spawnPos, Quaternion.identity);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        projGO.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        if (projGO.TryGetComponent<EnemyBolt>(out var bolt))
        {
            bolt.Launch(dir, cfg.speed, cfg.damage, cfg.lifetime);
        }
        else if (projGO.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.gravityScale   = 0f;
            rb.linearVelocity = dir * cfg.speed;
            Destroy(projGO, cfg.lifetime);
        }
    }

    // ── Pulse attack ─────────────────────────────────────────────────────────

    // Public — invoked directly as an Animation Event on Pulse.anim.
    public void FirePulse()
    {
        if (_shockwavePrefab == null)
        {
            Debug.LogWarning($"[Boss] No shockwavePrefab assigned on {name}.", this);
            return;
        }

        var go = Instantiate(_shockwavePrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<ShockwaveRing>(out var ring))
            ring.Launch(_pulseRadius, _pulseThickness, _pulseDamage, _pulseDuration, _player);
    }
}
