using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Optional goal marker for a Sokoban puzzle. Put one on each target cell.
// After every crate settles, PushableCrate calls EvaluateWin: when every target
// cell is covered by a crate, onAllTargetsCovered fires once.
//
// Entirely optional — if a puzzle has no CrateTarget in the scene, crates still
// push normally and nothing win-related happens.
public class CrateTarget : MonoBehaviour
{
    [Tooltip("Must match the crates' grid so cells line up.")]
    [SerializeField] float tileSize = 1f;

    [Tooltip("Must match the crates' Grid Origin so cells line up.")]
    [SerializeField] Vector2 gridOrigin = Vector2.zero;

    [Tooltip("Fires once when all targets in the scene are covered by crates.")]
    public UnityEvent onAllTargetsCovered;

    static readonly List<CrateTarget> all = new List<CrateTarget>();
    static bool solved;

    // Clears stale state at the start of every Play (survives Fast Play Mode).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        solved = false;
    }

    Vector2Int cell;

    void OnEnable()
    {
        cell = new Vector2Int(
            Mathf.RoundToInt((transform.position.x - gridOrigin.x) / tileSize),
            Mathf.RoundToInt((transform.position.y - gridOrigin.y) / tileSize));
        all.Add(this);
    }

    void OnDisable() => all.Remove(this);

    // Called by PushableCrate after each push. occupiedCells is the set of cells
    // that currently hold a crate.
    public static void EvaluateWin(ICollection<Vector2Int> occupiedCells)
    {
        if (all.Count == 0) return;

        foreach (var target in all)
            if (!occupiedCells.Contains(target.cell))
            {
                solved = false;
                return;
            }

        if (solved) return;             // don't re-fire while it stays solved
        solved = true;
        foreach (var target in all)
            target.onAllTargetsCovered?.Invoke();
    }
}
