#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for SpawnZone that enables painting polygon points
/// directly in the Scene view.
///
/// CONTROLS:
///   Shift + Left Click       — add new point (inserts on nearest edge)
///   Ctrl  + Left Click       — remove nearest point
///   Left Click + Drag        — move existing point
/// </summary>
[CustomEditor(typeof(SpawnZone))]
public class SpawnZoneEditor : Editor
{
    private const float PickRadius        = 0.3f;
    private const float PointDisplaySize  = 0.12f;

    private SpawnZone  _zone;
    private int        _dragIndex   = -1;
    private bool       _isDragging;

    private void OnEnable()  => _zone = (SpawnZone)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene view controls:\n" +
            "  Shift + Click  → Add point (on nearest edge)\n" +
            "  Ctrl  + Click  → Remove nearest point\n" +
            "  Click + Drag   → Move point",
            MessageType.Info);

        if (GUILayout.Button("Reset to Default Square"))
        {
            Undo.RecordObject(_zone, "Reset SpawnZone");
            _zone.Points.Clear();
            _zone.Points.Add(new Vector2(-2f,  2f));
            _zone.Points.Add(new Vector2( 2f,  2f));
            _zone.Points.Add(new Vector2( 2f, -2f));
            _zone.Points.Add(new Vector2(-2f, -2f));
            EditorUtility.SetDirty(_zone);
        }

        EditorGUILayout.LabelField($"Points: {_zone.Points.Count}   " +
                                   $"Area: {_zone.GetArea():F2} u²",
                                   EditorStyles.miniLabel);
    }

    private void OnSceneGUI()
    {
        if (_zone == null || _zone.Points == null) return;

        Event e      = Event.current;
        Vector3 orig = _zone.transform.position;
        int n        = _zone.Points.Count;

        // Consume mouse events so the scene view camera doesn't also react
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        // ── Draw editable handles ─────────────────────────────────────────────
        Handles.color = new Color(1f, 1f, 0.2f, 1f);
        for (int i = 0; i < n; i++)
        {
            Vector3 wp = orig + (Vector3)(Vector2)_zone.Points[i];
            Handles.DrawSolidDisc(wp, Vector3.forward, PointDisplaySize);
            Handles.Label(wp + Vector3.up * 0.25f,
                          i.ToString(), EditorStyles.miniLabel);
        }

        // ── Mouse input ───────────────────────────────────────────────────────
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 mouseWorld = HandleUtility.GUIPointToWorldRay(e.mousePosition)
                                              .origin;
            Vector2 localMouse = mouseWorld - (Vector2)orig;

            if (e.shift)
            {
                // Shift + Click — insert point on the nearest edge
                Undo.RecordObject(_zone, "Add SpawnZone Point");
                int insertIdx = FindNearestEdge(localMouse);
                _zone.Points.Insert(insertIdx + 1, localMouse);
                EditorUtility.SetDirty(_zone);
                e.Use();
            }
            else if (e.control)
            {
                // Ctrl + Click — remove nearest point
                if (n > 3)
                {
                    Undo.RecordObject(_zone, "Remove SpawnZone Point");
                    int idx = FindNearestPoint(localMouse);
                    _zone.Points.RemoveAt(idx);
                    EditorUtility.SetDirty(_zone);
                }
                e.Use();
            }
            else
            {
                // Plain click — start drag on nearest point if close enough
                int idx = FindNearestPoint(localMouse);
                if (idx >= 0 && Vector2.Distance(localMouse, _zone.Points[idx]) < PickRadius)
                {
                    _dragIndex  = idx;
                    _isDragging = true;
                    GUIUtility.hotControl = controlID;
                    e.Use();
                }
            }
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && _isDragging && _dragIndex >= 0)
        {
            Undo.RecordObject(_zone, "Move SpawnZone Point");
            Vector2 mouseWorld = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            _zone.Points[_dragIndex] = mouseWorld - (Vector2)orig;
            EditorUtility.SetDirty(_zone);
            e.Use();
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            _isDragging = false;
            _dragIndex  = -1;
            GUIUtility.hotControl = 0;
        }

        // Keep scene view repainting while dragging
        if (_isDragging) HandleUtility.Repaint();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int FindNearestPoint(Vector2 localMouse)
    {
        int   best   = 0;
        float bestSq = float.MaxValue;
        for (int i = 0; i < _zone.Points.Count; i++)
        {
            float sq = (localMouse - _zone.Points[i]).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = i; }
        }
        return best;
    }

    private int FindNearestEdge(Vector2 localMouse)
    {
        int   best   = 0;
        float bestDist = float.MaxValue;
        int n = _zone.Points.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a    = _zone.Points[i];
            Vector2 b    = _zone.Points[(i + 1) % n];
            float dist   = DistancePointToSegment(localMouse, a, b);
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + t * ab);
    }
}
#endif