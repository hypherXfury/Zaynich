using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stationary nest that spawns enemies once activated. Nests no longer
/// self-activate by player proximity — LevelProgression activates them one at
/// a time, in order: the first nest is active from level start, and each next
/// nest activates when the previous one is fully cleared (see
/// <see cref="OnFullyCleared"/>).
///
/// Spawning never stops on its own: the first _totalEnemiesToSpawn enemies are
/// the "core" wave — while they are being spawned the nest is INVULNERABLE.
/// Once all core enemies are out, the nest becomes vulnerable to player
/// bullets but keeps spawning extra "bonus" enemies indefinitely, purely for
/// difficulty. Destroying the nest instantly kills every enemy it spawned
/// (core and bonus alike). Only core-enemy kills and the nest itself count
/// toward the infection calculation — bonus enemies never touch it.
///
/// The enemy intro card is shown 1 second after the first spawned enemy from
/// this nest becomes visible inside the camera bounds.
///
/// Reuses the EnemySpawnConfig pattern from EnemySpawner so nest behaviour
/// stays consistent with the rest of the spawning system.
///
/// SETUP:
///   1. Place this on an empty GameObject at the nest's world position.
///   2. Assign _player.
///   3. Add one or more entries to _spawnConfigs (same fields as EnemySpawner).
///   4. Set _totalEnemiesToSpawn — the size of the core wave. The nest is
///      invulnerable until that many have spawned, then becomes damageable
///      while continuing to spawn bonus enemies until destroyed.
///   5. Assign _sr (SpriteRenderer) and tweak _activeColor for the visual cue.
///   6. Order the nests in LevelProgression's points array — that IS the
///      activation sequence.
/// </summary>
public class EnemyNest : MonoBehaviour
{
    // ── Static registry ──────────────────────────────────────────────────────
    private static readonly List<EnemyNest> s_all = new();

    /// <summary>All currently active nests. Read-only. Used by abilities for target acquisition.</summary>
    public static IReadOnlyList<EnemyNest> All => s_all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => s_all.Clear();
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Health")]
    [Tooltip("Total HP. Reaching 0 destroys the nest immediately, regardless of spawn progress.")]
    [SerializeField] private float _maxHealth = 60f;

    [Header("Activation")]
    [SerializeField] private Transform _player;

    [Header("Enemy Card")]
    [Tooltip("Seconds after the first spawned enemy enters the camera bounds before its card shows.")]
    [SerializeField] private float _cardDelayAfterVisible = 1f;

    [Header("Spawn Configs")]
    [Tooltip("Same structure as EnemySpawner — interval, group size, enemy type per entry.")]
    [SerializeField] private List<EnemySpawnConfig> _spawnConfigs = new();

    [Header("Total Output")]
    [Tooltip("Core wave size. The nest is INVULNERABLE until this many enemies have " +
             "spawned; after that it keeps spawning bonus enemies (pure difficulty) " +
             "and can be damaged. Only these core enemies count toward infection.")]
    [SerializeField] private int _totalEnemiesToSpawn = 10;

    [Header("Spawn Position")]
    [Tooltip("Random radius around the nest where each enemy actually appears.")]
    [SerializeField] private float _spawnScatterRadius = 1.5f;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _sr;
    [SerializeField] private Color _idleColor   = Color.white;
    [SerializeField] private Color _activeColor = new Color(1f, 0.35f, 0.1f, 1f);
    [Tooltip("Seconds to fade between idle and active colour.")]
    [SerializeField] private float _colorTransitionTime = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool _showGizmo = true;

    [SerializeField] private  EnemyCardData _enemyCardData;
    private bool cardQueued;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool  _isActive;
    private bool  _isDestroyed;   // true once health reaches 0
    private bool  _fullyCleared;  // destroyed AND every CORE enemy killed
    private int   _spawnedCount;  // total spawned, core + bonus
    private int   _coreKilledCount;
    private float _colorLerpT;
    private float _currentHealth;
    private Camera _cam;
    private string _cachedName;   // safe to log after Destroy(gameObject)

