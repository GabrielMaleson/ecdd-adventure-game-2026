using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Minimum overlap (world units) on the axis perpendicular to the push before a push counts. Rejects ultra edge cases where the player only clips a crate's corner by a sliver. Bigger = more alignment required; keep small (~0.15). 0 = any touch counts (old behaviour).")]
    [SerializeField] float minLineupOverlap = 0.15f;

    [Tooltip("Print to the console why a push was refused, naming whatever holds the target cell. Debug aid — turn off when done.")]
    [SerializeField] bool logRefusedPushes = false;

    [Header("Encaixe no portal (só visual)")]
    [Tooltip("Quanto o DESENHO fica deslocado do centro da célula quando esta caixa para em cima de um portal (qualquer CrateTarget). Serve pra arte cuja base não é o meio do sprite: a estátua sobe um pouco e a base dela cai dentro da elipse do portal. A CÉLULA não muda — isto é só aparência. Y positivo = sobe. Ajusta no olho com o jogo rodando.")]
    [SerializeField] Vector2 portalLandingOffset = new Vector2(0f, 0.25f);

    // Sem duração própria e sem exceção por direção: o encaixe acontece DENTRO do
    // deslize, na mesma curva. Vindo de lado a caixa faz uma diagonal pro portal; vindo
    // de baixo ou de cima ela só anda um pouco menos (ou mais) no Y. Nenhum dos casos
    // tem um segundo movimento pra ficar estranho.

    protected override Color DebugColor => Color.yellow;

    Vector2Int? lastRefusedCell;

    Rigidbody2D      rb;
    Collider2D       col;
    Collider2D       playerCol;
    PlayerController player;
    bool             isMoving;
    Vector2Int       pushDir;      // the direction this crate registered this frame

    // Priority ONLY when more than one crate is pushable at once. Each frame, every
    // crate that passes the push gates adds itself to `candidates`; then one decision
    // (guarded to run a single time per frame) picks the winner:
    //   - one candidate  -> it moves, no comparison (a lone push is unchanged);
    //   - many           -> the one the player OVERLAPS MOST wins.
    // `committed` locks that winner to the whole key-hold, so the loser never sneaks a
    // move in a later frame after the winner slides away — the choice belongs to the
    // push gesture, and clears when the player stops pushing.
    static readonly List<PushableCrate> candidates = new List<PushableCrate>();
    static int             candFrame    = -1;
    static int             decidedFrame = -1;
    static PushableCrate   committed;

    // Crates locked out for the rest of this key-hold because they were candidates
    // TOGETHER with the committed crate (both under the player the same frame). Only
    // the "together" case locks — a separate lone crate the player reaches later is
    // never in here, so lone->lone pushing in one hold still works. Cleared on release.
    static readonly HashSet<PushableCrate> lockedLosers = new HashSet<PushableCrate>();

    // Clears the static arbitration state at the start of every Play (survives Fast
    // Play Mode), so a previous run can't leave a stale committed crate behind.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetArbitration()
    {
        candidates.Clear();
        lockedLosers.Clear();
        candFrame = decidedFrame = -1;
        committed = null;
    }

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

        SettleIfAuthoredOnPortal();
    }

    // A crate placed on a portal in the EDITOR has never been pushed, so nothing ever
    // gave it its landing offset — it would sit honestly centred (and wrong) until the
    // first push snapped it into place. Applied dry here, no pull: there was no arrival
    // to react to.
    void SettleIfAuthoredOnPortal()
    {
        if (portalLandingOffset == Vector2.zero) return;
        if (grid == null || !grid.IsReady) return;

        // Read live rather than trusting Cell: OnEnable bails out without claiming
        // anything if the grid wasn't ready yet, and Cell would still be at its default.
        Vector2Int here = grid.WorldToCell(VisualCenter);
        if (!CrateTarget.IsTargetCell(here)) return;

        CosmeticOffset      = portalLandingOffset;
        transform.position += (Vector3)portalLandingOffset;
        if (rb != null) rb.position = transform.position;
    }

    void Update()
    {
        if (player == null) return;

        // Player not pushing -> the gesture is over. Release the committed crate and
        // unlock everyone, so a fresh press re-decides from scratch.
        if (player.MoveDirection == Vector2.zero) { committed = null; lockedLosers.Clear(); return; }

        if (isMoving) return;

        Vector2Int dir = CardinalCell(player.MoveDirection);

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

        // Register as this frame's candidate instead of pushing now. The single
        // winner is chosen in LateUpdate, once every crate has had its say.
        if (candFrame != Time.frameCount)
        {
            candFrame = Time.frameCount;
            candidates.Clear();
        }
        pushDir = dir;
        candidates.Add(this);
    }

    // Runs after all Updates. Guarded so the decision happens exactly ONCE per frame
    // no matter which crate's LateUpdate fires first.
    void LateUpdate()
    {
        // candFrame != this frame means NOBODY registered this frame — the list still
        // holds last frame's stale entries. Only act on a list filled this frame (else
        // a stale candidate would be re-pushed every frame and fly off), and once.
        if (candFrame != Time.frameCount || candidates.Count == 0 || decidedFrame == Time.frameCount)
            return;
        decidedFrame = Time.frameCount;

        // Winner mid-slide: nobody moves during the slide.
        if (committed != null && committed.isMoving)
            return;

        // Committed crate still under the player: keep pushing it. AND lock any OTHER
        // crate that's a candidate this same frame — it's "together" with the committed
        // one (e.g. the committed crate slid up beside it), so it can't be pushed until
        // release. This is the two-crates-together rule.
        if (committed != null && candidates.Contains(committed))
        {
            foreach (var c in candidates)
                if (c != committed) lockedLosers.Add(c);
            committed.TryPush(committed.pushDir);
            return;
        }

        // Committed crate is null (fresh press) or stale (player walked off it to a
        // SEPARATE crate). Pick the biggest overlap among candidates that aren't locked,
        // and lock the rest of this frame's candidates (they competed = together). A
        // lone crate reached after leaving another isn't locked, so lone->lone works.
        PushableCrate w = BiggestOverlap();
        if (w == null) return;

        foreach (var c in candidates)
            if (c != w) lockedLosers.Add(c);

        committed = w;
        w.TryPush(w.pushDir);
    }

    // The crate the player overlaps the most (collider distance smallest / most
    // negative), skipping locked losers. Returns null if every candidate is locked.
    static PushableCrate BiggestOverlap()
    {
        PushableCrate best = null;
        float bestOverlap = float.MaxValue;
        foreach (var c in candidates)
        {
            if (lockedLosers.Contains(c)) continue;
            float o = c.PlayerOverlap();
            if (o < bestOverlap) { bestOverlap = o; best = c; }
        }
        return best;
    }

    // Collider distance to the player: negative when overlapping, more negative the
    // deeper — so the smallest value is the crate the player is hitting most.
    float PlayerOverlap() =>
        playerCol != null ? col.Distance(playerCol).distance
                          : ((Vector2)transform.position - (Vector2)player.transform.position).magnitude;

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

    // Is the player positioned to push the crate in `dir`? Uses collider BOUNDS: the
    // player is "behind" if their collider centre is on the far side of the crate's
    // centre for this direction, and "lined up" if the two colliders overlap on the
    // perpendicular axis. Works from a touching position regardless of pivots.
    bool PlayerBehind(Vector2Int dir)
    {
        Bounds cb = col.bounds;
        Bounds pb = playerCol != null
            ? playerCol.bounds
            : new Bounds(player.transform.position, Vector3.one * 0.1f);

        // "linedUp" needs a MINIMUM overlap on the perpendicular axis, not just any
        // touch. A 1-pixel corner clip (feet straddling a grid line, barely catching a
        // crate's corner) has near-zero overlap, so it's rejected and the crate stays
        // put — while a real edge-of-crate push keeps a solid overlap and still works.
        // minLineupOverlap = 0 restores the old "any touch" behaviour.
        if (dir.x != 0)
        {
            bool behind  = dir.x > 0 ? pb.center.x <= cb.center.x : pb.center.x >= cb.center.x;
            bool linedUp = OverlapDepth(pb.min.y, pb.max.y, cb.min.y, cb.max.y) >= minLineupOverlap;
            return behind && linedUp;
        }
        else
        {
            bool behind  = dir.y > 0 ? pb.center.y <= cb.center.y : pb.center.y >= cb.center.y;
            bool linedUp = OverlapDepth(pb.min.x, pb.max.x, cb.min.x, cb.max.x) >= minLineupOverlap;
            return behind && linedUp;
        }
    }

    // How deep two 1-D ranges overlap; <= 0 means they only touch or miss.
    static float OverlapDepth(float aMin, float aMax, float bMin, float bMax) =>
        Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin);

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
        Vector3    crateFromCosm = CosmeticOffset;        // parked on a portal? undo has to put that back too
        PlayerController pusher   = player;               // may be null (see Start)
        Vector3    playerFromPos  = pusher != null ? pusher.transform.position : Vector3.zero;

        PuzzleUndo.Record(() =>
        {
            if (this == null) return;

            // A estátua não é desfeita pelo Z, então o tabuleiro pode ter mudado DEPOIS
            // deste empurrão: um arbusto pode estar ocupando a célula de onde a caixa
            // saiu. Voltar pra cima dele deixaria dois ocupantes na mesma célula e
            // corromperia o mapa de ocupação. Nesse caso o undo se recusa.
            if (GridOccupant.TryGetOccupant(crateFromCell, out var blocker) && blocker != null && blocker != this)
            {
                if (Logging)
                    Debug.Log($"{name}: UNDO recusado — a célula {crateFromCell} agora está ocupada por '{blocker.name}' (a estátua mexeu no tabuleiro depois deste empurrão).", this);
                return;
            }

            RestoreTo(crateFromCell, crateFromPos, crateFromCosm);
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
    public void RestoreTo(Vector2Int cell, Vector3 worldPos, Vector3 cosmetic = default)
    {
        StopAllCoroutines();
        isMoving = false;

        ClearCell();
        transform.position = worldPos;
        rb.position        = worldPos;      // keep the physics body in step with the teleport

        // Restored TOGETHER with the position: the two only mean anything as a pair.
        // StopAllCoroutines can cut a portal pull halfway and leave a partial offset —
        // without this the crate would sit at the old position while still lying about
        // where its artwork is, and its cell would read a tile off.
        CosmeticOffset = cosmetic;

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

        // Where the ART wants to end up relative to the cell: nudged if this cell has a
        // portal, dead-centre otherwise. Decided BEFORE the slide so the suck is part of
        // the one movement instead of a second tug bolted onto the end.
        Vector3 offsetFrom = CosmeticOffset;
        Vector3 offsetTo   = CrateTarget.IsTargetCell(target) ? (Vector3)portalLandingOffset
                                                             : Vector3.zero;

        // The honest landing spot is read with the offset temporarily cleared, because
        // RootPositionForCell works backwards from VisualCenter and VisualCenter has the
        // offset baked into it. Zero it, ask, put it back.
        CosmeticOffset = Vector3.zero;
        Vector3 endHonest = RootPositionForCell(target);
        CosmeticOffset = offsetFrom;

        // Aim at the cell's exact center rather than "current position + one tile".
        // A relative step would preserve any authoring error forever; this makes a
        // crate that was left slightly off-grid ease back into alignment on its
        // first push instead of drifting further.
        Vector2 start   = transform.position;
        Vector2 end     = (Vector2)(endHonest + offsetTo);
        float   elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

            // The offset rides along with the position on the same curve. That's what
            // makes the visible path bow diagonally into the portal while the LOGICAL
            // path — VisualCenter — stays the same straight line to the cell centre it
            // has always been. Anything checking cells mid-slide (a statue turning, an
            // undo) sees exactly what it saw before this feature existed.
            CosmeticOffset = Vector3.Lerp(offsetFrom, offsetTo, t);
            rb.MovePosition(Vector2.Lerp(start, end, t));
            yield return null;
        }
        CosmeticOffset = offsetTo;

        // Set DIRECTLY, not via MovePosition. MovePosition only takes effect on the next
        // physics step, so the transform would still be a frame behind while
        // CosmeticOffset is already at its final value — VisualCenter then subtracts the
        // full offset from a position that hasn't caught up and reports the wrong cell
        // for one frame. EvaluateWin runs in exactly that frame, which is how the portal
        // stopped noticing the statue landing on it. Safe here: the slide is over and
        // this is the same spot MovePosition was about to apply anyway.
        transform.position = end;
        rb.position        = end;
        isMoving = false;

        CrateTarget.EvaluateWin();
    }

    static Vector2Int CardinalCell(Vector2 dir) =>
        Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
            ? new Vector2Int((int)Mathf.Sign(dir.x), 0)
            : new Vector2Int(0, (int)Mathf.Sign(dir.y));
}
