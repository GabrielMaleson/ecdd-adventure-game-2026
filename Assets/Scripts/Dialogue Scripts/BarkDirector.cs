using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// One per scene. The switchboard between the .yarn files and every CharacterDialogue
// in the world, so bark writing can live in .yarn with the rest of the script instead
// of being typed into prefabs.
//
// It owns three things:
//   1. the id -> CharacterDialogue registry (characters register themselves)
//   2. the "is a real conversation happening" flag every idle chatterer checks
//   3. the SECOND DialogueRunner used for bark conversations
//
// Why a second runner: the main runner (DialogueManager.dialogueRunner) drives the
// blocking dialogue box. A bark conversation is the same Yarn content presented
// differently, and one runner can only run one thing at a time — so bark nodes get
// their own runner with only a BarkPresenter on it. That also means NO node header or
// naming convention is needed to mark a node as bark-style: whichever runner starts
// the node decides how it looks.
public class BarkDirector : MonoBehaviour
{
    [Header("Runners")]
    [Tooltip("The blocking dialogue runner — the one inside DialogueManager. Auto-found if left empty.")]
    public DialogueRunner mainRunner;

    [Tooltip("A SECOND DialogueRunner, on its own GameObject, whose only Dialogue Presenter is a " +
             "BarkPresenter. Nodes started through RunConversation() play on this one.")]
    public DialogueRunner barkRunner;

    [Header("Bark Sets")]
    [Tooltip("Every BarkSet the .yarn can name in <<barkset id SetName>>. Looked up by asset name.")]
    public List<BarkSet> barkSets = new List<BarkSet>();

    [Header("Behaviour")]
    [Tooltip("Silence idle chatter while the blocking dialogue box is open.")]
    public bool suppressDuringBlockingDialogue = true;

    [Tooltip("Silence idle chatter while a bark conversation is playing, so an overheard " +
             "exchange isn't buried under ambient noise.")]
    public bool suppressDuringBarkConversation = true;

    [Tooltip("InteractDialogue draws its text above the PLAYER, same spot MC barks use, and the two " +
             "systems can't see each other. Ducking MC barks on interact keeps them from overlapping.")]
    public bool duckPlayerBarkOnInteract = true;

    private static BarkDirector instance;
    public static BarkDirector Instance => instance;

    // Keyed lowercase so <<bark Marcus ...>>, <<bark marcus ...>> and a .yarn character
    // name of "Marcus" all land on the same character.
    private readonly Dictionary<string, CharacterDialogue> speakers =
        new Dictionary<string, CharacterDialogue>();

    private Transform playerTransform;

    // Static so CharacterDialogue can ask without a null-check dance every frame.
    // Defaults to "nothing is happening" when there is no director in the scene, which
    // keeps a director-less scene behaving exactly like it did before.
    public static bool DialogueInProgress
    {
        get
        {
            if (instance == null) return false;
            return instance.IsBlockingDialogueRunning || instance.IsBarkConversationRunning;
        }
    }

    public static Transform PlayerTransform
    {
        get
        {
            if (instance == null) return null;
            if (instance.playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                instance.playerTransform = p != null ? p.transform : null;
            }
            return instance.playerTransform;
        }
    }

    public bool IsBlockingDialogueRunning =>
        suppressDuringBlockingDialogue && mainRunner != null && mainRunner.IsDialogueRunning;

