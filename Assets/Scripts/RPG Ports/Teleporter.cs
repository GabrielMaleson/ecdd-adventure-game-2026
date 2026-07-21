using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TeleporterScript : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform destination; // Where to teleport to
    public bool teleportOnTrigger = true; // Teleport when player enters trigger
    public KeyCode interactKey = KeyCode.Space; // Alternative: teleport on key press

    [Header("Teleport Targets")]
    public bool teleportPlayer = true; // Whether to teleport the player
    public bool teleportAllies = true; // Whether to teleport objects tagged "Ally"
    public GameObject Camera;
    public GameObject OldCamera;
    public List<GameObject> additionalObjects = new List<GameObject>(); // Specific objects to teleport

    [Header("Screen Transition")]
    public bool useScreenTransition = true; // Whether to use the screen transition effect
    public float transitionDelay = 0.5f; // Delay after transition before teleporting

    [Header("Cooldown")]
    public float teleportCooldown = 2f; // Time before player can teleport again
    private bool isOnCooldown = false;

    [Header("Effects")]
    public GameObject teleportEffect; // Optional effect to play at origin and destination
    public float effectDuration = 1f;
    public AudioClip teleportSound; // Optional sound effect

    [Header("Visual Feedback")]
    public SpriteRenderer spriteRenderer; // For animation/sprite change if needed
    public Sprite activeSprite; // Optional sprite to show when active
    public Sprite cooldownSprite; // Optional sprite to show during cooldown

    [Header("Interaction")]
    public GameObject interactionPopup; // Optional popup to show when player is nearby

    private bool playerInRange = false;
    private bool teleportPending = false; // Track if teleport is pending activation
    private GameObject player;
    private GameObject cachedPlayer; // Store player reference for teleport even if they leave trigger
    private List<GameObject> allies = new List<GameObject>();
    private Sprite originalSprite;
    private float cooldownTimer = 0f;

    private void Start()
    {
        // Store original sprite if using sprite change
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            originalSprite = spriteRenderer.sprite;

        // Hide interaction popup initially
        if (interactionPopup != null)
            interactionPopup.SetActive(false);
    }

    private void Update()
    {
        // Update cooldown timer
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                isOnCooldown = false;
                UpdateVisualState();
            }
        }

        // Check for input if teleport is triggered by key press
        if (!teleportOnTrigger && playerInRange && !isOnCooldown && Input.GetKeyDown(interactKey))
        {
            // Cache the player reference before they might leave
            cachedPlayer = player;
            teleportPending = true;
            PerformTeleport();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            player = collision.gameObject;
            playerInRange = true;

            // Show popup
            ShowPopup();

            // Auto-teleport if enabled and not on cooldown
            if (teleportOnTrigger && !isOnCooldown)
            {
                // Cache the player reference
                cachedPlayer = player;
                teleportPending = true;
                PerformTeleport();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInRange = false;

            // Don't clear player reference if teleport is pending
            if (!teleportPending)
            {
                player = null;
            }

            // Hide popup
            HidePopup();
        }
    }

    private void ShowPopup()
    {
        GameObject popup = GameObject.FindWithTag("Notification");
        if (popup != null)
        {
            SpriteRenderer spriteRend = popup.GetComponent<SpriteRenderer>();
            if (spriteRend != null)
            {
                Color color = spriteRend.color;
                color.a = 1f;
                spriteRend.color = color;
            }
        }

        // Update visual state
        UpdateVisualState();
    }

    private void HidePopup()
    {
        GameObject popup = GameObject.FindWithTag("Notification");
        if (popup != null)
        {
            SpriteRenderer spriteRend = popup.GetComponent<SpriteRenderer>();
            if (spriteRend != null)
            {
                Color color = spriteRend.color;
                color.a = 0f;
                spriteRend.color = color;
            }
        }

        // Only restore sprite if no teleport is pending
        if (!teleportPending && spriteRenderer != null && originalSprite != null)
        {
            spriteRenderer.sprite = originalSprite;
        }
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null) return;

        if (isOnCooldown && cooldownSprite != null)
        {
            spriteRenderer.sprite = cooldownSprite;
        }
        else if (!isOnCooldown && activeSprite != null && playerInRange)
        {
            spriteRenderer.sprite = activeSprite;
        }
        else
        {
            spriteRenderer.sprite = originalSprite;
        }
    }

    private void PerformTeleport()
    {
        if (destination == null)
        {
            Debug.LogError("Teleporter destination not set!");
            teleportPending = false; // Clear pending flag on error
            cachedPlayer = null;
            return;
        }

        if (isOnCooldown)
        {
            Debug.Log("Teleporter is on cooldown!");
            teleportPending = false; // Clear pending flag on cooldown
            cachedPlayer = null;
            return;
        }

        if (useScreenTransition && ScreenTransition.Instance != null)
        {
            // Use screen transition, then wait for delay, then teleport
            StartCoroutine(TransitionThenTeleport());
        }
        else
        {
            // No transition, teleport immediately
            ExecuteTeleport();
            teleportPending = false; // Clear pending flag after teleport
            cachedPlayer = null;
        }
    }

    private IEnumerator TransitionThenTeleport()
    {
        // Start cooldown immediately to prevent multiple triggers
        StartCooldown();

        // Play the screen transition
        if (ScreenTransition.Instance != null)
        {
            ScreenTransition.Instance.PlayTransition();
        }

        // Additional delay before teleporting
        yield return new WaitForSeconds(transitionDelay);

        // Switch cameras
        if (Camera != null && OldCamera != null)
        {
            Camera.SetActive(true);
            OldCamera.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Camera or OldCamera not assigned!");
        }

        // Now teleport
        ExecuteTeleport();
        teleportPending = false; // Clear pending flag after teleport
        cachedPlayer = null;
    }

    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = teleportCooldown;
        UpdateVisualState();
    }

    private void ExecuteTeleport()
    {
        // Use cached player if available, otherwise use current player
        GameObject targetPlayer = cachedPlayer != null ? cachedPlayer : player;

        // Play effect at origin
        if (teleportEffect != null)
        {
            GameObject effect = Instantiate(teleportEffect, transform.position, Quaternion.identity);
            Destroy(effect, effectDuration);
        }

        // Play sound
        if (teleportSound != null)
        {
            AudioSource.PlayClipAtPoint(teleportSound, transform.position);
        }

        // Teleport player
        if (teleportPlayer && targetPlayer != null)
        {
            targetPlayer.transform.position = destination.position;
            Debug.Log($"Player teleported to {destination.position}");
        }
        else if (teleportPlayer && targetPlayer == null)
        {
            Debug.LogWarning("No player reference available for teleport!");
        }

        // Teleport allies
        if (teleportAllies)
        {
            // Find all objects with "Ally" tag
            GameObject[] foundAllies = GameObject.FindGameObjectsWithTag("Ally");
            foreach (GameObject ally in foundAllies)
            {
                ally.transform.position = destination.position;
            }
        }

        // Teleport additional specified objects
        foreach (GameObject obj in additionalObjects)
        {
            if (obj != null)
            {
                obj.transform.position = destination.position;
            }
        }

        // Play effect at destination
        if (teleportEffect != null)
        {
            GameObject destEffect = Instantiate(teleportEffect, destination.position, Quaternion.identity);
            Destroy(destEffect, effectDuration);
        }

        Debug.Log($"Teleported to {destination.name}");

        // Clear references after teleport
        player = null;
        playerInRange = false;
    }

    // Public method to manually trigger teleport (for buttons or other scripts)
    public void Teleport()
    {
        if (player != null)
        {
            cachedPlayer = player;
        }
        teleportPending = true;
        PerformTeleport();
    }

    // Public method to check cooldown status
    public bool IsOnCooldown()
    {
        return isOnCooldown;
    }

    // Public method to get remaining cooldown time
    public float GetRemainingCooldown()
    {
        return isOnCooldown ? cooldownTimer : 0f;
    }

    // Draw gizmos to visualize teleporter connection
    private void OnDrawGizmosSelected()
    {
        if (destination != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, destination.position);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(destination.position, 0.5f);
        }
    }
}