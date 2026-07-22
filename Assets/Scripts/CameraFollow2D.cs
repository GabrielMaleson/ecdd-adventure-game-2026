using UnityEngine;

// Minimal 2D camera follow. Replaces parenting the camera under the MC, so the
// SAME camera can switch between tracking the MC and tracking the ghost:
// GhostControl's onCameraTargetChanged event calls SetTarget with whichever the
// camera should currently look at.
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("Who the camera tracks. GhostControl overrides this at runtime; still assign the MC here so frame zero is framed before the first event fires.")]
    [SerializeField] Transform target;

    [Tooltip("World offset from the target. Keep Z negative (e.g. -10) so a 2D camera stays in front. Match X/Y to the framing you had while the camera was a child of the MC.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Seconds to ease toward the target. 0 = locked instantly (exactly like being parented). A small value (~0.15) gives a soft pan when control switches to the ghost.")]
    [SerializeField] float smoothTime = 0.15f;

    Vector3 velocity;

    // Wire GhostControl.onCameraTargetChanged here (pick SetTarget under the
    // dynamic Transform section so it passes the target through automatically).
    public void SetTarget(Transform t) => target = t;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        if (smoothTime <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}
