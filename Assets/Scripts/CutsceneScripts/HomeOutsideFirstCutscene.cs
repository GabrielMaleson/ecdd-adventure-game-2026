using System.Collections;
using UnityEngine;
using Yarn.Unity;

public class HomeOutsideCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string realHazeTag = "Haze";
    [SerializeField] private string fakeHazeTag = "FakeHaze";
    
    [Header("Position References")]
    [SerializeField] private Transform hazeStartPosition; // Behind house
    [SerializeField] private Transform hazeFloatTarget; // Where Haze floats to for conversation
    [SerializeField] private Transform playerApproachPosition; // Where player approaches to
    [SerializeField] private Transform walkToHousePosition; // Where they walk together
    [SerializeField] private Transform teleportPlayerDestination; // Where player teleports after walking
    [SerializeField] private Transform teleportFakeHazeDestination; // Where fake Haze teleports after walking
    
    [Header("Inside House Cutscene")]
    [SerializeField] private GameObject insideHouseCutsceneObject; // The inside house cutscene GameObject
    [SerializeField] private string insideHouseStartNode = "home_inside_first";

    [Header("Actor Toggle")]
    [Tooltip("Assign this cutscene's own fake Haze here (and nothing from another cutscene). Used to show/hide it instead of reaching into fakeHaze directly.")]
    [SerializeField] private CutsceneObjectToggle hazeToggle;
    
    [Header("Animation Settings")]
    [SerializeField] private float floatDuration = 2f;
    [SerializeField] private float floatHeight = 0.5f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float playerApproachSpeed = 3f;
    [SerializeField] private float fadeDuration = 0.5f;
    
    [Header("Dialogue")]
    [SerializeField] private string startNode = "home_outside_first";
    
    private GameObject player;
    private GameObject realHaze;
    private GameObject fakeHaze;
    private PlayerController playerController;
    private DialogueRunner dialogueRunner;
    
    private bool isCutscenePlaying = false;
    private bool dialogueStarted = false;
    private bool isFading = false;
    
    private Vector2 hazeStartPos;
    private Vector2 hazeFloatPos;
    private Vector2 playerStartPos;
    private Vector2 playerApproachPos;
    private Vector2 walkTargetPos;
    
    private SpriteRenderer fadeSprite;
    private Color fadeColor;
    
    private enum CutsceneState
    {
        Idle,
        PlayerApproaching,
        HazeFloating,
        Dialogue,
        WalkingToHouse,
        FadingOut,
        Teleporting,
        FadingIn,
        Complete
    }
    
    private CutsceneState currentState = CutsceneState.Idle;
    
    private void Start()
    {
        // Find objects by tag
        FindReferencesByTag();
        
        // Validate references
        ValidateReferences();
        
        // Find DialogueRunner in scene if not assigned
        if (dialogueRunner == null)
        {
            dialogueRunner = FindObjectOfType<DialogueRunner>();
            if (dialogueRunner == null)
            {
                Debug.LogError("DialogueRunner not found in scene!");
            }
        }
        
        // Create fade sprite for transitions
        CreateFadeSprite();
        
        // Subscribe to dialogue events
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        }
        
        // Initially disable fake Haze and real Haze if not needed
        if (fakeHaze != null) fakeHaze.SetActive(false);
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
        
        fadeColor = fadeSprite.color;
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
        // Find Player
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj;
            playerController = playerObj.GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError($"PlayerController component not found on GameObject with tag '{playerTag}'!");
            }
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{playerTag}'!");
        }
        
        // Find Real Haze
        GameObject hazeObj = GameObject.FindGameObjectWithTag(realHazeTag);
        if (hazeObj != null)
        {
            realHaze = hazeObj;
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{realHazeTag}'!");
        }
        
        // Find Fake Haze
        GameObject fakeHazeObj = GameObject.FindGameObjectWithTag(fakeHazeTag);
        if (fakeHazeObj != null)
        {
            fakeHaze = fakeHazeObj;
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{fakeHazeTag}'!");
        }
    }
    
    private void ValidateReferences()
    {
        if (player == null) Debug.LogError("Player reference not set!");
        if (realHaze == null) Debug.LogError("Real Haze reference not set!");
        if (fakeHaze == null) Debug.LogError("Fake Haze reference not set!");
        if (playerController == null) Debug.LogError("PlayerController reference not set!");
    }
    
    private void OnDestroy()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == player && !isCutscenePlaying)
        {
            StartCutscene();
        }
    }
    
    public void StartCutscene()
    {
        if (isCutscenePlaying) return;
        
        isCutscenePlaying = true;
        currentState = CutsceneState.PlayerApproaching;

        CutsceneTeleportGuard.DisableTeleporters();

        // Disable real Haze
        if (realHaze != null)
        {
            realHaze.SetActive(false);
        }

        // Position fake Haze behind the house, but keep it HIDDEN for now — it only
        // becomes visible in HazeFloatOut(), which is the actual "comes out from
        // behind the house" reveal. Turning it on here made it visible for the whole
        // PlayerApproach beat first, which defeated the reveal (Haze's sorting order
        // is above the house's, so as soon as it's active it draws on top either way).
        if (fakeHaze != null && hazeStartPosition != null)
        {
            hazeStartPos = hazeStartPosition.position;
            fakeHaze.transform.position = hazeStartPos;

            // Face away initially (behind house).
            SpriteRenderer hazeSprite = fakeHaze.GetComponent<SpriteRenderer>();
            if (hazeSprite != null && player != null)
            {
                Vector2 directionAway = (hazeStartPos - (Vector2)player.transform.position).normalized;
                hazeSprite.flipX = directionAway.x < 0;
            }
        }

        // Disable player controls using InputEnabled property
        if (playerController != null)
        {
            playerController.InputEnabled = false;
        }

        // Store positions
        if (player != null)
        {
            playerStartPos = player.transform.position;
            playerApproachPos = playerApproachPosition != null ? (Vector2)playerApproachPosition.position : playerStartPos;
        }

        if (walkToHousePosition != null)
        {
            walkTargetPos = walkToHousePosition.position;
        }

        StartCoroutine(CutsceneSequence());
    }
    
    private IEnumerator CutsceneSequence()
    {
        // Step 1: Player approaches the area
        yield return StartCoroutine(PlayerApproach());
        
        // Step 2: Haze floats out from behind the house
        yield return StartCoroutine(HazeFloatOut());
        
        // Step 3: Start dialogue
        currentState = CutsceneState.Dialogue;
        dialogueStarted = true;
        dialogueRunner.StartDialogue(startNode);
        
        // Wait for dialogue to complete
        while (dialogueStarted)
        {
            yield return null;
        }
        
        // Step 4: Walk together to the house
        yield return StartCoroutine(WalkToHouse());
        
        // Step 5: Fade out
        yield return StartCoroutine(FadeOut());
        
        // Step 6: Teleport
        yield return StartCoroutine(TeleportCharacters());
        
        // Step 7: Fade in
        yield return StartCoroutine(FadeIn());
        
        // Step 8: Start inside house cutscene
        yield return StartCoroutine(StartInsideHouseCutscene());
        
        // Step 9: Cleanup
        currentState = CutsceneState.Complete;
        isCutscenePlaying = false;
        
        // Re-enable player controls (will be disabled again by inside cutscene)
        if (playerController != null)
        {
            playerController.InputEnabled = true;
        }

        // Disable fake Haze
        if (hazeToggle != null)
            hazeToggle.Deactivate();
        else if (fakeHaze != null)
            fakeHaze.SetActive(false);

        // Enable real Haze (it will be positioned by the inside cutscene)
        if (realHaze != null)
        {
            realHaze.SetActive(true);
        }

        CutsceneTeleportGuard.RestoreTeleporters();

        // Destroy this cutscene object or disable it
        gameObject.SetActive(false);
    }

    private IEnumerator PlayerApproach()
    {
        if (player == null || playerApproachPosition == null) yield break;

        Vector2 startPos = player.transform.position;
        Vector2 targetPos = playerApproachPosition.position;
        Vector2 moveDir = (targetPos - startPos).normalized;
        float journey = 0f;
        float distance = Vector2.Distance(startPos, targetPos);

        // Disable physics while we manually control position
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        while (journey < 1f)
        {
            journey += Time.deltaTime * playerApproachSpeed / Mathf.Max(distance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            // Move player
            Vector2 newPos = Vector2.Lerp(startPos, targetPos, journey);
            player.transform.position = newPos;

            // Keep Rigidbody2D in sync if it exists
            if (rb != null)
            {
                rb.position = newPos;
            }

            // Drives the walk animation + facing the same way normal movement does.
            if (playerController != null)
                playerController.SetCutsceneMoveDirection(moveDir);

            yield return null;
        }

        // Re-enable physics
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (playerController != null)
            playerController.SetCutsceneMoveDirection(Vector2.zero);
    }

    private IEnumerator HazeFloatOut()
    {
        if (fakeHaze == null || hazeFloatTarget == null) yield break;

        currentState = CutsceneState.HazeFloating;

        // The reveal: Haze becomes visible right as it starts moving, not any earlier.
        // (Its sorting order draws above the house regardless of position, so turning
        // it on any sooner — e.g. back in StartCutscene — showed it sitting in front of
        // the house the whole time the player was still approaching.)
        if (hazeToggle != null)
            hazeToggle.Activate();
        else
            fakeHaze.SetActive(true);

        Vector2 startPos = fakeHaze.transform.position;
        Vector2 targetPos = hazeFloatTarget.position;
        float journey = 0f;
        
        // Get sprite renderer for flipping
        SpriteRenderer hazeSprite = fakeHaze.GetComponent<SpriteRenderer>();
        
        while (journey < 1f)
        {
            journey += Time.deltaTime / floatDuration;
            journey = Mathf.Min(journey, 1f);
            
            // Float with slight bobbing
            float yOffset = Mathf.Sin(journey * Mathf.PI * 4) * floatHeight * (1 - journey);
            Vector2 currentPos = Vector2.Lerp(startPos, targetPos, journey);
            currentPos.y += yOffset;
            fakeHaze.transform.position = currentPos;
            
            // Flip sprite to face player
            if (hazeSprite != null && player != null)
            {
                Vector2 faceDir = (player.transform.position - fakeHaze.transform.position).normalized;
                hazeSprite.flipX = faceDir.x < 0;
            }
            
            yield return null;
        }
        
        // Final position
        fakeHaze.transform.position = targetPos;
        
        // Make sure Haze is facing the player
        if (hazeSprite != null && player != null)
        {
            Vector2 finalDir = (player.transform.position - fakeHaze.transform.position).normalized;
            hazeSprite.flipX = finalDir.x < 0;
        }
    }
    
    private IEnumerator WalkToHouse()
    {
        if (player == null || fakeHaze == null || walkToHousePosition == null) yield break;
        
        currentState = CutsceneState.WalkingToHouse;
        
        Vector2 playerStartPos = player.transform.position;
        Vector2 hazeStartPos = fakeHaze.transform.position;
        Vector2 targetPos = walkToHousePosition.position;
        
        // Calculate offset to keep characters side by side
        Vector2 walkDirection = (targetPos - playerStartPos).normalized;
        Vector2 sideOffset = Vector2.Perpendicular(walkDirection).normalized;
        
        // Position Haze slightly to the side
        Vector2 hazeTargetPos = targetPos + sideOffset * 2f;
        Vector2 playerTargetPos = targetPos - sideOffset * 2f;
        
        float journey = 0f;
        float walkDistance = Vector2.Distance(playerStartPos, playerTargetPos);
        
        // Disable physics while we manually control positions
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        Rigidbody2D hazeRb = fakeHaze.GetComponent<Rigidbody2D>();
        
        if (playerRb != null) playerRb.isKinematic = true;
        if (hazeRb != null) hazeRb.isKinematic = true;

        // Haze has no PlayerController, so it still just flips its own sprite.
        SpriteRenderer hazeSprite = fakeHaze.GetComponent<SpriteRenderer>();

        while (journey < 1f)
        {
            journey += Time.deltaTime * walkSpeed / Mathf.Max(walkDistance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            // Move both characters
            Vector2 newPlayerPos = Vector2.Lerp(playerStartPos, playerTargetPos, journey);
            Vector2 newHazePos = Vector2.Lerp(hazeStartPos, hazeTargetPos, journey);

            player.transform.position = newPlayerPos;
            fakeHaze.transform.position = newHazePos;

            // Keep Rigidbody2Ds in sync
            if (playerRb != null) playerRb.position = newPlayerPos;
            if (hazeRb != null) hazeRb.position = newHazePos;

            // Drives the player's walk animation + facing.
            if (playerController != null)
                playerController.SetCutsceneMoveDirection(walkDirection);

            if (hazeSprite != null)
            {
                hazeSprite.flipX = walkDirection.x < 0;
            }

            yield return null;
        }

        // Snap to final positions
        player.transform.position = playerTargetPos;
        fakeHaze.transform.position = hazeTargetPos;

        if (playerRb != null)
        {
            playerRb.position = playerTargetPos;
            playerRb.isKinematic = false;
        }
        if (hazeRb != null)
        {
            hazeRb.position = hazeTargetPos;
            hazeRb.isKinematic = false;
        }

        if (playerController != null)
            playerController.SetCutsceneMoveDirection(Vector2.zero);
    }
    
    private IEnumerator FadeOut()
    {
        currentState = CutsceneState.FadingOut;
        isFading = true;
        
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            fadeSprite.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        
        fadeSprite.color = new Color(0, 0, 0, 1);
        isFading = false;
    }
    
    private IEnumerator FadeIn()
    {
        currentState = CutsceneState.FadingIn;
        isFading = true;
        
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadeSprite.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        
        fadeSprite.color = new Color(0, 0, 0, 0);
        isFading = false;
    }
    
    private IEnumerator TeleportCharacters()
    {
        currentState = CutsceneState.Teleporting;
        
        // Get Rigidbody2D components if they exist
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        Rigidbody2D hazeRb = fakeHaze.GetComponent<Rigidbody2D>();
        
        // Teleport player
        if (teleportPlayerDestination != null && player != null)
        {
            Vector2 targetPos = teleportPlayerDestination.position;
            player.transform.position = targetPos;
            
            if (playerRb != null)
            {
                playerRb.position = targetPos;
                playerRb.linearVelocity = Vector2.zero;
            }
        }
        
        // Teleport fake Haze
        if (teleportFakeHazeDestination != null && fakeHaze != null)
        {
            Vector2 targetPos = teleportFakeHazeDestination.position;
            fakeHaze.transform.position = targetPos;
            
            if (hazeRb != null)
            {
                hazeRb.position = targetPos;
                hazeRb.linearVelocity = Vector2.zero;
            }
        }
        
        // Wait a frame for the teleport to register
        yield return null;
    }
    
    private IEnumerator StartInsideHouseCutscene()
    {
        if (insideHouseCutsceneObject != null)
        {
            // Find the inside house cutscene script and start it
            HomeInsideCutscene insideCutscene = insideHouseCutsceneObject.GetComponent<HomeInsideCutscene>();
            if (insideCutscene != null)
            {
                // Wait a frame for the fade to complete
                yield return null;
                
                // Start the inside cutscene
                insideCutscene.StartCutscene(insideHouseStartNode);
            }
            else
            {
                Debug.LogError("HomeInsideCutscene component not found on insideHouseCutsceneObject!");
            }
        }
        else
        {
            Debug.LogWarning("Inside house cutscene object not assigned!");
        }
    }
    
    private void OnDialogueComplete()
    {
        dialogueStarted = false;
    }
    
    // Public method to skip cutscene (for testing)
    public void SkipCutscene()
    {
        StopAllCoroutines();
        isCutscenePlaying = false;
        dialogueStarted = false;
        isFading = false;
        
        // Re-enable player controls
        if (playerController != null)
        {
            playerController.InputEnabled = true;
        }
        
        // Reset fade
        if (fadeSprite != null)
        {
            fadeSprite.color = new Color(0, 0, 0, 0);
        }
        
        // Get Rigidbody2D components
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        Rigidbody2D hazeRb = fakeHaze != null ? fakeHaze.GetComponent<Rigidbody2D>() : null;
        
        // Teleport to destinations if needed
        if (teleportPlayerDestination != null && player != null)
        {
            Vector2 targetPos = teleportPlayerDestination.position;
            player.transform.position = targetPos;
            if (playerRb != null)
            {
                playerRb.position = targetPos;
                playerRb.isKinematic = false;
                playerRb.linearVelocity = Vector2.zero;
            }
        }
        
        // Disable fake Haze, enable real Haze
        if (hazeToggle != null)
            hazeToggle.Deactivate();
        else if (fakeHaze != null)
            fakeHaze.SetActive(false);
        if (realHaze != null) realHaze.SetActive(true);

        CutsceneTeleportGuard.RestoreTeleporters();

        // Start inside cutscene immediately
        if (insideHouseCutsceneObject != null)
        {
            HomeInsideCutscene insideCutscene = insideHouseCutsceneObject.GetComponent<HomeInsideCutscene>();
            if (insideCutscene != null)
            {
                insideCutscene.StartCutscene(insideHouseStartNode);
            }
        }

        gameObject.SetActive(false);
    }
}