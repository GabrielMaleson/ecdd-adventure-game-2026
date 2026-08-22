using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The statue: an interactable that turns one or more ObstacleGroups by 90 degrees
// every time it's used, reshaping the board mid-puzzle.
//
// Driven by the FRAGMENT (ghost), not the MC. Needs a TRIGGER Collider2D marking
// the range at which the ghost can use it. Whenever the ghost is inside that range
// — whether you're piloting it or it's parked there — pressing E turns the statue.
// The MC alone can never use it, and it never fires from across the map.
[RequireComponent(typeof(Collider2D))]
public class StatueSwitch : MonoBehaviour
{
    // One rotating group + its own direction override. The little "Counter Clockwise"
    // checkbox next to each group flips THAT group against the statue's base direction,
    // leaving the others alone.
    [System.Serializable]
    public class RotatingGroup
    {
        public ObstacleGroup group;

        [Tooltip("Turn this specific group the OTHER way from the statue's base direction.")]
        public bool counterClockwise;
    }

    [Tooltip("Rotating bush groups this statue turns. All of them turn together, or none do. Each has its own Counter Clockwise toggle.")]
    [SerializeField] List<RotatingGroup> groups = new List<RotatingGroup>();

    [Tooltip("Tile cycles this statue advances one step. Turn together with the groups, all-or-nothing.")]
    [SerializeField] List<TileCycle> cycles = new List<TileCycle>();

    [Tooltip("Prompt shown on the interact label while the parked ghost can use this.")]
    [SerializeField] string interactLabel = "E";

    [Tooltip("Fires when the statue is used successfully — rumble, dust, stone-grinding SFX.")]
    public UnityEvent onActivated;

    [Tooltip("Fires when the statue is used but nothing can turn (something's in the way). Fires EVERY time — hook the grind/clunk sound and a shake here.")]
    public UnityEvent onRefused;

    [Tooltip("Fires only the FIRST time this statue is refused. Hook Haze's explanatory line here: the first refusal teaches the rule, and repeating the line on every later bump would just be noise.")]
    public UnityEvent onRefusedFirstTime;

    [Tooltip("Flash whatever is in the way when a turn is refused. Needs nothing on the crates — see BlockedFlash.")]
    [SerializeField] bool flashBlocker = true;

    bool ghostInRange;
    bool refusedBefore;

    // Identify the ghost by its FragmentFollow component (on the root), so no tag
    // string has to be kept in sync. The collider may sit on a child.
    static bool IsGhost(Collider2D other) => other.GetComponentInParent<FragmentFollow>() != null;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsGhost(other)) return;

        ghostInRange = true;
        RefreshPrompt();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsGhost(other)) return;

        ghostInRange = false;
        RefreshPrompt();
    }

    // The ghost sitting inside the trigger while the statue is disabled or the
    // scene unloads would otherwise leave ghostInRange stuck true.
    void OnDisable()
    {
        ghostInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // E works whenever the ghost is in range — piloted or parked, no parked-only
    // restriction. Only the ghost's presence in the trigger gates it.
    void RefreshPrompt()
    {
        if (ghostInRange)
            InteractButton.Instance?.SetInteraction(this, interactLabel, OnInteractPressed);
        else
            InteractButton.Instance?.ClearInteraction(this);
    }

    // InteractButton blanks the prompt the instant E is pressed (so it can't be
    // spammed mid-activation); the statue's own use is instantaneous, so re-arm right
    // away if the ghost is still standing here.
    void OnInteractPressed()
    {
        Activate();
        RefreshPrompt();
    }

    void Activate()
    {
        if (groups.Count == 0 && cycles.Count == 0) return;

        // Check EVERYTHING before moving anything: a partial activation would leave
        // the board in a state the player never asked for and can't undo. The flip
        // side is that ONE bad group/cycle refuses the whole statue — so name it, or
        // a misconfigured second one looks like "the statue just stopped working".
        //
        // CLEARED sub-puzzles are skipped everywhere below: once a group/cycle's rug is
        // covered by a crate it drops out of the sweep, so it neither moves nor blocks
        // the rest. That's what stops a crate parked on a rug from freezing the statue.
        int active = 0;
        foreach (var entry in groups)
        {
            if (entry == null || entry.group == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Groups list — fill or remove it.", this);
                Refuse(null);
                return;
            }
            if (entry.group.IsCleared) continue;
            if (!entry.group.CanRotate(DirectionFor(entry), out string reason, out GameObject blocker))
            {
                Debug.Log($"{name}: won't turn because group '{entry.group.name}' can't rotate — {reason}.", entry.group);
                Refuse(blocker);
                return;
            }
            active++;
        }
        foreach (var c in cycles)
        {
            if (c == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Cycles list — fill or remove it.", this);
                Refuse(null);
                return;
            }
            if (c.IsCleared) continue;
            if (!c.CanStep(out string reason, out GameObject blocker))
            {
                Debug.Log($"{name}: won't turn because cycle '{c.name}' can't step — {reason}.", c);
                Refuse(blocker);
                return;
            }
            active++;
        }

        // Everything the statue drives is cleared — nothing left to move, so it just
        // sits (no refuse buzz: this is a solved state, not a blocked one).
        if (active == 0) return;

        // A estátua NÃO entra no undo. O Z desfaz só o que o player fez com o próprio
        // corpo — empurrar caixa. Girar a estátua é uma jogada do tabuleiro, e voltar
        // atrás nela é usar a estátua de novo, não apertar Z.
        foreach (var entry in groups) if (!entry.group.IsCleared) entry.group.TryRotate(DirectionFor(entry));
        foreach (var c in cycles) if (!c.IsCleared) c.TryStep();
        onActivated?.Invoke();
    }

    // One refusal, three audiences: the world (sound/shake on every refusal), the player's
    // eye (the thing in the way lights up), and Haze (who explains the rule once and then
    // shuts up about it).
    //
    // The blocker may be null — an empty Inspector slot or a mid-spin refusal has no
    // single culprit to point at — and only the flash cares, so the rest still fires.
    void Refuse(GameObject blocker)
    {
        onRefused?.Invoke();

        if (flashBlocker && blocker != null)
            BlockedFlash.Play(blocker);

        if (!refusedBefore)
        {
            refusedBefore = true;
            onRefusedFirstTime?.Invoke();
        }
    }

    // Groups turn the default direction unless a group opts into the other way with
    // its Counter Clockwise toggle. (This preserves the exact behaviour from when the
    // statue's global direction was left unchecked — no global base anymore.)
    bool DirectionFor(RotatingGroup entry) => entry.counterClockwise;
}
