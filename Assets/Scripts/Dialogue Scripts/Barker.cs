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

    [Header("Text Position (2D)")]
    public Vector2 textOffset = new Vector2(0f, 2f);
    public float textScale = 1f;
    public bool flipTextWithCharacter = true;

    [Header("Animation")]
    public bool animateBark = true;
    public float animationScale = 1.2f;
    public float animationDuration = 0.3f;
    public bool floatUpwards = true;
    public float floatHeight = 0.5f;
    public bool bounceEffect = true;
    public float bounceAmount = 0.3f;

    [Header("Appearance")]
    public Color textColor = Color.white;
    public float fontSize = 4f;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
    public FontStyles fontStyle = FontStyles.Normal;

    [Header("Background")]
    public bool showBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Vector2 backgroundPadding = new Vector2(0.5f, 0.3f);
    public float backgroundCornerRadius = 0.2f;

    private TextMeshPro textMeshPro;
    private Transform textTransform;
    private SpriteRenderer backgroundRenderer;
    private Coroutine barkCoroutine;
    private Coroutine autoBarkCoroutine;
    private Vector2 originalPosition;
    private Sprite backgroundSprite;

    private void Awake()
    {
        // Create a new GameObject for the text
        GameObject textObject = new GameObject("BarkText");
        textObject.transform.SetParent(transform);
        textObject.transform.localPosition = (Vector3)textOffset;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one * textScale;

        // Set the sorting layer for 2D rendering
        textObject.layer = gameObject.layer;

        // Add and configure TextMeshPro component
        textMeshPro = textObject.AddComponent<TextMeshPro>();
        textTransform = textObject.transform;

        // Configure text properties
        textMeshPro.text = "";
        textMeshPro.color = textColor;
        textMeshPro.fontSize = fontSize;
        textMeshPro.alignment = textAlignment;
        textMeshPro.fontStyle = fontStyle;
        textMeshPro.autoSizeTextContainer = true;
        textMeshPro.textWrappingMode = TextWrappingModes.Normal;
        textMeshPro.sortingOrder = 10; // Higher than character for visibility

        // Create background if enabled
        if (showBackground)
        {
            CreateBackground();
        }

        // Start with text hidden
        textObject.SetActive(false);

        originalPosition = textTransform.localPosition;
    }

    private void CreateBackground()
    {
        // Create background GameObject
        GameObject bgObject = new GameObject("BarkBackground");
        bgObject.transform.SetParent(textTransform);
        bgObject.transform.localPosition = Vector3.zero;
        bgObject.transform.localScale = Vector3.one;

        // Add sprite renderer
        backgroundRenderer = bgObject.AddComponent<SpriteRenderer>();
        
        // Create a white square texture for the background
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        
        backgroundSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        backgroundRenderer.sprite = backgroundSprite;
        backgroundRenderer.color = backgroundColor;
        backgroundRenderer.sortingOrder = 9; // Behind text but above character
        
        // Make it a circle/rounded square by using a 9-slice or custom shader
        // For now we'll use a simple square
        UpdateBackgroundSize();
    }

    private void UpdateBackgroundSize()
    {
        if (backgroundRenderer == null || string.IsNullOrEmpty(textMeshPro.text))
            return;

        // Get text bounds
        Vector2 textSize = textMeshPro.GetRenderedValues(false);
        
        // Add padding
        Vector2 bgSize = textSize + backgroundPadding * 2;
        bgSize.x = Mathf.Max(bgSize.x, 0.5f);
        bgSize.y = Mathf.Max(bgSize.y, 0.3f);
        
        backgroundRenderer.transform.localScale = new Vector3(bgSize.x, bgSize.y, 1f);
    }

    private void LateUpdate()
    {
        // Handle character flipping for 2D
        if (flipTextWithCharacter)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.flipX)
            {
                textTransform.localScale = new Vector3(-textScale, textScale, 1f);
            }
            else
            {
                textTransform.localScale = new Vector3(textScale, textScale, 1f);
            }
        }
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
        // Reset position
        textTransform.localPosition = (Vector3)originalPosition;

        // Update background size
        if (showBackground)
        {
            UpdateBackgroundSize();
        }

        // Show the text
        textMeshPro.gameObject.SetActive(true);

        // Store original scale
        Vector3 originalScale = textTransform.localScale;
        Vector3 targetScale = originalScale * animationScale;

        // Animate the bark if enabled
        if (animateBark)
        {
            // Pop in animation
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                
                // Ease out cubic for a nice pop effect
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                // Scale animation
                textTransform.localScale = Vector3.Lerp(originalScale, targetScale, easedT);

                // Float upwards during animation
                if (floatUpwards)
                {
                    float floatT = easedT * floatHeight;
                    Vector2 pos = originalPosition + Vector2.up * floatT;
                    
                    // Add bounce effect
                    if (bounceEffect && t < 0.5f)
                    {
                        float bounceT = Mathf.Sin(t * Mathf.PI * 4f) * bounceAmount * (1f - t);
                        pos += Vector2.up * bounceT;
                    }
                    
                    textTransform.localPosition = (Vector3)pos;
                }

                yield return null;
            }

            // Hold at max scale briefly
            yield return new WaitForSeconds(0.1f);

            // Scale back down to original
            elapsed = 0f;
            while (elapsed < animationDuration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (animationDuration * 0.7f);
                // Ease in cubic
                float easedT = t * t * t;
                textTransform.localScale = Vector3.Lerp(targetScale, originalScale, easedT);

                // Continue floating upwards slightly
                if (floatUpwards)
                {
                    float floatT = floatHeight * (1f + easedT * 0.2f);
                    textTransform.localPosition = (Vector3)(originalPosition + Vector2.up * floatT);
                }

                yield return null;
            }

            // Ensure we end at original scale
            textTransform.localScale = originalScale;
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

        // Clean up background sprite
        if (backgroundSprite != null)
        {
            Destroy(backgroundSprite);
        }
    }

    /// <summary>
    /// Update the text color at runtime
    /// </summary>
    public void SetTextColor(Color newColor)
    {
        if (textMeshPro != null)
        {
            textMeshPro.color = newColor;
        }
    }

    /// <summary>
    /// Update the font size at runtime
    /// </summary>
    public void SetFontSize(float newSize)
    {
        if (textMeshPro != null)
        {
            textMeshPro.fontSize = newSize;
        }
    }

    /// <summary>
    /// Set a new position offset for the text
    /// </summary>
    public void SetTextOffset(Vector2 newOffset)
    {
        textOffset = newOffset;
        if (textTransform != null)
        {
            originalPosition = textOffset;
            textTransform.localPosition = (Vector3)textOffset;
        }
    }

    /// <summary>
    /// Immediately hide the current bark
    /// </summary>
    public void HideBark()
    {
        if (barkCoroutine != null)
        {
            StopCoroutine(barkCoroutine);
            barkCoroutine = null;
        }
        
        if (textMeshPro != null && textMeshPro.gameObject.activeSelf)
        {
            textMeshPro.gameObject.SetActive(false);
        }
    }
}