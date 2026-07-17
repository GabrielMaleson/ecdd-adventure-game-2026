using UnityEngine;

// Marks a prefab as a static blocker: a wall, rock, tree — anything a crate must
// not be pushed into. Add it, snap it, done; there is nothing to configure.
//
// Covers exactly ONE cell. An obstacle spanning 2x2 needs four of these (or a
// footprint field, if that becomes common).
public class GridObstacle : GridOccupant
{
    protected override Color DebugColor => Color.red;
}
