using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeInAndEnlarge : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Duration of the animation in seconds")]
    public float animationDuration = 1.0f;

    [Tooltip("Starting scale of the image")]
    public Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);

    [Tooltip("Target scale of the image")]
    public Vector3 targetScale = Vector3.one;

    [Tooltip("Starting alpha value (0 = transparent, 1 = opaque)")]
    [Range(0f, 1f)]
    public float startAlpha = 0f;

    [Tooltip("Target alpha value")]
    [Range(0f, 1f)]
    public float targetAlpha = 1f;

    [Header("Animation Curve")]
    [Tooltip("Curve for animation easing (default is linear)")]
    public AnimationCurve animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Options")]
    [Tooltip("Should the animation play on Start?")]
    public bool playOnStart = true;

    [Tooltip("Should the animation loop?")]
    public bool loop = false;

    [Tooltip("Delay before animation starts")]
    public float startDelay = 0f;

    private Image imageComponent;
    private RectTransform rectTransform;
    private Coroutine animationCoroutine;

    void Awake()
    {
        // Get required components
        imageComponent = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        if (imageComponent == null)
        {
            Debug.LogError("FadeInAndEnlarge requires an Image component!", this);
            enabled = false;
            return;
        }

        if (rectTransform == null)
        {
            Debug.LogError("FadeInAndEnlarge requires a RectTransform component!", this);
            enabled = false;
            return;
        }
    }

    void Start()
    {
        if (playOnStart)
        {
            PlayAnimation();
        }
    }

    /// <summary>
    /// Starts the fade in and enlarge animation
    /// </summary>
    public void PlayAnimation()
    {
        // Stop any existing animation
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateImage());
    }

    /// <summary>
    /// Stops the current animation
    /// </summary>
    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }

    /// <summary>
    /// Resets the image to its starting state
    /// </summary>
    public void ResetToStart()
    {
        StopAnimation();

        if (rectTransform != null)
        {
            rectTransform.localScale = startScale;
        }

        if (imageComponent != null)
        {
            Color color = imageComponent.color;
            color.a = startAlpha;
            imageComponent.color = color;
        }
    }

    private IEnumerator AnimateImage()
    {
        // Apply start delay if any
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        // Set initial state
        rectTransform.localScale = startScale;
        Color imageColor = imageComponent.color;
        imageColor.a = startAlpha;
        imageComponent.color = imageColor;

        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / animationDuration);

            // Apply animation curve
            float curveValue = animationCurve.Evaluate(normalizedTime);

            // Interpolate scale
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);

            // Interpolate alpha
            imageColor.a = Mathf.Lerp(startAlpha, targetAlpha, curveValue);
            imageComponent.color = imageColor;

            yield return null;
        }

        // Ensure final values are exact
        rectTransform.localScale = targetScale;
        imageColor.a = targetAlpha;
        imageComponent.color = imageColor;

        // Loop if enabled
        if (loop)
        {
            yield return new WaitForSeconds(0.5f); // Brief pause before looping
            animationCoroutine = StartCoroutine(AnimateImage());
        }
        else
        {
            animationCoroutine = null;
        }
    }

    /// <summary>
    /// Plays the animation in reverse (fade out and shrink)
    /// </summary>
    public void PlayReverseAnimation()
    {
        // Swap start and target values temporarily
        Vector3 tempScale = startScale;
        float tempAlpha = startAlpha;

        startScale = targetScale;
        targetScale = tempScale;

        startAlpha = targetAlpha;
        targetAlpha = tempAlpha;

        PlayAnimation();

        // Swap back after a frame
        StartCoroutine(SwapBackNextFrame(tempScale, tempAlpha));
    }

    private IEnumerator SwapBackNextFrame(Vector3 originalStartScale, float originalStartAlpha)
    {
        yield return null;

        // Restore original values
        targetScale = startScale;
        startScale = originalStartScale;

        targetAlpha = startAlpha;
        startAlpha = originalStartAlpha;
    }
}