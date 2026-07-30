using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Floating world-space text that hovers above a character. Two ways it gets filled:
//   - Any other script can push a specific line in with ShowDialogue("..."), e.g. a
//     puzzle button's onClick wired straight to it, or a code call when something happens.
//   - It can also chatter on its own, picking a random line from Random Lines on an
//     Inspector-tunable timer, to give idle NPCs a sense of life.
// Either way the line holds for a moment, then floats up and fades out.
public class CharacterDialogue : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("World-space TextMeshPro positioned above the character. Assign in the Inspector.")]
    [SerializeField] private TextMeshPro dialogueText;

    [Header("Random Dialogue")]
    [Tooltip("Lines picked at random while idle-chattering.")]
    public List<string> randomLines = new List<string>();
    public bool autoPlayRandomDialogue = true;
    [Tooltip("Random delay range between idle lines.")]
    public float minInterval = 3f;
    public float maxInterval = 8f;

    [Header("Display Timing")]
    [Tooltip("How long a line stays fully visible before it starts floating up and fading.")]
    public float holdDuration = 1.5f;

    [Header("Float & Fade")]
    public float floatDistance = 0.5f;
    public float floatFadeDuration = 1f;

    private Vector3 originalLocalPosition;
    private Color originalColor;
    private Coroutine displayRoutine;
    private Coroutine randomRoutine;

    private void Awake()
    {
        if (dialogueText == null)
        {
            Debug.LogError($"{name}: CharacterDialogue has no TextMeshPro assigned.", this);
            enabled = false;
            return;
        }

        originalLocalPosition = dialogueText.transform.localPosition;
        originalColor = dialogueText.color;
        dialogueText.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (autoPlayRandomDialogue)
            StartRandomDialogue();
    }

    // The external hook — any script can call this to show a specific line here.
    public void ShowDialogue(string text)
    {
        if (!enabled || dialogueText == null || string.IsNullOrEmpty(text))
            return;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplayRoutine(text));
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

    // Lets other scripts pause/resume the idle chatter (e.g. while a real conversation
    // is happening, so this doesn't talk over it).
    public void StartRandomDialogue()
    {
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

            if (displayRoutine == null && randomLines.Count > 0)
                ShowDialogue(randomLines[Random.Range(0, randomLines.Count)]);
        }
    }

    private IEnumerator DisplayRoutine(string text)
    {
        dialogueText.transform.localPosition = originalLocalPosition;
        dialogueText.color = originalColor;
        dialogueText.text = text;
        dialogueText.gameObject.SetActive(true);

        yield return new WaitForSeconds(holdDuration);

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
