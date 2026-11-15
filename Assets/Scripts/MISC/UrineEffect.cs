using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 kidney hazard. Spawns a urine particle burst from a randomly picked chamber
/// at a fixed interval and moves each droplet along that chamber's authored spline path.
/// The spawned prefab needs no physics components — movement is purely path-driven.
///
/// While the player is inside ANY arena Chamber the whole hazard is suspended via
/// <see cref="SetHazardActive"/> — see that method for why toggling <c>enabled</c>
/// is not enough on its own.
/// </summary>
public class UrineEffect : MonoBehaviour
{
    [System.Serializable]
    public class Chamber
    {
        [Tooltip("Only used for the debug gizmo — the actual spawn point is the path's first waypoint.")]
        public Transform point;

        [Tooltip("Path the droplet follows from spawn to its end point. Edit the waypoints in the Scene view.")]
        public SplinePath path;
    }

    [Header("Chambers")]
    [SerializeField] private Chamber[] _chambers = new Chamber[7];

    [Header("Spawning")]
    [SerializeField] private GameObject _urinePrefab;
    [SerializeField] private float _minInterval = 1.5f;
    [SerializeField] private float _maxInterval = 3f;

    [Header("Burst")]
    [Tooltip("Number of droplets spawned per burst, one chamber per burst.")]
    [SerializeField] private int _burstCountMin = 5;
    [SerializeField] private int _burstCountMax = 6;

    [Tooltip("Delay between each droplet within a burst.")]
    [SerializeField] private float _burstInterval = 0.5f;

    [Header("Travel")]
    [Tooltip("Time it takes a droplet to travel the full length of its spline path.")]
    [SerializeField] private float _minTravelTime = 1.5f;
    [SerializeField] private float _maxTravelTime = 2.5f;

    [Tooltip("How long the droplet stays alive after reaching the end of its path.")]
    [SerializeField] private float _extraLifetime = 0.5f;

    [Header("Puddles")]
    [SerializeField] private GameObject[] _puddlePrefabs;
    [SerializeField] private int _maxPuddles = 2;
    [SerializeField] private float _puddleMinScale = 0.5f;
    [SerializeField] private float _puddleMaxScale = 1f;
    [SerializeField] private float _puddleSpreadDuration = 0.4f;
    [SerializeField] private float _puddleMinLifetime = 6f;
    [SerializeField] private float _puddleMaxLifetime = 8f;

    [Header("Debug")]
    [SerializeField] private bool _drawChamberPoints = true;

    private float _timer;

    /// <summary>Chamber indices permanently excluded from spawning (their arena chamber has been cleared).</summary>
    private readonly HashSet<int> _disabledChambers = new();

    /// <summary>False while the player is inside an arena Chamber (any of them).</summary>
    private bool _hazardActive = true;

    /// <summary>
    /// Parent for every droplet and puddle spawned at runtime. They're standalone
    /// instances, not part of the overworld environment Chamber hides, so without
    /// a handle on them they'd keep flying and pooling in plain sight over the arena.
    /// </summary>
    private Transform _spawnRoot;

    private Transform SpawnRoot
    {
        get
        {
            if (_spawnRoot == null)
            {
                var root = new GameObject("UrineSpawned");
                root.transform.SetParent(transform, worldPositionStays: false);
                _spawnRoot = root.transform;
            }
            return _spawnRoot;
        }
    }

    private void Start() => _timer = Random.Range(_minInterval, _maxInterval);

    private void Update()
    {
        if (!_hazardActive) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = Random.Range(_minInterval, _maxInterval);

        int index = GetRandomEnabledChamberIndex();
        if (index >= 0) StartCoroutine(SpawnBurst(index));
    }

