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

    public static Cutscener Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning($"Cutscener: '{name}' is a second Cutscener in the scene — only one can be the active Instance at a time. Whichever one initializes last silently wins for every <<movement>>/<<enable>>/<<face>> in the game, which will look like the wrong data is firing. Consolidate everything into one Cutscener.", this);

        Instance = this;

        CheckForDuplicateNames();
    }

    // A duplicate name in Movements or Objects silently loses — List.Find always
    // returns the FIRST match, so an edited/added SECOND entry with the same name has
    // no effect at all, which looks exactly like "I fixed this but nothing changed."
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

        return StartCoroutine(MoveRoutine(movement, flip));
    }

    // Static so a Yarn command (which must be a static method) can trigger a movement
    // without needing its own reference to a specific Cutscener.
    public static void Trigger(string movementName, bool flip = false)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Cutscener: no Cutscener in the scene to run movement '{movementName}'.");
            return;
        }

        Instance.Move(movementName, flip);
    }

    public static Coroutine TriggerAndWait(string movementName, bool flip = false)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Cutscener: no Cutscener in the scene to run movement '{movementName}'.");
            return null;
        }

        return Instance.MoveAndWait(movementName, flip);
    }

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
    // specific Cutscener.
    public static void TriggerSetActive(string objectName, bool active)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Cutscener: no Cutscener in the scene to {(active ? "enable" : "disable")} object '{objectName}'.");
            return;
        }

        Instance.SetObjectActive(objectName, active);
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

        SpriteRenderer sprite = entry.target.GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.flipX = dir.x < 0f;
    }

    public static void TriggerFace(string objectName, string direction)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Cutscener: no Cutscener in the scene to face object '{objectName}'.");
            return;
        }

        Instance.Face(objectName, direction);
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

    private IEnumerator MoveRoutine(Movement movement, bool flip)
    {
        Transform obj = movement.target;
        Vector2 destination = movement.destination.position;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        SpriteRenderer sprite = obj.GetComponent<SpriteRenderer>();
        PlayerController controller = obj.GetComponent<PlayerController>();

        RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // If this is the player, take input away for the duration so a click-to-move
        // or WASD press can't fight this scripted walk, and restore whatever it was
        // set to afterward (it's usually already off, e.g. during a running Yarn
        // dialogue — this is just a safety net for triggering movement outside one).
        bool restoreInput = false;
        if (controller != null)
        {
            restoreInput = controller.InputEnabled;
            controller.InputEnabled = false;
        }

        // Flip is a one-time "turn around" before setting off (e.g. a villager
        // wheeling around to walk away) — only for plain sprites, since the player's
        // facing is driven by SetCutsceneMoveDirection instead. Once flipped, the
        // per-frame direction-based flip below is skipped so this doesn't immediately
        // get overwritten on the first step.
        if (flip && controller == null && sprite != null)
            sprite.flipX = !sprite.flipX;

        while (Vector2.Distance(obj.position, destination) > movement.arriveThreshold)
        {
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
        if (rb != null)
        {
            rb.position = destination;
            rb.bodyType = originalBodyType;
            rb.linearVelocity = Vector2.zero;
        }

        if (controller != null)
        {
            controller.SetCutsceneMoveDirection(Vector2.zero);
            controller.InputEnabled = restoreInput;
        }
    }
}
