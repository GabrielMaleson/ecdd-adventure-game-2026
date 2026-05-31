using UnityEngine;
using UnityEngine.InputSystem;

public class FragmentFollow : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float followSpeed = 3f;
    [SerializeField] Vector2 offset;

    SpriteRenderer spriteRenderer;
    Vector2 lastPlayerPos;
    Vector2 activeOffset;
    Vector2 desiredOffset;
    public bool IsPossessing = false;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (player == null) return;
        lastPlayerPos  = player.position;
        activeOffset   = offset;
        desiredOffset  = offset;
        transform.position = (Vector2)player.position + activeOffset;
    }

    void Update()
    {
        if (Keyboard.current.pKey.wasPressedThisFrame)
            IsPossessing = !IsPossessing;
    }

    void LateUpdate()
    {
        if (player == null) return;
        if (IsPossessing) return;
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

        activeOffset = Vector2.Lerp(activeOffset, desiredOffset, followSpeed * Time.deltaTime);

        transform.position = Vector2.Lerp(
            transform.position,
            playerPos + activeOffset,
            followSpeed * Time.deltaTime
        );

        spriteRenderer.flipX = (playerPos.x - transform.position.x) < 0;
    }
}