    public bool IsBarkConversationRunning =>
        suppressDuringBarkConversation && barkRunner != null && barkRunner.IsDialogueRunning;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"{name}: a second BarkDirector was found — destroying this one.", this);
            Destroy(this);
            return;
        }
        instance = this;

        if (mainRunner == null && DialogueManager.Instance != null)
            mainRunner = DialogueManager.Instance.dialogueRunner;

        if (barkRunner != null && barkRunner == mainRunner)
        {
            Debug.LogError($"{name}: Bark Runner and Main Runner are the SAME DialogueRunner. " +
                           "Barks need their own runner or they will fight with the dialogue box.", this);
            barkRunner = null;
        }
    }

    private void OnEnable()
    {
        if (duckPlayerBarkOnInteract && InteractButton.Instance != null)
            InteractButton.Instance.OnPressed += OnInteractPressed;

        if (mainRunner != null)
            mainRunner.onDialogueStart?.AddListener(OnBlockingDialogueStarted);
    }

    private void OnDisable()
    {
        if (InteractButton.Instance != null)
            InteractButton.Instance.OnPressed -= OnInteractPressed;

        if (mainRunner != null)
            mainRunner.onDialogueStart?.RemoveListener(OnBlockingDialogueStarted);

        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        // InteractButton is a scene singleton assigned in its own Awake, which may run
        // after ours — same retry reason TeleporterScript has.
        if (duckPlayerBarkOnInteract && InteractButton.Instance != null)
        {
            InteractButton.Instance.OnPressed -= OnInteractPressed;
            InteractButton.Instance.OnPressed += OnInteractPressed;
        }

        // Characters register themselves in their own OnEnable, which can run BEFORE this
        // director's Awake — those registrations hit a null instance and are lost. One
        // sweep here picks up everyone who was too early. Re-registering is harmless.
        foreach (CharacterDialogue speaker in FindObjectsByType<CharacterDialogue>(FindObjectsSortMode.None))
            Register(speaker);
    }

    // ---------------------------------------------------------------- registry

    public static void Register(CharacterDialogue speaker)
    {
        if (instance == null || speaker == null || string.IsNullOrEmpty(speaker.barkId))
            return;

        instance.speakers[speaker.barkId.ToLowerInvariant()] = speaker;
    }

    public static void Unregister(CharacterDialogue speaker)
    {
        if (instance == null || speaker == null || string.IsNullOrEmpty(speaker.barkId))
            return;

        string key = speaker.barkId.ToLowerInvariant();
        if (instance.speakers.TryGetValue(key, out CharacterDialogue registered) && registered == speaker)
            instance.speakers.Remove(key);
    }

    public CharacterDialogue Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        speakers.TryGetValue(id.ToLowerInvariant(), out CharacterDialogue speaker);
        return speaker;
    }

    // ---------------------------------------------------------------- yarn commands

    // Static so [YarnCommand] auto-registers them globally rather than needing a
    // GameObject name as the first argument in every call.

    // <<bark marcus "Cara, essa névoa tá pior hoje.">>
    [YarnCommand("bark")]
    public static void BarkCommand(string id, string line)
    {
        Bark(id, line, BarkPriority.Scripted);
    }

    // <<barkset marcus Marcus_MistRising>>  — swaps that character's idle lines.
    // <<barkset marcus none>>               — back to the lines typed on the prefab.
    [YarnCommand("barkset")]
    public static void BarkSetCommand(string id, string setName)
    {
        if (instance == null) return;

        CharacterDialogue speaker = instance.Find(id);
        if (speaker == null)
        {
            Debug.LogWarning($"<<barkset>>: no character registered with bark id '{id}'.");
            return;
        }

        if (string.IsNullOrEmpty(setName) || setName.Equals("none", System.StringComparison.OrdinalIgnoreCase))
        {
            speaker.SetBarkSet(null);
            return;
        }

        BarkSet set = instance.barkSets.Find(
            s => s != null && s.name.Equals(setName, System.StringComparison.OrdinalIgnoreCase));

        if (set == null)
        {
            Debug.LogWarning($"<<barkset>>: no BarkSet named '{setName}' in the BarkDirector's Bark Sets list.");
            return;
        }

        speaker.SetBarkSet(set);
    }

    // <<barkstop marcus>> — clears whatever that character is saying and shuts up its chatter.
    [YarnCommand("barkstop")]
    public static void BarkStopCommand(string id)
    {
        if (instance == null) return;

        CharacterDialogue speaker = instance.Find(id);
        if (speaker == null) return;

        speaker.HideDialogue();
        speaker.StopRandomDialogue();
    }

    // <<barkresume marcus>>
    [YarnCommand("barkresume")]
    public static void BarkResumeCommand(string id)
    {
        if (instance == null) return;
        instance.Find(id)?.StartRandomDialogue();
    }

    // <<barkconvo overheard_marcus_erika>> — runs a whole node as floating barks,
    // without the dialogue box and without freezing the player.
    [YarnCommand("barkconvo")]
    public static void BarkConversationCommand(string nodeName)
    {
        RunConversation(nodeName);
    }

    // ---------------------------------------------------------------- api

    public static void Bark(string id, string line, BarkPriority priority = BarkPriority.Scripted)
    {
        if (instance == null)
        {
            Debug.LogWarning("BarkDirector.Bark called but there is no BarkDirector in the scene.");
            return;
        }

        CharacterDialogue speaker = instance.Find(id);
        if (speaker == null)
        {
            Debug.LogWarning($"BarkDirector.Bark: no character registered with bark id '{id}'.");
            return;
        }

        speaker.Show(line, priority);
    }

    // Starts a node on the bark runner. Returns false when it couldn't start, so
    // callers can fall back to the blocking runner if they want to.
    public static bool RunConversation(string nodeName)
    {
        if (instance == null || instance.barkRunner == null)
        {
            Debug.LogWarning($"BarkDirector: no Bark Runner assigned — can't run '{nodeName}' as a bark conversation.");
            return false;
        }

        // The dialogue box always wins. Starting an overheard exchange under it would
        // put two conversations on screen at once.
        if (instance.mainRunner != null && instance.mainRunner.IsDialogueRunning)
            return false;

        if (instance.barkRunner.IsDialogueRunning)
            return false;

        // Deliberately not awaited — the node plays out on its own while the game continues.
        _ = instance.barkRunner.StartDialogue(nodeName);
        return true;
    }

    public static void StopConversation()
    {
        if (instance == null || instance.barkRunner == null) return;
        if (!instance.barkRunner.IsDialogueRunning) return;

        _ = instance.barkRunner.Stop();
    }

    // Reads a Yarn variable ($YarnTalkedElder) off the main runner's storage, which is
    // where the story flags actually live. Used by BarkSet conditions.
    public static bool GetYarnBool(string variableName)
    {
        if (instance == null || instance.mainRunner == null || string.IsNullOrEmpty(variableName))
            return false;

        if (!variableName.StartsWith("$"))
            variableName = "$" + variableName;

        var storage = instance.mainRunner.VariableStorage;
        if (storage == null) return false;

        return storage.TryGetValue<bool>(variableName, out bool value) && value;
    }

    // ---------------------------------------------------------------- reactions

    private void OnBlockingDialogueStarted()
    {
        // A bark conversation caught mid-exchange by a real one is abandoned, not queued —
        // its lines belonged to a moment that has now been replaced.
        StopConversation();
    }

    private void OnInteractPressed()
    {
        // InteractDialogue is about to draw above the player. Clear the MC's bark so the
        // two don't stack in the same spot.
        if (!duckPlayerBarkOnInteract) return;

        Transform player = PlayerTransform;
        if (player == null) return;

        CharacterDialogue playerBark = player.GetComponentInChildren<CharacterDialogue>();
        playerBark?.HideDialogue();
    }
}
