using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// The statue: an interactable that turns one or more ObstacleGroups by 90 degrees
// every time it's used, reshaping the board mid-puzzle.
//
// Needs a TRIGGER Collider2D marking the range at which the player can use it.
// Activated with the E key while the player stands inside that trigger — E only
// does anything in range, so it never fires from across the map.
[RequireComponent(typeof(Collider2D))]
public class StatueSwitch : MonoBehaviour
{
    [Tooltip("Groups this statue turns. All of them turn together, or none do.")]
    [SerializeField] List<ObstacleGroup> groups = new List<ObstacleGroup>();

    [Tooltip("Clockwise, per the design. Uncheck for a statue that turns the other way.")]
    [SerializeField] bool clockwise = true;

    [Tooltip("Prompt shown on the interact label while in range.")]
    [SerializeField] string interactLabel = "Statue (E)";

    [Tooltip("Fires when the statue is used successfully — rumble, dust, stone-grinding SFX.")]
    public UnityEvent onActivated;

    [Tooltip("Fires when the statue is used but nothing can turn (something's in the way).")]
    public UnityEvent onRefused;

    bool playerInRange;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        InteractButton.Instance?.SetLabel(interactLabel);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    // The player standing inside the trigger while the statue is disabled or the
    // scene unloads would otherwise leave playerInRange stuck true.
    void OnDisable() => playerInRange = false;

    void Update()
    {
        if (!playerInRange) return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Activate();
    }

    void Activate()
    {
        if (groups.Count == 0) return;

        // Check every group before turning any: partial turns would leave the
        // board in a state the player never asked for and can't undo. The flip side
        // is that ONE bad group refuses the whole statue — so name it, or a
        // misconfigured second group looks like "the statue just stopped working".
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

        // Record ONE undo step for the whole activation, captured before anything
        // turns. A single Z then reverts every group this statue moved, atomically
        // — matching the fact that they turned together.
        var restores = new List<System.Action>();
        foreach (var g in groups) restores.Add(g.CaptureRestore());
        PuzzleUndo.Record(() => { foreach (var r in restores) r(); });

        foreach (var g in groups) g.TryRotate(clockwise);
        onActivated?.Invoke();
    }
}
