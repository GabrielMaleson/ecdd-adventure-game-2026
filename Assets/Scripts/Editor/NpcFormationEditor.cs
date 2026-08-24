using UnityEditor;
using UnityEngine;

// Two buttons, so a formation is authored by dragging characters around the Scene view
// instead of guessing world units and typing them in.
//
//   Capture From Scene — reads where each slot's character is standing RIGHT NOW and
//                        stores it as an offset from the anchor. This is the one you use.
//   Apply Now          — puts them back on it, without entering Play.
//
// Both are editor-only and manual, same rule as the grid Snap buttons: what you lay out
// in the scene is exactly what ships, and nothing rearranges itself behind your back.
[CustomEditor(typeof(NpcFormation))]
public class NpcFormationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var formation = (NpcFormation)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Capture From Scene"))
            Capture(formation);

        if (GUILayout.Button("Apply Now"))
        {
            Undo.RecordObjects(TransformsOf(formation), "Apply NPC Formation");
            formation.Apply();
        }

        EditorGUILayout.HelpBox(
            "Posicione o Marcus e a Erika no olho, na cena, e clique em Capture From Scene. " +
            "Os offsets ficam guardados relativos ao Anchor — mover o marcador depois leva o " +
            "grupo inteiro junto.",
            MessageType.Info);
    }

    static void Capture(NpcFormation formation)
    {
        Transform anchor = formation.anchor != null ? formation.anchor : formation.transform;

        Undo.RecordObject(formation, "Capture NPC Formation");

        foreach (NpcFormation.Slot slot in formation.slots)
        {
            if (slot == null || slot.npc == null) continue;

            Vector3 delta = slot.npc.position - anchor.position;
            slot.offset = new Vector2(delta.x, delta.y);

            // Capture the facing too, so "how they're standing" means the whole pose and
            // not just the spot. A slot deliberately left on Leave stays on Leave — that
            // is an instruction ("don't touch this one"), not a missing value.
            if (slot.facing != NpcFormation.Facing.Leave)
            {
                SpriteRenderer sprite = slot.npc.GetComponentInChildren<SpriteRenderer>(true);
                if (sprite != null)
                    slot.facing = sprite.flipX ? NpcFormation.Facing.Left : NpcFormation.Facing.Right;
            }
        }

        EditorUtility.SetDirty(formation);
    }

    static Object[] TransformsOf(NpcFormation formation)
    {
        var list = new System.Collections.Generic.List<Object>();
        foreach (NpcFormation.Slot slot in formation.slots)
            if (slot != null && slot.npc != null) list.Add(slot.npc);
        return list.ToArray();
    }
}
