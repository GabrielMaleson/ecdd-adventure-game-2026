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

            // O LADO tambem, quando da para ler da cena. Um slot em Leave fica em Leave —
            // isso e uma instrucao ("nao mexa neste"), nao um valor faltando.
            //
            // Ler flipX, como estava aqui, nao funciona para NINGUEM neste projeto: o
            // CharacterFacing zera o flipX de proposito no inicio, para o espelhamento ter
            // uma fonte so. O resultado era gravar Right sempre e, de quebra, apagar o que
            // o autor tinha escolhido no dropdown.
            if (slot.facing != NpcFormation.Facing.Leave)
            {
                CharacterFacing cf = slot.npc.GetComponentInChildren<CharacterFacing>(true);
                if (cf == null) cf = slot.npc.GetComponentInParent<CharacterFacing>();

                // Arte de dois lados (o Marcus): virar e trocar de ESTADO do Animator, nao
                // espelhar. Fora de Play nao ha nada na cena que diga para que lado ele
                // olha — nem escala, nem flipX. Entao o dropdown manda, e o Capture nao
                // encosta nele.
                if (cf != null && cf.sidesDrawnSeparately) continue;

                // Personagem espelhado (a Erika, o Josh): o lado E o sinal da escala X do
                // objeto visual. Isso da para ler no editor.
                Transform visual = cf != null && cf.visual != null ? cf.visual : slot.npc;
                slot.facing = visual.localScale.x < 0f
                    ? NpcFormation.Facing.Left
                    : NpcFormation.Facing.Right;
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
