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

    // Alguem com fala na tela AGORA. O prompt de interacao se apaga enquanto isto for
    // verdade: um E boiando por cima de uma fala compete com ela pela atencao, e pior,
    // convida a apertar no meio da frase.
    public static bool AnyBarkShowing
    {
        get
        {
            // Pergunta primeiro a quem esta falando. O dicionario de speakers e indexado
            // por barkId e por isso perde um falante quando dois compartilham o id — era
            // assim que o E sobrevivia por cima de um bark.
            if (CharacterDialogue.AlgumMostrando) return true;

            if (instance == null) return false;

            foreach (var pair in instance.speakers)
                if (pair.Value != null && pair.Value.IsShowing) return true;

            return false;
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
        // FindObjectsInactive.Include, e nao a sobrecarga curta: aquela IGNORA objetos
        // inativos, e Marcus e Erika comecam desativados — eram varridos como se nao
        // existissem, e so entravam no registro se o OnEnable deles rodasse depois. Um
        // personagem que nunca e reativado nunca ficava registrado, e o bark dele falhava
        // sem dizer nada.
        foreach (CharacterDialogue speaker in FindObjectsByType<CharacterDialogue>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
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

    // Acha quem fala e, se ele ainda nao tiver CharacterDialogue, MONTA um na hora.
    //
    // Existe porque so o Marcus e a Erika tem o componente no prefab. O Josh e a Haze nao
    // tem, e exigir que alguem monte cada um a mao antes de qualquer fala funcionar e um
    // passo silencioso que ninguem lembra — o balao nasce pelo TextStyle de qualquer jeito,
    // entao ja sai com a fonte, o tamanho e a altura dos outros.
    //
    // A busca acontece na HORA DE FALAR, e nao ao carregar a cena, de proposito: os quatro
    // objetos Haze comecam desativados, e qual deles esta em cena muda conforme o beat.
    // Procurar agora acha o que esta de pe agora.
    public static CharacterDialogue EnsureSpeaker(string id)
    {
        if (string.IsNullOrEmpty(id) || instance == null) return null;

        CharacterDialogue found = instance.Find(id);
        if (found != null && found.gameObject.activeInHierarchy) return found;

        GameObject host = FindHost(id);
        if (host == null) return found;   // o registrado, mesmo desligado, e melhor que nada

        CharacterDialogue speaker = host.GetComponentInChildren<CharacterDialogue>();

        if (speaker == null)
        {
            speaker = host.AddComponent<CharacterDialogue>();
            speaker.barkId = id;

            // Conversa fiada DESLIGADA: os prefabs do Marcus e da Erika estao assim, e o
            // padrao do campo e ligado. Ligada, a rotina ociosa nao tem nada para dizer e
            // apaga o balao por cima da fala que acabou de ser pedida.
            speaker.autoPlayRandomDialogue = false;

            Debug.Log($"BarkDirector: '{host.name}' nao tinha CharacterDialogue — criei um " +
                      $"com bark id '{id}'.", host);
        }

        if (string.IsNullOrEmpty(speaker.barkId)) speaker.barkId = id;
        Register(speaker);
        return speaker;
    }

    // O objeto que deve falar com este id. Player pela tag; o resto pelo nome, preferindo
    // sempre quem esta ATIVO — "Haze" e "CutsceneHaze" coexistem na cena e so um deles
    // esta de pe em cada momento.
    private static GameObject FindHost(string id)
    {
        if (id.Equals("josh", System.StringComparison.OrdinalIgnoreCase))
            return GameObject.FindGameObjectWithTag("Player");

        GameObject best = null;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.Equals(id, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (t.gameObject.activeInHierarchy) return t.gameObject;
            if (best == null) best = t.gameObject;
        }

        return best;
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

        CharacterDialogue speaker = EnsureSpeaker(id);
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

        CharacterDialogue speaker = EnsureSpeaker(id);
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

    // Devolve a duracao da fala, ou 0 quando ela NAO foi mostrada. Quem chama precisa
    // saber: avancar o proprio estado por cima de uma fala descartada corrompe a sequencia
    // — foi assim que uma entrada de duas linhas se esgotou tendo dito so a primeira.
    public static float Bark(string id, string line, BarkPriority priority = BarkPriority.Scripted)
    {
        if (instance == null)
        {
            Debug.LogWarning("BarkDirector.Bark called but there is no BarkDirector in the scene.");
            return 0f;
        }

        CharacterDialogue speaker = EnsureSpeaker(id);
        if (speaker == null)
        {
            // Listar QUEM esta registrado, e nao so dizer que faltou: na pratica o erro e
            // sempre um Bark Id escrito diferente dos dois lados, e ver a lista resolve na
            // hora em vez de virar caca ao tesouro pelo Inspector.
            string known = instance.speakers.Count > 0
                ? string.Join(", ", instance.speakers.Keys)
                : "(ninguem)";

            Debug.LogWarning($"BarkDirector.Bark: nenhum personagem registrado com o bark id " +
                             $"'{id}'. Registrados agora: {known}. O Bark Id do " +
                             "CharacterDialogue tem que bater com o Speaker Id de quem pediu a fala.");
            return 0f;
        }

        return speaker.Show(line, priority);
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
