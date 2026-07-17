using UnityEditor;
using UnityEngine;

// One-shot cleanup for objects that ended up with more than one grid component.
//
// [DisallowMultipleComponent] stops new ones being added, but it can't touch
// copies that were already serialised — those have to be destroyed, and doing it
// by hand is error-prone precisely because the duplicates look identical in the
// Inspector.
//
// Keeps the FIRST grid component on each object and removes the rest. Undoable.
public static class GridDuplicateCleaner
{
    const string Menu = "Tools/Grid/Remove Duplicate Grid Components (Selection)";

    [MenuItem(Menu)]
    static void Clean()
    {
        int removed = 0;
        int objects = 0;

        foreach (GameObject root in Selection.gameObjects)
        {
            // Includes inactive: a disabled crate with duplicates is still broken,
            // it just hasn't shouted yet.
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GridObject[] comps = t.GetComponents<GridObject>();
                if (comps.Length <= 1) continue;

                objects++;
                // Backwards: destroying shifts the rest, and index 0 is the keeper.
                for (int i = comps.Length - 1; i >= 1; i--)
                {
                    Undo.DestroyObjectImmediate(comps[i]);
                    removed++;
                }
            }
        }

        Debug.Log(removed == 0
            ? "Grid cleanup: nothing to do — no object in the selection had duplicates."
            : $"Grid cleanup: removed {removed} duplicate component(s) across {objects} object(s). Ctrl+Z undoes it.");
    }

    [MenuItem(Menu, true)]
    static bool CleanEnabled() => Selection.gameObjects.Length > 0;

    // The duplicates turned out to be per-INSTANCE overrides, not prefab data, so
    // counts differ from crate to crate and cleaning the prefab fixes nothing.
    // Hunting them one at a time is exactly the tedium worth automating.
    [MenuItem("Tools/Grid/Remove Duplicate Grid Components (WHOLE SCENE)")]
    static void CleanScene()
    {
        int removed = 0;
        int objects = 0;

        foreach (GridObject g in Object.FindObjectsByType<GridObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // The list was captured before anything was destroyed, so by now it can
            // contain components this same pass already removed. Touching one at
            // all — even GetComponents — throws MissingReferenceException.
            if (g == null) continue;

            // FindObjectsByType returns every component, so an object with three
            // copies shows up three times. Only act from the keeper.
            GridObject[] comps = g.GetComponents<GridObject>();
            if (comps.Length <= 1 || comps[0] != g) continue;

            objects++;
            for (int i = comps.Length - 1; i >= 1; i--)
            {
                Undo.DestroyObjectImmediate(comps[i]);
                removed++;
            }
        }

        Debug.Log(removed == 0
            ? "Grid cleanup: scene is clean — no object has duplicate grid components."
            : $"Grid cleanup: removed {removed} duplicate component(s) from {objects} object(s). Ctrl+Z undoes it. Save the scene.");
    }
}
