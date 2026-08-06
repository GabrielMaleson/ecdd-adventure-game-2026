using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// How much right a line has to be on screen. A line only interrupts one of STRICTLY
// lower priority — so idle chatter can never stomp something the story asked for.
public enum BarkPriority
{
    Idle = 0,       // ambient chatter on a timer
    Reactive = 1,   // responses to something the player did
    Scripted = 2    // asked for by name from .yarn or a BarkTrigger
}

// Floating world-space text above a character. Filled three ways:
//   - idle chatter on a timer (randomLines, or a BarkSet asset)
//   - a specific line pushed in by BarkDirector / BarkTrigger / a UnityEvent
//   - a line of a Yarn conversation, routed here by BarkPresenter
// Whichever it is, the line holds, then floats up and fades.
public class CharacterDialogue : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Id this character answers to from .yarn — <<bark marcus \"...\">>. Case-insensitive. " +
             "Should match the CHARACTER NAME used in the .yarn lines so bark conversations can find it.")]
    public string barkId;

    [Header("Text")]
    [Tooltip("World-space TextMeshPro positioned above the character. Assign in the Inspector.")]
    [SerializeField] private TextMeshPro dialogueText;

    [Header("Random Dialogue")]
    [Tooltip("Idle lines typed straight onto this character. Used when Bark Set is empty, " +
             "so existing prefabs keep working untouched.")]
    public List<string> randomLines = new List<string>();

    [Tooltip("Preferred source of idle lines. When assigned, Random Lines is ignored.")]
    public BarkSet barkSet;

    public bool autoPlayRandomDialogue = true;
    [Tooltip("Random delay range between idle lines.")]
    public float minInterval = 3f;
    public float maxInterval = 8f;

    [Header("Idle Gating")]
    [Tooltip("Idle chatter only runs while the player is within this distance. 0 = no distance check.")]
    public float idleRange = 14f;
    [Tooltip("Idle chatter also requires the character to be on screen. Needs a Renderer somewhere on this object.")]
    public bool idleRequiresVisible = true;

    [Header("Display Timing")]
    [Tooltip("Minimum time a line stays fully visible before it floats up and fades.")]
    public float holdDuration = 1.5f;
    [Tooltip("Long lines stay up longer instead of every line getting the same slot.")]
    public bool scaleDurationWithLength = true;
    public float secondsPerCharacter = 0.045f;
    public float maxHoldDuration = 6f;

    [Header("Float & Fade")]
    public float floatDistance = 0.5f;
    public float floatFadeDuration = 1f;

    private Vector3 originalLocalPosition;
    private Color originalColor;
    private Coroutine displayRoutine;
    private Coroutine randomRoutine;
    private Renderer visibilityRenderer;

    // Priority of whatever is on screen right now. Meaningless while nothing is showing.
    private BarkPriority currentPriority = BarkPriority.Idle;

    // Shuffled bag so a line can't come up twice in a row while others go unused.
    private readonly List<int> bag = new List<int>();

    public bool IsShowing => displayRoutine != null;

    private void Awake()
    {
        if (dialogueText == null)
        {
            Debug.LogError($"{name}: CharacterDialogue has no TextMeshPro assigned.", this);
            enabled = false;
            return;
        }

        originalLocalPosition = dialogueText.transform.localPosition;
        // Keep only the RGB. Alpha is driven entirely by the fade, so re-reading a
        // mid-fade colour here can never leave the line stuck semi-transparent.
        originalColor = new Color(dialogueText.color.r, dialogueText.color.g, dialogueText.color.b, 1f);
        dialogueText.gameObject.SetActive(false);

        visibilityRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        BarkDirector.Register(this);

        if (autoPlayRandomDialogue)
            StartRandomDialogue();
    }

    private void OnDisable()
    {
        // Without this a character disabled mid-line leaves its text hanging on screen.
        BarkDirector.Unregister(this);
        StopRandomDialogue();
        HideDialogue();
    }

    // ---------------------------------------------------------------- showing lines

    // Kept parameterless-priority so existing Inspector UnityEvent wiring
    // (which can only pass one string) still works and comes in as Idle.
    public void ShowDialogue(string text)
    {
        Show(text, BarkPriority.Idle);
    }

    // Returns how long the line will be on screen, or 0 if it was refused.
    // BarkPresenter uses the return value to know how long to wait.
    public float Show(string text, BarkPriority priority)
    {
        if (!enabled || !gameObject.activeInHierarchy || dialogueText == null || string.IsNullOrEmpty(text))
            return 0f;

        // A line already on screen is only displaced by something more important.
        // Equal priority is dropped rather than queued: a queue would hold stale lines
        // that arrive long after the moment that asked for them.
        if (displayRoutine != null && priority <= currentPriority)
            return 0f;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        currentPriority = priority;
        float duration = DurationFor(text);
        displayRoutine = StartCoroutine(DisplayRoutine(text, duration));
        return duration;
    }

    public void HideDialogue()
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(false);
    }

    // Total on-screen time: the hold plus the float-out, since the line is still
    // readable while it fades.
    private float DurationFor(string text)
    {
        float hold = holdDuration;

        if (scaleDurationWithLength)
            hold = Mathf.Clamp(text.Length * secondsPerCharacter, holdDuration, maxHoldDuration);

        return hold + floatFadeDuration;
    }

    // ---------------------------------------------------------------- idle chatter

    public void StartRandomDialogue()
    {
        if (!isActiveAndEnabled)
            return;

        if (randomRoutine != null)
            StopCoroutine(randomRoutine);
        randomRoutine = StartCoroutine(RandomDialogueRoutine());
    }

    public void StopRandomDialogue()
    {
        if (randomRoutine != null)
        {
            StopCoroutine(randomRoutine);
            randomRoutine = null;
        }
    }

    private IEnumerator RandomDialogueRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (!CanChatter())
                continue;

            string line = NextIdleLine();
            if (!string.IsNullOrEmpty(line))
                Show(line, BarkPriority.Idle);
        }
    }

    // Every reason idle chatter should keep its mouth shut.
    private bool CanChatter()
    {
        if (displayRoutine != null)
            return false;

        // Never talk over a real conversation — neither the blocking dialogue box
        // nor a bark conversation being run through BarkPresenter.
        if (BarkDirector.DialogueInProgress)
            return false;

        if (idleRequiresVisible && visibilityRenderer != null && !visibilityRenderer.isVisible)
            return false;

        if (idleRange > 0f)
        {
            Transform player = BarkDirector.PlayerTransform;
            if (player == null)
                return false;
            if ((player.position - transform.position).sqrMagnitude > idleRange * idleRange)
                return false;
        }

        return true;
    }

    // Active source of idle lines: the BarkSet asset if there is one and its condition
    // passes, otherwise whatever was typed on the prefab.
    private List<string> ActiveLines()
    {
        if (barkSet != null && barkSet.lines.Count > 0 && barkSet.ConditionsMet())
            return barkSet.lines;

        return randomLines;
    }

    public void SetBarkSet(BarkSet set)
    {
        barkSet = set;
        bag.Clear();
    }

    // Shuffled bag: every line is used once before any repeats.
    private string NextIdleLine()
    {
        List<string> lines = ActiveLines();
        if (lines == null || lines.Count == 0)
            return null;

        if (bag.Count == 0)
        {
            for (int i = 0; i < lines.Count; i++)
                bag.Add(i);

            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            // Avoid the one case a bag still allows: the last line of the old bag
            // landing first in the new one.
            if (bag.Count > 1 && lines.Count > 1 && bag[0] == lastPlayedIndex)
                (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
        }

        int index = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);

        // The set can be swapped between picks, so an index from an older, longer
        // bag has to be treated as stale rather than thrown.
        if (index >= lines.Count)
        {
            bag.Clear();
            return null;
        }

        lastPlayedIndex = index;
        return lines[index];
    }

    private int lastPlayedIndex = -1;

    // ---------------------------------------------------------------- rendering

    private IEnumerator DisplayRoutine(string text, float totalDuration)
    {
        float hold = Mathf.Max(0f, totalDuration - floatFadeDuration);

        dialogueText.transform.localPosition = originalLocalPosition;
        dialogueText.color = originalColor;
        dialogueText.text = text;
        dialogueText.gameObject.SetActive(true);

        yield return new WaitForSeconds(hold);

        Vector3 startPos = dialogueText.transform.localPosition;
        Vector3 endPos = startPos + Vector3.up * floatDistance;
        Color startColor = dialogueText.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < floatFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / floatFadeDuration;

            dialogueText.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            dialogueText.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        dialogueText.gameObject.SetActive(false);
        displayRoutine = null;
    }

    private void OnDestroy()
    {
        if (displayRoutine != null) StopCoroutine(displayRoutine);
        if (randomRoutine != null) StopCoroutine(randomRoutine);
    }
}
