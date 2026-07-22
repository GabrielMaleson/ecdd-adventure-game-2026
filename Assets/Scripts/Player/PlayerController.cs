using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float     moveSpeed     = 100f;
    [SerializeField] Transform visualTransform;
    [SerializeField] float     stopThreshold = 0.05f;

    // Set false by GhostControl while the player is piloting the ghost: the MC
    // ignores all input and stands idle until control returns to him.
    public bool InputEnabled = true;

    // Direction the player is currently trying to move in (zero if idle). Read by
    // things like PushableCrate to know whether the player is walking into them.
    public Vector2 MoveDirection { get; private set; }

    Animator       animator;
    SpriteRenderer spriteRenderer;
    Rigidbody2D    rb;
    Vector2        targetPosition;
    Vector2        keyboardDir;    // current WASD input, zero if none (applied in FixedUpdate)
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
        rb             = GetComponent<Rigidbody2D>();
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

    // Input, facing and MoveDirection are decided here every frame; the ACTUAL motion
    // happens in FixedUpdate via Rigidbody2D.MovePosition, so the player stops CLEANLY
    // on contact with crates and walls. (The old code wrote transform.position on a
    // Dynamic body, which never reached real contact — it rested ~0.1 short, so a push
    // right next to a crate silently failed even though it looked like he was touching.)
    void Update()
    {
        // Frozen while the player is piloting the ghost (see GhostControl): no
        // walking, no click-to-move — just stand idle.
        if (!InputEnabled)
        {
            if (isMoving || MoveDirection != Vector2.zero || keyboardDir != Vector2.zero)
            {
                isMoving      = false;
                keyboardDir   = Vector2.zero;
                MoveDirection = Vector2.zero;
                SetDir(DIR_IDLE);
            }
            return;
        }

        Vector2 keyboardInput = ReadKeyboardInput();
        if (keyboardInput != Vector2.zero)
        {
            isMoving      = false; // keyboard input overrides any active click-to-move target
            keyboardDir   = keyboardInput;
            MoveDirection = keyboardInput;
            FaceDirection(keyboardInput);
            return;
        }
        keyboardDir = Vector2.zero;

        if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            HandleClick();

        // Click-to-move: decide direction / arrival here; FixedUpdate does the moving.
        if (isMoving)
        {
            Vector2 pos = rb.position;
            if (Vector2.Distance(pos, targetPosition) < stopThreshold)
            {
                isMoving      = false;
                MoveDirection = Vector2.zero;
                SetDir(DIR_IDLE);
            }
            else
            {
                Vector2 dir   = (targetPosition - pos).normalized;
                MoveDirection = dir;
                FaceDirection(dir);
            }
        }
        else
        {
            MoveDirection = Vector2.zero;
            SetDir(DIR_IDLE);
        }
    }

    void FixedUpdate()
    {
        if (!InputEnabled) return;

        float step = (moveSpeed / PPU) * Time.fixedDeltaTime;

        if (keyboardDir != Vector2.zero)
            rb.MovePosition(rb.position + keyboardDir * step);
        else if (isMoving)
            rb.MovePosition(Vector2.MoveTowards(rb.position, targetPosition, step));
    }

    Vector2 ReadKeyboardInput()
    {
        var kb = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        return input.normalized;
    }

    void LateUpdate()
    {
        // Applied in LateUpdate so the Animator cannot overwrite it each frame.
        visualTransform.localScale = desiredScale;
    }

    // Prevents clicking a UI element (e.g. the InteractButton) from also
    // sending the player walking toward that screen position.
    bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

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

    // Instantly places the player and cancels whatever move was in progress, so he
    // doesn't immediately walk back toward an old click target. Used by the puzzle
    // undo to yank him back to where he stood before a push (framed in-story as the
    // Fragment pulling him back).
    public void TeleportTo(Vector2 pos)
    {
        isMoving       = false;
        keyboardDir    = Vector2.zero;
        MoveDirection  = Vector2.zero;
        targetPosition = pos;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        if (rb != null) rb.position = pos;   // keep the physics body in step with the teleport
        SetDir(DIR_IDLE);
    }

    void FaceDirection(Vector2 dir)
    {
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
