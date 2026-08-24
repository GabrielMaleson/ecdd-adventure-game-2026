using UnityEngine;

// "How tall is this thing, actually?" — one answer, shared by everything that has to put
// something above an object (the interact prompt, an auto-created floating label).
//
// renderer.bounds is NOT that answer. It is the sprite's whole CELL, transparent padding
// included, and a character sheet is mostly padding: Marcus is 36px of art in a 64px
// frame, so 14 empty pixels sit above his head — 0.56 world units at scale 4. Anything
// placed off renderer.bounds floats a head and a half too high on a character while
// looking correct on a prop whose art fills its frame, which is exactly the bug this
// exists to stop being rediscovered.
public static class VisibleArt
{
    // Looks for artwork DOWN first, then UP, then falls back to the trigger's own shape.
    // Down alone is not enough: a trigger is very often an empty child sitting inside the
    // object it belongs to, with no renderer of its own.
    public static bool TryGetBounds(Transform target, out Bounds bounds)
    {
        if (target == null) { bounds = default; return false; }

        if (Encapsulate(target.GetComponentsInChildren<SpriteRenderer>(), out bounds))
            return true;

        SpriteRenderer up = target.GetComponentInParent<SpriteRenderer>();
        if (up != null && Encapsulate(up.transform.GetComponentsInChildren<SpriteRenderer>(), out bounds))
            return true;

        // Nothing anywhere in this object's own hierarchy — which is the NORMAL shape of a
        // hand-placed interactable: an empty trigger object parked under an "Interactables"
        // folder, sitting on top of scenery that lives somewhere else entirely. The well is
        // exactly that. So look for the artwork it is standing on.
        SpriteRenderer under = SmallestContaining(target.position);
        if (under != null)
        {
            bounds = Of(under);
            return true;
        }

        // Last resort. Deliberately last: the trigger's size is a REACH setting, tuned for
        // how close you must stand, and hanging the prompt's height off it means changing
        // one silently moves the other — which is how standardising every trigger to 2x2
        // dropped the prompt onto the well's roof.
        Collider2D collider = target.GetComponentInParent<Collider2D>();
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    // Among every sprite the point falls inside, the SMALLEST one — the most specific
    // thing at that spot. Smallest and not nearest because a floor tile or a background
    // panel can be centred closer than the prop actually standing there, while it can
    // never be smaller than it.
    //
    // Runs once when an interactable registers (i.e. when you walk into its trigger), not
    // per frame.
    private static SpriteRenderer SmallestContaining(Vector3 point)
    {
        SpriteRenderer best = null;
        float bestArea = float.MaxValue;

        foreach (SpriteRenderer sprite in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (sprite == null || !sprite.enabled || sprite.sprite == null)
                continue;

            Bounds b = sprite.bounds;
            if (point.x < b.min.x || point.x > b.max.x) continue;
            if (point.y < b.min.y || point.y > b.max.y) continue;

            float area = b.size.x * b.size.y;
            if (area < bestArea)
            {
                bestArea = area;
                best = sprite;
            }
        }

        return best;
    }

    public static bool Encapsulate(SpriteRenderer[] sprites, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (SpriteRenderer sprite in sprites)
        {
            // A disabled renderer contributes nothing visible, so letting it into the
            // bounds would push things off into empty space.
            if (sprite == null || !sprite.enabled || sprite.sprite == null)
                continue;

            Bounds b = Of(sprite);

            if (!any) { bounds = b; any = true; }
            else bounds.Encapsulate(b);
        }

        return any;
    }

    // The sprites in this project are imported with a Tight mesh, so sprite.vertices is
    // fitted to the OPAQUE pixels — that is the art itself rather than the frame around
    // it. A sprite imported Full Rect returns its four corners and behaves exactly like
    // renderer.bounds, so nothing regresses by measuring this way.
    public static Bounds Of(SpriteRenderer renderer)
    {
        Vector2[] vertices = renderer.sprite.vertices;
        if (vertices == null || vertices.Length == 0)
            return renderer.bounds;

        Transform t = renderer.transform;
        Bounds result = default;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 v = vertices[i];

            // Flip lives on the RENDERER, not the transform, so it has to be applied by
            // hand or a flipped character measures mirrored.
            if (renderer.flipX) v.x = -v.x;
            if (renderer.flipY) v.y = -v.y;

            Vector3 world = t.TransformPoint(v);
            if (i == 0) result = new Bounds(world, Vector3.zero);
            else result.Encapsulate(world);
        }

        return result;
    }
}
