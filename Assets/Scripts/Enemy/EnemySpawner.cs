using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-enemy-type spawn configuration with an optional time window and group-spawn support.
/// </summary>
[Serializable]
public class EnemySpawnConfig
{
    public Enemy     enemyPrefab;
    public EnemyData enemyData;

    [Tooltip("Seconds between spawn events for this type.")]
    public float spawnInterval = 1.5f;

    [Tooltip("Seconds from run start before this type begins spawning. 0 = immediately.")]
    public float startTime = 0f;

    [Tooltip("Seconds from run start after which this type stops spawning. Use a large value for indefinite.")]
    public float endTime = 9999f;

    [Tooltip("Number of enemies spawned per interval. Values greater than 1 produce group spawns.")]
    [Min(1)]
    public int spawnGroupSize = 1;

    [Tooltip("Random radius spread applied around the base spawn point for each member of a group.")]
    [Min(0f)]
    public float groupSpreadRadius = 1f;

    /// <summary>Tracks active enemies spawned by this config so the global cap is shared.</summary>
    [NonSerialized] public int   activeCount;
    [NonSerialized] public float spawnTimer;
}

/// <summary>
/// Continuously spawns enemies just outside the camera's visible bounds.
/// Supports multiple enemy types, each with its own interval, time window, and group size.
/// Attach to any persistent scene GameObject (e.g., "EnemySpawner").
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private const float DefaultSpawnRadiusBuffer = 1.2f;
    private const int   DefaultMaxEnemies        = 60;

    [Header("Spawn Configs")]
    [SerializeField] private List<EnemySpawnConfig> _spawnConfigs = new();

    [Header("Global Settings")]
    [SerializeField] private Transform _player;
    [SerializeField] private int   _maxEnemies        = DefaultMaxEnemies;

    /// <summary>Extra world units added beyond the camera's half-diagonal before spawning.</summary>
    [SerializeField] private float _spawnRadiusBuffer = DefaultSpawnRadiusBuffer;

    [Header("Spawn points")]
    [SerializeField] private Transform[] spawnPoints;

    private float  _runTime;
    private int    _totalActiveCount;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;

        // Prime each config so the first tick can spawn immediately
        foreach (EnemySpawnConfig cfg in _spawnConfigs)
            cfg.spawnTimer = cfg.spawnInterval;
    }

    private void Update()
    {
        _runTime += Time.deltaTime;

        if (_totalActiveCount >= _maxEnemies) return;

        foreach (EnemySpawnConfig cfg in _spawnConfigs)
        {
            if (_runTime < cfg.startTime || _runTime > cfg.endTime) continue;

            cfg.spawnTimer += Time.deltaTime;
            if (cfg.spawnTimer < cfg.spawnInterval) continue;

            cfg.spawnTimer = 0f;
            SpawnGroup(cfg);
        }
    }

    private void SpawnGroup(EnemySpawnConfig cfg)
    {
        if (cfg.enemyPrefab == null || cfg.enemyData == null || _player == null) return;

        int     count   = Mathf.Max(1, cfg.spawnGroupSize);
        Vector3 basePos = GetSpawnPosition();

        for (int i = 0; i < count; i++)
        {
            if (_totalActiveCount >= _maxEnemies) break;

            Vector3 spawnPos = count > 1
                ? basePos + (Vector3)(UnityEngine.Random.insideUnitCircle * cfg.groupSpreadRadius)
                : basePos;

            //int ind = UnityEngine.Random.Range(0, spawnPoints.Length);
            Enemy enemy = Instantiate(cfg.enemyPrefab, spawnPos, Quaternion.identity);
            enemy.Initialize(cfg.enemyData, _player);
            enemy.OnDeath += _ => HandleEnemyDeath(cfg);
            cfg.activeCount++;
            _totalActiveCount++;
        }
    }

    private Vector3 GetSpawnPosition()
    {
        float halfH  = _mainCamera.orthographicSize;
        float halfW  = halfH * _mainCamera.aspect;
        float radius = Mathf.Sqrt(halfW * halfW + halfH * halfH) + _spawnRadiusBuffer;
        Vector2 dir  = UnityEngine.Random.insideUnitCircle.normalized;
        Vector3 origin = _player != null ? _player.position : Vector3.zero;
        return origin + new Vector3(dir.x, dir.y, 0f) * radius;
    }

    private void HandleEnemyDeath(EnemySpawnConfig cfg)
    {
        cfg.activeCount--;
        _totalActiveCount--;
    }
}
