using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// A ring of cells that bushes CYCLE through, one step per activation. With fewer
// bushes than cells, one (or more) cell is always empty — a GAP that circulates,
// opening a path where a bush just vacated. Different from ObstacleGroup: that one
// rotates a rigid shape 90 degrees; this one shifts occupancy around a fixed ring.
//
// Authoring: make THIS the parent object. Its direct children define the ring, in
// HIERARCHY ORDER = the order the cycle walks:
//   - a child WITH a GridObstacle (a bush)  = a cell that starts occupied.
//   - a child WITHOUT one (an empty marker)  = a cell that starts as a gap.
// Snap everything. Each activation shifts every bush to the NEXT ring cell (or the
// previous one, if Reverse is ticked); the gaps move with them.
//
// Driven by the statue exactly like ObstacleGroup — add it to the StatueSwitch's
// Cycles list.
public class TileCycle : GridObject
{
    [Tooltip("Reverse the cycle direction. Off = forward (hierarchy order). Tick it if the gap circulates the wrong way.")]
    [SerializeField] bool reverse = false;

    [Tooltip("Optional. When the puzzle with THIS Puzzle Id is solved (its rug covered by a crate), the statue STOPS stepping this cycle — a 'cleared' sub-puzzle drops out of the sweep so it can't block the rest. Re-arms if the crate is pulled off. Blank = always steps.")]
    [SerializeField] string clearedWhenPuzzleSolved = "";

    // True while this cycle's gate-puzzle is solved: the statue skips it entirely.
    public bool IsCleared =>
        !string.IsNullOrEmpty(clearedWhenPuzzleSolved) && CrateTarget.IsSolved(clearedWhenPuzzleSolved);

    [Tooltip("How long one step-slide takes.")]
    [SerializeField] float stepDuration = 0.2f;

    [Tooltip("Fires when a step is refused because a target cell is blocked by something outside the cycle (a crate, the player).")]
    public UnityEvent onBlocked;

    public bool IsStepping { get; private set; }

    protected override Color DebugColor => Color.magenta;

    // Pure anchor: no sprite of its own, so its own cell never matters.
    protected override SpriteRenderer FindVisual() => null;

    Vector2Int[]   ringCells;      // the ring, in child order
    GridObstacle[] bushes;         // the movers
    int[]          bushIndex;      // each bush's current slot in ringCells

    void Start() => BuildRing();

    // The ring and starting occupancy come straight from the children — no cell is
    // ever typed by hand. Bushes claim their own cells (GridOccupant.OnEnable); we
    // only record where each one sits in the ring so we can advance it.
    void BuildRing()
    {
        if (!HasGrid()) return;

        var cells   = new List<Vector2Int>();
        var movers  = new List<GridObstacle>();
        var moverIx = new List<int>();

        int i = 0;
        foreach (Transform child in transform)
        {
            var obst = child.GetComponent<GridObstacle>();
            // A bush's cell comes from its visible centre (how it claims occupancy);
            // an empty marker's from its transform.
            Vector2Int cell = grid.WorldToCell(obst != null ? obst.VisualCenter : child.position);

            cells.Add(cell);
            if (obst != null) { movers.Add(obst); moverIx.Add(i); }
            i++;
        }

        ringCells = cells.ToArray();
        bushes    = movers.ToArray();
        bushIndex = moverIx.ToArray();
    }

    int NextIndex(int idx)
    {
        int n = ringCells.Length;
        if (n == 0) return idx;
        int step = reverse ? -1 : 1;
        return ((idx + step) % n + n) % n;
    }

