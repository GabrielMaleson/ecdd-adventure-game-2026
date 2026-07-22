using UnityEngine;

// A snappable position marker with NO presence on the occupancy map.
//
// Why this exists: GridObject (the snappable base) is abstract, so you can't add it
// to an object directly. The only concrete "just snap me" option used to be
// GridObstacle — but that inherits GridOccupant, so it CLAIMS a cell (even with no
// collider) and, worse, TileCycle reads "has GridObstacle" as "this is a bush".
//
// A TileCycle GAP cell must be snapped like everything else, yet must NOT claim a
// cell and must NOT be mistaken for a bush. GridMarker is exactly that: a GridObject
// (so Snap works) that is NOT a GridOccupant (so it owns nothing and TileCycle counts
// it as an empty/gap).
//
// Use it on the empty gap markers you parent under a TileCycle.
public class GridMarker : GridObject
{
    protected override Color DebugColor => Color.grey;

    // Pure position anchor: no artwork, so its center is just its transform and Snap
    // aligns the transform straight to the cell.
    protected override SpriteRenderer FindVisual() => null;
}
