using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any enemy spawned from a nest to constrain it within the
/// SpawnZone polygons painted in the scene.
///
/// Behaviour at the boundary:
///   - While inside any zone: full speed, no interference.
///   - Approaching the edge: velocity is scaled down proportionally
///     based on how close the enemy is to the nearest boundary.
///   - Outside all zones: velocity is zeroed and the enemy is nudged
///     back toward the nearest zone interior point.
///
/// SETUP:
///   Add this component to your Enemy prefab that is spawned from EnemyNest.
///   No manual zone assignment needed — it finds all SpawnZone objects in
///   the scene automatically on Start.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ZoneBounded : MonoBehaviour
{
    [Header("Boundary Settings")]
    [Tooltip("Distance from the zone edge where the enemy starts slowing down.")]
    [SerializeField] private float _slowdownStartDistance = 0.8f;

    [Tooltip("How strongly the enemy is nudged back when it is fully outside all zones.")]
    [SerializeField] private float _pushBackStrength = 3f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Rigidbody2D        _rb;
    private List<SpawnZone>    _zones = new();
    private JumperEnemy        _jumper;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        TryGetComponent(out _jumper);

        // Auto-find all SpawnZone objects in the scene
        var found = Object.FindObjectsByType<SpawnZone>(FindObjectsSortMode.None);
        _zones.AddRange(found);

        if (_zones.Count == 0)
            Debug.LogWarning($"[ZoneBounded] No SpawnZones found in scene for {name}. " +
                             "Enemy will move freely.", this);
    }

    private void FixedUpdate()
    {
        if (_zones.Count == 0) return;

        // While a jumper is airborne, its coroutine owns MovePosition and the
        // arc's vertical offset can carry rb.position outside the zone polygon.
        // Interfering here would zero velocity / push back and stick the enemy
        // at the edge — so let the jump complete untouched.
        if (_jumper != null && _jumper.IsJumping) return;

        Vector2 pos = _rb.position;

        // ── Find which zone the enemy is in (if any) ──────────────────────────
        SpawnZone currentZone = FindContainingZone(pos);

        if (currentZone != null)
        {
            // Inside a zone — only dampen motion that heads OUT through the
            // nearest edge. Inward / tangential motion (e.g. toward the player)
            // is untouched, so enemies never get stuck against a boundary.
            Vector2 vel = _rb.linearVelocity;
            if (vel.sqrMagnitude > 0.0001f &&
                TryGetNearestEdge(pos, currentZone, out Vector2 closestWorld, out float distToEdge) &&
                distToEdge < _slowdownStartDistance)
            {
                // Exit direction points from the enemy toward the nearest edge —
                // motion along it would carry the enemy out of the zone.
                Vector2 exitDir = (closestWorld - pos).normalized;
                float exitSpeed = Vector2.Dot(vel, exitDir);

                if (exitSpeed > 0f)
                {
                    // Remove a fraction of the outward component that grows as the
                    // enemy nears the edge (t=0 at the edge → cancel it entirely).
                    float t = Mathf.Clamp01(distToEdge / _slowdownStartDistance);
                    _rb.linearVelocity = vel - exitDir * exitSpeed * (1f - t);
                }
            }
        }
        else
        {
            // Outside all zones — stop and push back toward nearest zone point
            _rb.linearVelocity = Vector2.zero;

            Vector2 nearest = GetNearestPointInAnyZone(pos);
            Vector2 pushDir = (nearest - pos).normalized;
            _rb.MovePosition(pos + pushDir * _pushBackStrength * Time.fixedDeltaTime);
        }
    }

    // ── Zone queries ──────────────────────────────────────────────────────────

    /// <summary>Returns the first zone that contains the given world position, or null.</summary>
    private SpawnZone FindContainingZone(Vector2 worldPos)
    {
        foreach (var zone in _zones)
            if (zone != null && zone.ContainsWorldPoint(worldPos))
                return zone;
        return null;
    }

    /// <summary>
    /// Finds the closest point on the given zone's boundary to worldPos, in
    /// world space, plus the distance to it. Returns false if the zone has no
    /// polygon points.
    /// </summary>
    private bool TryGetNearestEdge(Vector2 worldPos, SpawnZone zone, out Vector2 closestWorld, out float distance)
    {
        closestWorld = worldPos;
        distance     = float.MaxValue;

        Vector2 localPos = worldPos - (Vector2)zone.transform.position;
        var     pts      = zone.Points;
        int     n        = pts.Count;
        if (n == 0) return false;

        for (int i = 0; i < n; i++)
        {
            Vector2 a       = pts[i];
            Vector2 b       = pts[(i + 1) % n];
            Vector2 closest = ClosestPointOnSegment(localPos, a, b);
            float   dist    = Vector2.Distance(localPos, closest);
            if (dist < distance)
            {
                distance     = dist;
                closestWorld = (Vector2)zone.transform.position + closest;
            }
        }
        return true;
    }

    /// <summary>
    /// Finds the nearest point on any zone's polygon boundary to the given world position.
    /// Used to push the enemy back inside when it has escaped all zones.
    /// </summary>
    private Vector2 GetNearestPointInAnyZone(Vector2 worldPos)
    {
        Vector2 best     = worldPos;
        float   bestDist = float.MaxValue;

        foreach (var zone in _zones)
        {
            if (zone == null) continue;

            Vector2 localPos = worldPos - (Vector2)zone.transform.position;
            var     pts      = zone.Points;
            int     n        = pts.Count;

            for (int i = 0; i < n; i++)
            {
                Vector2 a         = pts[i];
                Vector2 b         = pts[(i + 1) % n];
                Vector2 closest   = ClosestPointOnSegment(localPos, a, b);
                float   dist      = Vector2.Distance(localPos, closest);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    // Convert back to world space — step slightly inward
                    Vector2 edgeNormal = (localPos - closest).normalized;
                    best = (Vector2)zone.transform.position + closest + edgeNormal * 0.1f;
                }
            }
        }
        return best;
    }

    // ── Geometry helpers ──────────────────────────────────────────────────────

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float   t  = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + t * ab;
    }
}