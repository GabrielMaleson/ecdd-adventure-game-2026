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
            dialogueText = TextStyle.CreateWorldLabel(transform, name + "Text");
        else
            TextStyle.PlaceWorldLabel(transform, dialogueText);

        ApplyTextStyle();

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

    // Font and size come from the single Assets/Resources/TextStyle.asset.
    public void ApplyTextStyle()
    {
        TextStyle.Apply(dialogueText, TextStyle.Role.WorldText);

        TextStyle style = TextStyle.Current;
        if (style == null) return;

        if (style.holdDuration > 0f)        holdDuration        = style.holdDuration;
        if (style.floatDistance > 0f)       floatDistance       = style.floatDistance;
        if (style.floatFadeDuration > 0f)   floatFadeDuration   = style.floatFadeDuration;
        if (style.fadeInDuration > 0f)      fadeInDuration      = style.fadeInDuration;
        if (style.fadeInFloatDistance > 0f) fadeInFloatDistance = style.fadeInFloatDistance;
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

    // Above THIS object's artwork, at the height set in Assets/Resources/TextStyle.asset.
    // It used to be the player's position plus a hardcoded 1.5, which drew the prop's own
    // line over Josh's head wherever he happened to be standing — and made World Text
    // Height look like it did nothing, because nothing ever read it.
    private Vector3 SpeechPosition()
    {
        float height = TextStyle.Current != null ? TextStyle.Current.worldTextHeight : 0.35f;

        if (VisibleArt.TryGetBounds(transform, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y + height, dialogueText.transform.position.z);

        return new Vector3(transform.position.x, transform.position.y + height, dialogueText.transform.position.z);
    }

    private IEnumerator DisplayRoutine(string text)
    {
        if (playerObject == null)
        {
            dialogueText.gameObject.SetActive(false);
            displayRoutine = null;
            yield break;
        }

        Vector3 basePosition = SpeechPosition();
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