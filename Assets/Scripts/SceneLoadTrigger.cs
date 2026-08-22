using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Loads another scene — the area transition (graveyard → crypt interior, etc.).
//
// Two entry modes (pick per instance in the Inspector):
//   OnTouch — player walks into the trigger and the scene loads. Good for a
//             doorway/stairs you just step onto.
//   PressE  — player walks into range, then presses E.
//
// Gating it to "only after the puzzle is solved": there is NO logic for that
// here on purpose. Just leave this object INACTIVE until the crypt opens, then
// flip it on from the same moment that opens the crypt — either the crate
// puzzle's CrateTarget.onAllTargetsCovered (SetActive true), or the Pickup's
// show[] list. While inactive it can't fire, so no extra checks are needed.
// (Simplest wiring: put this on/under the OpenCrypt object that the solve
// already switches on.)
//
// You can also skip the walk entirely and call Load() straight from a
// UnityEvent (e.g. onAllTargetsCovered -> SceneLoadTrigger.Load) to jump to the
// new scene the instant the puzzle is solved.
//
// The target scene MUST be added to File -> Build Settings or the load throws.
// This is a plain single-scene load: the DontDestroyOnLoad managers (Dialogue
// Manager, SaveManager and its progress flags) survive; the destination scene's
// own Player is used, positioned wherever you place it there.
//
// Needs a Collider2D with Is Trigger ON; the player must have the "Player" tag.
[RequireComponent(typeof(Collider2D))]
public class SceneLoadTrigger : MonoBehaviour
{
    public enum EnterMode { OnTouch, PressE }

    [Header("Where it goes")]
    [Tooltip("Exact name of the scene to load. Must be in File -> Build Settings.")]
    [SerializeField] string sceneToLoad;

    [Header("How it's entered")]
    [Tooltip("OnTouch: loads when the player walks in. PressE: walk into range, then press E.")]
    [SerializeField] EnterMode enterMode = EnterMode.OnTouch;

    [Tooltip("PressE only — prompt shown on the interact label while in range.")]
    [SerializeField] string interactLabel = "E";

    [Tooltip("Extra hook fired the instant before the scene loads — a sound, a fade, a save flag. Optional.")]
    public UnityEvent onBeforeLoad;

    bool loading;
    bool playerInRange;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (enterMode == EnterMode.OnTouch)
        {
            Load();
            return;
        }

        // PressE: register the prompt, InteractButton handles the keypress.
        playerInRange = true;
        InteractButton.Instance?.SetInteraction(this, interactLabel, Load);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    void OnDisable()
    {
        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // Public so a UnityEvent (onAllTargetsCovered, a Pickup's onCollected, a
    // dialogue) can drive the transition directly, no trigger-walk needed.
    public void Load()
    {
        if (loading) return;               // load once, ignore repeat contacts
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning($"{name}: SceneLoadTrigger has no scene set.", this);
            return;
        }
        loading = true;

        onBeforeLoad?.Invoke();
        SceneManager.LoadScene(sceneToLoad);
    }
}
