using UnityEngine;
using UnityEngine.EventSystems;

public class WeightedDragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float weight = 1f;
    [SerializeField] private float baseDragSpeed = 1f;
    [SerializeField] private AnimationCurve weightToSpeedCurve = AnimationCurve.Linear(0, 1, 10, 0.1f);

    [Header("Collision Settings")]
    [SerializeField] private LayerMask obstacleLayerMask = -1;
    [SerializeField] private float collisionCheckRadius = 0.5f;
    [SerializeField] private int maxCollisionAttempts = 20;

    private Vector3 offset;
    private Camera mainCamera;
    private Rigidbody2D rb2d;
    private Rigidbody rb3d;
    private bool isDragging = false;
    private Vector3 targetPosition;
    private Vector3 mouseWorldPosition;

    // For smoothing the drag movement
    private Vector3 velocityRef = Vector3.zero;
    private float smoothTime = 0.05f;

    void Start()
    {
        mainCamera = Camera.main;

        // Get rigidbody components (supports both 2D and 3D physics)
        rb2d = GetComponent<Rigidbody2D>();
        rb3d = GetComponent<Rigidbody>();

        // Calculate drag speed based on weight
        float dragSpeedModifier = weightToSpeedCurve.Evaluate(weight);
        smoothTime = baseDragSpeed * dragSpeedModifier;
    }

    void Update()
    {
        if (isDragging)
        {
            // Get mouse position in world coordinates
            mouseWorldPosition = GetMouseWorldPosition();

            // Calculate desired position
            Vector3 desiredPosition = mouseWorldPosition + offset;

            // Check for collisions before moving
            if (!WouldCollideAtPosition(desiredPosition))
            {
                targetPosition = desiredPosition;
            }
            else
            {
                // Try to find a valid position nearby
                Vector3 validPosition = FindValidPositionNearby(desiredPosition);
                targetPosition = validPosition;
            }

            // Smooth movement to simulate weight and inertia
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocityRef, smoothTime);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Calculate offset between object position and mouse position
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        offset = transform.position - mouseWorldPos;

        isDragging = true;

        // Disable physics while dragging
        if (rb2d != null) rb2d.isKinematic = true;
        if (rb3d != null) rb3d.isKinematic = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // The actual movement is handled in Update for smooth physics
        // This method just ensures the drag continues
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        // Re-enable physics
        if (rb2d != null) rb2d.isKinematic = false;
        if (rb3d != null) rb3d.isKinematic = false;

        // Reset velocity reference
        velocityRef = Vector3.zero;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private bool WouldCollideAtPosition(Vector3 position)
    {
        // Store original position
        Vector3 originalPosition = transform.position;

        // Temporarily move to test position
        transform.position = position;

        bool collides = false;

        // Check for 2D collisions
        if (rb2d != null)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(position, collisionCheckRadius, obstacleLayerMask);
            foreach (var hit in hitColliders)
            {
                if (hit.gameObject != gameObject && hit.CompareTag("Agarravel"))
                {
                    collides = true;
                    break;
                }
            }
        }
        // Check for 3D collisions
        else if (rb3d != null)
        {
            Collider[] hitColliders = Physics.OverlapSphere(position, collisionCheckRadius, obstacleLayerMask);
            foreach (var hit in hitColliders)
            {
                if (hit.gameObject != gameObject && hit.CompareTag("Agarravel"))
                {
                    collides = true;
                    break;
                }
            }
        }

        // Restore original position
        transform.position = originalPosition;

        return collides;
    }

    private Vector3 FindValidPositionNearby(Vector3 desiredPosition)
    {
        Vector3 originalPosition = transform.position;
        Vector3 bestPosition = originalPosition;
        float bestDistance = float.MaxValue;

        // Try positions in a spiral pattern around the desired position
        for (int attempt = 0; attempt < maxCollisionAttempts; attempt++)
        {
            float angle = attempt * 137.5f; // Golden angle for even distribution
            float radius = collisionCheckRadius * (attempt + 1) / maxCollisionAttempts;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                0
            );

            Vector3 testPosition = desiredPosition + offset;

            // Check if this position is valid (no collisions)
            transform.position = testPosition;
            bool collides = WouldCollideAtPosition(testPosition);

            if (!collides)
            {
                float distance = Vector3.Distance(testPosition, desiredPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = testPosition;

                    // If we found a position very close to desired, use it immediately
                    if (distance < collisionCheckRadius * 0.5f)
                    {
                        break;
                    }
                }
            }
        }

        return bestPosition;
    }

    // Visual feedback for debugging
    private void OnDrawGizmosSelected()
    {
        if (isDragging)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, collisionCheckRadius);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, collisionCheckRadius);
        }
    }
}