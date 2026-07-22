using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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
    [Tooltip("Rotating bush groups this statue turns. All of them turn together, or none do.")]
    [SerializeField] List<ObstacleGroup> groups = new List<ObstacleGroup>();

    [Tooltip("Tile cycles this statue advances one step. Turn together with the groups, all-or-nothing.")]
    [SerializeField] List<TileCycle> cycles = new List<TileCycle>();

    [Tooltip("Clockwise, per the design. Uncheck for a statue that turns the other way. (Tile cycles use their own Reverse toggle.)")]
    [SerializeField] bool clockwise = true;

    [Tooltip("Prompt shown on the interact label while the parked ghost can use this.")]
    [SerializeField] string interactLabel = "Statue (E)";

    [Tooltip("Fires when the statue is used successfully — rumble, dust, stone-grinding SFX.")]
    public UnityEvent onActivated;

    [Tooltip("Fires when the statue is used but nothing can turn (something's in the way).")]
    public UnityEvent onRefused;

    bool ghostInRange;
    bool promptShown;

    // Identify the ghost by its FragmentFollow component (on the root), so no tag
    // string has to be kept in sync. The collider may sit on a child.
    static bool IsGhost(Collider2D other) => other.GetComponentInParent<FragmentFollow>() != null;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsGhost(other)) ghostInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsGhost(other)) ghostInRange = false;
    }

    // The ghost sitting inside the trigger while the statue is disabled or the
    // scene unloads would otherwise leave ghostInRange stuck true.
    void OnDisable()
    {
        ghostInRange = false;
        if (promptShown) { InteractButton.Instance?.SetLabel(""); promptShown = false; }
    }

    void Update()
    {
        // E works whenever the ghost is in range — piloted or parked, no parked-only
        // restriction. Only the ghost's presence in the trigger gates it.
        bool canUse = ghostInRange;

        if (canUse != promptShown)
        {
            InteractButton.Instance?.SetLabel(canUse ? interactLabel : "");
            promptShown = canUse;
        }

        if (!canUse) return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Activate();
    }

    void Activate()
    {
        if (groups.Count == 0 && cycles.Count == 0) return;

        // Check EVERYTHING before moving anything: a partial activation would leave
        // the board in a state the player never asked for and can't undo. The flip
        // side is that ONE bad group/cycle refuses the whole statue — so name it, or
        // a misconfigured second one looks like "the statue just stopped working".
        foreach (var g in groups)
        {
            if (g == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Groups list — fill or remove it.", this);
                onRefused?.Invoke();
                return;
            }
            if (!g.CanRotate(clockwise, out string reason))
            {
                Debug.Log($"{name}: won't turn because group '{g.name}' can't rotate — {reason}.", g);
                onRefused?.Invoke();
                return;
            }
        }
        foreach (var c in cycles)
        {
            if (c == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Cycles list — fill or remove it.", this);
                onRefused?.Invoke();
                return;
            }
            if (!c.CanStep(out string reason))
            {
                Debug.Log($"{name}: won't turn because cycle '{c.name}' can't step — {reason}.", c);
                onRefused?.Invoke();
                return;
            }
        }

        // Record ONE undo step for the whole activation, captured before anything
        // moves. A single Z then reverts every group AND cycle this statue moved,
        // atomically — matching the fact that they moved together.
        var restores = new List<System.Action>();
        foreach (var g in groups) restores.Add(g.CaptureRestore());
        foreach (var c in cycles) restores.Add(c.CaptureRestore());
        PuzzleUndo.Record(() => { foreach (var r in restores) r(); });

        foreach (var g in groups) g.TryRotate(clockwise);
        foreach (var c in cycles) c.TryStep();
        onActivated?.Invoke();
    }
}
