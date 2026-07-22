using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// A set of obstacles that rotates as one rigid shape around THIS object's cell.
//
// Authoring: make the obstacles children of this GameObject. This object's own
// cell is the pivot — put it wherever you want the shape to turn about, snap it,
// and the arrangement swings around it in 90-degree steps.
//
// Rotation is exact because it happens in CELL space: an offset of (dx,dy) from
// the pivot becomes (dy,-dx) clockwise. Integers in, integers out — the shape can
// never drift off the lattice no matter how many times it turns. The visible arc
// is just cosmetic interpolation on top of an already-decided result.
public class ObstacleGroup : GridObject
{
    [Tooltip("How long one 90-degree turn takes.")]
    [SerializeField] float rotateDuration = 0.35f;

    [Tooltip("Fires when a turn is refused because something is standing where an obstacle would land. Good place for a 'clunk' sound.")]
    public UnityEvent onBlocked;

    public bool IsRotating { get; private set; }

    protected override Color DebugColor => Color.cyan;

    // This object is a pure pivot: it has no artwork, so its center is its
    // transform. Without this it would adopt a child obstacle's sprite and rotate
    // around that obstacle instead of the group.
    protected override SpriteRenderer FindVisual() => null;

    // Snapshot for undo. Captures where every member sits RIGHT NOW and returns a
    // closure that puts them all back there instantly, cancelling any spin. Call it
    // before rotating; the statue stores it so one Z reverts the whole turn.
    public System.Action CaptureRestore()
    {
        var members = GetComponentsInChildren<GridObstacle>();
        var cells   = new Vector2Int[members.Length];
        var pos     = new Vector3[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            cells[i] = members[i].Cell;
            pos[i]   = members[i].transform.position;
        }
        return () => RestoreMembers(members, cells, pos);
    }

