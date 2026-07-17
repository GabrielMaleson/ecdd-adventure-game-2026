using UnityEngine;

// Base for anything that lives on the puzzle lattice: crates, obstacles, targets.
//
// The key idea is the VISIBLE CENTER. A prefab's transform sits at its pivot
// (usually the feet), not at the middle of the drawing, so aligning the transform
// to a cell leaves the artwork looking off. Everything here — snapping AND the
// cell each object claims — is measured from the sprite's center instead, so
// "crate looks centered on the rug" and "code says crate is on the rug" can never
// disagree. Pivots therefore don't matter and never need configuring.
// One grid component per object, enforced by Unity: an object owns exactly one
// cell, so a second copy just fights the first over the registry and leaves ghost
// cells behind. Applied on the base, so PushableCrate + GridObstacle on the same
// object is refused too, not only PushableCrate twice.
[DisallowMultipleComponent]
public abstract class GridObject : MonoBehaviour
{
    [Tooltip("Sprite that defines this object's visible center. Auto-filled with the first SpriteRenderer found. Change it if the prefab has extra sprites (shadow, glow) that would drag the average off-center.")]
    [SerializeField] protected SpriteRenderer visual;

    // Looked up, never serialized. These components live on PREFAB ASSETS, and a
    // prefab asset cannot store a reference to a scene object — a serialized grid
    // field would show up as "Type mismatch" and silently do nothing.
    protected PuzzleGrid grid => PuzzleGrid.Active;

    // The cell this object currently sits on.
    public Vector2Int Cell { get; protected set; }

    // Middle of the artwork in world space. Falls back to the pivot if no sprite
    // was assigned — alignment then depends on the pivot, which is why `visual`
    // exists.
    public Vector3 VisualCenter => visual != null ? visual.bounds.center : transform.position;

    // Rigid pivot->artwork offset. Children keep their local offsets, so moving
    // the root by this much moves the whole prefab as one piece.
    public Vector3 VisualOffset => VisualCenter - transform.position;

    protected virtual void Reset()      => AutoFill();
    protected virtual void OnValidate() => AutoFill();

    // What counts as "the artwork" for this object. Overridable because some grid
    // objects are pure anchors with no sprite of their own (see ObstacleGroup),
    // and grabbing a child's sprite would silently move their center.
    protected virtual SpriteRenderer FindVisual() => GetComponentInChildren<SpriteRenderer>();

    void AutoFill()
    {
        if (visual == null) visual = FindVisual();
    }

    // Moves the root so the ARTWORK lands dead-center on its nearest cell.
    // Editor-only in practice (see GridObjectEditor): what you build in the scene
    // is what ships — nothing re-snaps itself at runtime.
    public void SnapToGrid()
    {
        if (!HasGrid()) return;

        Vector3 offset = VisualOffset;                  // read BEFORE moving
        Vector3 target = grid.CellCenter(grid.WorldToCell(VisualCenter)) - offset;
        target.z = transform.position.z;                // keep sorting depth
        transform.position = target;
    }

    // Cell claimed by the artwork's center, not the pivot.
    protected Vector2Int CurrentCell() => grid.WorldToCell(VisualCenter);

    // Where the root must sit for the artwork to be centered on `cell`.
    protected Vector3 RootPositionForCell(Vector2Int cell)
    {
        Vector3 p = grid.CellCenter(cell) - VisualOffset;
        p.z = transform.position.z;
        return p;
    }

    // Colour this object's cell is drawn in by the occupancy debug view.
    protected virtual Color DebugColor => Color.white;

    // Makes the LOGICAL state visible, which is otherwise invisible and therefore
    // impossible to argue about: a square on the cell this object claims, and a
    // line from its artwork to that square.
    //
    // The line is the whole point. Zero length means the object sits exactly where
    // the code thinks it does. A visible line is the bug, drawn to scale — your
    // eyes and the grid disagree, and you can see by how much and in which
    // direction. In Play mode it reads the live cell, so a crate claiming the wrong
    // cell shows up the instant it happens.
    void OnDrawGizmos()
    {
        if (!PuzzleGrid.ShowOccupancy) return;

        PuzzleGrid g = PuzzleGrid.Active;
        if (g == null || !g.IsReady) return;

        // Outside Play the registry hasn't run, so derive the cell the same way
        // OnEnable would. Inside Play, show what's actually claimed.
        Vector2Int c = Application.isPlaying ? Cell : g.WorldToCell(VisualCenter);
        Vector3 cellCenter = g.CellCenter(c);

        Gizmos.color = DebugColor;
        Gizmos.DrawWireCube(cellCenter, new Vector3(g.CellSize.x, g.CellSize.y, 0.01f) * 0.9f);
        Gizmos.DrawLine(VisualCenter, cellCenter);
    }

    protected bool HasGrid()
    {
        if (grid != null && grid.IsReady) return true;
        Debug.LogError(
            $"{name}: no usable PuzzleGrid. Add a PuzzleGrid to the scene and drag the ground " +
            $"tilemap (BelowCliff) into its Reference Tilemap field.", this);
        return false;
    }
}
