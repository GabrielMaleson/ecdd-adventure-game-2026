using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// A portal: walking in either teleports immediately (teleportOnTrigger) or arms an
// InteractButton prompt and waits for the player to click it.
[RequireComponent(typeof(Collider2D))]
public class TeleporterScript : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform destination;
    public bool teleportOnTrigger = true;

    [Tooltip("Only used when Teleport On Trigger is off — shown on the Interact Button while in range; click it to teleport.")]
    public string interactLabel = "Teleport";

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
    private float cooldownTimer;
    private bool playerInRange;
    private bool promptShown;
    private bool subscribedToInteractButton;
    private GameObject player;

    private void Update()
    {
        // InteractButton is a scene singleton set up in its own Awake(); if this object's
        // Update runs before that Awake happens, Instance can still be null on the very
        // first frames, so keep retrying until the subscription actually takes.
        if (!subscribedToInteractButton && InteractButton.Instance != null)
        {
            InteractButton.Instance.OnPressed += HandleInteractButtonPressed;
            subscribedToInteractButton = true;
        }

        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
                isOnCooldown = false;
        }

        if (teleportOnTrigger || !playerInRange)
            return;

        bool canUse = !isOnCooldown;
        if (canUse != promptShown)
        {
            InteractButton.Instance?.SetLabel(canUse ? interactLabel : string.Empty);
            promptShown = canUse;
        }
    }

    // OnPressed fires for every interact-button press in the game, so only act on it
    // while OUR prompt is the one actually showing.
    private void HandleInteractButtonPressed()
    {
        if (teleportOnTrigger || !playerInRange || !promptShown)
            return;

        Teleport();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        player = collision.gameObject;
        playerInRange = true;

        if (teleportOnTrigger && !isOnCooldown)
            Teleport();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        playerInRange = false;
        HidePrompt();
    }

    private void OnDisable()
    {
        if (subscribedToInteractButton && InteractButton.Instance != null)
        {
            InteractButton.Instance.OnPressed -= HandleInteractButtonPressed;
            subscribedToInteractButton = false;
        }

        playerInRange = false;
        HidePrompt();
    }

    private void HidePrompt()
    {
        if (!promptShown)
            return;

        InteractButton.Instance?.SetLabel(string.Empty);
        promptShown = false;
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

        if (target != null)
            target.transform.position = destination.position;
        else
            Debug.LogWarning($"{name}: no player reference available for teleport.", this);

        foreach (var obj in additionalObjects)
        {
            if (obj != null)
                obj.transform.position = destination.position;
        }

        if (teleportEffect != null)
            Destroy(Instantiate(teleportEffect, destination.position, Quaternion.identity), effectDuration);

        playerInRange = false;
        HidePrompt();
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
