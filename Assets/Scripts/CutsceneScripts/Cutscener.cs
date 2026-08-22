using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Generic, data-driven scripted movement. Assign a list of named moves in the
// Inspector — an object, a destination Transform, a name — then trigger one from
// anywhere by that name; most commonly a Yarn <<movement Name>> command (see
// ImageScript's DialogueManager.Movement). No bespoke per-cutscene script needed for
// "make X walk to Y" — just add an entry here and reference its name.
public class Cutscener : MonoBehaviour
{
    [System.Serializable]
    public class Movement
    {
        public string name;
        public Transform target;      // the object that moves
        public Transform destination; // where it moves to
        public float speed = 2f;
        public float arriveThreshold = 0.05f;
    }

    [SerializeField] private List<Movement> movements = new List<Movement>();

    [System.Serializable]
    public class NamedObject
    {
        public string name;
        public GameObject target;
    }

    [Tooltip("Objects that can be shown/hidden by name from <<enable Name>> / <<disable Name>>.")]
    [SerializeField] private List<NamedObject> objects = new List<NamedObject>();

    // Every Cutscener in the scene registers here — different cutscenes are expected
    // to each get their own Cutscener with their own movements/objects, so a name is
    // looked up across ALL of them (see FindOwner) rather than through a single
    // singleton. A single "last one wins" Instance used to mean only whichever
    // Cutscener initialized last ever actually fired anything, silently.
    private static readonly List<Cutscener> allInstances = new List<Cutscener>();

    private void Awake()
    {
        allInstances.Add(this);
        CheckForDuplicateNames();
    }

    private void OnDestroy()
    {
        allInstances.Remove(this);
    }

    // A duplicate name within THIS Cutscener's own Movements or Objects silently
    // loses — List.Find always returns the FIRST match, so an edited/added SECOND
    // entry with the same name has no effect at all, which looks exactly like "I
    // fixed this but nothing changed."
    private void CheckForDuplicateNames()
    {
        var seenMovements = new HashSet<string>();
        foreach (var m in movements)
        {
            if (string.IsNullOrEmpty(m.name)) continue;
            if (!seenMovements.Add(m.name))
                Debug.LogWarning($"Cutscener: '{name}' has more than one Movement named '{m.name}' — only the first one in the list will ever fire.", this);
        }

        var seenObjects = new HashSet<string>();
        foreach (var o in objects)
        {
            if (string.IsNullOrEmpty(o.name)) continue;
            if (!seenObjects.Add(o.name))
                Debug.LogWarning($"Cutscener: '{name}' has more than one Object named '{o.name}' — only the first one in the list will ever fire.", this);
        }
    }

    // Finds whichever registered Cutscener owns an entry matching hasEntry. Warns if
    // more than one does (an ambiguous cross-Cutscener name clash) or if none do.
    private static Cutscener FindOwner(System.Func<Cutscener, bool> hasEntry, string kind, string entryName)
    {
        Cutscener owner = null;
        int matches = 0;

        foreach (var c in allInstances)
        {
            if (hasEntry(c))
            {
                matches++;
                if (owner == null) owner = c;
            }
        }

        if (matches > 1)
            Debug.LogWarning($"Cutscener: {kind} '{entryName}' is defined in {matches} different Cutscener components — only '{owner.name}' will be used.");

        if (owner == null)
            Debug.LogWarning($"Cutscener: no {kind} named '{entryName}' is assigned in any Cutscener in the scene.");

        return owner;
    }

    // The external hook — call this directly, or trigger it from Yarn via
    // <<movement Name>>, using whatever name was given to the entry in the Inspector.
    // Fire-and-forget: returns immediately, the move plays out in the background.
    // Pass flip to have the target's sprite flip X once before it sets off, instead of
    // following the direction of travel like it normally does.
    public void Move(string movementName, bool flip = false)
    {
        StartMove(movementName, flip);
    }

    // Same move, but returns a handle the caller can yield on to wait for it to finish —
    // used by the blocking <<movement Name freeze>> form.
    public Coroutine MoveAndWait(string movementName, bool flip = false)
    {
        return StartMove(movementName, flip);
    }

