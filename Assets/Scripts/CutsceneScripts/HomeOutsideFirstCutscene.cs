using System.Collections;
using UnityEngine;
using Yarn.Unity;

// The full "first meet Haze" sequence, outside AND inside the house, in one continuous
// coroutine. Previously this was two separate components (an outside cutscene that
// teleported the player to a point just inside the door, then handed off to a second
// "inside" component that immediately teleported him AGAIN to its own start position) —
// that hand-off was what caused the visible double-teleport right after entering the
// house. One coroutine means there's exactly one "now you're inside" step.
public class HomeOutsideCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string realHazeTag = "Haze";

    [Header("Cutscene Actors")]
    [Tooltip("This cutscene's own Haze stand-in (a clone used only for this scripted reveal, kept hidden until it's needed). Assigned directly rather than by tag — each cutscene that needs one gets its own clone, so a shared tag would collide between them.")]
    [SerializeField] private GameObject cutsceneHaze;

    [Header("Outside — Position References")]
    [SerializeField] private Transform hazeStartPosition; // Behind the house, hidden
    [SerializeField] private Transform hazeFloatTarget;    // Where Haze floats to for the conversation
    [SerializeField] private Transform playerApproachPosition;

    [Header("Walking To The House")]
    [Tooltip("First leg: both characters walk here together (general approach toward the house).")]
    [SerializeField] private Transform houseApproachPosition;
    [Tooltip("Second leg: from the approach point, both then walk here — the doorway itself — before fading inside.")]
    [SerializeField] private Transform houseEntrancePosition;

    [Header("Inside — Position References")]
    [SerializeField] private Transform insidePlayerPosition;
    [SerializeField] private Transform insideHazePosition;
    [SerializeField] private Transform tvPosition; // Where Haze goes to look at TV

    [Header("Animation Settings")]
    [SerializeField] private float floatDuration = 2f;
    [SerializeField] private float floatHeight = 0.5f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float playerApproachSpeed = 3f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Dialogue")]
    [SerializeField] private string outsideStartNode = "home_outside_first";
    [SerializeField] private string insideStartNode = "home_inside_first";

    private GameObject player;
    private GameObject realHaze;
    private PlayerController playerController;
    private DialogueRunner dialogueRunner;

    private bool isCutscenePlaying;
    private bool dialogueStarted;

    private SpriteRenderer fadeSprite;

    private void Start()
    {
        FindReferencesByTag();
        ValidateReferences();

        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
            if (dialogueRunner == null)
                Debug.LogError("DialogueRunner not found in scene!", this);
        }

        CreateFadeSprite();

        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);

        if (cutsceneHaze != null) cutsceneHaze.SetActive(false);
        if (realHaze != null) realHaze.SetActive(true);
    }

    private void CreateFadeSprite()
    {
        GameObject fadeObj = new GameObject("FadeSprite");
        fadeObj.transform.SetParent(transform);
        fadeObj.transform.position = Vector3.zero;

        fadeSprite = fadeObj.AddComponent<SpriteRenderer>();
        fadeSprite.sprite = CreateWhiteTexture();
        fadeSprite.color = new Color(0, 0, 0, 0);
        fadeSprite.sortingOrder = 9999; // High sorting order to be on top

        // Make it cover the whole screen
        fadeObj.transform.localScale = new Vector3(100, 100, 1);
    }

    private Sprite CreateWhiteTexture()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    private void FindReferencesByTag()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj;
            playerController = playerObj.GetComponent<PlayerController>();
            if (playerController == null)
                Debug.LogError($"PlayerController component not found on GameObject with tag '{playerTag}'!", this);
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{playerTag}'!", this);
        }

        GameObject hazeObj = GameObject.FindGameObjectWithTag(realHazeTag);
        if (hazeObj != null)
            realHaze = hazeObj;
        else
            Debug.LogError($"No GameObject found with tag '{realHazeTag}'!", this);
    }

    private void ValidateReferences()
    {
        if (player == null) Debug.LogError("Player reference not set!", this);
        if (realHaze == null) Debug.LogError("Real Haze reference not set!", this);
        if (cutsceneHaze == null) Debug.LogError("Cutscene Haze reference not assigned!", this);
        if (playerController == null) Debug.LogError("PlayerController reference not set!", this);
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == player && !isCutscenePlaying)
            StartCutscene();
    }

    public void StartCutscene()
    {
        if (isCutscenePlaying) return;

        isCutscenePlaying = true;
        CutsceneTeleportGuard.DisableTeleporters();

        if (realHaze != null)
            realHaze.SetActive(false);

        // Position cutsceneHaze behind the house, but keep it HIDDEN — HazeFloatOut()
        // is what reveals it, right as it starts moving.
        if (cutsceneHaze != null && hazeStartPosition != null)
        {
            cutsceneHaze.transform.position = hazeStartPosition.position;

            SpriteRenderer hazeSprite = cutsceneHaze.GetComponent<SpriteRenderer>();
            if (hazeSprite != null && player != null)
            {
                Vector2 directionAway = ((Vector2)hazeStartPosition.position - (Vector2)player.transform.position).normalized;
                hazeSprite.flipX = directionAway.x < 0;
            }
        }

        if (playerController != null)
            playerController.InputEnabled = false;

        StartCoroutine(CutsceneSequence());
    }

    private IEnumerator CutsceneSequence()
    {
        yield return StartCoroutine(PlayerApproach());
        yield return StartCoroutine(HazeFloatOut());

        yield return StartCoroutine(RunDialogue(outsideStartNode));

        yield return StartCoroutine(WalkToHouse());

        yield return StartCoroutine(FadeOut());
        EnterHouse();
        yield return StartCoroutine(FadeIn());

        yield return StartCoroutine(RunDialogue(insideStartNode));

        yield return StartCoroutine(HazeWalksToTV());

        // Cleanup
        isCutscenePlaying = false;

        if (playerController != null)
            playerController.InputEnabled = true;

        if (cutsceneHaze != null)
            cutsceneHaze.SetActive(false);

        CutsceneTeleportGuard.RestoreTeleporters();

        gameObject.SetActive(false);
    }

    private IEnumerator RunDialogue(string node)
    {
        if (dialogueRunner == null || string.IsNullOrEmpty(node)) yield break;

        dialogueStarted = true;
        dialogueRunner.StartDialogue(node);

        while (dialogueStarted)
            yield return null;
    }

    private void OnDialogueComplete()
    {
        dialogueStarted = false;
    }

    private IEnumerator PlayerApproach()
    {
        if (player == null || playerApproachPosition == null) yield break;

        Vector2 startPos = player.transform.position;
        Vector2 targetPos = playerApproachPosition.position;
        Vector2 moveDir = (targetPos - startPos).normalized;
        float journey = 0f;
        float distance = Vector2.Distance(startPos, targetPos);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.isKinematic = true;

        while (journey < 1f)
        {
            journey += Time.deltaTime * playerApproachSpeed / Mathf.Max(distance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, journey);
            player.transform.position = newPos;
            if (rb != null) rb.position = newPos;

            if (playerController != null)
                playerController.SetCutsceneMoveDirection(moveDir);

            yield return null;
        }

        if (rb != null) rb.isKinematic = false;

        if (playerController != null)
            playerController.SetCutsceneMoveDirection(Vector2.zero);
    }

    private IEnumerator HazeFloatOut()
    {
        if (cutsceneHaze == null || hazeFloatTarget == null) yield break;

        // The reveal: Haze becomes visible right as it starts moving, not any earlier
        // (its sorting order draws above the house regardless of position, so turning
        // it on any sooner would show it sitting in front of the house too early).
        cutsceneHaze.SetActive(true);

        Vector2 startPos = cutsceneHaze.transform.position;
        Vector2 targetPos = hazeFloatTarget.position;
        float journey = 0f;

        SpriteRenderer hazeSprite = cutsceneHaze.GetComponent<SpriteRenderer>();

        while (journey < 1f)
        {
            journey += Time.deltaTime / floatDuration;
            journey = Mathf.Min(journey, 1f);

            float yOffset = Mathf.Sin(journey * Mathf.PI * 4) * floatHeight * (1 - journey);
            Vector2 currentPos = Vector2.Lerp(startPos, targetPos, journey);
            currentPos.y += yOffset;
            cutsceneHaze.transform.position = currentPos;

            if (hazeSprite != null && player != null)
            {
                Vector2 faceDir = (player.transform.position - cutsceneHaze.transform.position).normalized;
                hazeSprite.flipX = faceDir.x < 0;
            }

            yield return null;
        }

        cutsceneHaze.transform.position = targetPos;

        if (hazeSprite != null && player != null)
        {
            Vector2 finalDir = (player.transform.position - cutsceneHaze.transform.position).normalized;
            hazeSprite.flipX = finalDir.x < 0;
        }
    }

    // Two legs: walk to the general approach point together, then funnel through the
    // doorway itself — better than a single straight-line walk into the house.
    private IEnumerator WalkToHouse()
    {
        if (houseApproachPosition != null)
            yield return StartCoroutine(WalkBothTo(houseApproachPosition.position, sideBySide: true));

        if (houseEntrancePosition != null)
            yield return StartCoroutine(WalkBothTo(houseEntrancePosition.position, sideBySide: false));
    }

    private IEnumerator WalkBothTo(Vector2 targetPos, bool sideBySide)
    {
        if (player == null || cutsceneHaze == null) yield break;

        Vector2 playerStart = player.transform.position;
        Vector2 hazeStart = cutsceneHaze.transform.position;
        Vector2 walkDirection = (targetPos - playerStart).normalized;

        Vector2 hazeTarget = targetPos;
        Vector2 playerTarget = targetPos;
        if (sideBySide)
        {
            Vector2 sideOffset = Vector2.Perpendicular(walkDirection).normalized;
            hazeTarget = targetPos + sideOffset * 2f;
            playerTarget = targetPos - sideOffset * 2f;
        }

        float journey = 0f;
        float walkDistance = Vector2.Distance(playerStart, playerTarget);

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        Rigidbody2D hazeRb = cutsceneHaze.GetComponent<Rigidbody2D>();
        if (playerRb != null) playerRb.isKinematic = true;
        if (hazeRb != null) hazeRb.isKinematic = true;

        SpriteRenderer hazeSprite = cutsceneHaze.GetComponent<SpriteRenderer>();

        while (journey < 1f)
        {
            journey += Time.deltaTime * walkSpeed / Mathf.Max(walkDistance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            Vector2 newPlayerPos = Vector2.Lerp(playerStart, playerTarget, journey);
            Vector2 newHazePos = Vector2.Lerp(hazeStart, hazeTarget, journey);

            player.transform.position = newPlayerPos;
            cutsceneHaze.transform.position = newHazePos;

            if (playerRb != null) playerRb.position = newPlayerPos;
            if (hazeRb != null) hazeRb.position = newHazePos;

            if (playerController != null)
                playerController.SetCutsceneMoveDirection(walkDirection);

            if (hazeSprite != null)
                hazeSprite.flipX = walkDirection.x < 0;

            yield return null;
        }

        player.transform.position = playerTarget;
        cutsceneHaze.transform.position = hazeTarget;

        if (playerRb != null)
        {
            playerRb.position = playerTarget;
            playerRb.isKinematic = false;
        }
        if (hazeRb != null)
        {
            hazeRb.position = hazeTarget;
            hazeRb.isKinematic = false;
        }

        if (playerController != null)
            playerController.SetCutsceneMoveDirection(Vector2.zero);
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            fadeSprite.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        fadeSprite.color = new Color(0, 0, 0, 1);
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadeSprite.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        fadeSprite.color = new Color(0, 0, 0, 0);
    }

    // Swaps from the outside cutscene's Haze clone to the real Haze and places both
    // characters inside — the ONE "now you're inside" step (replacing what used to be
    // two separate teleports from two separate components).
    private void EnterHouse()
    {
        if (insidePlayerPosition != null && player != null)
        {
            player.transform.position = insidePlayerPosition.position;
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.position = insidePlayerPosition.position;
                playerRb.linearVelocity = Vector2.zero;
            }
        }

        if (cutsceneHaze != null)
            cutsceneHaze.SetActive(false);

        if (realHaze != null)
        {
            if (insideHazePosition != null)
            {
                realHaze.transform.position = insideHazePosition.position;
                Rigidbody2D hazeRb = realHaze.GetComponent<Rigidbody2D>();
                if (hazeRb != null)
                {
                    hazeRb.position = insideHazePosition.position;
                    hazeRb.linearVelocity = Vector2.zero;
                }
            }
            realHaze.SetActive(true);
        }
    }

    private IEnumerator HazeWalksToTV()
    {
        if (realHaze == null || tvPosition == null) yield break;

        Vector2 startPos = realHaze.transform.position;
        Vector2 targetPos = tvPosition.position;
        float journey = 0f;
        float distance = Vector2.Distance(startPos, targetPos);

        Rigidbody2D rb = realHaze.GetComponent<Rigidbody2D>();
        if (rb != null) rb.isKinematic = true;

        SpriteRenderer hazeSprite = realHaze.GetComponent<SpriteRenderer>();
        Vector2 walkDirection = (targetPos - startPos).normalized;

        while (journey < 1f)
        {
            journey += Time.deltaTime * walkSpeed / Mathf.Max(distance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, journey);
            realHaze.transform.position = newPos;
            if (rb != null) rb.position = newPos;

            if (hazeSprite != null)
                hazeSprite.flipX = walkDirection.x < 0;

            yield return null;
        }

        realHaze.transform.position = targetPos;
        if (rb != null)
        {
            rb.position = targetPos;
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }
    }

    // Public method to skip cutscene (for testing) — snaps straight to the inside state.
    public void SkipCutscene()
    {
        StopAllCoroutines();
        isCutscenePlaying = false;
        dialogueStarted = false;

        if (playerController != null)
            playerController.InputEnabled = true;

        if (fadeSprite != null)
            fadeSprite.color = new Color(0, 0, 0, 0);

        EnterHouse();

        CutsceneTeleportGuard.RestoreTeleporters();

        gameObject.SetActive(false);
    }
}
