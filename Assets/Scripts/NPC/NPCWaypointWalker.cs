using UnityEngine;
using System.Collections.Generic;

// Makes an NPC wander between designer-placed waypoints to give the village a sense of life.
// Walks waypoint to waypoint, and can optionally putter around each waypoint in short
// random bursts (never straying past ShortBurstRadius) before moving on.
//
// Moves through an optional Rigidbody2D (MovePosition, in FixedUpdate) when the NPC has
// one, exactly like PlayerController — writing straight to transform.position on a body
// the physics engine is also tracking is what made the player shove/jitter against
// crates (see the Sokoban notes in CLAUDE.md), and the same mismatch here is what made
// this walker "lock on" to one direction against a collider instead of cleanly turning
// at its waypoints. NPCs with no Rigidbody2D still work — they just move via transform.
public class NPCWaypointWalker : MonoBehaviour
{
    enum State { MovingToWaypoint, Waiting, Bursting, BurstPause }

    [Header("Waypoints")]
    public Transform[] waypoints;
    public bool        pingPong             = true;  // false = loop back to waypoints[0]
    public bool        startAtRandomWaypoint;
    public float       arriveThreshold       = 0.05f;

    [Header("Movement")]
    public float walkSpeed        = 1.5f;
    public float waitAtWaypointMin = 1f;
    public float waitAtWaypointMax = 3f;

    [Header("Short Bursts")]
    public bool  useShortBursts     = true;
    public float shortBurstRadius   = 1f;   // max distance from the waypoint during bursts
    public int   shortBurstsMin     = 1;
    public int   shortBurstsMax     = 3;
    public float shortBurstPauseMin = 0.5f;
    public float shortBurstPauseMax = 1.5f;

    [Header("Random Movement & Flipping")]
    public float walkSpeedVariance      = 0.2f;  // +/- fraction applied once at spawn
    public bool  flipSpriteToFaceMovement = true;
    public bool  randomIdleFlip         = true;
    public float randomIdleFlipChance   = 0.15f;

    [Header("Animation (optional)")]
    public Animator animator;
    public string   walkBoolParam = "IsWalking";

    Rigidbody2D    rb;
    SpriteRenderer spriteRenderer;
    List<Transform> points; // waypoints with any unassigned (null) slots dropped
    State          state;
    int            currentIndex;
    int            pingPongDir = 1;
    Vector2        anchor;
    Vector2        burstTarget;
    int            burstsRemaining;
    float          stateTimer;
    float          effectiveWalkSpeed;

    void Awake()
    {
        rb             = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        points = new List<Transform>();
        if (waypoints != null)
        {
            foreach (var wp in waypoints)
            {
                if (wp != null) points.Add(wp);
            }
        }

        if (points.Count == 0)
        {
            Debug.LogWarning($"{name}: NPCWaypointWalker has no valid waypoints assigned, disabling.", this);
            enabled = false;
            return;
        }

        effectiveWalkSpeed = walkSpeed * Random.Range(1f - walkSpeedVariance, 1f + walkSpeedVariance);
        currentIndex       = startAtRandomWaypoint ? Random.Range(0, points.Count) : 0;
        state              = State.MovingToWaypoint;
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.MovingToWaypoint:
                if (MoveToward(points[currentIndex].position))
                    OnArrivedAtWaypoint();
                break;

            case State.Waiting:
                TickTimer(AdvanceToNextWaypoint);
                break;

            case State.Bursting:
                if (MoveToward(burstTarget))
                {
                    state      = State.BurstPause;
                    stateTimer = Random.Range(shortBurstPauseMin, shortBurstPauseMax);
                }
                break;

            case State.BurstPause:
                TickTimer(StartNextBurstOrAdvance);
                break;
        }

        SetWalkAnim(state == State.MovingToWaypoint || state == State.Bursting);
    }

    void TickTimer(System.Action onElapsed)
    {
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f)
            onElapsed();
    }

    void OnArrivedAtWaypoint()
    {
        anchor = points[currentIndex].position;

        if (useShortBursts)
        {
            burstsRemaining = Random.Range(shortBurstsMin, shortBurstsMax + 1);
            StartNextBurstOrAdvance();
        }
        else
        {
            EnterWaiting();
        }
    }

    void StartNextBurstOrAdvance()
    {
        if (burstsRemaining <= 0)
        {
            EnterWaiting();
            return;
        }

        burstsRemaining--;
        burstTarget = anchor + Random.insideUnitCircle * shortBurstRadius;
        state       = State.Bursting;
    }

    void EnterWaiting()
    {
        state      = State.Waiting;
        stateTimer = Random.Range(waitAtWaypointMin, waitAtWaypointMax);

        if (randomIdleFlip && spriteRenderer != null && Random.value < randomIdleFlipChance)
            spriteRenderer.flipX = !spriteRenderer.flipX;
    }

    void AdvanceToNextWaypoint()
    {
        if (points.Count > 1)
        {
            if (pingPong)
            {
                currentIndex += pingPongDir;
                if (currentIndex >= points.Count)
                {
                    currentIndex = points.Count - 2;
                    pingPongDir  = -1;
                }
                else if (currentIndex < 0)
                {
                    currentIndex = 1;
                    pingPongDir  = 1;
                }
            }
            else
            {
                currentIndex = (currentIndex + 1) % points.Count;
            }
        }

        state = State.MovingToWaypoint;
    }

    // Returns true once the target has been reached. Moves through the Rigidbody2D
    // when present so it stops cleanly on contact instead of fighting physics.
    bool MoveToward(Vector2 target)
    {
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 dir = target - pos;

        if (flipSpriteToFaceMovement && spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x < 0f;

        Vector2 next = Vector2.MoveTowards(pos, target, effectiveWalkSpeed * Time.fixedDeltaTime);
        if (rb != null)
            rb.MovePosition(next);
        else
            transform.position = next;

        return Vector2.Distance(next, target) <= arriveThreshold;
    }

    void SetWalkAnim(bool isWalking)
    {
        if (animator != null && !string.IsNullOrEmpty(walkBoolParam))
            animator.SetBool(walkBoolParam, isWalking);
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawWireSphere(waypoints[i].position, 0.1f);
            if (useShortBursts)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawWireSphere(waypoints[i].position, shortBurstRadius);
                Gizmos.color = Color.cyan;
            }

            int next = i + 1;
            if (next < waypoints.Length && waypoints[next] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
}
