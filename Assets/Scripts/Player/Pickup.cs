using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// A collectible item: key, lore fragment, whatever.
//
// Two grab modes (pick per instance in the Inspector):
//   OnTouch  — walk onto it and it's collected. Good for keys.
//   PressE   — walk into range, then press E. Good for lore ("examine this").
//
// For now it does ONE thing on pickup — swap GameObjects: hide some, show others.
// That covers this puzzle's case (the grid crypt turns into a non-grid crypt) and
// simple "a door opens" cases. When a pickup needs a different KIND of effect,
// that's a deliberate add — ask and it gets its own handling rather than being
// guessed at up front.
//
// Needs a Collider2D with Is Trigger ON marking the grab area, and the player must
// have the "Player" tag.
[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    public enum GrabMode { OnTouch, PressE }

    [Header("How it's grabbed")]
    [Tooltip("OnTouch: collected by walking onto it. PressE: walk into range, then press E.")]
    [SerializeField] GrabMode grabMode = GrabMode.OnTouch;

    [Tooltip("PressE only — prompt shown on the interact label while in range.")]
    [SerializeField] string interactLabel = "Pick up (E)";

    [Header("On pickup — swap GameObjects")]
    [Tooltip("Objects switched OFF when collected (e.g. the grid crypt, the blocking bush).")]
    [SerializeField] GameObject[] hide;

    [Tooltip("Objects switched ON when collected (e.g. the non-grid crypt, an open-door sprite).")]
    [SerializeField] GameObject[] show;

    [Header("The item itself")]
    [Tooltip("Hide this whole object once grabbed, so the key visually disappears. Leave on for almost everything.")]
    [SerializeField] bool removeSelfOnPickup = true;

    [Tooltip("Extra hook for anything the swap above doesn't cover — sound, a dialogue trigger, a save flag. Optional; leave empty if unused.")]
    public UnityEvent onCollected;

    bool collected;
    bool playerInRange;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (grabMode == GrabMode.OnTouch)
        {
            Collect();
            return;
        }

        // PressE: arm the prompt, wait for the key in Update.
        playerInRange = true;
        InteractButton.Instance?.SetLabel(interactLabel);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    void Update()
    {
        if (grabMode != GrabMode.PressE || !playerInRange) return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Collect();
    }

    void Collect()
    {
        if (collected) return;                 // grab once, never re-fire
        collected = true;

        foreach (var go in hide) if (go != null) go.SetActive(false);
        foreach (var go in show) if (go != null) go.SetActive(true);

        onCollected?.Invoke();

        // Last — deactivating this object stops its own code, so anything above must
        // already have run.
        if (removeSelfOnPickup) gameObject.SetActive(false);
    }
}
