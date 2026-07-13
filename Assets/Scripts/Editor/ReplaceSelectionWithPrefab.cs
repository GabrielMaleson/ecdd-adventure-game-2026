#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor utility: select a bunch of loose GameObjects in the Hierarchy (e.g. a
// wall of hand-placed bushes), assign an existing prefab, and replace every
// selected object with an instance of that prefab, keeping position/rotation/scale.
public class ReplaceWithPrefabWindow : EditorWindow
{
    GameObject prefab;

    [MenuItem("Tools/Replace Selection With Prefab")]
    static void Open()
    {
        GetWindow<ReplaceWithPrefabWindow>("Replace With Prefab");
    }

    void OnSelectionChange() => Repaint();

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Select the GameObjects to replace in the Hierarchy, assign the prefab below, then click Replace.",
            MessageType.Info);

        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

        int count = Selection.gameObjects.Length;
        using (new EditorGUI.DisabledScope(prefab == null || count == 0))
        {
            if (GUILayout.Button($"Replace {count} Selected Object(s)"))
                Replace();
        }
    }

    void Replace()
    {
        if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Replace With Prefab", "Assigned object isn't a prefab asset.", "OK");
            return;
        }

        GameObject[] selected = Selection.gameObjects;

        Undo.SetCurrentGroupName("Replace Selection With Prefab");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject go in selected)
        {
            Transform  t      = go.transform;
            Transform  parent = t.parent;
            Vector3    pos    = t.position;
            Quaternion rot    = t.rotation;
            Vector3    scale  = t.localScale;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Replace Selection With Prefab");
            instance.transform.SetParent(parent, false);
            instance.transform.position   = pos;
            instance.transform.rotation   = rot;
            instance.transform.localScale = scale;

            Undo.DestroyObjectImmediate(go);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Replaced {selected.Length} object(s) with prefab '{prefab.name}'.");
    }
}
#endif
