using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a ChamberEnemyEntry's enemies enter the arena.
/// </summary>
public enum ChamberSpawnType
{
    /// <summary>Instantiated at the spawn point and behaves normally (walks/chases the player).</summary>
    Normal,

    /// <summary>Fired like a cannon shot from the spawn pore straight toward the player;
    /// self-destructs with no kill rewards once it exits the arena bounds.</summary>
    Shootable
}

/// <summary>
/// One entry in a Chamber's enemy wave: which enemy, how many, and when.
/// </summary>
[System.Serializable]
public class ChamberEnemyEntry
{
    public Enemy     enemyPrefab;
    public EnemyData enemyData;

    [Tooltip("Normal = spawns and behaves as usual. Shootable = fired like a cannon shot " +
             "from the spawn point toward the player and destroyed on leaving the arena bounds.")]
    public ChamberSpawnType spawnType = ChamberSpawnType.Normal;

    [Tooltip("World-units/second this entry is launched at. Only used when spawnType is Shootable.")]
    [Min(0.1f)] public float launchSpeed = 8f;

    [Min(1)] public int count = 1;

    [Tooltip("Seconds after the chamber activates before this entry starts spawning.")]
    [Min(0f)] public float spawnDelay = 0f;

    [Tooltip("Seconds between each individual enemy in this entry. 0 = all at once.")]
    [Min(0f)] public float spacing = 0f;
}

