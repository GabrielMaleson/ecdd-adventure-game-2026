using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] float     moveSpeed       = 100f;
    [SerializeField] Transform visualTransform;

    Animator animator;
    Vector2  targetPosition;
    bool     isMoving;
    int      currentDir = -1;

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
        targetPosition = transform.position;
    }

    void Start()
    {
        SetDir(DIR_IDLE);
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();
        MoveToTarget();
    }

    void HandleClick()
    {
        if (Camera.main == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 world    = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0));
        world.z          = 0f;
        targetPosition   = world;
        isMoving         = true;
        ClickIndicator.Spawn(world);
    }

    void MoveToTarget()
    {
        if (!isMoving) return;

        Vector2 pos  = transform.position;
        float   dist = Vector2.Distance(pos, targetPosition);

        if (dist < 0.05f)
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
            visualTransform.localScale = new Vector3(dir.x < 0 ? -1f : 1f, 1f, 1f);
        }
        else
        {
            SetDir(dir.y < 0 ? DIR_DOWN : DIR_UP);
            visualTransform.localScale = Vector3.one;
        }
    }

    void SetDir(int dir)
    {
        if (dir == currentDir) return;
        currentDir = dir;
        animator.SetInteger(DirectionHash, dir);
    }
}
