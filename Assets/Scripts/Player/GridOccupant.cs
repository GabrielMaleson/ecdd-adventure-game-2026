using System.Collections.Generic;
using UnityEngine;

// A GridObject that physically OWNS its cell — crates and obstacles.
//
// Blocking is decided here, logically, by a shared occupancy map: one cell, one
// occupant. This replaces the old Physics2D.OverlapBox + LayerMask probe, which
// asked "is something roughly near where I'd land?" and therefore depended on how
// straight the obstacle happened to be placed. A dictionary lookup can't be
// almost-right.
//
// Consequence worth knowing: an obstacle only blocks if it HAS a GridObstacle
// component. A wall prefab without one is invisible to the puzzle and crates will
// slide through it.
public abstract class GridOccupant : GridObject
{
    static readonly Dictionary<Vector2Int, GridOccupant> occupied =
        new Dictionary<Vector2Int, GridOccupant>();

    // Wipes stale cells at the start of every Play. Needed because the dictionary
    // is static and survives when "Enter Play Mode" has domain reload disabled
    // (Fast Play Mode) — without this, last run's cells would block everything.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => occupied.Clear();

    public static bool IsFree(Vector2Int cell) => !occupied.ContainsKey(cell);

    public static bool TryGetOccupant(Vector2Int cell, out GridOccupant occupant) =>
        occupied.TryGetValue(cell, out occupant);

    protected virtual void OnEnable()
    {
        if (!HasGrid()) return;
        Claim(CurrentCell());
    }

    protected virtual void OnDisable() => Release();

    // Registry-only cell hand-off, for systems that relocate occupants themselves
    // (ObstacleGroup rotation). The caller moves the transform; these only update
    // who-holds-what. Always ClearCell() every mover BEFORE reassigning any of
    // them, or members will collide with each other's old cells.
    public void ClearCell()                    => Release();
    public void ReassignCell(Vector2Int cell)  => Claim(cell);

    protected void Claim(Vector2Int cell)
    {
        // Two different objects authored onto one cell — usually two bushes/crates
        // overlapping. Worth a heads-up since occupancy then depends on load order.
        if (occupied.TryGetValue(cell, out var other) && other != this)
            Debug.LogWarning($"{name}: cell {cell} is already held by '{other.name}' — " +
                             $"two objects share one cell, snap them and check the layout.", this);

        Release();
        occupied[cell] = this;
        Cell = cell;
    }

    protected void Release()
    {
        if (occupied.TryGetValue(Cell, out var o) && o == this)
            occupied.Remove(Cell);
    }
}