/// <summary>
/// A self-contained arena fight in Level 2. The player walks into the trigger,
/// the open-world environment is swapped out for this chamber's arena (same
/// art/size reused at every chamber — only <see cref="_enemyWave"/> differs per
/// instance), the camera is clamped to the arena's bounds instead of the map's,
/// and a one-shot enemy wave spawns. Clearing every enemy reverses all of it
/// and the chamber is done.
///
/// SETUP:
///   1. Place this on the Chamber's trigger GameObject (needs a Collider2D
///      with Is Trigger checked).
///   2. Assign _overworldEnvironment — the open-world geometry to hide while
///      this chamber is active (likely shared across all 4 chambers).
///   3. Assign _arenaEnvironment — this chamber's own arena art/walls,
///      positioned wherever you've placed it, kept disabled until entered.
///   4. Assign _cameraController (the scene's CameraController) and
///      _arenaBounds (this arena's BoxCollider2D — set Is Trigger so it
///      doesn't block movement) plus _lockedOrthoSize for the arena framing.
///   5. Fill in _enemyWave — one entry per enemy type/count for THIS chamber.
///      _spawnPoints is optional; if empty, enemies scatter around this
///      transform instead. Set an entry's spawnType to Shootable to have it
///      fired like a cannon shot from its spawn point toward the player
///      instead of spawning normally (used for the "pore" cannon enemies —
///      they self-destruct with no rewards once they exit _arenaBounds).
///   6. Place a ChamberExitDoor (with a trigger Collider2D, e.g. the door's
///      CircleCollider2D) on the arena's door and assign this Chamber to it.
///      Clearing the wave does NOT swap the environment back on its own —
///      the arena stays fully active until the player walks through that
///      door, so they always leave the way they came in.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Chamber : MonoBehaviour
{
    [Header("Environment Swap")]
    [Tooltip("Open-world geometry to hide while this chamber is active.")]
    [SerializeField] private GameObject _overworldEnvironment;
    [Tooltip("This chamber's own arena art/walls, kept disabled until entered.")]
    [SerializeField] private GameObject _arenaEnvironment;

    [Header("Camera Lock")]
    [SerializeField] private CameraController _cameraController;
    [Tooltip("The arena's BoxCollider2D — the camera follows the player but is clamped inside it.")]
    [SerializeField] private BoxCollider2D _arenaBounds;
    [SerializeField] private float _lockedOrthoSize = 5f;

    [Header("Enemy Wave")]
    [SerializeField] private List<ChamberEnemyEntry> _enemyWave = new();
    [Tooltip("Optional fixed spawn points (e.g. the cannon 'pores'). Left empty = scatter around this transform.")]
    [SerializeField] private Transform[] _spawnPoints;
    [Tooltip("Scatter radius used when no spawn points are assigned.")]
    [SerializeField] private float _spawnScatterRadius = 2f;

    [Header("Enemy Cards")]
    [Tooltip("Intro cards for this chamber's enemy types, shown once the player is inside " +
             "the arena and before the wave spawns. Level 1 triggers these from EnemyNest, " +
             "which Level 2 has none of — chambers queue their own instead. Each card is " +
             "still only ever shown the first time the player meets that enemy type.")]
    [SerializeField] private EnemyCardData[] _enemyCards;

    [Header("Player")]
    [SerializeField] private Transform _player;
    [SerializeField] private GameObject _infectionVisuals;

    [Header("Level 2 Hazards")]
    [Tooltip("Level 2's urine hazard system — switched off for the duration of the chamber fight.")]
    [SerializeField] private UrineEffect _urineEffect;

    [Tooltip("Index into _urineEffect's Chambers array for the urine point tied to this arena " +
             "chamber. -1 = none. That point stops spawning urine for good once this chamber is cleared.")]
    [SerializeField] private int _urineChamberIndex = -1;

    [Header("Exit")]
    [Tooltip("Optional. The arena's exit door — what the guide arrow points at once this chamber is " +
             "cleared. Leave empty to auto-find the ChamberExitDoor that references this chamber.")]
    [SerializeField] private Transform _exitDoor;

    [Header("Intro Blink")]
    [Tooltip("Sprite flashed while this chamber's tutorial intro card is up. Leave empty to use " +
             "this GameObject's SpriteRenderer, or the first one on an active child.")]
    [SerializeField] private SpriteRenderer _blinkTarget;

    [Header("Transitions")]
    [Tooltip("Seconds for the fade-to-black / fade-back-in when entering or exiting this chamber.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    /// <summary>Raised once every enemy in this chamber's wave has been killed.</summary>
    public event System.Action<Chamber> OnChamberCleared;

    public bool IsActive  { get; private set; }
    public bool IsCleared { get; private set; }

    /// <summary>
    /// True once the player has walked back out through the arena's exit door and
    /// the overworld has been restored. Between IsCleared and this, the player is
    /// still standing inside the arena — which is what the guide arrow watches for
    /// so it can point at the door instead of at the next chamber out in the map.
    /// </summary>
    public bool HasExited => _hasExited;

    /// <summary>
    /// This arena's exit door. Resolved on first use rather than in Awake, because
    /// the door lives under _arenaEnvironment, which is disabled from Awake onward —
    /// the includeInactive search below handles that, and the fallback covers doors
    /// parented outside the arena root.
    /// </summary>
    public Transform ExitDoor
    {
        get
        {
            if (_exitDoor == null && !_exitDoorResolved)
            {
                _exitDoorResolved = true;
                _exitDoor = FindExitDoor();

                if (_exitDoor == null)
                    Debug.LogWarning($"[Chamber] {name}: no ChamberExitDoor found — the guide arrow " +
                                     "will stay hidden after this chamber is cleared.");
            }
            return _exitDoor;
        }
    }

    /// <summary>Total enemies across every entry in this chamber's wave — used by LevelProgression for level-completion math.</summary>
    public int TotalEnemies
    {
        get
        {
            int total = 0;
            foreach (var entry in _enemyWave)
                total += entry.count;
            return total;
        }
    }

    private readonly List<Enemy> _alive = new();
    private Collider2D _trigger;
    private bool        _hasExited;
    private bool        _exitDoorResolved;
    private bool        _blinking;
    private Coroutine   _blinkCoroutine;
    private Color       _blinkOriginalColor;

    private Transform FindExitDoor()
    {
        if (_arenaEnvironment != null)
        {
            foreach (ChamberExitDoor door in _arenaEnvironment.GetComponentsInChildren<ChamberExitDoor>(true))
                if (door.Chamber == this) return door.transform;
        }

        foreach (ChamberExitDoor door in FindObjectsByType<ChamberExitDoor>(FindObjectsInactive.Include,
                                                                           FindObjectsSortMode.None))
            if (door.Chamber == this) return door.transform;

        return null;
    }

    private void Awake()
    {
        _infectionVisuals.SetActive(true);
        _trigger = GetComponent<Collider2D>();
        _trigger.isTrigger = true;

        if (_arenaEnvironment != null) _arenaEnvironment.SetActive(false);
    }

    // ── First-sight visibility ────────────────────────────────────────────────

    /// <summary>
    /// Raised once, the first time this chamber enters the camera bounds — the
    /// Level 2 counterpart to <see cref="EnemyNest.OnNestVisible"/>. TutorialManager
    /// listens on whichever chamber the player lays eyes on first, to pan the
    /// camera over and explain what a chamber is before they walk into it.
    /// </summary>
    public event System.Action<Chamber> OnFirstVisible;

    private bool   _seen;
    private Camera _cam;

    private void Update()
    {
        if (_seen || IsActive || IsCleared) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 vp = _cam.WorldToViewportPoint(transform.position);
        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;

        _seen = true;
        OnFirstVisible?.Invoke(this);
    }

    // ── Blink (intro highlight) ───────────────────────────────────────────────

    /// <summary>
    /// Flashes the chamber's overworld sprite until <see cref="StopBlink"/> is
    /// called, so the tutorial's intro card has something to point at — the same
    /// treatment <see cref="EnemyNest.StartBlink"/> gives nests in Level 1. Runs
    /// on unscaled time because that intro freezes the game while the card is up.
    /// </summary>
    public void StartBlink(float speed = 3f, float minAlpha = 0.15f)
    {
        if (_blinking) return;

        if (_blinkTarget == null) _blinkTarget = ResolveBlinkTarget();
        if (_blinkTarget == null)
        {
            Debug.LogWarning($"[Chamber] {name}: no sprite to blink — assign _blinkTarget " +
                             "if this chamber's marker sprite lives somewhere unexpected.");
            return;
        }

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
            _blinkTarget.color = _blinkOriginalColor;
            _blinking = false;
        }
    }

    /// <summary>
    /// The chamber's own marker sprite. The arena's art is skipped automatically:
    /// _arenaEnvironment is deactivated from Awake, and this search ignores
    /// inactive objects.
    /// </summary>
    private SpriteRenderer ResolveBlinkTarget()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr : GetComponentInChildren<SpriteRenderer>(false);
    }

    private IEnumerator BlinkRoutine(float speed, float minAlpha)
    {
        _blinking = true;
        _blinkOriginalColor = _blinkTarget.color;

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = (Mathf.Sin(elapsed * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color c = _blinkOriginalColor;
            c.a = Mathf.Lerp(minAlpha, 1f, t);
            _blinkTarget.color = c;

            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsActive || IsCleared) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        ActivateChamber();
    }

    private void ActivateChamber()
    {
        IsActive = true;
        _trigger.enabled = false;   // can't be re-entered while running

        // Suspended here rather than inside EnterRoutine so nothing spawns during
        // the fade-out. Kills the hazard globally, not just this chamber's point:
        // the player is in an arena, so urine from ANY chamber has to stop.
        if (_urineEffect != null) _urineEffect.SetHazardActive(false);

        StartCoroutine(EnterRoutine());

        Debug.Log($"[Chamber] {name} activated.");
    }

    private IEnumerator EnterRoutine()
    {
        yield return StartCoroutine(ScreenFader.Instance.FadeOut(_fadeDuration));

        if (_overworldEnvironment != null) _overworldEnvironment.SetActive(false);
        if (_arenaEnvironment     != null) _arenaEnvironment.SetActive(true);

        if (_cameraController != null && _arenaBounds != null)
            _cameraController.EnterChamberBounds(_arenaBounds, _lockedOrthoSize);

        yield return StartCoroutine(ScreenFader.Instance.FadeIn(_fadeDuration));

        yield return StartCoroutine(ShowEnemyCards());

        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Queues this chamber's enemy intro cards and holds the wave until they're
    /// dismissed, so the player reads them before anything starts moving.
    /// Returns immediately when every card has already been seen.
    /// </summary>
    private IEnumerator ShowEnemyCards()
    {
        if (_enemyCards == null || _enemyCards.Length == 0) yield break;

        if (EnemyCardManager.Instance == null)
        {
            Debug.LogWarning($"[Chamber] {name}: EnemyCardManager.Instance is NULL — cannot show enemy cards.");
            yield break;
        }

        foreach (EnemyCardData card in _enemyCards)
            EnemyCardManager.Instance.ShowCard(card);

        // EnemyCardUI zeroes timeScale while a card is up, so this has to be a
        // frame-based wait — a WaitForSeconds here would never elapse.
        yield return new WaitWhile(() => EnemyCardManager.Instance.IsBusy);
    }

    private IEnumerator SpawnWave()
    {
        foreach (var entry in _enemyWave)
        {
            if (entry.enemyPrefab == null || entry.enemyData == null) continue;
            StartCoroutine(SpawnEntry(entry));
        }
        yield return null;
    }

    private IEnumerator SpawnEntry(ChamberEnemyEntry entry)
    {
        if (entry.spawnDelay > 0f)
            yield return new WaitForSeconds(entry.spawnDelay);

        for (int i = 0; i < entry.count; i++)
        {
            SpawnOne(entry);

            if (entry.spacing > 0f)
                yield return new WaitForSeconds(entry.spacing);
        }
    }

    private void SpawnOne(ChamberEnemyEntry entry)
    {
        Vector3 spawnPos = GetSpawnPosition();

        Enemy enemy = Instantiate(entry.enemyPrefab, spawnPos, Quaternion.identity);
        enemy.Initialize(entry.enemyData, _player);
        enemy.OnDeath += HandleEnemyDeath;
        _alive.Add(enemy);

        if (entry.spawnType == ChamberSpawnType.Shootable)
        {
            var cannon = enemy.GetComponent<CannonEnemy>();
            if (cannon == null) cannon = enemy.gameObject.AddComponent<CannonEnemy>();
            cannon.Launch(_arenaBounds, _player, entry.launchSpeed);
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            var point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            return point.position;
        }

        Vector2 offset = Random.insideUnitCircle * _spawnScatterRadius;
        return transform.position + (Vector3)offset;
    }

    private void HandleEnemyDeath(Enemy enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        _alive.Remove(enemy);

        // Feeds LevelProgression's per-level infection math, same as EnemyNest
        // reports its own core-enemy kills via ReportNestEnemyKilled.
        LevelProgression.Instance?.ReportChamberEnemyKilled();

        if (_alive.Count == 0)
            ClearChamber();
    }

    private void ClearChamber()
    {
        _infectionVisuals.SetActive(false);
        IsActive  = false;
        IsCleared = true;

        // The same "infection cleared" alert Level 1 raises when a nest goes
        // down — chambers are this level's equivalent objective, so clearing one
        // gets the same feedback. Fired on the kill, not on leaving through the
        // door, to match Level 1's immediacy.
        GameNotifier.Instance?.ShowInfectionReduced();

        // Arena stays fully active — the player leaves through the door
        // (see ExitThroughDoor), not the instant the wave is cleared.
        Debug.Log($"[Chamber] {name} cleared — waiting for the player to leave through the door.");
        OnChamberCleared?.Invoke(this);
    }

    /// <summary>
    /// Called by ChamberExitDoor once the player walks through the arena's exit
    /// door after this chamber has been cleared. Restores the overworld, camera,
    /// and hazards, fading the transition — the arena environment stays active
    /// until this point so the player always leaves the way they came in.
    /// </summary>
    public void ExitThroughDoor()
    {
        if (!IsCleared || _hasExited) return;
        _hasExited = true;

        StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine()
    {
        yield return StartCoroutine(ScreenFader.Instance.FadeOut(_fadeDuration));

        if (_cameraController != null) _cameraController.Unlock();

        if (_urineEffect != null)
        {
            // This chamber's own point is retired for good; the rest resume.
            if (_urineChamberIndex >= 0) _urineEffect.DisableChamberPoint(_urineChamberIndex);
            _urineEffect.SetHazardActive(true);
        }

        if (_arenaEnvironment     != null) _arenaEnvironment.SetActive(false);
        if (_overworldEnvironment != null) _overworldEnvironment.SetActive(true);

        yield return StartCoroutine(ScreenFader.Instance.FadeIn(_fadeDuration));

        Debug.Log($"[Chamber] {name} exited — overworld restored.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _spawnScatterRadius);
    }
}
