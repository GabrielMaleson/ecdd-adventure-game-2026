using System.Collections;
using UnityEngine;
using Yarn.Unity;

// The square_haze cutscene: Josh catches up to Haze in the town square. Haze and Josh
// walk to their marks, "square_haze" plays, and mid-conversation a Villager interrupts
// them (<<villagerenter>> below), then leaves again (<<villagerexit>>) once they're
// done — both wired as Yarn commands so their timing is driven by the dialogue lines
// themselves ("Villager: Josh! What are you doing just standing here?" etc.), not by a
// fixed delay guessed from outside the conversation.
public class SquareHazeCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string hazeTag = "Haze";

    [Header("Position References")]
    [SerializeField] private Transform hazeSquareTarget; // Where Haze stops in the square
    [SerializeField] private Transform joshSquareTarget;  // Where Josh stops, facing Haze

    [Header("Villager")]
    [SerializeField] private GameObject villagerNpc;
    [SerializeField] private Transform villagerEntryTarget; // Walked to on <<villagerenter>>
    [SerializeField] private Transform villagerExitTarget;  // Walked to on <<villagerexit>>

    [Header("Animation Settings")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float villagerWalkSpeed = 2f;

    [Header("Dialogue")]
    [SerializeField] private string startNode = "square_haze";

    private GameObject player;
    private GameObject haze;
    private PlayerController playerController;
    private DialogueRunner dialogueRunner;

    private bool isCutscenePlaying;
    private bool dialogueStarted;

    private static SquareHazeCutscene instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj;
            playerController = playerObj.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{playerTag}'!", this);
        }

        GameObject hazeObj = GameObject.FindGameObjectWithTag(hazeTag);
        if (hazeObj != null)
        {
            haze = hazeObj;
        }
        else
        {
            Debug.LogError($"No GameObject found with tag '{hazeTag}'!", this);
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
            if (dialogueRunner == null)
                Debug.LogError("DialogueRunner not found in scene!", this);
        }

        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);

        if (villagerNpc != null)
            villagerNpc.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

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

        gameObject.SetActive(true); // see HomeInsideCutscene for why this matters
        isCutscenePlaying = true;

        CutsceneTeleportGuard.DisableTeleporters();

        if (playerController != null)
            playerController.InputEnabled = false;

        StartCoroutine(CutsceneSequence());
    }

    private IEnumerator CutsceneSequence()
    {
        // Haze and Josh walk to their marks together.
        Coroutine hazeWalk = hazeSquareTarget != null ? StartCoroutine(WalkActor(haze, null, hazeSquareTarget.position, walkSpeed)) : null;
        Coroutine joshWalk = joshSquareTarget != null ? StartCoroutine(WalkActor(player, playerController, joshSquareTarget.position, walkSpeed)) : null;

        if (hazeWalk != null) yield return hazeWalk;
        if (joshWalk != null) yield return joshWalk;

        // Face each other once both have arrived.
        FaceEachOther();

        dialogueStarted = true;
        dialogueRunner.StartDialogue(startNode);

        while (dialogueStarted)
            yield return null;

        if (playerController != null)
            playerController.InputEnabled = true;

        CutsceneTeleportGuard.RestoreTeleporters();

        isCutscenePlaying = false;
    }

    private void FaceEachOther()
    {
        if (haze == null || player == null) return;

        SpriteRenderer hazeSprite = haze.GetComponent<SpriteRenderer>();
        if (hazeSprite != null)
        {
            Vector2 dir = (player.transform.position - haze.transform.position).normalized;
            hazeSprite.flipX = dir.x < 0;
        }

        if (playerController != null)
        {
            Vector2 dir = (haze.transform.position - player.transform.position).normalized;
            playerController.SetCutsceneMoveDirection(dir);
            playerController.SetCutsceneMoveDirection(Vector2.zero); // sets facing, then goes idle
        }
    }

    // Walks any actor to a position. Pass a PlayerController for the player (drives his
    // walk animation); pass null for Haze/NPCs (falls back to a plain sprite flip).
    private IEnumerator WalkActor(GameObject actor, PlayerController controller, Vector2 targetPos, float speed)
    {
        if (actor == null) yield break;

        Vector2 startPos = actor.transform.position;
        Vector2 dir = (targetPos - startPos).normalized;
        float distance = Vector2.Distance(startPos, targetPos);
        float journey = 0f;

        Rigidbody2D rb = actor.GetComponent<Rigidbody2D>();
        RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        SpriteRenderer sprite = controller == null ? actor.GetComponent<SpriteRenderer>() : null;

        while (journey < 1f)
        {
            journey += Time.deltaTime * speed / Mathf.Max(distance, 0.01f);
            journey = Mathf.Min(journey, 1f);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, journey);
            actor.transform.position = newPos;
            if (rb != null) rb.position = newPos;

            if (controller != null)
                controller.SetCutsceneMoveDirection(dir);
            else if (sprite != null)
                sprite.flipX = dir.x < 0;

            yield return null;
        }

        actor.transform.position = targetPos;
        if (rb != null)
        {
            rb.position = targetPos;
            rb.bodyType = originalBodyType;
            rb.linearVelocity = Vector2.zero;
        }

        if (controller != null)
            controller.SetCutsceneMoveDirection(Vector2.zero);
    }

    private void OnDialogueComplete()
    {
        dialogueStarted = false;
    }

    // --- Yarn commands: the Villager's entrance/exit, timed by the dialogue itself ---

    [YarnCommand("villagerenter")]
    public static void VillagerEnter()
    {
        instance?.StartCoroutine(instance.VillagerEnterRoutine());
    }

    [YarnCommand("villagerexit")]
    public static void VillagerExit()
    {
        instance?.StartCoroutine(instance.VillagerExitRoutine());
    }

    private IEnumerator VillagerEnterRoutine()
    {
        if (villagerNpc == null || villagerEntryTarget == null) yield break;

        villagerNpc.SetActive(true);
        yield return WalkActor(villagerNpc, null, villagerEntryTarget.position, villagerWalkSpeed);
    }

    private IEnumerator VillagerExitRoutine()
    {
        if (villagerNpc == null || villagerExitTarget == null) yield break;

        SpriteRenderer sprite = villagerNpc.GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.flipX = !sprite.flipX;

        yield return WalkActor(villagerNpc, null, villagerExitTarget.position, villagerWalkSpeed);

        villagerNpc.SetActive(false);
    }

    // Public method to skip cutscene (for testing) — resets the Villager back to
    // hidden. Haze is left alone (he's meant to keep wandering the world afterward,
    // skip or not).
    public void SkipCutscene()
    {
        StopAllCoroutines();
        isCutscenePlaying = false;
        dialogueStarted = false;

        if (playerController != null)
            playerController.InputEnabled = true;

        CutsceneTeleportGuard.RestoreTeleporters();

        if (villagerNpc != null)
            villagerNpc.SetActive(false);
    }
}
