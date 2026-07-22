using UnityEngine;

public class FragmentFollow : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float     smoothTime = 0.25f;
    [SerializeField] Vector2   offset;

    [Tooltip("Cap on how fast the ghost moves while following (world units/sec). Stops it from zipping/teleporting back when it re-attaches from far away (e.g. after being parked at a statue) — it walks back instead. Keep it a bit above the MC's speed so normal following never lags. Set very high to disable the cap.")]
    [SerializeField] float     maxFollowSpeed = 4f;

    SpriteRenderer spriteRenderer;
    Vector2        lastPlayerPos;
    Vector2        activeOffset;
    Vector2        desiredOffset;
    Vector2        positionVelocity;
    Vector2        offsetVelocity;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (player == null) return;
        lastPlayerPos      = player.position;
        activeOffset       = offset;
        desiredOffset      = offset;
        transform.position = (Vector2)player.position + activeOffset;
        // Ensure the visual child has no stale local position offset.
        spriteRenderer.transform.localPosition = Vector3.zero;
    }

    // Re-enabling after the ghost was piloted or parked: it may be far from the
    // player. Reset the smoothing state so it eases back cleanly instead of
    // lurching from a stale velocity.
    void OnEnable()
    {
        if (player != null) lastPlayerPos = player.position;
        positionVelocity = Vector2.zero;
        offsetVelocity   = Vector2.zero;
    }

    void LateUpdate()
    {
        if (player == null) return;

        Vector2 playerPos = player.position;
        float   deltaX    = playerPos.x - lastPlayerPos.x;
        float   deltaY    = playerPos.y - lastPlayerPos.y;
        lastPlayerPos = playerPos;

        if (deltaX > 0.001f)
            desiredOffset.x = -Mathf.Abs(offset.x);
        else if (deltaX < -0.001f)
            desiredOffset.x =  Mathf.Abs(offset.x);

        if (deltaY > 0.001f)
            desiredOffset.y = -Mathf.Abs(offset.y);
        else if (deltaY < -0.001f)
            desiredOffset.y =  Mathf.Abs(offset.y);

        activeOffset = Vector2.SmoothDamp(activeOffset, desiredOffset, ref offsetVelocity, smoothTime * 0.25f);

        transform.position = Vector2.SmoothDamp(
            transform.position,
            (Vector2)playerPos + activeOffset,
            ref positionVelocity,
            smoothTime,
            maxFollowSpeed
        );

        spriteRenderer.flipX = (playerPos.x - transform.position.x) < 0;
    }
}
