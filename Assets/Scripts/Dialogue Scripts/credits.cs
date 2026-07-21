using UnityEngine;
using TMPro;
using System.Collections;

public class CreditsRoll : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private TextMeshProUGUI creditsText;
    [SerializeField] private string creditsContent = "";

    [Header("Movement Settings")]
    [SerializeField] private float scrollSpeed = 50f;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private bool startOnEnable = true;

    [Header("Bounds Settings")]
    [SerializeField] private float endPosition = 1000f;
    [SerializeField] private bool loopCredits = false;
    [SerializeField] private float resetDelay = 2f;

    [Header("Optional Events")]
    [SerializeField] private UnityEngine.Events.UnityEvent onCreditsComplete;
    [SerializeField] private UnityEngine.Events.UnityEvent onCreditsLoop;

    private RectTransform rectTransform;
    private float startPosition;
    private bool isRolling = false;
    private bool isWaitingForReset = false;

    void Awake()
    {
        if (creditsText == null)
            creditsText = GetComponent<TextMeshProUGUI>();

        rectTransform = creditsText.GetComponent<RectTransform>();
    }

    void Start()
    {
        if (creditsText != null && !string.IsNullOrEmpty(creditsContent))
            SetCreditsText(creditsContent);

        if (startOnEnable)
            StartCredits();
    }

    void Update()
    {
        if (isRolling && !isWaitingForReset)
        {
            // Move the credits upward
            Vector2 anchoredPosition = rectTransform.anchoredPosition;
            anchoredPosition.y += scrollSpeed * Time.deltaTime;
            rectTransform.anchoredPosition = anchoredPosition;

            // Check if credits have reached the end
            if (rectTransform.anchoredPosition.y >= endPosition)
            {
                isRolling = false;

                if (loopCredits)
                {
                    StartCoroutine(ResetAndLoop());
                }
                else
                {
                    onCreditsComplete?.Invoke();
                }
            }
        }
    }

    public void StartCredits()
    {
        if (creditsText == null)
        {
            Debug.LogError("CreditsRoll: No TextMeshProUGUI component assigned!");
            return;
        }

        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(startDelay);

        // Store the starting position
        startPosition = rectTransform.anchoredPosition.y;
        isRolling = true;
        isWaitingForReset = false;
    }

    private IEnumerator ResetAndLoop()
    {
        isWaitingForReset = true;
        yield return new WaitForSeconds(resetDelay);

        // Reset position to start
        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        anchoredPosition.y = startPosition;
        rectTransform.anchoredPosition = anchoredPosition;

        isRolling = true;
        isWaitingForReset = false;
        onCreditsLoop?.Invoke();
    }

    public void StopCredits()
    {
        isRolling = false;
        StopAllCoroutines();
        isWaitingForReset = false;
    }

    public void ResetCredits()
    {
        StopCredits();

        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        anchoredPosition.y = startPosition;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public void SetCreditsText(string text)
    {
        if (creditsText != null)
        {
            creditsText.text = text;
        }
    }

    public void SetCreditsText(string[] lines)
    {
        if (creditsText != null)
        {
            creditsText.text = string.Join("\n", lines);
        }
    }

    public void SetScrollSpeed(float speed)
    {
        scrollSpeed = speed;
    }

    public void SetEndPosition(float position)
    {
        endPosition = position;
    }

    // Optional: Automatically calculate end position based on text height
    public void AutoSetEndPosition(float offset = 0f)
    {
        if (creditsText != null)
        {
            float textHeight = creditsText.preferredHeight;
            endPosition = textHeight + offset;
        }
    }
}