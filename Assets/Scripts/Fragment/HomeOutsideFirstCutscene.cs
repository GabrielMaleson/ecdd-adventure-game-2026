using UnityEngine;
using System.Collections;

// The MC's first encounter with the Fragment (Haze), triggered outside the house.
// Beats: Haze appears from behind the house, floats to a spot, flips to face the MC,
// they run the "home_outside_first" Yarn dialogue, then both walk to the door and get
// teleported inside to their interior spots. The player has no control for the whole
// sequence — driven start to finish by RunCutscene() below.
[RequireComponent(typeof(Collider2D))]
public class HomeOutsideFirstCutscene : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Only fire once — this is a one-time story beat.")]
    public bool oneShot = true;

    [Header("References")]
    [Tooltip("The MC. Auto-found by the \"Player\" tag if left empty.")]
    public PlayerController player;
    [Tooltip("Haze's root transform. Its GameObject is activated at the start of the cutscene.")]
    public Transform haze;
    [Tooltip("Auto-found on Haze if left empty.")]
    public SpriteRenderer hazeSpriteRenderer;
    [Tooltip("Optional — Haze's normal follow behaviour, disabled for the cutscene and restored after.")]
    public FragmentFollow hazeFollow;

    [Header("Dialogue")]
    public string dialogueName = "home_outside_first";

    [Header("Haze Entrance")]
    [Tooltip("Where Haze floats to before the conversation starts.")]
    public Transform hazeFloatTarget;
    public float floatSpeed = 1.5f;
    [Tooltip("FlipX value Haze's sprite is set to once it arrives (e.g. to face the MC).")]
    public bool hazeFlipXOnArrival = true;

    [Header("Walk Inside")]
    [Tooltip("Shared point outside the door both characters walk to after the conversation.")]
    public Transform houseEntrancePoint;
    public float walkSpeed = 2f;
    public float arriveThreshold = 0.1f;

    [Header("Teleport Inside")]
    public Transform playerInsideSpawn;
    public Transform hazeInsideSpawn;
    public bool useScreenTransition = true;
    public float transitionDelay = 0.5f;
    [Tooltip("Optional — swapped on during the teleport, matching TeleporterScript's camera handoff.")]
    public GameObject insideCamera;
    public GameObject outsideCamera;

    Rigidbody2D playerRb;
    Rigidbody2D hazeRb;
    bool hasPlayed;
    Coroutine cutsceneRoutine;

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.GetComponent<PlayerController>();
        }
        if (hazeSpriteRenderer == null && haze != null)
            hazeSpriteRenderer = haze.GetComponentInChildren<SpriteRenderer>();

        playerRb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        hazeRb   = haze   != null ? haze.GetComponent<Rigidbody2D>()   : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (oneShot && hasPlayed) return;

        StartCutscene();
    }

    // Public so it can also be fired from a UnityEvent or another script if needed.
    public void StartCutscene()
    {
        if (cutsceneRoutine != null) return;
        hasPlayed = true;
        cutsceneRoutine = StartCoroutine(RunCutscene());
    }

    IEnumerator RunCutscene()
    {
        if (player != null) player.InputEnabled = false;
        if (hazeFollow != null) hazeFollow.enabled = false;

        if (haze != null) haze.gameObject.SetActive(true);

        if (haze != null && hazeFloatTarget != null)
            yield return MoveTo(haze, hazeRb, hazeFloatTarget.position, floatSpeed);

        if (hazeSpriteRenderer != null)
            hazeSpriteRenderer.flipX = hazeFlipXOnArrival;

        yield return RunDialogue(dialogueName);

        if (houseEntrancePoint != null)
        {
            Coroutine playerWalk = player != null
                ? StartCoroutine(MoveTo(player.transform, playerRb, houseEntrancePoint.position, walkSpeed))
                : null;
            Coroutine hazeWalk = haze != null
                ? StartCoroutine(MoveTo(haze, hazeRb, houseEntrancePoint.position, walkSpeed))
                : null;

            if (playerWalk != null) yield return playerWalk;
            if (hazeWalk != null) yield return hazeWalk;
        }

        yield return TeleportInside();

        if (hazeFollow != null) hazeFollow.enabled = true;
        if (player != null) player.InputEnabled = true;

        cutsceneRoutine = null;
    }

    // Moves through the Rigidbody2D when one is present (same MovePosition/FixedUpdate
    // approach as PlayerController and WaypointWalker), otherwise falls back to the
    // transform directly — Haze, being a floating ghost, may not have a Rigidbody2D at all.
    IEnumerator MoveTo(Transform t, Rigidbody2D rb, Vector3 target, float speed)
    {
        while (Vector2.Distance(t.position, target) > arriveThreshold)
        {
            Vector2 next = Vector2.MoveTowards(t.position, target, speed * Time.fixedDeltaTime);
            if (rb != null) rb.MovePosition(next);
            else t.position = next;
            yield return new WaitForFixedUpdate();
        }

        if (rb != null) rb.MovePosition(target);
        else t.position = target;
    }

    IEnumerator RunDialogue(string name)
    {
        if (string.IsNullOrEmpty(name)) yield break;

        DialogueManager manager = DialogueManager.Instance;
        manager?.StartDialogue(name);

        Yarn.Unity.DialogueRunner runner = manager?.dialogueRunner;
        if (runner == null) yield break;

        while (runner.IsDialogueRunning)
            yield return null;
    }

    IEnumerator TeleportInside()
    {
        if (useScreenTransition && ScreenTransition.Instance != null)
        {
            ScreenTransition.Instance.PlayTransition();
            yield return new WaitForSeconds(transitionDelay);
        }

        if (insideCamera != null && outsideCamera != null)
        {
            insideCamera.SetActive(true);
            outsideCamera.SetActive(false);
        }

        if (player != null && playerInsideSpawn != null)
            player.TeleportTo(playerInsideSpawn.position);

        if (haze != null && hazeInsideSpawn != null)
        {
            haze.position = hazeInsideSpawn.position;
            if (hazeRb != null) hazeRb.position = hazeInsideSpawn.position;
        }
    }
}
