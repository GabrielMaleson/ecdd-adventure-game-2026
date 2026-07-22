using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Optional goal marker for a Sokoban puzzle (the rug). Put one on each target
// cell. Targets that share a Puzzle Id form ONE puzzle: when every target with
// that id is covered by a crate, that puzzle's onAllTargetsCovered fires once —
// INDEPENDENTLY of any other puzzle in the same scene. This is what lets one
// scene (e.g. the main scene) hold several separate crate puzzles.
//
// Leave Puzzle Id blank to drop a target into the default (unnamed) puzzle; a
// scene with a single puzzle can ignore the field entirely and behaves exactly
// as before.
//
// Entirely optional — if a puzzle has no CrateTarget in the scene, crates still
// push normally and nothing win-related happens.
//
// It does NOT occupy its cell (it isn't a GridOccupant): a crate has to be able
// to move onto it.
public class CrateTarget : GridObject
{
    [Tooltip("Targets sharing this id are ONE puzzle and fire together, independently of other puzzles in the scene. Leave blank for the default puzzle.")]
    [SerializeField] string puzzleId = "";

    [Tooltip("Fires once when every target sharing this target's Puzzle Id is covered by a crate.")]
    public UnityEvent onAllTargetsCovered;

    public string PuzzleId => puzzleId ?? "";

    // Lets other systems ask whether a puzzle is currently solved. Used by the statue
    // to DROP a group/cycle out of its sweep once that sub-puzzle's rug is covered —
    // a "cleared" sub-puzzle stops moving and stops blocking the rest. Re-arms
    // automatically if the crate is later pulled off (solvedByGroup flips back).
    public static bool IsSolved(string id)
        => solvedByGroup.TryGetValue(id ?? "", out bool s) && s;

    protected override Color DebugColor => Color.green;

    static readonly List<CrateTarget> all = new List<CrateTarget>();

    // One solved flag PER puzzle id, so each puzzle latches (and can re-arm if a
    // crate is later pulled off) on its own.
    static readonly Dictionary<string, bool> solvedByGroup = new Dictionary<string, bool>();

    // Clears stale state at the start of every Play (survives Fast Play Mode).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        solvedByGroup.Clear();
    }

    void OnEnable()
    {
        if (!HasGrid()) return;
        Cell = CurrentCell();
        all.Add(this);
    }

    void OnDisable() => all.Remove(this);

    // Runs after every target and crate has registered, so a puzzle authored
    // already-solved reports it instead of waiting for a push that never comes.
    void Start() => EvaluateWin();

    // Called by PushableCrate after each push (and by the statue's undo). Re-checks
    // EVERY puzzle group; each one fires, latches, or re-arms on its own.
    public static void EvaluateWin()
    {
        if (all.Count == 0) return;

        // Distinct puzzle ids present. Small allocation, but puzzle scale is tiny
        // and building the set here keeps the pass reentrancy-safe.
        var groups = new HashSet<string>();
        foreach (var target in all) groups.Add(target.PuzzleId);

        foreach (var id in groups) EvaluateGroup(id);
    }

    static void EvaluateGroup(string id)
    {
        bool allCovered = true;
        foreach (var target in all)
        {
            if (target.PuzzleId != id) continue;

            // An obstacle sitting on a target must not count as a solve — only a
            // crate does.
            bool covered = GridOccupant.TryGetOccupant(target.Cell, out var occupant)
                           && occupant is PushableCrate;
            if (!covered) { allCovered = false; break; }
        }

        if (!allCovered)
        {
            solvedByGroup[id] = false;       // re-arm: an uncovered puzzle can fire again
            return;
        }

        solvedByGroup.TryGetValue(id, out bool wasSolved);
        if (wasSolved) return;               // don't re-fire while it stays solved
        solvedByGroup[id] = true;

        foreach (var target in all)
            if (target.PuzzleId == id)
                target.onAllTargetsCovered?.Invoke();
    }
}
