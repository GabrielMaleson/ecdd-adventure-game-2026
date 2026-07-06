using UnityEngine;

// Makes an NPC wander between designer-placed waypoints to give the village a sense of life.
// Walks waypoint to waypoint, and can optionally putter around each waypoint in short
// random bursts (never straying past ShortBurstRadius) before moving on.
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

    SpriteRenderer spriteRenderer;
    State          state;
    public int            currentIndex;
    public int            pingPongDir = 1;
    Vector2        anchor;
    Vector2        burstTarget;
    public int            burstsRemaining;
    public float          stateTimer;
    public float          effectiveWalkSpeed;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{name}: NPCWaypointWalker has no waypoints assigned, disabling.", this);
            enabled = false;
            return;
        }

        effectiveWalkSpeed = walkSpeed * Random.Range(1f - walkSpeedVariance, 1f + walkSpeedVariance);
        currentIndex       = startAtRandomWaypoint ? Random.Range(0, waypoints.Length) : 0;
        state              = State.MovingToWaypoint;
    }

    private void Update()
    {
        switch (state)
        {
            case State.MovingToWaypoint:
                if (MoveToward(waypoints[currentIndex].position))
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

    private void TickTimer(System.Action onElapsed)
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            onElapsed();
    }

    private void OnArrivedAtWaypoint()
    {
        anchor = waypoints[currentIndex].position;

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

    private void StartNextBurstOrAdvance()
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

    private void EnterWaiting()
    {
        state      = State.Waiting;
        stateTimer = Random.Range(waitAtWaypointMin, waitAtWaypointMax);

        if (randomIdleFlip && spriteRenderer != null && Random.value < randomIdleFlipChance)
            spriteRenderer.flipX = !spriteRenderer.flipX;
    }

    private void AdvanceToNextWaypoint()
    {
        if (waypoints.Length > 1)
        {
            if (pingPong)
            {
                currentIndex += pingPongDir;
                if (currentIndex >= waypoints.Length)
                {
                    currentIndex = waypoints.Length - 2;
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
                currentIndex = (currentIndex + 1) % waypoints.Length;
            }
        }

        state = State.MovingToWaypoint;
    }

    // Returns true once the target has been reached.
    bool MoveToward(Vector2 target)
    {
        Vector2 pos = transform.position;
        Vector2 dir = target - pos;

        if (flipSpriteToFaceMovement && spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x < 0f;

        transform.position = Vector2.MoveTowards(pos, target, effectiveWalkSpeed * Time.deltaTime);
        return Vector2.Distance(transform.position, target) <= arriveThreshold;
    }

    private void SetWalkAnim(bool isWalking)
    {
        if (animator != null && !string.IsNullOrEmpty(walkBoolParam))
            animator.SetBool(walkBoolParam, isWalking);
    }

    private void OnDrawGizmosSelected()
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