    private int GetRandomEnabledChamberIndex()
    {
        if (_chambers == null || _chambers.Length == 0) return -1;

        List<int> candidates = new(_chambers.Length);
        for (int i = 0; i < _chambers.Length; i++)
            if (!_disabledChambers.Contains(i)) candidates.Add(i);

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    /// <summary>
    /// Permanently stops the given chamber point from producing urine — used when
    /// the arena Chamber tied to it is cleared. Has no effect on other points.
    /// </summary>
    public void DisableChamberPoint(int chamberIndex)
    {
        if (chamberIndex < 0 || chamberIndex >= _chambers.Length) return;
        _disabledChambers.Add(chamberIndex);
    }

    /// <summary>
    /// Suspends/resumes the hazard as a whole — every chamber point, not just the
    /// one tied to the arena being entered. Called by Chamber on enter/exit.
    ///
    /// This exists because setting <c>enabled = false</c> is not sufficient: Unity
    /// stops Update() on a disabled component but lets its already-running
    /// coroutines finish, so a SpawnBurst mid-flight would keep dripping droplets
    /// through the whole arena fight. Suspending also clears droplets and puddles
    /// already in the world, which are standalone instances that would otherwise
    /// stay visible after the overworld environment is hidden.
    /// </summary>
    public void SetHazardActive(bool active)
    {
        if (_hazardActive == active) return;
        _hazardActive = active;

        if (active)
        {
            _timer = Random.Range(_minInterval, _maxInterval);
            return;
        }

        StopAllCoroutines();
        ClearSpawned();
    }

    /// <summary>Destroys every droplet and puddle currently in the world.</summary>
    private void ClearSpawned()
    {
        if (_spawnRoot == null) return;

        for (int i = _spawnRoot.childCount - 1; i >= 0; i--)
            Destroy(_spawnRoot.GetChild(i).gameObject);
    }

    /// <summary>
    /// Safety net for anything that still toggles this component directly —
    /// without it, disabling the component would leave its bursts running.
    /// </summary>
    private void OnDisable()
    {
        StopAllCoroutines();
        ClearSpawned();
    }

    /// <summary>Spawns several droplets from the same chamber a short delay apart, so the burst reads as a flowing stream.</summary>
    private System.Collections.IEnumerator SpawnBurst(int chamberIndex)
    {
        int count = Random.Range(_burstCountMin, _burstCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            if (!_hazardActive || _disabledChambers.Contains(chamberIndex)) yield break;

            Spawn(chamberIndex);

            if (i < count - 1) yield return new WaitForSeconds(_burstInterval);
        }
    }

    /// <summary>Spawns a single droplet from the given chamber index and sends it along its spline path.</summary>
    public void Spawn(int chamberIndex)
    {
        if (_urinePrefab == null) return;
        if (!_hazardActive) return;
        if (chamberIndex < 0 || chamberIndex >= _chambers.Length) return;
        if (_disabledChambers.Contains(chamberIndex)) return;

        Chamber chamber = _chambers[chamberIndex];
        if (chamber?.path == null) return;

        Vector3 spawnPos = chamber.path.Evaluate(0f);
        GameObject instance = Instantiate(_urinePrefab, spawnPos, Quaternion.identity, SpawnRoot);

        UrineDroplet droplet = instance.GetComponent<UrineDroplet>();
        if (droplet == null) droplet = instance.AddComponent<UrineDroplet>();
        droplet.enabled = true;

        float travelTime = Random.Range(_minTravelTime, _maxTravelTime);
        droplet.Initialize(chamber.path, travelTime, _puddlePrefabs, _maxPuddles, _puddleMinScale, _puddleMaxScale, _puddleSpreadDuration, _puddleMinLifetime, _puddleMaxLifetime);

        Destroy(instance, travelTime + _extraLifetime);
    }

    private void OnDrawGizmos()
    {
        if (!_drawChamberPoints || _chambers == null) return;

        Gizmos.color = new Color(0.95f, 0.9f, 0.35f, 1f);
        foreach (Chamber chamber in _chambers)
        {
            if (chamber?.point == null) continue;
            Gizmos.DrawSphere(chamber.point.position, 0.1f);
        }
    }
}
