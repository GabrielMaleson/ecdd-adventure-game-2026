using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Optional goal marker for a Sokoban puzzle (the rug). Put one on each target
// cell. When every target is covered by a crate, onAllTargetsCovered fires once.
//
// Entirely optional — if a puzzle has no CrateTarget in the scene, crates still
// push normally and nothing win-related happens.
//
// It does NOT occupy its cell (it isn't a GridOccupant): a crate has to be able
// to move onto it.
public class CrateTarget : GridObject
{
    [Tooltip("Fires once when all targets in the scene are covered by crates.")]
    public UnityEvent onAllTargetsCovered;

    protected override Color DebugColor => Color.green;

    static readonly List<CrateTarget> all = new List<CrateTarget>();
    static bool solved;

    // Clears stale state at the start of every Play (survives Fast Play Mode).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        solved = false;
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

    // Called by PushableCrate after each push.
    public static void EvaluateWin()
    {
        if (all.Count == 0) return;

        foreach (var target in all)
        {
            // An obstacle sitting on a target must not count as a solve — only a
            // crate does.
            bool covered = GridOccupant.TryGetOccupant(target.Cell, out var occupant)
                           && occupant is PushableCrate;
            if (!covered)
            {
                solved = false;
                return;
            }
        }

        if (solved) return;             // don't re-fire while it stays solved
        solved = true;
        foreach (var target in all)
            target.onAllTargetsCovered?.Invoke();
    }
}
