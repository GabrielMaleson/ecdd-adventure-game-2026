using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenTransition : MonoBehaviour
{
    public static ScreenTransition Instance { get; private set; }

    [Header("UI References")]
    public Image blackOverlay; // Full-screen Image with black color
    public float transitionDuration = 1f; // Total duration for fill + remove

    [Header("Fill Settings")]
    public AnimationCurve fillCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve removeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private RectTransform overlayRect;
    private bool isTransitioning = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Set up overlay
        if (blackOverlay == null)
        {
            Debug.LogError("Black overlay Image not assigned to ScreenTransition!");
            return;
        }

        overlayRect = blackOverlay.GetComponent<RectTransform>();

        // Ensure overlay is full screen
        blackOverlay.rectTransform.anchorMin = Vector2.zero;
        blackOverlay.rectTransform.anchorMax = Vector2.one;
        blackOverlay.rectTransform.offsetMin = Vector2.zero;
        blackOverlay.rectTransform.offsetMax = Vector2.zero;
        QuickTransition();
    }

    // Set the fill amount of the overlay (0 = invisible, 1 = full screen black)
    private void SetOverlayFill(float fillAmount)
    {
        if (blackOverlay == null) return;

        // For bottom-to-top fill, we adjust the anchorMin.y
        // At fill=0: anchorMin.y = 1 (top) - overlay is invisible (at top)
        // At fill=1: anchorMin.y = 0 (bottom) - overlay covers entire screen
        overlayRect.anchorMin = new Vector2(0, 1 - fillAmount);
        overlayRect.anchorMax = Vector2.one;

        // Reset offsets to ensure full width
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
    }

    // Play transition: fill bottom to top, then remove top to bottom
    public void PlayTransition(System.Action onComplete = null)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("Transition already in progress!");
            return;
        }

        StartCoroutine(TransitionRoutine(onComplete));
    }

    private IEnumerator TransitionRoutine(System.Action onComplete)
    {
        isTransitioning = true;

        // Phase 1: Fill from bottom to top (0 to 1)
        float halfDuration = transitionDuration / 2f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float curveValue = fillCurve.Evaluate(t);

            SetOverlayFill(curveValue);

            yield return null;
        }

        // Ensure full black at the end of phase 1
        SetOverlayFill(1f);

        // Brief pause at full black (optional)
        yield return new WaitForSeconds(0.1f);

        // Phase 2: Remove from top to bottom (1 to 0)
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float curveValue = removeCurve.Evaluate(t);

            SetOverlayFill(curveValue);

            yield return null;
        }

        // Ensure completely visible at end
        SetOverlayFill(0f);

        isTransitioning = false;

        // Call completion callback
        onComplete?.Invoke();
    }

    // Fast transition without waiting (for testing)
    public void QuickTransition()
    {
        StartCoroutine(QuickTransitionRoutine());
    }

    private IEnumerator QuickTransitionRoutine()
    {
        SetOverlayFill(1f);
        float halfDuration = transitionDuration / 2f;
        float elapsed = 0f;
        yield return new WaitForSeconds(0.2f);

        // Phase 2: Remove from top to bottom (1 to 0)
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float curveValue = removeCurve.Evaluate(t);

            SetOverlayFill(curveValue);

            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
        SetOverlayFill(0f);
    }

    // Utility method to check if transitioning
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
}