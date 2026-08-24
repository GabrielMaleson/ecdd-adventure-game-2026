using System.Collections.Generic;
using UnityEngine;

// Per-object control over WHERE the interact prompt floats, for the cases the automatic
// rule gets wrong. Drop it on the interactable (the object carrying the DialogueStarter /
// Pickup / StatueSwitch / Teleporter / InteractDialogue) and it wins over everything else.
//
// The automatic rule — top of the object's own sprite — is right for a character, a
// statue, a key on the floor. It goes wrong in two shapes:
//
//   * the trigger is a bare collider with no artwork under it, so there are no bounds to
//     measure and the prompt lands on a guessed height. The well is this.
//   * the interaction covers MORE THAN ONE character, and any single anchor means the
//     prompt sticks to one of them no matter which one you walk up to. Talking to Marcus
//     and Erika is this.
//
// Nothing here is required. An interactable without this component keeps behaving exactly
// as before — this only exists to be dropped on the handful that need it.
[DisallowMultipleComponent]
public class InteractPromptAnchor : MonoBehaviour
{
    [Header("Onde")]
    [Tooltip("Objeto sobre o qual o prompt aparece. Vazio = este mesmo objeto.")]
    public Transform anchor;

    [Tooltip("Varios candidatos: o prompt vai sobre o MAIS PERTO do jogador. Use quando uma " +
             "interacao so cobre mais de um personagem — arraste os dois aqui. Preenchido, " +
             "isto ganha do campo Anchor.")]
    public List<Transform> nearestOf = new List<Transform>();

    [Header("Ajuste fino")]
    [Tooltip("Deslocamento extra, em unidades de mundo, somado ao ponto final.")]
    public Vector2 offset;

    [Tooltip("Ligado: mede o topo do sprite. Desligado: usa a posicao crua do transform. " +
             "Desligue quando o sprite tem muito espaco transparente e o topo dos bounds " +
             "fica longe da arte de verdade.")]
    public bool useSpriteBounds = true;

    [Tooltip("Ligado: este objeto usa a folga abaixo em vez da folga geral do InteractButton.")]
    public bool overrideHeightMargin;

    [Tooltip("Folga entre o topo do objeto e o prompt, so para este objeto.")]
    public float heightMargin = 0.35f;

    // Whichever transform the prompt should sit over right now. Re-asked every frame
    // rather than resolved once, because the nearest of several changes as you walk.
    public Transform Resolve(Transform player)
    {
        if (nearestOf != null && nearestOf.Count > 0 && player != null)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;

            foreach (Transform candidate in nearestOf)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                float d = (candidate.position - player.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }

            if (best != null)
                return best;
        }

        return anchor != null ? anchor : transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (nearestOf != null && nearestOf.Count > 0)
        {
            foreach (Transform candidate in nearestOf)
                if (candidate != null) Gizmos.DrawWireSphere(candidate.position, 0.25f);
            return;
        }

        Transform a = anchor != null ? anchor : transform;
        Gizmos.DrawWireSphere(a.position + (Vector3)offset, 0.25f);
    }
}
