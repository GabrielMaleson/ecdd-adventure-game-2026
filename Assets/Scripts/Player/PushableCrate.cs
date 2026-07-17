using System.Collections;
using UnityEngine;

// Sokoban-style pushable crate driven by GRID LOGIC, not physics.
//
// Why not physics: pushing with contact/forces makes crates drift off-grid,
// jitter against walls, and stop misaligned between tiles. Instead each crate
// owns exactly one cell (see GridOccupant) and pushes are all-or-nothing.
//
// The Collider2D + Kinematic Rigidbody2D are kept ONLY so the crate keeps
// physically blocking the player like a wall. They never decide pushes.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PushableCrate : GridOccupant
{
    [Tooltip("Max gap between the player's and crate's colliders that still counts as 'touching'. Small (~0.1). Push fires on real contact — not center distance — so sprite/tile size doesn't matter.")]
    [SerializeField] float contactPadding = 0.1f;

    [Tooltip("Fallback proximity range, ONLY used if the player has no Collider2D. Center-to-center.")]
    [SerializeField] float pushRange = 0.9f;

    [Tooltip("How long the slide from one cell to the next takes.")]
    [SerializeField] float moveDuration = 0.12f;

    [Tooltip("Print to the console why a push was refused, naming whatever holds the target cell. Debug aid — turn off when done.")]
    [SerializeField] bool logRefusedPushes = false;

    protected override Color DebugColor => Color.yellow;

    Vector2Int? lastRefusedCell;

    Rigidbody2D      rb;
    Collider2D       col;
    Collider2D       playerCol;
    PlayerController player;
    bool             isMoving;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Start()
    {
        player = FindObjectOfType<PlayerController>();
        if (player != null)
            foreach (var c in player.GetComponentsInChildren<Collider2D>())
                if (!c.isTrigger) { playerCol = c; break; }   // the player's solid body
    }

    void Update()
    {
        if (isMoving || player == null) return;

        Vector2 moveDir = player.MoveDirection;
        if (moveDir == Vector2.zero) return;

        Vector2Int dir = CardinalCell(moveDir);

        // Two gates, both size/offset independent:
        //   1) the player is actually TOUCHING the crate, and
        //   2) the player is on the correct SIDE for this push direction and lined
        //      up on the perpendicular axis.
        // When a push silently doesn't happen, it died at one of these — so with
        // logging on, name which, with the numbers, so an intermittent stall stops
        // being invisible.
        if (!PlayerTouching())
        {
            // Only bother reporting a "not touching" miss when the player is actually
            // NEAR this crate — otherwise every crate on the map logs while you walk.
            float gap = playerCol != null ? col.Distance(playerCol).distance : float.MaxValue;
            if (gap < 0.75f)
                LogGate(dir, $"not touching (gap {gap:0.00} > padding {contactPadding:0.00})");
            return;
        }
        if (!PlayerBehind(dir))
        {
            LogGate(dir, $"touching but not lined up/behind (player feet {grid.WorldToCell(player.transform.position)}, crate {Cell})");
            return;
        }

        gateStamp = default;   // a clean push resets the throttle
        TryPush(dir);
    }

    // On when EITHER this crate's own checkbox or the grid-wide toggle is set, so you
    // can flip logging once on the PuzzleGrid instead of per crate.
    bool Logging => logRefusedPushes || PuzzleGrid.LogPushFailures;

    // Throttled so an intermittent stall prints once with its reason instead of
    // flooding every frame you lean on the crate.
    (Vector2Int dir, string why) gateStamp;
    void LogGate(Vector2Int dir, string why)
    {
        if (!Logging) return;
        if (gateStamp.dir == dir && gateStamp.why == why) return;
        gateStamp = (dir, why);
        Debug.Log($"{name} at cell {Cell}: push {dir} didn't fire — {why}.", this);
    }

    // Real contact, measured collider-edge to collider-edge (negative when
    // overlapping). Falls back to center proximity only if the player has no
    // solid collider.
    bool PlayerTouching()
    {
        if (playerCol != null)
            return col.Distance(playerCol).distance <= contactPadding;

        Vector2 toCrate = (Vector2)transform.position - (Vector2)player.transform.position;
        return toCrate.sqrMagnitude <= pushRange * pushRange;
    }

    // Is the player positioned to push the crate in `dir`?
    //
    // Lined up = same cell ROW/COLUMN as the crate. Behind = simply on the far side
    // of the crate's cell centre. Neither reads a collider's size, offset or shape,
    // which is the whole point: the old version compared collider BOUNDS, so it
    // broke whenever the artwork or the collider changed — moving the player's
    // collider to the feet was enough to kill pushing upward.
    //
    // Note this deliberately does NOT require the cells to be adjacent. Adjacency
    // sounds right but isn't: with the crate's collider offset up 0.44 the player's
    // feet stop INSIDE the crate's own cell, and an adjacency test would never fire.
    // Proximity stays the job of PlayerTouching() — which is also what keeps pushing
    // from feeling cartoonish, since the player must genuinely reach the crate first
    // rather than nudge it from across a cell.
    // Is the player positioned to push the crate in `dir`? Uses collider BOUNDS, not
    // cell adjacency: when the player pushes from below, "touching" already puts
    // their feet in the crate's OWN cell (see the log: feet == crate cell), so any
    // adjacent-cell rule is impossible to satisfy and the crate never moves. This
    // compares the two colliders' centres/extents instead, which works from a
    // touching position. Known tradeoff: it can be fooled when the player jams deep
    // into the crate, causing the rare "shove" — accepted for now over a crate that
    // won't move at all.
    bool PlayerBehind(Vector2Int dir)
    {
        Bounds cb = col.bounds;
        Bounds pb = playerCol != null
            ? playerCol.bounds
            : new Bounds(player.transform.position, Vector3.one * 0.1f);

        if (dir.x != 0)
        {
            bool behind  = dir.x > 0 ? pb.center.x <= cb.center.x : pb.center.x >= cb.center.x;
            bool linedUp = pb.min.y < cb.max.y && pb.max.y > cb.min.y;   // overlap on Y
            return behind && linedUp;
        }
        else
        {
            bool behind  = dir.y > 0 ? pb.center.y <= cb.center.y : pb.center.y >= cb.center.y;
            bool linedUp = pb.min.x < cb.max.x && pb.max.x > cb.min.x;   // overlap on X
            return behind && linedUp;
        }
    }

    void TryPush(Vector2Int dir)
    {
        Vector2Int target = Cell + dir;

        // One lookup covers BOTH walls and other crates: anything holding that
        // cell blocks the push. No layer masks, no physics probe, no tolerance.
        if (!IsFree(target))
        {
            LogRefusal(target);
            return;
        }

        lastRefusedCell = null;

        // Snapshot BEFORE the push so undo can put both the crate and the player
        // back exactly where they stood. Restoring the player too is deliberate —
        // it will be framed in-story as the Fragment pulling him back.
        Vector2Int crateFromCell = Cell;
        Vector3    crateFromPos  = transform.position;
        PlayerController pusher   = player;               // may be null (see Start)
        Vector3    playerFromPos  = pusher != null ? pusher.transform.position : Vector3.zero;

        PuzzleUndo.Record(() =>
        {
            if (this != null) RestoreTo(crateFromCell, crateFromPos);
            if (pusher != null) pusher.TeleportTo(playerFromPos);
        });

        // Log every push that FIRES, with who was where. The shove is too fast to
        // screenshot, but it leaves a trail here: scroll back and any push whose
        // direction points at the player, or a burst of them in one keypress, is the
        // shove caught in the act.
        if (Logging)
            Debug.Log($"{name}: PUSH {dir}  cell {crateFromCell}->{target}  " +
                      $"(player feet {grid.WorldToCell(playerFromPos)}, behind-cell {crateFromCell - dir}).", this);

        StartCoroutine(StepTo(target));
    }

    // Puts the crate back on a cell instantly (undo). Cancels any slide in flight
    // and re-syncs the occupancy map, so the registry never keeps a ghost of where
    // the crate was mid-animation.
    public void RestoreTo(Vector2Int cell, Vector3 worldPos)
    {
        StopAllCoroutines();
        isMoving = false;

        ClearCell();
        transform.position = worldPos;
        rb.position        = worldPos;      // keep the physics body in step with the teleport
        ReassignCell(cell);

        CrateTarget.EvaluateWin();
    }

    // "It just won't move" is unanswerable from the outside — the target cell's
    // occupancy lives in a dictionary nobody can see. This names what holds it.
    void LogRefusal(Vector2Int target)
    {
        if (!Logging) return;
        if (lastRefusedCell == target) return;   // once per refusal, not once per frame

        lastRefusedCell = target;

        string who;
        if (grid.WorldToCell(player.transform.position) == target)
        {
            who = "the player standing there";
        }
        else if (TryGetOccupant(target, out var blocker) && blocker != null)
        {
            // GHOST DETECTION: does the thing that "holds" this cell actually SIT on
            // it? Compare the occupant's claimed cell to where its artwork really is.
            // If they disagree, the registry has a stale entry — the cell reads as
            // blocked with nothing visibly there. THAT is problem 2's real bug, and
            // this line says so outright instead of making you eyeball a gizmo.
            Vector2Int realCell = grid.WorldToCell(blocker.VisualCenter);
            who = realCell == target
                ? $"'{blocker.name}' ({blocker.GetType().Name}), really there"
                : $"'{blocker.name}' ({blocker.GetType().Name}) — GHOST: it actually sits on {realCell}, not {target}. Stale occupancy = bug.";
        }
        else
        {
            who = "nothing? (unexpected)";
        }

        Debug.Log($"{name} at cell {Cell}: push into {target} refused — held by {who}.", this);
    }

    IEnumerator StepTo(Vector2Int target)
    {
        isMoving = true;

        // Claim the target cell up front so nothing pushes into it mid-slide.
        Claim(target);

        // Aim at the cell's exact center rather than "current position + one tile".
        // A relative step would preserve any authoring error forever; this makes a
        // crate that was left slightly off-grid ease back into alignment on its
        // first push instead of drifting further.
        Vector2 start   = transform.position;
        Vector2 end     = RootPositionForCell(target);
        float   elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            rb.MovePosition(Vector2.Lerp(start, end, t));
            yield return null;
        }
        rb.MovePosition(end);
        isMoving = false;

        CrateTarget.EvaluateWin();
    }

    static Vector2Int CardinalCell(Vector2 dir) =>
        Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? new Vector2Int((int)Mathf.Sign(dir.x), 0)
            : new Vector2Int(0, (int)Mathf.Sign(dir.y));
}
