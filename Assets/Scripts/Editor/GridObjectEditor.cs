using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Adds the Snap buttons to every GridObject (crates, obstacles, targets).
//
// Snapping is deliberately EDITOR-ONLY and manual: the layout you build in the
// scene is exactly what ships. Nothing realigns itself when you hit Play, so the
// design can't shift under you.
[CustomEditor(typeof(GridObject), true)]
[CanEditMultipleObjects]
public class GridObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Snap to Grid"))
            Snap(targets);

        if (GUILayout.Button("Snap ALL GridObjects in Scene"))
            Snap(FindObjectsByType<GridObject>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    static void Snap(Object[] objects)
    {
        var toSnap = new List<GridObject>();
        foreach (var o in objects)
            if (o is GridObject g) toSnap.Add(g);

        // Parents before children. Grid objects nest (an ObstacleGroup pivot with
        // GridObstacle children), and snapping a parent drags its children along —
        // so a child snapped first would be knocked back off-grid by its own
        // parent. Shallowest-first guarantees each object is aligned only after
        // everything that can still move it has settled.
        toSnap.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));

        foreach (var g in toSnap)
        {
            // Records the transform so Ctrl+Z undoes the snap and the scene is
            // flagged dirty for saving.
            Undo.RecordObject(g.transform, "Snap to Grid");
            g.SnapToGrid();
        }
    }

    static int Depth(Transform t)
    {
        int depth = 0;
        while (t.parent != null) { depth++; t = t.parent; }
        return depth;
    }
}
