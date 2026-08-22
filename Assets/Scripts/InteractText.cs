using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class InteractDialogue : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshPro dialogueText;

    [Header("Dialogue Sequence")]
    [Tooltip("List of dialogue lines to cycle through sequentially.")]
    public List<string> dialogueLines = new List<string>();

    [Header("Display Timing")]
    [Tooltip("How long a line stays fully visible before it starts floating up and fading.")]
    public float holdDuration = 1.5f;

    [Header("Float & Fade")]
    public float floatDistance = 0.5f;
    public float floatFadeDuration = 1f;

    [Header("Fade In")]
    public float fadeInDuration = 0.3f;
    public float fadeInFloatDistance = 0.3f;

    private Vector3 originalLocalPosition;
    private Color originalColor;
    private Coroutine displayRoutine;
    private bool isPlayerInRange = false;
    private GameObject playerObject;
    private int currentIndex = 0;

    private void Awake()
    {
        if (dialogueText == null)
        {
            Debug.LogError($"{name}: InteractDialogue has no TextMeshPro assigned.", this);
            enabled = false;
            return;
        }

        originalLocalPosition = dialogueText.transform.localPosition;
        originalColor = dialogueText.color;
        dialogueText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        isPlayerInRange = true;
        playerObject = collision.gameObject;

        if (dialogueLines.Count > 0)
            SendToInteractButton();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        isPlayerInRange = false;
        playerObject = null;
        HideDialogue();
        ClearInteractButton();
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(this, "E", OnInteractPressed);
    }

    private void ClearInteractButton()
    {
        InteractButton.Instance?.ClearInteraction(this);
    }

    public void OnInteractPressed()
    {
        if (!isPlayerInRange || dialogueLines.Count == 0)
            return;

        ShowNextDialogue();
    }

    public void ShowNextDialogue()
    {
        if (!enabled || dialogueText == null || dialogueLines.Count == 0)
            return;

        if (!isPlayerInRange)
            return;

        string text = dialogueLines[currentIndex];
        currentIndex = (currentIndex + 1) % dialogueLines.Count;

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

    public void ResetSequence()
    {
        currentIndex = 0;
    }

    private IEnumerator DisplayRoutine(string text)
    {
        if (playerObject == null)
        {
            dialogueText.gameObject.SetActive(false);
            displayRoutine = null;
            yield break;
        }

        Vector3 basePosition = playerObject.transform.position + Vector3.up * 1.5f;
        dialogueText.text = text;
        dialogueText.gameObject.SetActive(true);

        dialogueText.transform.position = basePosition - Vector3.up * fadeInFloatDistance;
        dialogueText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeInDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / fadeInDuration;

            dialogueText.transform.position = Vector3.Lerp(basePosition - Vector3.up * fadeInFloatDistance, basePosition, t);
            dialogueText.color = new Color(originalColor.r, originalColor.g, originalColor.b, t);

            yield return null;
        }

        dialogueText.transform.position = basePosition;
        dialogueText.color = originalColor;

        yield return new WaitForSeconds(holdDuration);

        Vector3 startPos = dialogueText.transform.position;
        Vector3 endPos = startPos + Vector3.up * floatDistance;
        Color startColor = dialogueText.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < floatFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / floatFadeDuration;

            dialogueText.transform.position = Vector3.Lerp(startPos, endPos, t);
            dialogueText.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        dialogueText.gameObject.SetActive(false);
        displayRoutine = null;
    }

    private void OnDestroy()
    {
        if (displayRoutine != null)
            StopCoroutine(displayRoutine);
    }
}