    // Tracks whichever object is currently mid-move, per target Transform — so a
    // SECOND <<movement>> firing for the same object before the first one arrives
    // (e.g. the player spamming through dialogue faster than a walk can finish)
    // SUPERSEDES it cleanly instead of both coroutines fighting over the same
    // Transform.position every frame. Also holds the object's true pre-move
    // Rigidbody2D/PlayerController state, captured once at the start of a chain of
    // moves and restored only once the chain's last move actually finishes — so an
    // intermediate superseded move can never mistake "currently Kinematic because
    // another move already grabbed it" for the real original state.
    private class MoveState
    {
        public int token;
        public RigidbodyType2D originalBodyType;
        public bool originalInputEnabled;
    }

    private readonly Dictionary<Transform, MoveState> moveStates = new Dictionary<Transform, MoveState>();

    private Coroutine StartMove(string movementName, bool flip)
    {
        Movement movement = movements.Find(m => m.name == movementName);
        if (movement == null)
        {
            Debug.LogWarning($"Cutscener: no movement named '{movementName}' is assigned.", this);
            return null;
        }

        if (movement.target == null || movement.destination == null)
        {
            Debug.LogWarning($"Cutscener: movement '{movementName}' is missing its object or destination.", this);
            return null;
        }

        Transform obj = movement.target;
        if (!moveStates.TryGetValue(obj, out MoveState state))
        {
            // Deliberately GetComponent + != null here rather than an `is Type variable`
            // pattern — Unity's fake-null wrapper for "no component found" is a non-null
            // managed reference, so `is` matches it and then touching .bodyType throws
            // MissingComponentException instead of just being null.
            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            PlayerController pc = obj.GetComponent<PlayerController>();

            state = new MoveState
            {
                originalBodyType = rb != null ? rb.bodyType : RigidbodyType2D.Dynamic,
                originalInputEnabled = pc == null || pc.InputEnabled
            };
            moveStates[obj] = state;
        }

        int token = ++state.token;

        return StartCoroutine(MoveRoutine(movement, flip, state, token));
    }

    // Static so a Yarn command (which must be a static method) can trigger a movement
    // without needing its own reference to a specific Cutscener — searches every
    // Cutscener in the scene for whichever one owns this movement name.
    public static void Trigger(string movementName, bool flip = false)
    {
        Cutscener owner = FindOwner(c => c.HasMovement(movementName), "movement", movementName);
        owner?.Move(movementName, flip);
    }

    public static Coroutine TriggerAndWait(string movementName, bool flip = false)
    {
        Cutscener owner = FindOwner(c => c.HasMovement(movementName), "movement", movementName);
        return owner?.MoveAndWait(movementName, flip);
    }

    private bool HasMovement(string movementName) => movements.Exists(m => m.name == movementName);
    private bool HasObject(string objectName) => objects.Exists(o => o.name == objectName);

    // The external hook for <<enable Name>> / <<disable Name>> — toggles the named
    // entry's GameObject active state.
    public void SetObjectActive(string objectName, bool active)
    {
        NamedObject entry = objects.Find(o => o.name == objectName);
        if (entry == null)
        {
            Debug.LogWarning($"Cutscener: no object named '{objectName}' is assigned.", this);
            return;
        }

        if (entry.target == null)
        {
            Debug.LogWarning($"Cutscener: object '{objectName}' has no GameObject assigned.", this);
            return;
        }

        entry.target.SetActive(active);
    }

    // Static so a Yarn command can reach it without needing its own reference to a
    // specific Cutscener — searches every Cutscener in the scene for whichever one
    // owns this object name.
    public static void TriggerSetActive(string objectName, bool active)
    {
        Cutscener owner = FindOwner(c => c.HasObject(objectName), "object", objectName);
        owner?.SetObjectActive(objectName, active);
    }