    // Asks whether a step would succeed WITHOUT doing it — so the statue can verify
    // every driven thing first and only then commit them all together.
    public bool CanStep(out string reason)
    {
        reason = null;
        if (IsStepping)                                 { reason = "it's still mid-step"; return false; }
        if (!HasGrid())                                 { reason = "no usable PuzzleGrid in the scene"; return false; }
        if (bushes == null || bushes.Length == 0)       { reason = "it has no GridObstacle children — parent the bushes that cycle under this object"; return false; }

        // Cells the cycle's own bushes currently hold: a bush landing where another
        // is leaving is fine, not a collision.
        var ownCells = new HashSet<Vector2Int>();
        foreach (var idx in bushIndex) ownCells.Add(ringCells[idx]);

        Vector2Int playerCell = PlayerCell();

        for (int b = 0; b < bushes.Length; b++)
        {
            Vector2Int target = ringCells[NextIndex(bushIndex[b])];

            if (target == playerCell) { reason = $"a bush would land on the player (cell {target}) — step aside"; return false; }
            if (ownCells.Contains(target)) continue;
            if (GridOccupant.IsFree(target)) continue;

            GridOccupant.TryGetOccupant(target, out var occ);
            // A stale (ghost) claim — occupant registered here but really elsewhere —
            // must not veto the step.
            if (occ == null || grid.WorldToCell(occ.VisualCenter) != target) continue;

            reason = $"a bush would land on cell {target}, already held by '{occ.name}'";
            return false;
        }
        return true;
    }

    // Undo snapshot (for PuzzleUndo / Z), same idea as ObstacleGroup.CaptureRestore.
    public System.Action CaptureRestore()
    {
        var b     = (GridObstacle[])bushes.Clone();
        var idx   = (int[])bushIndex.Clone();
        var cells = new Vector2Int[b.Length];
        var pos   = new Vector3[b.Length];
        for (int i = 0; i < b.Length; i++) { cells[i] = ringCells[idx[i]]; pos[i] = b[i].transform.position; }
        return () => RestoreBushes(b, idx, cells, pos);
    }

    void RestoreBushes(GridObstacle[] b, int[] idx, Vector2Int[] cells, Vector3[] pos)
    {
        StopAllCoroutines();
        IsStepping = false;

        foreach (var x in b) if (x != null) x.ClearCell();
        for (int i = 0; i < b.Length; i++)
        {
            if (b[i] == null) continue;
            b[i].transform.position = pos[i];
            b[i].ReassignCell(cells[i]);
        }
        bushIndex = (int[])idx.Clone();
        CrateTarget.EvaluateWin();
    }

    // Advances the cycle one step. Returns false and changes nothing if refused.
    public bool TryStep()
    {
        if (!CanStep(out _)) { onBlocked?.Invoke(); return false; }

        int[]        newIdx  = new int[bushes.Length];
        Vector2Int[] targets = new Vector2Int[bushes.Length];
        for (int b = 0; b < bushes.Length; b++)
        {
            newIdx[b]  = NextIndex(bushIndex[b]);
            targets[b] = ringCells[newIdx[b]];
        }

        // Vacate all before claiming any, so bushes swapping cells don't read each
        // other's stale occupancy.
        foreach (var bush in bushes) bush.ClearCell();
        for (int b = 0; b < bushes.Length; b++) bushes[b].ReassignCell(targets[b]);
        bushIndex = newIdx;

        StartCoroutine(Slide(targets));
        return true;
    }

    IEnumerator Slide(Vector2Int[] targets)
    {
        IsStepping = true;

        var starts = new Vector3[bushes.Length];
        var ends   = new Vector3[bushes.Length];
        for (int b = 0; b < bushes.Length; b++)
        {
            starts[b]   = bushes[b].transform.position;
            Vector3 end = grid.CellCenter(targets[b]) - bushes[b].VisualOffset;
            end.z       = bushes[b].transform.position.z;
            ends[b]     = end;
        }

        float t = 0f;
        while (t < stepDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / stepDuration);
            for (int b = 0; b < bushes.Length; b++)
                bushes[b].transform.position = Vector3.Lerp(starts[b], ends[b], k);
            yield return null;
        }
        for (int b = 0; b < bushes.Length; b++)
            bushes[b].transform.position = ends[b];

        IsStepping = false;
        CrateTarget.EvaluateWin();
    }

    static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

    Vector2Int PlayerCell()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return NoCell;
        foreach (var c in player.GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) return grid.WorldToCell(c.bounds.center);
        return grid.WorldToCell(player.transform.position);
    }
}
