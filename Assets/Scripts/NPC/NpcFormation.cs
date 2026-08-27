using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// "These characters stand HERE, arranged like THIS, facing THAT way."
//
// A formation is a MARKER object in the scene (FriendsPosition) plus one hand-authored
// offset per character. <<formation FriendsAtEldersHouse>> snaps the whole group onto it.
//
// Where to put the component: on the MARKER. Never on the characters, and never on a
// group that gets switched off — <<disable Friends>> turns the friends off between beats,
// and a formation living on a disabled object can't be found by name to place them again
// before turning them back on.
//
// How to author it: by EYE, not by typing numbers. Drag the characters where you want
// them in the Scene view, then press "Capture From Scene" on this component. Offsets are
// stored RELATIVE to the anchor, so moving the marker afterwards carries the whole group
// with it and the arrangement survives.
//
// Several formations can coexist — on the same marker or on different ones. Each has its
// own name, so the same two friends stand one way at the square, another way at the
// elder's house and another at a puzzle site, with no per-beat code anywhere.
public class NpcFormation : MonoBehaviour
{
    public enum Facing
    {
        Leave,   // don't touch this character's facing at all
        Right,   // sprite unflipped
        Left     // sprite flipped on X
    }

    [System.Serializable]
    public class Slot
    {
        public Transform npc;

        [Tooltip("Onde ele fica, em unidades, RELATIVO ao Anchor. Nao digite na mao: " +
                 "posicione na cena e clique em Capture From Scene.")]
        public Vector2 offset;

        [Tooltip("Para que lado ele olha ao ser colocado. Leave = nao mexe no lado atual.")]
        public Facing facing = Facing.Leave;
    }

    [Header("Identity")]
    [Tooltip("Nome usado no .yarn: <<formation EsteNome>>.")]
    public string formationName;

    [Tooltip("O ponto de referencia da formacao. Vazio = o proprio objeto deste componente.")]
    public Transform anchor;

    [Header("Who stands where")]
    public List<Slot> slots = new List<Slot>();

    [Header("Behaviour")]
    [Tooltip("Aplica a formacao sozinha no Start. Deixe DESLIGADO para beats de historia — " +
             "quem manda colocar e o <<formation>> no .yarn, no momento certo.")]
    public bool applyOnStart;

    [Tooltip("Zera uma rotacao de 180 no Y antes de espelhar pelo Flip X. Sao duas formas " +
             "de espelhar o mesmo sprite, e um personagem que carrega as duas fica virado " +
             "ao contrario de todo mundo — <<face Erika left>> mandaria ela olhar pra " +
             "direita. Normalizar deixa Flip X sendo a UNICA verdade sobre o lado.")]
    public bool normalizeMirrorRotation = true;

    // Every formation in the scene registers here, so <<formation Name>> is a lookup
    // across ALL of them rather than through one singleton — same reasoning as Cutscener:
    // different beats are expected to each get their own marker, and a "last one wins"
    // instance would mean only whichever registered last ever fired.
    private static readonly List<NpcFormation> allInstances = new List<NpcFormation>();

    private Transform Anchor => anchor != null ? anchor : transform;

    private void Awake()
    {
        allInstances.Add(this);
    }

    private void OnDestroy()
    {
        allInstances.Remove(this);
    }

    private void Start()
    {
        if (applyOnStart)
            Apply();
    }

    // The external hook. Public and parameterless so any UnityEvent already in the
    // Inspector can drive it — a BarkTrigger, a CrateTarget, a Pickup.
    public void Apply()
    {
        Vector3 origin = Anchor.position;

        foreach (Slot slot in slots)
        {
            if (slot == null || slot.npc == null)
                continue;

            // Keep whatever sorting depth the character already had: z here is the 2D
            // draw order, and flattening it to the marker's z would shove a character
            // in front of or behind scenery it was authored to sit between.
            Vector3 destination = new Vector3(
                origin.x + slot.offset.x,
                origin.y + slot.offset.y,
                slot.npc.position.z);

            slot.npc.position = destination;

            // Or physics drags it straight back next FixedUpdate — the same reason
            // Cutscener.PlaceAt does this. Skipped while the object is switched off:
            // writing to a disabled body warns and achieves nothing, and placing the
            // group BEFORE enabling it is the normal order (no pop on screen).
            if (slot.npc.gameObject.activeInHierarchy)
            {
                Rigidbody2D rb = slot.npc.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = destination;
            }

            if (slot.facing != Facing.Leave)
                SetFacing(slot.npc, slot.facing == Facing.Left);
        }
    }

    // Flips EVERY renderer under the character, not the first one found: a character
    // built out of parts would otherwise turn half of itself around (Cutscener.Face has
    // the same note for the same reason).
    private void SetFacing(Transform npc, bool faceLeft)
    {
        // Same reason as Cutscener.Face: a two-sided character turns itself instead of
        // being mirrored, or Marcus's watch ends up on the wrong wrist.
        CharacterFacing facing = npc.GetComponentInChildren<CharacterFacing>();
        if (facing == null) facing = npc.GetComponentInParent<CharacterFacing>();
        if (facing != null)
        {
            facing.FaceHorizontal(faceLeft);
            return;
        }

        if (normalizeMirrorRotation)
        {
            // A 180 on Y mirrors the sprite too, so a character carrying both that and
            // Flip X reads backwards from everyone else. Fold the rotation away and let
            // Flip X be the single source of truth about which way anyone is looking.
            Vector3 euler = npc.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(euler.y, 0f)) > 1f)
            {
                faceLeft = !faceLeft;   // the rotation WAS mirroring it; preserve the look
                npc.rotation = Quaternion.Euler(euler.x, 0f, euler.z);
            }
        }

        foreach (SpriteRenderer sprite in npc.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.flipX = faceLeft;
    }

    public bool HasName(string wanted) =>
        !string.IsNullOrEmpty(formationName) &&
        formationName.Equals(wanted, System.StringComparison.OrdinalIgnoreCase);

    // <<formation FriendsAtEldersHouse>> — static so the Yarn command needs no reference
    // to a specific marker, matching how <<movement>> and <<enable>> already work.
    [YarnCommand("formation")]
    public static void Trigger(string formationName)
    {
        NpcFormation owner = null;
        int matches = 0;

        foreach (NpcFormation f in allInstances)
        {
            if (!f.HasName(formationName)) continue;
            matches++;
            if (owner == null) owner = f;
        }

        if (owner == null)
        {
            Debug.LogWarning($"<<formation>>: no NpcFormation named '{formationName}' in the scene.");
            return;
        }

        // A duplicate name is the failure that looks like "I edited it and nothing
        // changed" — the second one silently never runs.
        if (matches > 1)
            Debug.LogWarning($"<<formation>>: '{formationName}' exists on {matches} objects — only '{owner.name}' will be used.");

        owner.Apply();
    }

    // Draws the arrangement in the Scene view even with nobody standing on it yet, so a
    // formation can be laid out before the characters exist.
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Anchor.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.15f);

        foreach (Slot slot in slots)
        {
            if (slot == null) continue;

            Vector3 spot = origin + new Vector3(slot.offset.x, slot.offset.y, 0f);

            Gizmos.DrawLine(origin, spot);
            Gizmos.DrawWireSphere(spot, 0.35f);
            Gizmos.DrawWireCube(spot, new Vector3(0.7f, 0.25f, 0f));
        }
    }
}