    private readonly List<Enemy>    _aliveSpawned = new();
    private readonly HashSet<Enemy> _coreEnemies  = new();   // alive core-wave enemies only

    public bool  IsActive      => _isActive;
    public bool  IsDestroyed   => _isDestroyed;
    /// <summary>True once the core wave has fully spawned — the nest can now be damaged.</summary>
    public bool  IsVulnerable  => _spawnedCount >= _totalEnemiesToSpawn;
    public int   SpawnedCount  => _spawnedCount;
    public int   TotalEnemies  => _totalEnemiesToSpawn;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth     => _maxHealth;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _cam = Camera.main;
        _cachedName = name;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _sr.color = _idleColor;

        foreach (var cfg in _spawnConfigs)
            cfg.spawnTimer = 0f;   // prime so first spawn happens almost immediately on activation
    }

    private void OnEnable()  => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    private void Update()
    {
        // Visibility must keep being checked until the first enemy has been
        // seen — it may only reach the camera bounds well after spawning began.
        if (!_isDestroyed && !_firstEnemySeen)
            CheckCardVisibility();

        if (!_isDestroyed && _isActive && !_nestSeen)
            CheckNestVisibility();

        if (_isDestroyed) return;
        if (_player == null) return;

        if (_isActive)
            TickSpawning();

        UpdateVisual();
    }

    // ── Activation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates this nest so it starts spawning. Called by LevelProgression —
    /// the first nest at level start, each subsequent nest when the previous
    /// one is fully cleared. Nests never self-activate.
    /// </summary>
    public void Activate()
    {
        if (_isActive || _isDestroyed) return;

        _isActive = true;

        // Reset timers so spawning starts promptly after activation
        foreach (var cfg in _spawnConfigs)
            cfg.spawnTimer = cfg.spawnInterval;

        Debug.Log($"[EnemyNest] {name} activated.");
    }

    // ── Enemy card ────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised once, the first time any enemy spawned by this nest becomes
    /// visible inside the camera bounds. LevelProgression uses this on nest 3
    /// to reveal the Zidebactam Pulse description panel.
    /// </summary>
    public event System.Action<EnemyNest> OnFirstEnemyVisible;

    private bool _firstEnemySeen;

    /// <summary>
    /// Once any spawned enemy from this nest is inside the camera bounds,
    /// raise OnFirstEnemyVisible, then wait _cardDelayAfterVisible seconds and
    /// show the enemy intro card.
    /// </summary>
    private void CheckCardVisibility()
    {
        if (_firstEnemySeen || _aliveSpawned.Count == 0) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var enemy in _aliveSpawned)
        {
            if (enemy == null) continue;

            Vector3 vp = _cam.WorldToViewportPoint(enemy.transform.position);
            if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
            {
                _firstEnemySeen = true;
                OnFirstEnemyVisible?.Invoke(this);

                if (!cardQueued && _enemyCardData != null)
                {
                    cardQueued = true;
                    StartCoroutine(ShowCardDelayed());
                }
                break;
            }
        }
    }

    private IEnumerator ShowCardDelayed()
    {
        yield return new WaitForSeconds(_cardDelayAfterVisible);

        if (EnemyCardManager.Instance == null)
        {
            Debug.LogWarning($"[EnemyNest] {name}: EnemyCardManager.Instance is NULL — cannot show enemy card.");
            yield break;
        }

        // The card is shown once ever — EnemyCardManager persists a per-type
        // 'seen' flag, so returning players don't sit through it again.
        EnemyCardManager.Instance.ShowCard(_enemyCardData);
    }

    // ── Nest visibility / blink ───────────────────────────────────────────────

    /// <summary>
    /// Raised once, the first time this nest itself enters the camera bounds.
    /// LevelProgression uses this on nest 1 to show its tutorial card.
    /// </summary>
    public event System.Action<EnemyNest> OnNestVisible;

    private bool _nestSeen;
    private bool _blinking;

    private void CheckNestVisibility()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 vp = _cam.WorldToViewportPoint(transform.position);
        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;

        _nestSeen = true;
        OnNestVisible?.Invoke(this);
    }

    /// <summary>
    /// Flashes the nest sprite until <see cref="StopBlink"/> is called, to draw
    /// the player's eye to it. Runs on unscaled time so it keeps animating
    /// while a tutorial card has the game paused.
    /// </summary>
    public void StartBlink(float speed = 3f, float minAlpha = 0.15f)
    {
        if (_sr == null || _blinking) return;
        _blinkCoroutine = StartCoroutine(BlinkRoutine(speed, minAlpha));
    }

    public void StopBlink()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
        if (_blinking)
        {
            _sr.color = _blinkOriginalColor;
            _blinking = false;
        }
    }

    private Coroutine _blinkCoroutine;
    private Color     _blinkOriginalColor;

    private IEnumerator BlinkRoutine(float speed, float minAlpha)
    {
        _blinking = true;   // suppresses UpdateVisual so it can't fight us for _sr.color
        _blinkOriginalColor = _sr.color;

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = (Mathf.Sin(elapsed * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color c = _blinkOriginalColor;
            c.a = Mathf.Lerp(minAlpha, 1f, t);
            _sr.color = c;

            yield return null;
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Holds/resumes spawning without deactivating the nest. The tutorial uses
    /// this while the camera is away introducing the nest: the nest is already
    /// active (that's what put it on screen), and without the hold the player
    /// would pan back to a crowd that spawned off-screen while they read.
    /// </summary>
    public void SetSpawningPaused(bool paused) => _spawningPaused = paused;

    private bool _spawningPaused;

    private void TickSpawning()
    {
        if (_spawningPaused) return;

        // Never stops — after the core wave the nest keeps producing bonus
        // enemies (pure difficulty) until the player destroys it.
        foreach (var cfg in _spawnConfigs)
        {
            cfg.spawnTimer += Time.deltaTime;
            if (cfg.spawnTimer < cfg.spawnInterval) continue;

            cfg.spawnTimer = 0f;
            SpawnGroup(cfg);
        }
    }

    private void SpawnGroup(EnemySpawnConfig cfg)
    {
        if (cfg.enemyPrefab == null || cfg.enemyData == null || _player == null) return;

        int count = Mathf.Max(1, cfg.spawnGroupSize);

        // Don't overshoot the core wave in a single group — the remainder of
        // the group spawns as bonus enemies, which is fine, but we still cap
        // the group size to the configured value.
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = count > 1
                ? UnityEngine.Random.insideUnitCircle * _spawnScatterRadius
                : Vector2.zero;

            Vector3 spawnPos = transform.position + (Vector3)offset;

            Enemy enemy = Instantiate(cfg.enemyPrefab, spawnPos, Quaternion.identity);
            enemy.Initialize(cfg.enemyData, _player);

            enemy.OnDeath += HandleSpawnedEnemyDeath;
            _aliveSpawned.Add(enemy);

            // The first _totalEnemiesToSpawn enemies are the core wave — only
            // their deaths count toward the infection calculation. Everything
            // after is a bonus enemy, spawned purely for difficulty.
            bool isCore = _spawnedCount < _totalEnemiesToSpawn;
            if (isCore) _coreEnemies.Add(enemy);

            // Constrain this enemy to the painted SpawnZone polygons
            enemy.gameObject.AddComponent<ZoneBounded>();

            cfg.activeCount++;
            _spawnedCount++;

            if (_spawnedCount == _totalEnemiesToSpawn)
                Debug.Log($"[EnemyNest] {name} core wave complete ({_spawnedCount} spawned) — nest is now VULNERABLE. Bonus spawning continues.");
        }
    }

    private void HandleSpawnedEnemyDeath(Enemy enemy)
    {
        enemy.OnDeath -= HandleSpawnedEnemyDeath;
        _aliveSpawned.Remove(enemy);

        // Only core-wave enemies feed the infection calculation. Bonus enemies
        // exist purely for difficulty and must not move the meter.
        if (_coreEnemies.Remove(enemy))
        {
            _coreKilledCount++;
            LevelProgression.Instance?.ReportNestEnemyKilled();
        }

        CheckFullyCleared();
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the nest is destroyed by player damage, before the GameObject is removed.</summary>
    public event System.Action<EnemyNest> OnDestroyedByPlayer;

    /// <summary>
    /// Raised once, when the nest has been destroyed AND every enemy it spawned
    /// has been killed. LevelProgression listens to this to activate the next
    /// nest in the sequence.
    /// </summary>
    public event System.Action<EnemyNest> OnFullyCleared;

    /// <summary>
    /// The nest counts as fully cleared once it is destroyed and every core
    /// enemy is dead. Since DestroyNest() kills all spawned enemies on the
    /// spot, this normally fires immediately when the nest dies.
    /// </summary>
    private void CheckFullyCleared()
    {
        if (_fullyCleared) return;
        if (!_isDestroyed) return;
        if (_coreKilledCount < _totalEnemiesToSpawn) return;

        _fullyCleared = true;
        Debug.Log($"[EnemyNest] {_cachedName} fully cleared (nest destroyed + {_coreKilledCount} core enemies killed).");
        OnFullyCleared?.Invoke(this);
    }

    /// <summary>
    /// Applies damage to the nest. Call from Bolt/Lance OnTriggerEnter2D.
    /// Has no effect while the nest is invulnerable — it only becomes damageable
    /// once the core wave (_totalEnemiesToSpawn) has fully spawned.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (_isDestroyed) return;
        if (!IsVulnerable) return;

        _currentHealth -= amount;
        if (_currentHealth <= 0f)
            DestroyNest();
    }

    private void DestroyNest()
    {
        _isDestroyed = true;
        _isActive    = false;

        Debug.Log($"[EnemyNest] {_cachedName} destroyed by player after spawning {_spawnedCount} enemies ({_totalEnemiesToSpawn} core). Killing all its spawned enemies.");

        // Destroying the nest wipes out every enemy it spawned. Each Kill()
        // fires OnDeath → HandleSpawnedEnemyDeath, which mutates _aliveSpawned,
        // so iterate over a snapshot.
        var toKill = _aliveSpawned.ToArray();
        foreach (var enemy in toKill)
        {
            if (enemy != null)
                enemy.Kill();
        }

        OnDestroyedByPlayer?.Invoke(this);
        CheckFullyCleared();
        Destroy(gameObject);
    }

    // ── Visual feedback ───────────────────────────────────────────────────────

    private void UpdateVisual()
    {
        if (_sr == null) return;
        if (_blinking) return;   // BlinkRoutine owns _sr.color until it finishes

        float targetT = _isActive ? 1f : 0f;
        _colorLerpT = Mathf.MoveTowards(
            _colorLerpT, targetT,
            Time.deltaTime / Mathf.Max(_colorTransitionTime, 0.001f));

        Color baseColor = Color.Lerp(_idleColor, _activeColor, _colorLerpT);

        // Darken toward black as health drops, so players get visual feedback
        // that their shots are actually working.
        float healthFraction = _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 1f;
        _sr.color = Color.Lerp(Color.black * 0.6f, baseColor, healthFraction);
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmo) return;

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _spawnScatterRadius);

#if UNITY_EDITOR
        // Health label above the nest
        float hpFraction = _maxHealth > 0f ? _currentHealth / _maxHealth : 1f;
        UnityEditor.Handles.color = Color.Lerp(Color.red, Color.green, hpFraction);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (_spawnScatterRadius + 0.5f),
            $"HP: {_currentHealth:0}/{_maxHealth:0}");
#endif
    }
}