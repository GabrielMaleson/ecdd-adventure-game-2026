using UnityEngine;

// Walks an object between Inspector-assigned waypoints — used to send NPCs wandering
// around the village. Moves through a Rigidbody2D via MovePosition when the object has
// one (in FixedUpdate, same as PlayerController), so it stops cleanly on collisions
// instead of fighting the physics engine; falls back to plain transform movement for
// objects with no Rigidbody2D.
public class WaypointWalker : MonoBehaviour
{
    public enum LoopMode { PingPong, Loop }

    enum State { Moving, Waiting }

    [Header("Waypoints")]
    public Transform[] waypoints;
    public LoopMode loopMode = LoopMode.PingPong;
    public bool startAtRandomWaypoint;

    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float arriveThreshold = 0.05f;
    public float waitAtWaypointMin = 1f;
    public float waitAtWaypointMax = 3f;

    [Header("Facing")]
    public bool flipSpriteToFaceMovement = true;

    [Header("Animation (optional)")]
    public Animator animator;
    public string walkBoolParam = "IsWalking";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private State state;
    private int currentIndex;
    private int direction = 1;
    private float waitTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{name}: WaypointWalker has no waypoints assigned, disabling.", this);
            enabled = false;
            return;
        }

        currentIndex = startAtRandomWaypoint ? Random.Range(0, waypoints.Length) : 0;
        state = State.Moving;
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case State.Moving:
                Transform target = waypoints[currentIndex];
                if (target == null || MoveToward(target.position))
                    EnterWaiting();
                break;

            case State.Waiting:
                waitTimer -= Time.fixedDeltaTime;
                if (waitTimer <= 0f)
                    AdvanceWaypoint();
                break;
        }

        if (animator != null && !string.IsNullOrEmpty(walkBoolParam))
            animator.SetBool(walkBoolParam, state == State.Moving);
    }

    private void EnterWaiting()
    {
        state = State.Waiting;
        waitTimer = Random.Range(waitAtWaypointMin, waitAtWaypointMax);
    }

    private void AdvanceWaypoint()
    {
        if (waypoints.Length > 1)
        {
            if (loopMode == LoopMode.PingPong)
            {
                currentIndex += direction;
                if (currentIndex >= waypoints.Length)
                {
                    currentIndex = waypoints.Length - 2;
                    direction = -1;
                }
                else if (currentIndex < 0)
                {
                    currentIndex = 1;
                    direction = 1;
                }
            }
            else
            {
                currentIndex = (currentIndex + 1) % waypoints.Length;
            }
        }

        state = State.Moving;
    }

    // Returns true once the target has been reached.
    private bool MoveToward(Vector2 target)
    {
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 dir = target - pos;

        if (flipSpriteToFaceMovement && spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x < 0f;

        Vector2 next = Vector2.MoveTowards(pos, target, moveSpeed * Time.fixedDeltaTime);
        if (rb != null)
            rb.MovePosition(next);
        else
            transform.position = next;

        return Vector2.Distance(next, target) <= arriveThreshold;
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawWireSphere(waypoints[i].position, 0.1f);

            int next = i + 1;
            if (next < waypoints.Length && waypoints[next] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
}
