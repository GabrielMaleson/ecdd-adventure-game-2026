using System.Collections;
using UnityEngine;
using Yarn.Unity;

public class HomeInsideCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string realHazeTag = "Haze";
    [SerializeField] private string fakeHazeTag = "FakeHaze";
    
    [Header("Position References")]
    [SerializeField] private Transform playerStartPosition;
    [SerializeField] private Transform hazeStartPosition;
    [SerializeField] private Transform tvPosition; // Where Haze goes to look at TV
    
    [Header("Animation Settings")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float lookAtTargetSpeed = 3f;
    
    [Header("Dialogue")]
    [SerializeField] private string startNode = "home_inside_first";
    
    private GameObject player;
    private GameObject realHaze;
    private GameObject fakeHaze;
    private PlayerController playerController;
    private DialogueRunner dialogueRunner;
    
    private bool isCutscenePlaying = false;
    private bool dialogueStarted = false;
    private bool isComplete = false;
    
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
        
        // Subscribe to dialogue events
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        }
        
        // Initially disable this cutscene
        gameObject.SetActive(false);
    }
    
    private void FindReferencesByTag()
    {
        // Find Player
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj;
            playerController = playerObj.GetComponent<PlayerController>();
        }
        
        // Find Real Haze
        GameObject hazeObj = GameObject.FindGameObjectWithTag(realHazeTag);
        if (hazeObj != null)
        {
            realHaze = hazeObj;
        }
        
        // Find Fake Haze (optional, might not exist inside)
        GameObject fakeHazeObj = GameObject.FindGameObjectWithTag(fakeHazeTag);
        if (fakeHazeObj != null)
        {
            fakeHaze = fakeHazeObj;
        }
    }
    
    private void ValidateReferences()
    {
        if (player == null) Debug.LogError("Player reference not set!");
        if (realHaze == null) Debug.LogError("Real Haze reference not set!");
        if (playerController == null) Debug.LogError("PlayerController reference not set!");
        if (dialogueRunner == null) Debug.LogError("DialogueRunner reference not set!");
    }
    
    private void OnDestroy()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }
    }
    
    public void StartCutscene(string nodeName = null)
    {
        if (isCutscenePlaying) return;
        
        isCutscenePlaying = true;
        
        // Disable player controls
        if (playerController != null)
        {
            playerController.InputEnabled = false;
        }
        
        // Position characters
        if (player != null && playerStartPosition != null)
        {
            player.transform.position = playerStartPosition.position;
            // Reset Rigidbody2D if it exists
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = playerStartPosition.position;
                rb.linearVelocity = Vector2.zero;
            }
        }
        
        if (realHaze != null && hazeStartPosition != null)
        {
            realHaze.transform.position = hazeStartPosition.position;
            Rigidbody2D rb = realHaze.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = hazeStartPosition.position;
                rb.linearVelocity = Vector2.zero;
            }
        }
        
        // Disable fake Haze if it exists
        if (fakeHaze != null)
        {
            fakeHaze.SetActive(false);
        }
        
        // Enable real Haze
        if (realHaze != null)
        {
            realHaze.SetActive(true);
        }
        
        // Start the dialogue
        string nodeToStart = string.IsNullOrEmpty(nodeName) ? startNode : nodeName;
        dialogueRunner.StartDialogue(nodeToStart);
        dialogueStarted = true;
        
        StartCoroutine(InsideCutsceneSequence());
    }
    
    private IEnumerator InsideCutsceneSequence()
    {
        // Wait for dialogue to complete
        while (dialogueStarted)
        {
            yield return null;
        }
        
        // After dialogue, Haze walks to TV
        yield return StartCoroutine(HazeWalksToTV());
        
        // Cutscene complete
        isComplete = true;
        isCutscenePlaying = false;
        
        // Re-enable player controls
        if (playerController != null)
        {
            playerController.InputEnabled = true;
        }
    }
    
    private IEnumerator HazeWalksToTV()
    {
        if (realHaze == null || tvPosition == null) yield break;
        
        Vector2 startPos = realHaze.transform.position;
        Vector2 targetPos = tvPosition.position;
        float journey = 0f;
        float distance = Vector2.Distance(startPos, targetPos);
        
        // Disable physics while we manually control
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
            
            // Flip sprite to face walking direction
            if (hazeSprite != null)
            {
                hazeSprite.flipX = walkDirection.x < 0;
            }
            
            yield return null;
        }
        
        // Snap to final position
        realHaze.transform.position = targetPos;
        if (rb != null)
        {
            rb.position = targetPos;
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    private void OnDialogueComplete()
    {
        dialogueStarted = false;
    }
}