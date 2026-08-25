using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// A portal: walking in either teleports immediately (teleportOnTrigger) or arms an
// InteractButton prompt and waits for the player to press E.
[RequireComponent(typeof(Collider2D))]
public class TeleporterScript : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform destination;
    public bool teleportOnTrigger = true;

    [Tooltip("Only used when Teleport On Trigger is off — shown on the Interact Button while in range; press E to teleport.")]
    public string interactLabel = "E";

    [Header("Camera Switch")]
    [Tooltip("Enabled after the transition — the camera for the destination area.")]
    public GameObject Camera;
    [Tooltip("Disabled after the transition — the camera for the area being left.")]
    public GameObject OldCamera;

    [Tooltip("Other objects that move with the player, e.g. the Fragment ghost (it follows with a speed cap and would otherwise lag behind across a teleport).")]
    public List<GameObject> additionalObjects = new List<GameObject>();

    [Header("Screen Transition")]
    public bool useScreenTransition = true;
    public float transitionDelay = 0.5f;

    [Header("Cooldown")]
    public float teleportCooldown = 2f;

    [Header("Effects")]
    public GameObject teleportEffect;
    public float effectDuration = 1f;
    public AudioClip teleportSound;

    private bool isOnCooldown;

    // Set when a teleport DROPS the player inside this portal. It is not a timer: it is
    // "you have to leave before this counts again", cleared by OnTriggerExit2D. A timer
    // expires while the player is still standing inside, and OnTriggerEnter2D only fires on
    // the way IN, so the portal would sit armed but unreachable until he walked out and
    // back — which is exactly how leaving the house broke.
    private bool suppressedUntilExit;
    private float cooldownTimer;
    private bool playerInRange;
    private GameObject player;

    private void Update()
    {
        if (!isOnCooldown)
            return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            isOnCooldown = false;
            RefreshPrompt(); // cooldown just ended — re-arm if the player is still here
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        player = collision.gameObject;
        playerInRange = true;

        if (teleportOnTrigger && !isOnCooldown && !suppressedUntilExit)
            Teleport();
        else
            RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        playerInRange = false;
        suppressedUntilExit = false;   // he left; the portal counts again
        RefreshPrompt();
    }

    // Walking around INSIDE a portal has to keep working after the suppression clears,
    // otherwise a player who arrives inside one and never fully leaves is stuck.
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!teleportOnTrigger || isOnCooldown || suppressedUntilExit) return;
        if (!collision.CompareTag("Player")) return;

        player = collision.gameObject;
        playerInRange = true;
        Teleport();
    }

    private void OnDisable()
    {
        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    private void RefreshPrompt()
    {
        if (teleportOnTrigger)
            return;

        if (playerInRange && !isOnCooldown && !suppressedUntilExit)
            InteractButton.Instance?.SetInteraction(this, interactLabel, Teleport);
        else
            InteractButton.Instance?.ClearInteraction(this);
    }

    // Public so a UnityEvent (a puzzle solve, a dialogue) can trigger a teleport directly.
    public void Teleport()
    {
        if (destination == null)
        {
            Debug.LogError($"{name}: Teleporter has no destination set.", this);
            return;
        }

        if (isOnCooldown)
            return;

        GameObject target = player;

        if (useScreenTransition && ScreenTransition.Instance != null)
            StartCoroutine(TransitionThenTeleport(target));
        else
            ExecuteTeleport(target);
    }

    private IEnumerator TransitionThenTeleport(GameObject target)
    {
        StartCooldown();
        ScreenTransition.Instance.PlayTransition();

        yield return new WaitForSeconds(transitionDelay);

        if (Camera != null && OldCamera != null)
        {
            Camera.SetActive(true);
            OldCamera.SetActive(false);
        }

        ExecuteTeleport(target);
    }

    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = teleportCooldown;
    }

    private void ExecuteTeleport(GameObject target)
    {
        if (!isOnCooldown)
            StartCooldown();

        if (teleportEffect != null)
            Destroy(Instantiate(teleportEffect, transform.position, Quaternion.identity), effectDuration);

        if (teleportSound != null)
            AudioSource.PlayClipAtPoint(teleportSound, transform.position);

        // Measured BEFORE the move: reading it afterwards asks a collider that the physics
        // world has not caught up with yet, which answers with the position the player just
        // left — the reason the first attempt at this fix silently did nothing.
        Collider2D body = target != null ? target.GetComponent<Collider2D>() : null;
        Vector3 bodyOffset = Vector3.zero;
        Vector3 bodySize = Vector3.zero;
        if (body != null)
        {
            bodyOffset = body.bounds.center - target.transform.position;
            bodySize = body.bounds.size;
        }

        if (target != null)
            target.transform.position = destination.position;
        else
            Debug.LogWarning($"{name}: no player reference available for teleport.", this);

        // Pushes the move into the physics world now instead of at the next FixedUpdate, so
        // the trigger the player landed in is found this frame — before it can fire.
        Physics2D.SyncTransforms();

        foreach (var obj in additionalObjects)
        {
            if (obj != null)
                obj.transform.position = destination.position;
        }

        if (teleportEffect != null)
            Destroy(Instantiate(teleportEffect, destination.position, Quaternion.identity), effectDuration);

        // Landing inside ANOTHER portal's trigger must not bounce the player straight back
        // out. The exit inside Josh's house sits ~0.06 units below where the front door
        // drops you, so walking in put the player on top of the exit and teleported him
        // outside again — while the "you're inside" dialogue, fired by the same arrival,
        // carried on playing over the outdoor scene. Putting whatever you land in on
        // cooldown means a portal only ever fires when you actually walk INTO it.
        ArmArrivalCooldown(destination.position, bodyOffset, bodySize);

        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    private static void ArmArrivalCooldown(Vector3 arrival, Vector3 bodyOffset, Vector3 bodySize)
    {
        // Where the traveller's BODY ends up, not where its pivot does. A character's
        // collider is a small box at his feet, well below the transform, so a pivot that
        // clears a trigger by a hair still lands the feet inside it.
        bool hasBody = bodySize.sqrMagnitude > 0f;
        Bounds landed = new Bounds(arrival + bodyOffset, bodySize);

        foreach (TeleporterScript other in FindObjectsByType<TeleporterScript>(FindObjectsSortMode.None))
        {
            if (other == null) continue;

            foreach (Collider2D trigger in other.GetComponents<Collider2D>())
            {
                if (!trigger.isTrigger) continue;

                // The point covers a destination marker with no body to measure; the bounds
                // check is what catches a player whose feet clip the corner of a trigger his
                // pivot is clear of, which is exactly the 6-centimetre overlap here.
                bool landedInside = trigger.OverlapPoint(arrival)
                                 || (hasBody && trigger.bounds.Intersects(landed));

                if (!landedInside) continue;

                other.suppressedUntilExit = true;
                other.playerInRange = false;
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (destination == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(destination.position, 0.5f);
    }
}
