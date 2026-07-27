using UnityEngine;
using TMPro;
using System.Collections;

public class CharacterBark : MonoBehaviour
{
    [Header("Quote Settings")]
    [TextArea(2, 4)]
    public string[] quotes = new string[]
    {
        "Hello there!",
        "I'm feeling great today!",
        "What's new?",
        "Time for an adventure!",
        "Did you know I can talk?",
        "The weather is nice!",
        "I love this place!",
        "Let's do something fun!"
    };

    [Header("Bark Duration")]
    public float barkDuration = 2.5f;
    public float minBarkDelay = 3f;
    public float maxBarkDelay = 8f;

    [Header("Animation")]
    public bool animateBark = true;
    public float animationScale = 1.2f;
    public float animationDuration = 0.3f;

    private TextMeshPro textMeshPro;
    private RectTransform rectTransform;
    private Coroutine barkCoroutine;
    private Coroutine autoBarkCoroutine;

    private void Awake()
    {
        // Get or add TextMeshPro component
        textMeshPro = GetComponentInChildren<TextMeshPro>();
        if (textMeshPro == null)
        {
            Debug.LogError("No TextMeshProUGUI found in children!");
            return;
        }

        rectTransform = textMeshPro.GetComponent<RectTransform>();

        // Start with text hidden
        textMeshPro.gameObject.SetActive(false);
    }

    private void Start()
    {
        // Start automatic barking
        StartAutoBark();
    }

    /// <summary>
    /// Display a random quote as a bark
    /// </summary>
    public void Bark()
    {
        if (textMeshPro == null) return;

        // Stop any existing bark animation
        if (barkCoroutine != null)
        {
            StopCoroutine(barkCoroutine);
        }

        // Pick a random quote
        string randomQuote = quotes[Random.Range(0, quotes.Length)];
        textMeshPro.text = randomQuote;

        // Start the bark process
        barkCoroutine = StartCoroutine(ShowBark());
    }

    /// <summary>
    /// Display a specific quote as a bark
    /// </summary>
    public void Bark(string quote)
    {
        if (textMeshPro == null) return;

        if (barkCoroutine != null)
        {
            StopCoroutine(barkCoroutine);
        }

        textMeshPro.text = quote;
        barkCoroutine = StartCoroutine(ShowBark());
    }

    /// <summary>
    /// Display a quote by index from the quotes array
    /// </summary>
    public void Bark(int quoteIndex)
    {
        if (textMeshPro == null) return;

        if (quoteIndex >= 0 && quoteIndex < quotes.Length)
        {
            if (barkCoroutine != null)
            {
                StopCoroutine(barkCoroutine);
            }

            textMeshPro.text = quotes[quoteIndex];
            barkCoroutine = StartCoroutine(ShowBark());
        }
        else
        {
            Debug.LogWarning($"Quote index {quoteIndex} out of range!");
        }
    }

    private IEnumerator ShowBark()
    {
        // Show the text
        textMeshPro.gameObject.SetActive(true);

        // Animate the bark if enabled
        if (animateBark && rectTransform != null)
        {
            // Store original scale
            Vector3 originalScale = rectTransform.localScale;
            Vector3 targetScale = originalScale * animationScale;

            // Scale up
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                // Ease out cubic for a nice pop effect
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, easedT);
                yield return null;
            }

            // Scale back down to original
            elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                // Ease in cubic
                float easedT = t * t * t;
                rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, easedT);
                yield return null;
            }

            // Ensure we end at original scale
            rectTransform.localScale = originalScale;
        }

        // Wait for the bark duration
        yield return new WaitForSeconds(barkDuration);

        // Hide the text
        textMeshPro.gameObject.SetActive(false);
        barkCoroutine = null;
    }

    /// <summary>
    /// Start automatic random barking
    /// </summary>
    public void StartAutoBark()
    {
        if (autoBarkCoroutine != null)
        {
            StopCoroutine(autoBarkCoroutine);
        }
        autoBarkCoroutine = StartCoroutine(AutoBarkRoutine());
    }

    /// <summary>
    /// Stop automatic barking
    /// </summary>
    public void StopAutoBark()
    {
        if (autoBarkCoroutine != null)
        {
            StopCoroutine(autoBarkCoroutine);
            autoBarkCoroutine = null;
        }
    }

    private IEnumerator AutoBarkRoutine()
    {
        while (true)
        {
            // Wait for a random delay
            float delay = Random.Range(minBarkDelay, maxBarkDelay);
            yield return new WaitForSeconds(delay);

            // Only bark if not already barking
            if (barkCoroutine == null)
            {
                Bark();
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up coroutines
        if (barkCoroutine != null)
        {
            StopCoroutine(barkCoroutine);
        }
        if (autoBarkCoroutine != null)
        {
            StopCoroutine(autoBarkCoroutine);
        }
    }
}