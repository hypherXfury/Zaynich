using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A paintable polygon zone drawn directly in the Scene view.
/// The SpriteSpawner reads all active SpawnZones to decide valid spawn positions.
///
/// SETUP:
///   1. Create an empty GameObject, name it "SpawnZone_Lungs" (or anything).
///   2. Add this component.
///   3. In the Scene view, hold Shift and click to add polygon points.
///   4. Drag existing points to reshape.
///   5. Add as many SpawnZone GameObjects as you need — the spawner
///      picks from all of them weighted by their area.
///
/// CONTROLS (Scene view, when this GameObject is selected):
///   Shift + Left Click       — add a new point
///   Ctrl  + Left Click       — remove the nearest point
///   Left Click + Drag        — move an existing point
/// </summary>
public class SpawnZone : MonoBehaviour
{
    [Tooltip("Polygon points in LOCAL space. Edit via Scene view handles.")]
    public List<Vector2> Points = new List<Vector2>
    {
        new Vector2(-2f,  2f),
        new Vector2( 2f,  2f),
        new Vector2( 2f, -2f),
        new Vector2(-2f, -2f),
    };

    [Header("Style")]
    [SerializeField] private Color _gizmoColor     = new Color(0.2f, 1f, 0.4f, 0.25f);
    [SerializeField] private Color _outlineColor   = new Color(0.2f, 1f, 0.4f, 0.9f);
    [SerializeField] private Color _pointColor     = new Color(1f,   1f, 0.2f, 1f);
    [SerializeField] private float _pointHandleSize = 0.15f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a random world-space point guaranteed to be inside this polygon.
    /// Uses rejection sampling within the bounding box — fast for convex/near-convex shapes.
    /// </summary>
    public Vector2 GetRandomPoint(int maxAttempts = 50)
    {
        if (Points == null || Points.Count < 3) return transform.position;

        Bounds b = GetLocalBounds();

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 local = new Vector2(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y));

            if (ContainsPoint(local))
                return (Vector2)transform.position + local;
        }

        // Fallback — return centroid
        return (Vector2)transform.position + GetCentroid();
    }

    /// <summary>Approximate area of the polygon (world units²). Used for weighted random selection.</summary>
    public float GetArea()
    {
        if (Points == null || Points.Count < 3) return 0f;
        float area = 0f;
        int n = Points.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = Points[i];
            Vector2 b = Points[(i + 1) % n];
            area += (a.x * b.y) - (b.x * a.y);
        }
        return Mathf.Abs(area) * 0.5f;
    }

    /// <summary>Returns true if the local-space point is inside the polygon (ray-casting).</summary>
    public bool ContainsPoint(Vector2 localPoint)
    {
        int n       = Points.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = Points[i];
            Vector2 pj = Points[j];
            if ((pi.y > localPoint.y) != (pj.y > localPoint.y) &&
                localPoint.x < (pj.x - pi.x) * (localPoint.y - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Returns true if the given world-space point is inside this zone.</summary>
    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        return ContainsPoint(worldPoint - (Vector2)transform.position);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Bounds GetLocalBounds()
    {
        Vector2 min = Points[0], max = Points[0];
        foreach (var p in Points)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        Vector2 centre = (min + max) * 0.5f;
        Vector2 size   = max - min;
        return new Bounds(centre, size);
    }

    private Vector2 GetCentroid()
    {
        Vector2 sum = Vector2.zero;
        foreach (var p in Points) sum += p;
        return sum / Points.Count;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        DrawZone(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZone(true);
    }

    private void DrawZone(bool selected)
    {
        if (Points == null || Points.Count < 2) return;

        Vector3 origin = transform.position;
        int n = Points.Count;

        // Filled polygon (approximate with triangles from centroid)
        if (selected)
        {
            Gizmos.color = _gizmoColor;
            Vector2 centroid = GetCentroid();
            for (int i = 0; i < n; i++)
            {
                Vector3 a = origin + (Vector3)(Vector2)Points[i];
                Vector3 b = origin + (Vector3)(Vector2)Points[(i + 1) % n];
                Vector3 c = origin + (Vector3)centroid;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, a);
            }
        }

        // Outline — always visible
        Gizmos.color = _outlineColor;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = origin + (Vector3)(Vector2)Points[i];
            Vector3 b = origin + (Vector3)(Vector2)Points[(i + 1) % n];
            Gizmos.DrawLine(a, b);
        }

        // Points — only when selected
        if (selected)
        {
            Gizmos.color = _pointColor;
            foreach (var p in Points)
                Gizmos.DrawSphere(origin + (Vector3)(Vector2)p, _pointHandleSize);
        }
    }
}