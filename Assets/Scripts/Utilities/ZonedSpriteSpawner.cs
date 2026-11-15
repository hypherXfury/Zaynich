using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns sprite prefabs within the CAMERA BOUNDS and within painted SpawnZone
/// polygons. Uses a fixed pool — objects are never destroyed, wrapping is
/// handled by CellWander. This script only handles initial placement.
///
/// If the camera is outside the zone entirely, falls back to camera-only
/// placement so the screen is never empty.
/// </summary>
public class ZonedSpriteSpawner : MonoBehaviour
{
    [Header("Zones")]
    [SerializeField] private List<SpawnZone> _spawnZones = new();

    [Header("Prefabs")]
    [SerializeField] private List<GameObject> _prefabs = new();

    [Header("Spawn Settings")]
    [Tooltip("Total number of sprites in the pool (all spawned at start within camera).")]
    [SerializeField] private int   _totalCount          = 25;
    [SerializeField] private float _minSpacing          = 0.5f;
    [SerializeField] private int   _maxAttemptsPerSprite = 30;

    [Header("Variation")]
    [SerializeField] private bool  _randomRotation = true;
    [SerializeField] private float _minScale       = 0.8f;
    [SerializeField] private float _maxScale       = 1.2f;

    [Header("References")]
    [SerializeField] private Camera    _camera;
    [SerializeField] private Transform _spawnParent;

    private readonly List<Vector2> _placedPositions = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (_camera == null) _camera = Camera.main;
        BuildWeightTable(out _, out _);
        SpawnAll();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    private void SpawnAll()
    {
        if (_prefabs == null || _prefabs.Count == 0) return;
        Transform parent = _spawnParent != null ? _spawnParent : transform;

        for (int i = 0; i < _totalCount; i++)
        {
            Vector2? pos = FindValidPosition();
            if (pos == null) continue;

            GameObject prefab = _prefabs[Random.Range(0, _prefabs.Count)];
            if (prefab == null) continue;

            GameObject go = Instantiate(prefab, pos.Value, Quaternion.identity, parent);

            if (_randomRotation)
                go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            go.transform.localScale = Vector3.one * Random.Range(_minScale, _maxScale);

            _placedPositions.Add(pos.Value);
        }
    }

    // ── Position finding ──────────────────────────────────────────────────────

    private Vector2? FindValidPosition()
    {
        Bounds cam = GetCameraBounds();

        for (int attempt = 0; attempt < _maxAttemptsPerSprite; attempt++)
        {
            // Random point inside camera bounds
            Vector2 candidate = new Vector2(
                Random.Range(cam.min.x, cam.max.x),
                Random.Range(cam.min.y, cam.max.y));

            // Must be inside at least one zone (if zones are assigned)
            if (_spawnZones != null && _spawnZones.Count > 0)
            {
                bool inZone = false;
                foreach (var zone in _spawnZones)
                    if (zone != null && zone.ContainsWorldPoint(candidate))
                    { inZone = true; break; }

                if (!inZone) continue;
            }

            // Must respect min spacing
            if (!IsFarEnough(candidate)) continue;

            return candidate;
        }

        // Fallback — no zone overlap found, place anywhere in camera
        for (int attempt = 0; attempt < _maxAttemptsPerSprite; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(cam.min.x, cam.max.x),
                Random.Range(cam.min.y, cam.max.y));

            if (IsFarEnough(candidate)) return candidate;
        }

        return null;
    }

    private bool IsFarEnough(Vector2 pos)
    {
        float minSq = _minSpacing * _minSpacing;
        foreach (var p in _placedPositions)
            if ((pos - p).sqrMagnitude < minSq) return false;
        return true;
    }

    private Bounds GetCameraBounds()
    {
        float h = _camera.orthographicSize;
        float w = h * _camera.aspect;
        return new Bounds(_camera.transform.position, new Vector3(w * 2f, h * 2f, float.MaxValue));
    }

    private void BuildWeightTable(out float[] weights, out float total)
    {
        weights = new float[_spawnZones.Count];
        total   = 0f;
        for (int i = 0; i < _spawnZones.Count; i++)
        {
            float area    = _spawnZones[i] != null ? _spawnZones[i].GetArea() : 0f;
            weights[i]    = area;
            total        += area;
        }
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (_camera == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Bounds b = GetCameraBounds();
        Gizmos.DrawWireCube(b.center, b.size);
    }
}