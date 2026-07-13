using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sokoban-style pushable crate driven by GRID LOGIC, not physics.
//
// Why not physics: pushing with contact/forces makes crates drift off-grid,
// jitter against walls, and stop misaligned between tiles. Instead each crate
// owns exactly one logical cell, and a shared registry tracks which cells are
// occupied. Pushing is all-or-nothing and deterministic.
//
// The Collider2D + Kinematic Rigidbody2D are kept ONLY so the crate keeps
// physically blocking the player like a wall. They never decide pushes, and the
// crate is NEVER teleported onto a lattice — it always slides exactly one
// tileSize from where it already is, so nothing jumps into a wall/the player.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PushableCrate : MonoBehaviour
{
    [Tooltip("World-unit distance of one push step. Must match your level's grid spacing.")]
    [SerializeField] float tileSize = 1f;

    [Tooltip("World point used as cell (0,0) for the logical labels. Only needs changing if crates and player use a different phase; movement never snaps to it.")]
    [SerializeField] Vector2 gridOrigin = Vector2.zero;

    [Tooltip("Max gap between the player's and crate's colliders that still counts as 'touching'. Small (~0.1). Push only fires on real contact — not by center distance — so sprite/tile size doesn't matter.")]
    [SerializeField] float contactPadding = 0.1f;

    [Tooltip("Fallback proximity range, ONLY used if the player has no Collider2D. Center-to-center.")]
    [SerializeField] float pushRange = 0.9f;

    [Tooltip("How long the slide from one cell to the next takes.")]
    [SerializeField] float moveDuration = 0.12f;

    [Tooltip("Layers that block a push (WALLS only). Other crates are handled logically.")]
    [SerializeField] LayerMask blockingMask;

    // Shared logical occupancy. Cells holding a crate live here, so crate-vs-crate
    // blocking never touches the physics engine.
    static readonly Dictionary<Vector2Int, PushableCrate> occupied = new Dictionary<Vector2Int, PushableCrate>();

    // Runs at the very start of every Play, even when "Enter Play Mode" has domain
    // reload disabled (Fast Play Mode). Without this the dictionary would keep
    // stale cells from the previous run and block everything.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => occupied.Clear();

    Rigidbody2D      rb;
    Collider2D       col;
    Collider2D       playerCol;
    PlayerController player;
    Vector2Int       cell;
    bool             isMoving;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void OnEnable()
    {
        // Label our current cell WITHOUT moving. We trust the crate's authored
        // position; we never teleport it onto a global lattice.
        cell = WorldToCell(transform.position);
        occupied[cell] = this;
    }

    void OnDisable()
    {
        if (occupied.TryGetValue(cell, out var c) && c == this)
            occupied.Remove(cell);
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
        if (!PlayerTouching()) return;
        if (!PlayerBehind(dir)) return;

        // Movement is pure grid logic (below): deterministic, on-cell.
        TryPush(dir);
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

    // Is the player positioned to push the crate in `dir`? Uses collider BOUNDS,
    // not center-to-center direction, so a large crate or a pivot offset (feet)
    // can't make one axis fail like it did for "up".
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
        Vector2Int target = cell + dir;

        // Blocked by another crate? Pure logic, no physics.
        if (occupied.ContainsKey(target)) return;

        // Blocked by a wall? One discrete query at the exact spot the crate would
        // land (current position + one step), not a lattice cell — so this works
        // regardless of how the level was phased. IMPORTANT: ignore our own
        // collider (a tall/offset crate collider can otherwise overlap this box and
        // block itself — this is what broke pushing "up") and ignore other crates
        // (crate-vs-crate is handled logically above).
        Vector2 targetWorld = (Vector2)transform.position + (Vector2)dir * tileSize;
        foreach (var hit in Physics2D.OverlapBoxAll(targetWorld, Vector2.one * tileSize * 0.8f, 0f, blockingMask))
        {
            if (hit == col) continue;                              // never block on ourselves
            if (hit.GetComponent<PushableCrate>() != null) continue; // crates are logical, not physical, blockers
            return;                                                // a real wall is in the way
        }

        StartCoroutine(StepTo(dir, target));
    }

    IEnumerator StepTo(Vector2Int dir, Vector2Int target)
    {
        isMoving = true;

        // Claim the target cell up front so nothing pushes into it mid-slide.
        occupied.Remove(cell);
        occupied[target] = this;

        Vector2 start   = transform.position;
        Vector2 end     = start + (Vector2)dir * tileSize;   // relative step, no teleport
        float   elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            rb.MovePosition(Vector2.Lerp(start, end, t));
            yield return null;
        }
        rb.MovePosition(end);
        cell     = target;
        isMoving = false;

        CrateTarget.EvaluateWin(occupied.Keys);
    }

    // ---- grid labelling helpers (never move the crate) ----

    Vector2Int WorldToCell(Vector2 world) => new Vector2Int(
        Mathf.RoundToInt((world.x - gridOrigin.x) / tileSize),
        Mathf.RoundToInt((world.y - gridOrigin.y) / tileSize));

    static Vector2Int CardinalCell(Vector2 dir) =>
        Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? new Vector2Int((int)Mathf.Sign(dir.x), 0)
            : new Vector2Int(0, (int)Mathf.Sign(dir.y));
}