    // The external hook for <<face Name Direction>> — sets a named object's facing
    // outright ("left"/"right"/"up"/"down"), independent of any movement. Exists
    // because a <<movement>>'s facing is derived from the ACTUAL travel direction each
    // frame, which for the player depends on wherever he happened to be standing when
    // the move started — reliable when you need a specific, guaranteed facing (e.g.
    // after a <<movement ... freeze>>) rather than whatever direction he walked in from.
    public void Face(string objectName, string direction)
    {
        NamedObject entry = objects.Find(o => o.name == objectName);
        if (entry == null || entry.target == null)
        {
            Debug.LogWarning($"Cutscener: no object named '{objectName}' is assigned for Face.", this);
            return;
        }

        Vector2 dir = DirectionFromString(direction);
        if (dir == Vector2.zero)
        {
            Debug.LogWarning($"Cutscener: '{direction}' isn't a recognized direction for Face (use left/right/up/down).", this);
            return;
        }

        PlayerController controller = entry.target.GetComponent<PlayerController>();
        if (controller != null)
        {
            // Sets facing via the same path movement uses, then immediately idles —
            // a facing update with no actual walk.
            controller.SetCutsceneMoveDirection(dir);
            controller.SetCutsceneMoveDirection(Vector2.zero);
            return;
        }

        SpriteRenderer sprite = entry.target.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            sprite.flipX = dir.x < 0f;
    }

    public static void TriggerFace(string objectName, string direction)
    {
        Cutscener owner = FindOwner(c => c.HasObject(objectName), "object", objectName);
        owner?.Face(objectName, direction);
    }

    private static Vector2 DirectionFromString(string direction)
    {
        switch (direction?.ToLowerInvariant())
        {
            case "left": return Vector2.left;
            case "right": return Vector2.right;
            case "up": return Vector2.up;
            case "down": return Vector2.down;
            default: return Vector2.zero;
        }
    }

    private IEnumerator MoveRoutine(Movement movement, bool flip, MoveState state, int token)
    {
        Transform obj = movement.target;
        Vector2 destination = movement.destination.position;

        // Rigidbody2D/PlayerController are expected on the assigned target itself (the
        // object that actually moves), but the sprite is often on a separate visual
        // child instead (e.g. Haze's Rigidbody2D is on the "Haze" root while its
        // SpriteRenderer is on a "HazeVisual" child) — GetComponentInChildren finds it
        // either way without requiring target to be reassigned to the visual object,
        // which would lose the Rigidbody2D that has to move instead.
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        SpriteRenderer sprite = obj.GetComponentInChildren<SpriteRenderer>();
        PlayerController controller = obj.GetComponent<PlayerController>();

        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        // If this is the player, take input away for the duration so a click-to-move
        // or WASD press can't fight this scripted walk (it's usually already off,
        // e.g. during a running Yarn dialogue — this is just a safety net for
        // triggering movement outside one).
        if (controller != null)
            controller.InputEnabled = false;

        // Flip is a one-time "turn around" before setting off (e.g. a villager
        // wheeling around to walk away) — only for plain sprites, since the player's
        // facing is driven by SetCutsceneMoveDirection instead. Once flipped, the
        // per-frame direction-based flip below is skipped so this doesn't immediately
        // get overwritten on the first step.
        if (flip && controller == null && sprite != null)
            sprite.flipX = !sprite.flipX;

        while (Vector2.Distance(obj.position, destination) > movement.arriveThreshold)
        {
            // A newer move for this same object has taken over — stop driving it and
            // hand off cleanly instead of fighting the newer move for control every
            // frame. That newer move owns restoring original state once IT finishes.
            if (state.token != token)
                yield break;

            Vector2 pos = obj.position;
            Vector2 dir = (destination - pos).normalized;
            Vector2 next = Vector2.MoveTowards(pos, destination, movement.speed * Time.deltaTime);

            obj.position = next;
            if (rb != null) rb.position = next;

            if (controller != null)
                controller.SetCutsceneMoveDirection(dir);
            else if (!flip && sprite != null && Mathf.Abs(dir.x) > 0.01f)
                sprite.flipX = dir.x < 0f;

            yield return null;
        }

        obj.position = destination;
        if (rb != null) rb.position = destination;

        if (controller != null)
            controller.SetCutsceneMoveDirection(Vector2.zero);

        // Only actually restore the object's original state if nothing has
        // superseded this move since it started — i.e. this really is the last move
        // in the chain for this object, not an earlier one that happened to arrive
        // after a later one already grabbed control.
        if (state.token == token)
        {
            if (rb != null)
            {
                rb.bodyType = state.originalBodyType;
                rb.linearVelocity = Vector2.zero;
            }
            if (controller != null)
                controller.InputEnabled = state.originalInputEnabled;

            moveStates.Remove(obj);
        }
    }
}
