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
    // How a moving object's sprite decides which way to look during the move.
    public enum FacingMode
    {
        Travel,    // default: follow the direction of travel, updated every frame
        FlipOnce,  // <<movement X flip>>: invert once before setting off, then hold
        Hold       // <<movement X hold>>: keep whatever facing it already had
    }

    [System.Serializable]
    public class Movement
    {
        public string name;
        public Transform target;      // the object that moves
        public Transform destination; // where it moves to

        [Tooltip("Deslocamento (em unidades) somado ao destino. Use com um destino que E OUTRO PERSONAGEM: p.ex. destino = Josh, offset X = -1.5 faz o Haze parar sempre 1.5 a ESQUERDA do Josh, onde quer que ele tenha parado.")]
        public Vector2 destinationOffset;   // read once, when the move starts

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
    public void Move(string movementName, FacingMode facing = FacingMode.Travel)
    {
        StartMove(movementName, facing);
    }

    // Same move, but returns a handle the caller can yield on to wait for it to finish —
    // used by the blocking <<movement Name freeze>> form.
    public Coroutine MoveAndWait(string movementName, FacingMode facing = FacingMode.Travel)
    {
        return StartMove(movementName, facing);
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

    private Coroutine StartMove(string movementName, FacingMode facing)
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

        return StartCoroutine(MoveRoutine(movement, facing, state, token));
    }

    // Static so a Yarn command (which must be a static method) can trigger a movement
    // without needing its own reference to a specific Cutscener — searches every
    // Cutscener in the scene for whichever one owns this movement name.
    public static void Trigger(string movementName, FacingMode facing = FacingMode.Travel)
    {
        Cutscener owner = FindOwner(c => c.HasMovement(movementName), "movement", movementName);
        owner?.Move(movementName, facing);
    }

    public static Coroutine TriggerAndWait(string movementName, FacingMode facing = FacingMode.Travel)
    {
        Cutscener owner = FindOwner(c => c.HasMovement(movementName), "movement", movementName);
        return owner?.MoveAndWait(movementName, facing);
    }

    private bool HasMovement(string movementName) => movements.Exists(m => m.name == movementName);
    private bool HasObject(string objectName) => objects.Exists(o => o.name == objectName);

    private GameObject GetObject(string objectName)
    {
        NamedObject entry = objects.Find(o => o.name == objectName);
        return entry != null ? entry.target : null;
    }

    // Looks a name up across EVERY Cutscener, not just this one — <<face Josh HazeOne>>
    // routinely names two objects that live in different Cutsceners.
    private static GameObject FindObjectAnywhere(string objectName)
    {
        foreach (var c in allInstances)
        {
            GameObject go = c.GetObject(objectName);
            if (go != null) return go;
        }
        return null;
    }

    // Where a character STANDS, for working out who is to the left of whom.
    //
    // Deliberately the ground position and NOT the sprite centre. The player's sprite
    // is 64px at scale 4 — about 2.5 world units tall — so its centre floats ~1.6
    // above his feet, while a small floating creature's sits ~0.5 above the ground.
    // Measuring centre-to-centre invents a vertical gap of over a unit that has nothing
    // to do with where either of them is, and for two characters standing close that
    // phantom Y beats the real X: the direction came out as "down", and down carries no
    // left or right, so the facing flip was discarded and the MC faced the same way
    // whoever he was talking to.
    private static Vector2 FacingAnchor(GameObject go)
    {
        // For the player the entry is often wired to the visual child, which can carry
        // its own offset — the controller's own transform is the honest ground point.
        PlayerController controller = ControllerFor(go);
        return controller != null ? (Vector2)controller.transform.position
                                  : (Vector2)go.transform.position;
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
    // specific Cutscener — searches every Cutscener in the scene for whichever one
    // owns this object name.
    public static void TriggerSetActive(string objectName, bool active)
    {
        Cutscener owner = FindOwner(c => c.HasObject(objectName), "object", objectName);
        owner?.SetObjectActive(objectName, active);
    }

    // The external hook for <<face Name Towards>> — sets a named object's facing
    // outright, independent of any movement. "Towards" is either a compass word
    // ("left"/"right"/"up"/"down") or THE NAME OF ANOTHER REGISTERED OBJECT, in which
    // case the object turns to look at wherever that one currently is.
    //
    // Exists because a <<movement>>'s facing is derived from the ACTUAL travel
    // direction each frame, which for the player depends on wherever he happened to be
    // standing when the move started. Prefer the object form whenever the player
    // reached the spot ON HIS OWN (no <<movement>> put him there) — it is the only
    // version that is right from every approach angle.
    public void Face(string objectName, string towards)
    {
        NamedObject entry = objects.Find(o => o.name == objectName);
        if (entry == null || entry.target == null)
        {
            Debug.LogWarning($"Cutscener: no object named '{objectName}' is assigned for Face.", this);
            return;
        }

        Vector2 dir = DirectionFromString(towards);

        if (dir == Vector2.zero)
        {
            // Not a compass word — read it as ANOTHER registered object's name and turn
            // toward wherever that object actually is. This is what makes a face-to-face
            // survive the player stopping wherever he likes instead of on a scripted
            // mark: "<<face Josh HazeOne>>" is correct from any approach angle, while
            // "<<face Josh left>>" is only correct if he happened to stop to Haze's right.
            GameObject other = FindObjectAnywhere(towards);
            if (other == null)
            {
                Debug.LogWarning($"Cutscener: '{towards}' is neither a direction (left/right/up/down) nor the name of an object registered in any Cutscener.", this);
                return;
            }

            dir = FacingAnchor(other) - FacingAnchor(entry.target);
            if (dir.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"Cutscener: '{objectName}' and '{towards}' are on the same spot — no direction to face.", this);
                return;
            }
            dir.Normalize();
        }

        PlayerController controller = ControllerFor(entry.target);
        if (controller != null)
        {
            // SetFacing rather than SetCutsceneMoveDirection twice: same result, but it
            // says what it means — turn and stand, no walk. The pose is the ordinary
            // Player_Idle plus the horizontal flip, i.e. exactly what the character
            // looks like whenever he stops during normal play.
            controller.SetFacing(dir);
            return;
        }

        // A character that owns its facing turns itself: Marcus has both sides DRAWN (the
        // watch, the jacket buttons) and must never be mirrored, and only he knows that.
        CharacterFacing facing = FacingFor(entry.target);
        if (facing != null)
        {
            facing.Set(dir);
            facing.SetIdle();
            return;
        }

        // Fallback for characters that are just a sprite. EVERY renderer under the
        // object flips, not the first one found: a character built out of parts (the
        // player is a Top and a Bottom) would otherwise turn half of itself around.
        if (Mathf.Abs(dir.x) <= 0.01f)
            return;

        foreach (SpriteRenderer sprite in entry.target.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.flipX = dir.x < 0f;
    }

    // The PlayerController is on the character's ROOT, but a Cutscener entry is very
    // easy to wire to the visual child instead — the visual is what you see and click
    // in the Hierarchy, and for <<enable>>/<<disable>> either one works, so nothing
    // complains. Face then silently fell through to the dumb sprite-flip path and the
    // MC stayed facing the camera through the whole conversation. Looking up and down
    // from whatever was assigned makes both wirings behave the same.
    private static CharacterFacing FacingFor(GameObject go)
    {
        CharacterFacing facing = go.GetComponent<CharacterFacing>();
        if (facing == null) facing = go.GetComponentInParent<CharacterFacing>();
        if (facing == null) facing = go.GetComponentInChildren<CharacterFacing>();
        return facing;
    }

    private static PlayerController ControllerFor(GameObject go)
    {
        PlayerController controller = go.GetComponent<PlayerController>();
        if (controller == null) controller = go.GetComponentInParent<PlayerController>();
        if (controller == null) controller = go.GetComponentInChildren<PlayerController>();
        return controller;
    }

    // The external hook for <<placeat A B>> — drops A exactly where B stands, matching
    // its facing, with no walking and no interpolation.
    //
    // Exists for handing a cutscene stand-in over to the real object: enable the real
    // one AT the stand-in's spot and the swap is invisible, instead of the replacement
    // appearing wherever it was parked (or snapping to the player, which is what
    // FragmentFollow used to do and what read as a teleport).
    public void PlaceAt(string objectName, string referenceName)
    {
        GameObject target = GetObject(objectName);
        GameObject reference = FindObjectAnywhere(referenceName);

        if (target == null)
        {
            Debug.LogWarning($"Cutscener: no object named '{objectName}' is assigned for PlaceAt.", this);
            return;
        }
        if (reference == null)
        {
            Debug.LogWarning($"Cutscener: PlaceAt reference '{referenceName}' is not registered in any Cutscener.", this);
            return;
        }

        Vector3 pos = reference.transform.position;
        pos.z = target.transform.position.z;   // keep whatever sorting depth it had
        target.transform.position = pos;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = pos;     // or physics drags it back next FixedUpdate

        // Carry the facing over too, so the handover does not flip on the swap.
        SpriteRenderer from = reference.GetComponentInChildren<SpriteRenderer>(true);
        if (from != null)
        {
            foreach (SpriteRenderer to in target.GetComponentsInChildren<SpriteRenderer>(true))
                to.flipX = from.flipX;
        }
    }

    public static void TriggerPlaceAt(string objectName, string referenceName)
    {
        Cutscener owner = FindOwner(c => c.HasObject(objectName), "object", objectName);
        owner?.PlaceAt(objectName, referenceName);
    }

    public static void TriggerFace(string objectName, string towards)
    {
        Cutscener owner = FindOwner(c => c.HasObject(objectName), "object", objectName);
        owner?.Face(objectName, towards);
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

    private IEnumerator MoveRoutine(Movement movement, FacingMode facing, MoveState state, int token)
    {
        Transform obj = movement.target;
        // Snapshotted at the START of the move, not tracked per frame: the destination
        // is allowed to be another character (that's the point of destinationOffset),
        // and chasing a live transform would turn a scripted walk into a pursuit.
        Vector2 destination = (Vector2)movement.destination.position + movement.destinationOffset;

        // Rigidbody2D/PlayerController are expected on the assigned target itself (the
        // object that actually moves), but the sprite is often on a separate visual
        // child instead (e.g. Haze's Rigidbody2D is on the "Haze" root while its
        // SpriteRenderer is on a "HazeVisual" child) — GetComponentInChildren finds it
        // either way without requiring target to be reassigned to the visual object,
        // which would lose the Rigidbody2D that has to move instead.
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        // Every renderer, not the first one: a character built out of parts would
        // otherwise turn half of itself around (see Face for the same reasoning).
        SpriteRenderer[] sprites = obj.GetComponentsInChildren<SpriteRenderer>(true);
        PlayerController controller = obj.GetComponent<PlayerController>();

        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        // If this is the player, take input away for the duration so a click-to-move
        // or WASD press can't fight this scripted walk (it's usually already off,
        // e.g. during a running Yarn dialogue — this is just a safety net for
        // triggering movement outside one).
        if (controller != null)
            controller.InputEnabled = false;

        // FlipOnce is a one-time "turn around" before setting off (e.g. a villager
        // wheeling around to walk away) — only for plain sprites, since the player's
        // facing is driven by SetCutsceneMoveDirection instead. Once flipped, the
        // per-frame direction-based flip below is skipped so this doesn't immediately
        // get overwritten on the first step.
        if (facing == FacingMode.FlipOnce && controller == null)
        {
            foreach (SpriteRenderer sprite in sprites)
                sprite.flipX = !sprite.flipX;
        }

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
            else if (facing == FacingMode.Travel && Mathf.Abs(dir.x) > 0.01f)
            {
                foreach (SpriteRenderer sprite in sprites)
                    sprite.flipX = dir.x < 0f;
            }

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
