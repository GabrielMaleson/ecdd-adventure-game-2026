using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Flashes whatever object is in the way when a board action is refused.
//
// The point is to tell "the statue is BLOCKED" apart from "my input didn't register".
// Without any feedback those two look identical, and the player's reaction to both is
// to mash E — which is exactly the reported symptom of the crate-push stalls. A refusal
// the player can SEE reads as a puzzle rule; a silent refusal reads as a bug.
//
// Setup: none required. The statue calls BlockedFlash.Play(blocker) and a runner is
// created on demand. Drop a BlockedFlash component in the scene ONLY if you want to
// tune the colour/timing — a scene instance is picked up automatically and its values
// win. Nothing needs to be added to the crates themselves.
public class BlockedFlash : MonoBehaviour
{
    [Tooltip("Colour multiplied over the blocked object while it pulses.")]
    [SerializeField] Color flashColor = new Color(1f, 0.45f, 0.4f, 1f);

    [Tooltip("Length of ONE pulse (fade in + out).")]
    [SerializeField] float pulseDuration = 0.18f;

    [Tooltip("How many times it pulses per refusal.")]
    [SerializeField] int pulses = 2;

    static BlockedFlash instance;

    // Objects currently mid-flash. A second refusal while one is still pulsing must not
    // start a competing coroutine — the two would race to restore the colour and one
    // would win with a stale value, leaving the crate permanently tinted.
    static readonly HashSet<GameObject> flashing = new HashSet<GameObject>();

    void Awake()
    {
        // An explicitly placed component wins, so tuning in the Inspector actually takes
        // effect instead of being shadowed by an auto-created runner.
        if (instance == null || instance.autoCreated)
            instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    bool autoCreated;

    public static void Play(GameObject target)
    {
        if (target == null) return;

        if (instance == null)
        {
            var host = new GameObject("BlockedFlash (auto)");
            instance = host.AddComponent<BlockedFlash>();
            instance.autoCreated = true;
        }

        instance.Flash(target);
    }

    public void Flash(GameObject target)
    {
        if (target == null || flashing.Contains(target)) return;

        // Renderers on children, not just the root: every character/prop prefab here
        // keeps its artwork on a visual child, so asking the root alone finds nothing.
        var renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return;

        StartCoroutine(FlashRoutine(target, renderers));
    }

    IEnumerator FlashRoutine(GameObject target, SpriteRenderer[] renderers)
    {
        flashing.Add(target);

        var original = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            original[i] = renderers[i].color;

        for (int p = 0; p < Mathf.Max(1, pulses); p++)
        {
            float t = 0f;
            while (t < pulseDuration)
            {
                t += Time.deltaTime;
                // Up then back down over the pulse, so it reads as a throb rather than
                // a hard colour swap.
                float k = Mathf.Sin(Mathf.Clamp01(t / pulseDuration) * Mathf.PI);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].color = Color.Lerp(original[i], flashColor, k);
                }
                yield return null;
            }
        }

        // Restore from the snapshot rather than to white — the crates may legitimately
        // be tinted by something else, and clobbering that would be a new bug.
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = original[i];

        flashing.Remove(target);
    }
}
