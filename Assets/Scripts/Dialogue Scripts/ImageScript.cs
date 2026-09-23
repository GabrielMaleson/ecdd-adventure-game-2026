using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using Yarn.Unity;
using TMPro;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    [System.Serializable]
    public class PositionData
    {
        public string positionName;
        public Transform positionTransform;
    }

    [System.Serializable]
    public class SoundData
    {
        public string soundName;
        public AudioClip soundClip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = false;
    }

    [Header("UI References")]
    public GameObject dialogueCanvas;
    public Button continueButton;
    public GraphicRaycaster graphicRaycaster;
    public TextMeshProUGUI objectiveText;
    public GameObject objectivePanel;
    public GameObject objectiveButton;

    [Header("Image Display")]
    public List<Sprite> sprites = new List<Sprite>();
    public List<string> spriteTags = new List<string>();
    public Image defaultImagePrefab;
    public List<PositionData> positions = new List<PositionData>();

    [Header("Audio")]
    public List<SoundData> sounds = new List<SoundData>();
    public AudioSource audioSource;

    [Header("Fade Settings")]
    public float defaultFadeDuration = 0.5f;

    [Header("Player Control")]
    public GameObject playerObject; // Reference to player GameObject
    private PlayerController playerMovement;

    [Header("Skip Button")]
    public Button skipButton; // Assign in inspector
    public int skipCount = 30; // Number of times to call next

    private static DialogueManager instance;
    public static DialogueManager Instance => instance;
    private static Dictionary<string, Image> activeImages = new Dictionary<string, Image>();
    public DialogueRunner dialogueRunner; // Make this public

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            gameObject.tag = "Inventory";

            if (dialogueCanvas == null)
                dialogueCanvas = gameObject;

            if (continueButton == null)
                continueButton = GetComponentInChildren<Button>();

            if (graphicRaycaster == null)
                graphicRaycaster = GetComponent<GraphicRaycaster>();

            if (objectivePanel != null)
            {
                CanvasGroup objectiveCanvasGroup = objectivePanel.GetComponent<CanvasGroup>();
                if (objectiveCanvasGroup != null)
                    objectiveCanvasGroup.alpha = 0f;
            }

            dialogueRunner = GetComponent<DialogueRunner>();
            if (dialogueRunner == null)
                dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Find player movement script if not assigned
        if (playerObject == null)
            playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            playerMovement = playerObject.GetComponent<PlayerController>();

        // Set up skip button
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(SkipDialogue);
        }

        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.AddListener(HideDialogueUI);
    }

    private void HideDialogueUI()
    {
        CanvasGroup linePresenterGroup = FindFirstObjectByType<LinePresenter>()?.GetComponent<CanvasGroup>();
        if (linePresenterGroup != null)
            linePresenterGroup.alpha = 0;
        EnablePlayerControl();
        RestoreTeleporters();
    }

    private void OnValidate()
    {
        while (sprites.Count > spriteTags.Count)
        {
            spriteTags.Add("");
        }

        while (spriteTags.Count > sprites.Count)
        {
            spriteTags.RemoveAt(spriteTags.Count - 1);
        }
    }

    private void EnsureContinueButtonInteractable()
    {
        if (continueButton != null)
        {
            continueButton.interactable = true;
            continueButton.enabled = true;
        }
    }

    // Method to disable player movement
    public void DisablePlayerControl()
    {
        instance.playerMovement = FindAnyObjectByType<PlayerController>();
        if (playerObject != null)
        {
            Rigidbody2D playerRigidbody = playerObject.GetComponent<Rigidbody2D>();
            playerRigidbody.Sleep();
        }
            if (instance.graphicRaycaster != null)
            instance.graphicRaycaster.enabled = true;
        if (playerMovement != null)
            playerMovement.enabled = false;
    }

    // Method to enable player movement
    public void EnablePlayerControl()
    {
        instance.playerMovement = FindAnyObjectByType<PlayerController>();
        if (playerMovement != null)
            playerMovement.enabled = true;
        if (playerObject != null)
        {
            Rigidbody2D playerRigidbody = playerObject.GetComponent<Rigidbody2D>();
            playerRigidbody.WakeUp();
        }
    }

    // While any dialogue is running, Teleport-tagged objects are switched off — a live
    // Teleporter firing mid-dialogue (e.g. the player standing on one while talking to
    // someone) would otherwise yank the scene out from under the conversation. Needs
    // the "Teleport" tag defined in Project Settings > Tags and Layers; until it exists
    // this just no-ops instead of throwing.
    private static List<GameObject> disabledTeleporters;

    private void DisableTeleporters()
    {
        if (disabledTeleporters != null) return; // already held by an overlapping dialogue

        GameObject[] found;
        try
        {
            found = GameObject.FindGameObjectsWithTag("Teleport");
        }
        catch (UnityException)
        {
            return;
        }

        disabledTeleporters = new List<GameObject>(found);
        foreach (var obj in disabledTeleporters)
            obj.SetActive(false);
    }

    private void RestoreTeleporters()
    {
        if (disabledTeleporters == null) return;

        foreach (var obj in disabledTeleporters)
        {
            if (obj != null)
                obj.SetActive(true);
        }
        disabledTeleporters = null;
    }

    // Skip dialogue function - calls next line 30 times rapidly
    public void SkipDialogue()
    {
        if (dialogueRunner == null) return;

        Debug.Log($"Skipping {skipCount} lines of dialogue...");
        StartCoroutine(SkipDialogueCoroutine());
    }

    private IEnumerator SkipDialogueCoroutine()
    {
        for (int i = 0; i < skipCount; i++)
        {
            if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
            {
                dialogueRunner.RequestNextLine();
                yield return new WaitForSeconds(0.05f); // Small delay between skips
            }
            else
            {
                break; // Exit if dialogue ended
            }
        }
        Debug.Log("Skip complete");
    }

    [YarnCommand("sprite")]
    public static void ShowImage(string spriteTag, string positionName, string prefabName = "")
    {
        if (instance == null)
        {
            Debug.LogError("No DialogueManager instance found in scene!");
            return;
        }

        RemoveImage(positionName);

        Sprite foundSprite = null;
        for (int i = 0; i < instance.spriteTags.Count; i++)
        {
            if (instance.spriteTags[i] == spriteTag)
            {
                foundSprite = instance.sprites[i];
                break;
            }
        }

        if (foundSprite == null)
        {
            Debug.LogError($"No sprite found with tag: {spriteTag}");
            return;
        }

        Transform positionTransform = null;
        foreach (var positionData in instance.positions)
        {
            if (positionData.positionName == positionName)
            {
                positionTransform = positionData.positionTransform;
                break;
            }
        }

        if (positionTransform == null)
        {
            Debug.LogError($"No position found with name: {positionName}");
            return;
        }

        Image imagePrefabToUse = instance.defaultImagePrefab;

        Image newImage = Instantiate(imagePrefabToUse, positionTransform);
        newImage.sprite = foundSprite;
        newImage.preserveAspect = true;
        RectTransform rt = newImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        newImage.gameObject.SetActive(true);

        activeImages[positionName] = newImage;
    }

    [YarnCommand("removesprite")]
    public static void RemoveImage(string positionName)
    {
        if (activeImages != null && activeImages.TryGetValue(positionName, out Image existingImage))
        {
            if (existingImage != null && existingImage.gameObject != null)
            {
                Destroy(existingImage.gameObject);
            }
            activeImages.Remove(positionName);
        }
    }

    [YarnCommand("fadein")]
    public static void FadeInImage(string positionName, float fadeDuration = -1f)
    {
        if (fadeDuration < 0) fadeDuration = instance.defaultFadeDuration;

        if (activeImages.TryGetValue(positionName, out Image image))
        {
            instance.StartCoroutine(instance.FadeImageRoutine(image, 0f, 1f, fadeDuration));
        }
    }

    [YarnCommand("fadeout")]
    public static void FadeOutImage(string positionName, float fadeDuration = -1f)
    {
        if (fadeDuration < 0) fadeDuration = instance.defaultFadeDuration;

        if (activeImages.TryGetValue(positionName, out Image image))
        {
            instance.StartCoroutine(instance.FadeImageRoutine(image, 1f, 0f, fadeDuration));
        }
    }

    private IEnumerator FadeImageRoutine(Image image, float startAlpha, float targetAlpha, float duration)
    {
        Color color = image.color;
        color.a = startAlpha;
        image.color = color;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            image.color = color;

            yield return null;
        }

        color.a = targetAlpha;
        image.color = color;
    }

    [YarnCommand("progress")]
    public static void AddProgress(string progressID)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.AddProgress(progressID);
        }
    }

    [YarnCommand("removeprogress")]
    public static void RemoveProgress(string progressID)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.RemoveProgress(progressID);
        }
    }

    [YarnFunction("hasprogress")]
    public static bool HasProgress(string progressID)
    {
        if (SaveManager.Instance != null)
        {
            return SaveManager.Instance.HasProgress(progressID);
        }
        return false;
    }

    [YarnCommand("objective")]
    public static void SetObjective(string objective)
    {
        if (instance == null) return;

        if (instance.objectiveText != null)
            instance.objectiveText.text = objective;

        if (instance.objectivePanel != null)
        {
            CanvasGroup canvasGroup = instance.objectivePanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }
    }

    [YarnCommand("clearobjective")]
    public static void ClearObjective()
    {
        if (instance == null) return;

        if (instance.objectiveText != null)
            instance.objectiveText.text = "";

        if (instance.objectivePanel != null)
        {
            CanvasGroup canvasGroup = instance.objectivePanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }
    }

    [YarnCommand("darken")]
    public static void DarkenScreen(float alpha = 0.37f, float duration = 0.5f)
    {
        if (instance.graphicRaycaster != null)
            instance.graphicRaycaster.enabled = true;
        instance.DisablePlayerControl();
    }

    public void StartDialogue(string dialogue)
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
            if (dialogueRunner == null)
            {
                Debug.LogError("DialogueRunner é null! Não foi possível iniciar o diálogo.");
                return;
            }
        }

        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning($"Tentativa de iniciar '{dialogue}' mas o DialogueRunner já está em execução. Ignorando.");
            EnablePlayerControl(); // Prevent softlock if player movement was already stopped
            return;
        }

        // Reset LinePresenter visibility — HideDialogueUI sets alpha=0 on dialogue end,
        // so the next dialogue must restore it before lines can be seen or clicked.
        LinePresenter linePresenter = FindFirstObjectByType<LinePresenter>();
        if (linePresenter != null)
        {
            CanvasGroup cg = linePresenter.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        // Comecar um dialogo com o runner JA rodando outro interleava dois loops da VM do
        // Yarn no mesmo objeto Dialogue. Como a chamada abaixo e fire-and-forget (`_ =`),
        // a excecao que isso gera e descartada: o jogo trava no meio de uma fala e o
        // Console nao diz uma palavra.
        //
        // Recusar e o certo. Quem chamou recebe "nao comecou" pelo IsDialogueRunning do
        // funil (DialogueStarter.StartAndFreezePlayer), entao um beat recusado nao e
        // marcado como jogado e continua podendo acontecer depois.
        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning($"[DialogueManager] pedido para comecar '{dialogue}' enquanto " +
                             "outro dialogo ainda esta rodando. RECUSADO — comecar dois no " +
                             "mesmo runner trava a fala no meio. Confira se algum " +
                             "DialogueRunner da cena esta com Auto Start ligado.", this);
            return;
        }

        DisablePlayerControl();
        DisableTeleporters();

        try
        {
            _ = dialogueRunner.StartDialogue(dialogue);
            Debug.Log($"Diálogo iniciado: {dialogue}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Falha ao iniciar diálogo '{dialogue}': {e.Message}");
            EnablePlayerControl();
            RestoreTeleporters();
        }
    }

    [YarnCommand("brighten")]
    public static void BrightenScreen(float duration = 0.5f)
    {
        instance.EnablePlayerControl();
        if (instance.graphicRaycaster != null)
            instance.graphicRaycaster.enabled = false;
    }


    [YarnCommand("playsound")]
    public static void PlaySound(string soundName)
    {
        if (instance.audioSource == null) return;

        SoundData foundSound = null;
        foreach (var sound in instance.sounds)
        {
            if (sound.soundName == soundName)
            {
                foundSound = sound;
                break;
            }
        }

        if (foundSound != null)
        {
            instance.audioSource.volume = foundSound.volume;
            instance.audioSource.loop = foundSound.loop;
            instance.audioSource.PlayOneShot(foundSound.soundClip);
        }
        else
        {
            // AVISO, e nao erro. Som que ainda nao foi feito e conteudo pendente, nao falha:
            // o dialogo segue sem ele de qualquer jeito. Como LogError, isso pausava o Play
            // toda vez em quem estiver com o Error Pause do Console ligado — um beat sem
            // audio derrubava a sessao inteira.
            Debug.LogWarning($"[PlaySound] som '{soundName}' nao existe na lista do " +
                             "DialogueManager. A fala continua sem ele.");
        }
    }

    [YarnCommand("stopsound")]
    public static void StopSound()
    {
        if (instance.audioSource != null)
        {
            instance.audioSource.Stop();
        }
    }

    [YarnCommand("sceneload")]
    public static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    [YarnCommand("enableraycaster")]
    public static void EnableRaycaster()
    {
        if (instance.graphicRaycaster != null)
            instance.graphicRaycaster.enabled = true;
    }

    [YarnCommand("disableraycaster")]
    public static void DisableRaycaster()
    {
        if (instance.graphicRaycaster != null)
            instance.graphicRaycaster.enabled = false;
    }

    // Triggers a named scripted move from a Cutscener in the scene — e.g. <<movement
    // JoshHome>> walks whatever object/destination pair was named "JoshHome" in that
    // Cutscener's Inspector list. Optional modifier words, in any order:
    //   freeze — the dialogue waits until the move actually finishes before showing
    //            the next line. Without it, the line advances immediately and the
    //            move plays out in the background.
    //   hold   — the sprite KEEPS the facing it already had for the whole move,
    //            instead of turning to follow the direction of travel. Use it with a
    //            <<face>> right before, when a character has to walk while still
    //            looking at someone: floating backwards, backing away, being pulled.
    //   flip   — invert the sprite once before setting off, then hold that. Legacy:
    //            it's a guess about which way the art currently points, so <<face>>
    //            plus hold is the version that's actually right every time.
    // e.g. <<movement JoshHome freeze>>, <<movement HazeFloatDown hold freeze>>.
    [YarnCommand("movement")]
    public static IEnumerator Movement(string movementName, string modifier1 = null, string modifier2 = null, string modifier3 = null)
    {
        bool Has(string word) => modifier1 == word || modifier2 == word || modifier3 == word;

        bool freeze = Has("freeze");

        // hold beats flip if somebody writes both — hold is the explicit "don't touch
        // the facing" instruction, so silently inverting the sprite would defeat it.
        Cutscener.FacingMode facing = Has("hold")  ? Cutscener.FacingMode.Hold
                                    : Has("flip")  ? Cutscener.FacingMode.FlipOnce
                                                   : Cutscener.FacingMode.Travel;

        if (freeze)
        {
            Coroutine handle = Cutscener.TriggerAndWait(movementName, facing);
            if (handle != null)
                yield return handle;
        }
        else
        {
            Cutscener.Trigger(movementName, facing);
        }
    }

    // Shows/hides a named object from a Cutscener in the scene — e.g. <<enable
    // ElderAmos>> / <<disable ElderAmos>>, matching whatever name was given to that
    // entry in the Cutscener's Objects list.
    [YarnCommand("enable")]
    public static void Enable(string objectName)
    {
        Cutscener.TriggerSetActive(objectName, true);
    }

    [YarnCommand("disable")]
    public static void Disable(string objectName)
    {
        Cutscener.TriggerSetActive(objectName, false);
    }

    // <<placeat TrueHaze HazeThree>> — puts the first object exactly where the second
    // one is, matching its facing, instantly. Made for swapping a cutscene stand-in for
    // the real object: place, disable the stand-in, enable the real one, and the
    // handover is invisible instead of the replacement popping in somewhere else.
    [YarnCommand("placeat")]
    public static void PlaceAt(string objectName, string referenceName)
    {
        Cutscener.TriggerPlaceAt(objectName, referenceName);
    }

    // Forces a named object's facing outright, instead of whatever direction a
    // <<movement>> happened to travel in. Two forms:
    //   <<face Josh left>>      — a fixed compass direction: left, right, up, down.
    //   <<face Josh HazeOne>>   — turn to look at ANOTHER registered object, wherever
    //                             it currently is.
    // Use the second form any time the player walked to the spot HIMSELF rather than
    // being placed there by a <<movement>> — it is the only one that stays correct no
    // matter which side he approached from.
    // Both names must be registered in a Cutscener's Objects list (the same list
    // <<enable>>/<<disable>> use); they may be in different Cutsceners.
    [YarnCommand("face")]
    public static void Face(string objectName, string towards)
    {
        Cutscener.TriggerFace(objectName, towards);
    }

    // Advances the story mist to stage N — e.g. <<mist 2>>. Stages only ever go up
    // (calling a lower number than already reached is a no-op); see MistDirector for
    // what each stage actually looks like.
    [YarnCommand("mist")]
    public static void Mist(int stage)
    {
        MistDirector.Trigger(stage);
    }

    // <<wait seconds>> é um comando nativo do Yarn Spinner — não precisa de registro manual.
}