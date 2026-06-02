using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float     moveSpeed     = 100f;
    [SerializeField] Transform visualTransform;
    [SerializeField] float     stopThreshold = 0.05f;

    Animator       animator;
    SpriteRenderer spriteRenderer;
    Vector2        targetPosition;
    Vector3        originalScale;
    Vector3        desiredScale;
    Vector2        visualOffset;   // world-space offset: root → sprite visual center
    bool           isMoving;
    int            currentDir = -1;

    static readonly int DirectionHash = Animator.StringToHash("Direction");

    const int   DIR_IDLE = 0;
    const int   DIR_DOWN = 1;
    const int   DIR_UP   = 2;
    const int   DIR_SIDE = 3;
    const float PPU      = 100f;

    void Awake()
    {
        animator       = GetComponentInChildren<Animator>();
        if (visualTransform == null)
            visualTransform = transform.Find("PlayerVisual");
        originalScale  = visualTransform.localScale;
        desiredScale   = originalScale;
        targetPosition = transform.position;
    }

    void Start()
    {
        SetDir(DIR_IDLE);
        // Measure how far the sprite's visual center is from the root.
        // Sprite pivots are often at the feet (bottom-center), not true center.
        // At 3× scale this offset is large enough to make clicks feel diagonal
        // and cause the character to appear to stop far from the target.
        // We subtract this offset when converting a click to a targetPosition so
        // that the visual center of the character lands exactly on the click point.
        spriteRenderer = visualTransform.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            visualOffset = (Vector2)spriteRenderer.bounds.center - (Vector2)transform.position;
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();
        MoveToTarget();
    }

    void LateUpdate()
    {
        // Applied in LateUpdate so the Animator cannot overwrite it each frame.
        visualTransform.localScale = desiredScale;
    }

    void HandleClick()
    {
        if (Camera.main == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        // Use the player's own screen-depth so the conversion is correct for both
        // orthographic and perspective cameras, regardless of camera z-position.
        float   depth = Camera.main.WorldToScreenPoint(transform.position).z;
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, depth));
        world.z = 0f;
        // Walk root to (click − visualOffset) so the sprite center ends up at click.
        targetPosition = (Vector2)world - visualOffset;
        isMoving       = true;
        ClickIndicator.Spawn(world);
    }

    void MoveToTarget()
    {
        if (!isMoving) return;

        Vector2 pos  = transform.position;
        float   dist = Vector2.Distance(pos, targetPosition);

        if (dist < stopThreshold)
        {
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
            isMoving           = false;
            SetDir(DIR_IDLE);
            return;
        }

        Vector2 dir = (targetPosition - pos).normalized;
        transform.position = Vector2.MoveTowards(pos, targetPosition, (moveSpeed / PPU) * Time.deltaTime);

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            SetDir(DIR_SIDE);
            desiredScale = new Vector3(dir.x < 0 ? -originalScale.x : originalScale.x, originalScale.y, originalScale.z);
        }
        else
        {
            SetDir(dir.y < 0 ? DIR_DOWN : DIR_UP);
            desiredScale = originalScale;
        }
    }

    void SetDir(int dir)
    {
        if (dir == currentDir) return;
        currentDir = dir;
        animator.SetInteger(DirectionHash, dir);
    }
}
