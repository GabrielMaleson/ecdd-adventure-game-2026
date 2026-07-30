using UnityEngine;
using TMPro;
using System.Collections;

public class CharacterBark : MonoBehaviour
{
    public enum Mode
    {
        Bark,   // ambient chatter: StartAutoBark() fires random quotes on a timer
        Puzzle, // driven entirely by other scripts calling StartText(...); no auto-firing
    }

    [Header("Mode")]
    public Mode mode = Mode.Bark;

    [Header("Text Component")]
    [SerializeField] private TextMeshPro textMeshPro; // Assign in Inspector
    [SerializeField] private Transform textTransform;  // Optional: auto-detected if not assigned

    [Header("Quotes (Bark mode)")]
    [TextArea(2, 4)]
    public string[] quotes = new string[]
    {
        "Hello there!",
        "I'm feeling great today!",
        "What's new?",
    };

    [Header("Display Duration")]
    public float textDuration = 2.5f;
    public float minBarkDelay = 3f;
    public float maxBarkDelay = 8f;

    [Header("Animation")]
    public bool animate = true;
    public float animationScale = 1.2f;
    public float animationDuration = 0.3f;
    public bool floatUpwards = true;
    public float floatHeight = 0.5f;
    public bool bounceEffect = true;
    public float bounceAmount = 0.3f;

    [Header("Background")]
    public bool showBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Vector2 backgroundPadding = new Vector2(0.5f, 0.3f);

    private SpriteRenderer backgroundRenderer;
    private Coroutine displayCoroutine;
    private Coroutine autoBarkCoroutine;
    private Vector3 originalPosition;
    private Vector3 originalScale;

    private void Awake()
    {
        if (textMeshPro == null)
        {
            Debug.LogError($"{name}: CharacterBark has no TextMeshPro assigned.", this);
            enabled = false;
            return;
        }

        if (textTransform == null)
            textTransform = textMeshPro.transform;

        originalPosition = textTransform.localPosition;
        originalScale = textTransform.localScale;

        textMeshPro.text = "";
        textMeshPro.autoSizeTextContainer = true;
        textMeshPro.textWrappingMode = TextWrappingModes.Normal;

        if (textMeshPro.sortingOrder < 10)
            textMeshPro.sortingOrder = 10; // above the character sprite

        if (showBackground)
            CreateBackground();

        textMeshPro.gameObject.SetActive(false);
    }

    private void CreateBackground()
    {
        Transform bgTransform = textTransform.Find("TextBackground");
        GameObject bgObject;

        if (bgTransform != null)
        {
            bgObject = bgTransform.gameObject;
            backgroundRenderer = bgObject.GetComponent<SpriteRenderer>();
        }
        else
        {
            bgObject = new GameObject("TextBackground");
            bgObject.transform.SetParent(textTransform);
            bgObject.transform.localPosition = Vector3.zero;
            bgObject.transform.localScale = Vector3.one;
            backgroundRenderer = bgObject.AddComponent<SpriteRenderer>();
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        backgroundRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        backgroundRenderer.color = backgroundColor;
        backgroundRenderer.sortingOrder = textMeshPro.sortingOrder - 1; // behind the text
    }

    private void UpdateBackgroundSize()
    {
        if (backgroundRenderer == null || string.IsNullOrEmpty(textMeshPro.text))
            return;

        Vector2 textSize = textMeshPro.GetRenderedValues(false);
        Vector2 bgSize = textSize + backgroundPadding * 2;
        bgSize.x = Mathf.Max(bgSize.x, 0.5f);
        bgSize.y = Mathf.Max(bgSize.y, 0.3f);

        backgroundRenderer.transform.localScale = new Vector3(bgSize.x, bgSize.y, 1f);
    }

    // The general entry point: any script can call this to show arbitrary text here,
    // e.g. a puzzle button's onClick wired straight to StartText("Wrong combination.").
    public void StartText(string text) => StartText(text, textDuration);

    public void StartText(string text, float duration)
    {
        if (!enabled || textMeshPro == null) return;

        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);

        textMeshPro.text = text;
        displayCoroutine = StartCoroutine(ShowText(duration));
    }

    // Legacy/Bark-mode convenience wrappers — kept so anything already calling these
    // (code or Inspector UnityEvents) keeps working; they all funnel into StartText.
    public void Bark() => StartText(quotes.Length > 0 ? quotes[Random.Range(0, quotes.Length)] : string.Empty);
    public void Bark(string quote) => StartText(quote);

    public void Bark(int quoteIndex)
    {
        if (quoteIndex < 0 || quoteIndex >= quotes.Length)
        {
            Debug.LogWarning($"{name}: quote index {quoteIndex} out of range.", this);
            return;
        }
        StartText(quotes[quoteIndex]);
    }

    private IEnumerator ShowText(float duration)
    {
        textTransform.localPosition = originalPosition;
        textTransform.localScale = originalScale;

        if (showBackground)
            UpdateBackgroundSize();

        textMeshPro.gameObject.SetActive(true);

        if (animate)
            yield return PlayPopAnimation();

        yield return new WaitForSeconds(duration);

        textMeshPro.gameObject.SetActive(false);
        displayCoroutine = null;
    }

    private IEnumerator PlayPopAnimation()
    {
        Vector3 targetScale = originalScale * animationScale;

        // Pop in.
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            textTransform.localScale = Vector3.Lerp(originalScale, targetScale, easedT);

            if (floatUpwards)
            {
                Vector3 pos = originalPosition + Vector3.up * (easedT * floatHeight);
                if (bounceEffect && t < 0.5f)
                    pos += Vector3.up * (Mathf.Sin(t * Mathf.PI * 4f) * bounceAmount * (1f - t));

                textTransform.localPosition = pos;
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // Settle back to original scale.
        elapsed = 0f;
        float settleDuration = animationDuration * 0.7f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            textTransform.localScale = Vector3.Lerp(targetScale, originalScale, t * t * t); // ease-in cubic

            if (floatUpwards)
                textTransform.localPosition = originalPosition + Vector3.up * (floatHeight * (1f + t * t * t * 0.2f));

            yield return null;
        }

        textTransform.localScale = originalScale;
    }

    // Ambient chatter — Bark mode only; harmless to call in Puzzle mode but pointless.
    public void StartAutoBark()
    {
        if (autoBarkCoroutine != null)
            StopCoroutine(autoBarkCoroutine);
        autoBarkCoroutine = StartCoroutine(AutoBarkRoutine());
    }

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
            yield return new WaitForSeconds(Random.Range(minBarkDelay, maxBarkDelay));

            if (displayCoroutine == null)
                Bark();
        }
    }

    // Immediately hide whatever is currently displayed.
    public void HideText()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        if (textMeshPro != null && textMeshPro.gameObject.activeSelf)
            textMeshPro.gameObject.SetActive(false);
    }

    public void HideBark() => HideText(); // legacy alias

    public void SetTextColor(Color newColor)
    {
        if (textMeshPro != null)
            textMeshPro.color = newColor;
    }

    public void SetFontSize(float newSize)
    {
        if (textMeshPro != null)
            textMeshPro.fontSize = newSize;
    }

    public void SetTextOffset(Vector3 newOffset)
    {
        if (textTransform == null) return;
        originalPosition = newOffset;
        textTransform.localPosition = newOffset;
    }

    private void OnDestroy()
    {
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        if (autoBarkCoroutine != null) StopCoroutine(autoBarkCoroutine);
    }
}
