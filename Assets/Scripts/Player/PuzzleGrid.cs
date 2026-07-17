using UnityEngine;
using UnityEngine.Tilemaps;

// Single source of truth for the puzzle lattice.
//
// Put ONE of these in the scene and drag the ground tilemap into it — the tilemap
// already defines an exact lattice, so tile size and phase never get typed by hand
// into individual components (which silently drift apart).
//
// Grid objects find this THEMSELVES via PuzzleGrid.Active; they deliberately do
// not hold a serialized reference to it. A prefab asset cannot reference a scene
// object, so a serialized field would break ("Type mismatch") the moment a puzzle
// component was added to a prefab rather than a scene instance — which is exactly
// how these prefabs are authored.
public class PuzzleGrid : MonoBehaviour
{
    [Tooltip("Ground tilemap that defines the lattice (e.g. BelowCliff). Its cells ARE the puzzle cells.")]
    [SerializeField] Tilemap referenceTilemap;

    public enum GridDisplay
    {
        WhenGridSelected,   // visible whenever the grid is IN the selection, alone or not
        Always,             // never goes away
        Never,
    }

    [Header("Scene view")]
    [Tooltip("When to draw the puzzle cells. 'When Grid Selected' means the grid is in the selection — selecting it TOGETHER with a crate keeps it up, unlike Unity's own grid gizmo. Editor-only; never renders in the game.")]
    [SerializeField] GridDisplay showGrid = GridDisplay.WhenGridSelected;

    [Tooltip("Colour of those lines. Keep the alpha low so the art stays readable underneath.")]
    [SerializeField] Color gridColor = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("Safety cap on cells drawn. A big tilemap would otherwise emit thousands of gizmo lines and crawl the editor.")]
    [SerializeField] int maxCellsDrawn = 40000;

    [Tooltip("Draw which cell each crate/obstacle/target actually CLAIMS, plus a line to it from the object's visible centre. A visible line means the object is off-cell — the logic and your eyes disagree. Works in Play mode too, where it shows the live cell.")]
    [SerializeField] bool showOccupancy = false;

    [Tooltip("Print to the console WHY a push didn't happen (not touching / not behind / target cell held by X). One switch for every crate. Crates far from the player stay quiet on their own.")]
    [SerializeField] bool logPushFailures = false;

    // Read by GridObject so the debug view is one switch for the whole puzzle
    // rather than a toggle on every crate.
    public static bool ShowOccupancy   => Active != null && Active.showOccupancy;
    public static bool LogPushFailures => Active != null && Active.logPushFailures;

    static PuzzleGrid active;

    // The scene's grid. Resolved lazily so it works in edit mode too (OnEnable
    // doesn't run outside play mode). Unity's fake-null makes a destroyed grid
    // compare null, so this re-finds itself after a scene change instead of
    // handing back a corpse.
    public static PuzzleGrid Active
    {
        get
        {
            if (active == null)
                active = FindAnyObjectByType<PuzzleGrid>(FindObjectsInactive.Include);
            return active;
        }
    }

    void OnEnable()  => active = this;
    void OnDisable() { if (active == this) active = null; }

    public bool IsReady => referenceTilemap != null;

    // World size of one cell. Crates step exactly this far per push.
    public Vector2 CellSize => referenceTilemap != null
        ? (Vector2)referenceTilemap.cellSize
        : Vector2.one;

    // Which cell a world point falls in. Z is ignored: this is a 2D lattice, and
    // feeding a sprite's Z into Tilemap.WorldToCell can shift the result when the
    // grid's cell Z size is 0.
    public Vector2Int WorldToCell(Vector3 world)
    {
        if (referenceTilemap == null) return Vector2Int.zero;
        Vector3Int c = referenceTilemap.WorldToCell(new Vector3(world.x, world.y, 0f));
        return new Vector2Int(c.x, c.y);
    }

    // Exact world center of a cell — what things get aligned to.
    public Vector3 CellCenter(Vector2Int cell)
    {
        if (referenceTilemap == null) return Vector3.zero;
        return referenceTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    // OnDrawGizmos (not ...Selected) so WE decide the visibility rule rather than
    // Unity. Unity never calls it in a build, so this costs the game nothing.
    void OnDrawGizmos()
    {
        if (showGrid == GridDisplay.Never || referenceTilemap == null) return;

#if UNITY_EDITOR
        if (showGrid == GridDisplay.WhenGridSelected && !GridIsSelected()) return;
#endif

        // Bounds of the PAINTED tiles, so the drawing stops where the level does
        // instead of running to infinity.
        BoundsInt b = referenceTilemap.cellBounds;
        if ((long)b.size.x * b.size.y > maxCellsDrawn) return;

        Gizmos.color = gridColor;

        // CellToWorld returns a cell's CORNER, which is exactly where a grid line
        // belongs — using cell centers would draw the lattice half a cell off.
        for (int x = b.xMin; x <= b.xMax; x++)
            Gizmos.DrawLine(referenceTilemap.CellToWorld(new Vector3Int(x, b.yMin, 0)),
                            referenceTilemap.CellToWorld(new Vector3Int(x, b.yMax, 0)));

        for (int y = b.yMin; y <= b.yMax; y++)
            Gizmos.DrawLine(referenceTilemap.CellToWorld(new Vector3Int(b.xMin, y, 0)),
                            referenceTilemap.CellToWorld(new Vector3Int(b.xMax, y, 0)));
    }

#if UNITY_EDITOR
    // Membership, not exclusivity. Unity's built-in Grid gizmo drops out the moment
    // the selection contains anything besides the Grid — which is precisely when
    // you need it, since lining a crate up against the lattice means having both
    // selected. Asking "is the grid among the selected?" fixes that.
    //
    // Counts the PuzzleGrid, the reference tilemap, or the Grid the tilemap sits
    // on, so it doesn't matter which of the three you happen to click.
    bool GridIsSelected()
    {
        GridLayout layout = referenceTilemap.layoutGrid;

        foreach (GameObject go in UnityEditor.Selection.gameObjects)
        {
            if (go == gameObject) return true;
            if (go == referenceTilemap.gameObject) return true;
            if (layout != null && go == layout.gameObject) return true;
        }
        return false;
    }
#endif

    void OnValidate()
    {
        // Rotation (ObstacleGroup) interpolates its arc in world space, which only
        // looks right on square cells. The cell logic copes either way; the visuals
        // wouldn't, so flag it here where the tilemap actually gets assigned.
        if (referenceTilemap != null && !Mathf.Approximately(CellSize.x, CellSize.y))
            Debug.LogWarning(
                $"{name}: tilemap cells aren't square ({CellSize}). Turns still land exactly on-cell, " +
                $"but the rotation arc will look skewed.", this);
    }
}