    void RestoreMembers(GridObstacle[] members, Vector2Int[] cells, Vector3[] pos)
    {
        StopAllCoroutines();
        IsRotating = false;

        // Vacate all before reclaiming any — same reason as the rotation itself:
        // members swapping cells must not read each other's stale occupancy.
        foreach (var m in members) if (m != null) m.ClearCell();
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] == null) continue;
            members[i].transform.position = pos[i];
            members[i].ReassignCell(cells[i]);
        }

        CrateTarget.EvaluateWin();
    }

    // Asks whether a turn would succeed WITHOUT performing it. Exists so a switch
    // driving several groups can verify all of them first and then commit — one
    // blocked group must not leave the others turned, or the puzzle desyncs into a
    // state the player can't reason about.
    public bool CanRotate(bool clockwise) => Plan(clockwise, out _, out _, out _);

    // Same, but hands back WHY it can't turn — used by the statue so a refusal names
    // the offending group and reason instead of just silently doing nothing.
    public bool CanRotate(bool clockwise, out string reason) => Plan(clockwise, out _, out _, out reason);

    // Turns the shape 90 degrees. Returns false (and changes nothing) if refused —
    // all-or-nothing, never partial.
    public bool TryRotate(bool clockwise)
    {
        if (IsRotating) return false;    // silent: spam-clicking isn't "blocked"

        if (!Plan(clockwise, out var members, out var targets, out _))
        {
            onBlocked?.Invoke();
            return false;
        }

        // Vacate everything before claiming anything, so members swapping cells
        // don't read each other's stale occupancy.
        foreach (var m in members) m.ClearCell();
        for (int i = 0; i < members.Length; i++) members[i].ReassignCell(targets[i]);

        StartCoroutine(Swing(members, targets, clockwise));
        return true;
    }

    // Works out where every member would land and whether that's allowed. Pure —
    // touches nothing. `reason` is null on success, else a human sentence.
    bool Plan(bool clockwise, out GridObstacle[] members, out Vector2Int[] targets, out string reason)
    {
        members = null;
        targets = null;
        reason  = null;

        if (IsRotating) { reason = "it's still mid-spin"; return false; }
        if (!HasGrid()) { reason = "no usable PuzzleGrid in the scene"; return false; }

        members = GetComponentsInChildren<GridObstacle>();
        if (members.Length == 0)
        {
            reason = "it has no GridObstacle children — parent the bushes that should turn under this group";
            return false;
        }

        Vector2Int pivot = CurrentCell();

        // Read each member's cell from where its ARTWORK actually is right now, not
        // from its cached claim. A stale or failed claim (Cell left at a default, or
        // a ghost value) would otherwise send a target flying to a random far cell
        // and collide with whatever legitimately sits there. Live positions can't be
        // stale, so the turn is computed from what you actually see.
        var memberCells = new Vector2Int[members.Length];
        for (int i = 0; i < members.Length; i++)
            memberCells[i] = grid.WorldToCell(members[i].VisualCenter);

        // Cells the group currently holds. A target landing here is fine — that's
        // a member vacating for another member, not a collision.
        var ownCells = new HashSet<Vector2Int>();
        foreach (var c in memberCells) ownCells.Add(c);

        targets = new Vector2Int[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            Vector2Int offset = memberCells[i] - pivot;
            Vector2Int turned = clockwise
                ? new Vector2Int(offset.y, -offset.x)
                : new Vector2Int(-offset.y, offset.x);
            targets[i] = pivot + turned;
        }

        return PathIsClear(targets, ownCells, out reason);
    }

    // A turn is refused if any destination holds something that isn't part of this
    // group (a crate, a wall, another group), or if the player is standing there.
    // Refusing keeps the puzzle honest: nothing gets crushed, shoved, or trapped
    // inside a rock, and the player just steps aside and tries again.
    bool PathIsClear(Vector2Int[] targets, HashSet<Vector2Int> ownCells, out string reason)
    {
        reason = null;
        Vector2Int playerCell = PlayerCell();

        foreach (var t in targets)
        {
            if (t == playerCell)
            {
                reason = $"a bush would land on the player (cell {t}) — step aside";
                return false;
            }

            if (ownCells.Contains(t)) continue;   // a member is leaving this cell
            if (GridOccupant.IsFree(t)) continue;

            GridOccupant.TryGetOccupant(t, out var occ);

            // Only an occupant that's ACTUALLY on the cell blocks. A stale claim —
            // registered here but whose artwork really sits somewhere else — is a
            // ghost and must not veto the turn, or a mis-registered object across the
            // map blocks a rotation it has nothing to do with.
            if (occ == null || grid.WorldToCell(occ.VisualCenter) != t)
                continue;

            reason = $"a bush would land on cell {t}, already held by '{occ.name}'";
            return false;
        }
        return true;
    }

    // Cell.zero is a legitimate cell, so "no player" is reported out-of-band via
    // an unreachable sentinel rather than a default value.
    static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

    Vector2Int PlayerCell()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return NoCell;

        // Measured from the collider, not the transform: the player's pivot is at
        // the feet, which can sit in a different cell than the body occupies.
        foreach (var c in player.GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) return grid.WorldToCell(c.bounds.center);

        return grid.WorldToCell(player.transform.position);
    }

    IEnumerator Swing(GridObstacle[] members, Vector2Int[] targets, bool clockwise)
    {
        IsRotating = true;

        Vector3 pivotWorld = grid.CellCenter(CurrentCell());
        float   sweep      = clockwise ? -90f : 90f;   // Z-rotation is CCW-positive in Unity

        // Cached because they're rigid: an object translating doesn't change where
        // its artwork sits relative to its pivot.
        var startCenters = new Vector3[members.Length];
        var rootOffsets  = new Vector3[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            startCenters[i] = members[i].VisualCenter;
            rootOffsets[i]  = members[i].VisualOffset;
        }

        float elapsed = 0f;
        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / rotateDuration);
            Quaternion turn = Quaternion.Euler(0f, 0f, sweep * t);

            for (int i = 0; i < members.Length; i++)
            {
                Vector3 center = pivotWorld + turn * (startCenters[i] - pivotWorld);
                Vector3 root   = center - rootOffsets[i];
                root.z = members[i].transform.position.z;
                members[i].transform.position = root;
            }
            yield return null;
        }

        // Land on the cells decided up front rather than wherever the arc ended.
        // Float error never accumulates across turns, and a non-square grid (where
        // the arc is only an approximation) still finishes exactly on-cell.
        for (int i = 0; i < members.Length; i++)
        {
            Vector3 root = grid.CellCenter(targets[i]) - rootOffsets[i];
            root.z = members[i].transform.position.z;
            members[i].transform.position = root;
        }

        IsRotating = false;
    }
}
