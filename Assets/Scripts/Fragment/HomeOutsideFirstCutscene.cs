using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class HomeOutsideFirstCutscene : MonoBehaviour
{
    [Header("Trigger")]
    public bool oneShot = true;

    [Header("References")]
    public PlayerController player;
    public Transform haze;
    public SpriteRenderer hazeSpriteRenderer;
    public FragmentFollow hazeFollow;

    [Header("Dialogue")]
    public string dialogueName = "home_outside_first";

    [Header("Haze Entrance")]
    public Transform hazeFloatTarget;
    public float floatSpeed = 1.5f;
    public bool hazeFlipXOnArrival = true;

    [Header("Walk Inside")]
    public Transform houseEntrancePoint;
    public float walkSpeed = 2f;
    public float arriveThreshold = 0.1f;

    [Header("Teleport Inside")]
    public Transform playerInsideSpawn;
    public Transform hazeInsideSpawn;
    public bool useScreenTransition = true;
    public float transitionDelay = 0.5f;
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

        if (haze == null)
        {
            GameObject hazeObj = GameObject.FindGameObjectWithTag("Haze");
            if (hazeObj != null) haze = hazeObj.transform;
        }

        if (hazeSpriteRenderer == null && haze != null)
            hazeSpriteRenderer = haze.GetComponentInChildren<SpriteRenderer>();

        if (hazeFollow == null && haze != null)
            hazeFollow = haze.GetComponent<FragmentFollow>();

        playerRb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        hazeRb = haze != null ? haze.GetComponent<Rigidbody2D>() : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (oneShot && hasPlayed) return;

        StartCutscene();
    }

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