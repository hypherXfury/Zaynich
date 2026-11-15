using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-editable path made of ordered waypoint transforms, interpolated with a
/// Catmull-Rom spline. Add child transforms under this object, assign them in
/// <see cref="_points"/> in order, and drag them in the Scene view to shape the curve.
/// The first point is the path's start, the last point is where it ends.
/// </summary>
public class SplinePath : MonoBehaviour
{
    [SerializeField] private List<Transform> _points = new();

    [Header("Debug")]
    [SerializeField] private bool  _drawGizmo = true;
    [SerializeField] private Color _gizmoColor = new(0.95f, 0.9f, 0.35f, 1f);
    [SerializeField] private int   _gizmoResolution = 20;

    public int PointCount => _points.Count;

    /// <summary>Evaluates a position along the path. t is clamped to [0, 1].</summary>
    public Vector3 Evaluate(float t)
    {
        if (_points.Count == 0) return transform.position;
        if (_points.Count == 1) return _points[0].position;

        t = Mathf.Clamp01(t);
        int segments = _points.Count - 1;
        float scaled = t * segments;
        int i = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
        float localT = scaled - i;

        Vector3 p0 = _points[Mathf.Max(i - 1, 0)].position;
        Vector3 p1 = _points[i].position;
        Vector3 p2 = _points[Mathf.Min(i + 1, segments)].position;
        Vector3 p3 = _points[Mathf.Min(i + 2, segments)].position;

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmo || _points.Count < 2) return;

        Gizmos.color = _gizmoColor;
        Vector3 prev = Evaluate(0f);
        for (int i = 1; i <= _gizmoResolution; i++)
        {
            float t = i / (float)_gizmoResolution;
            Vector3 next = Evaluate(t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        foreach (Transform point in _points)
        {
            if (point == null) continue;
            Gizmos.DrawSphere(point.position, 0.08f);
        }
    }
}
