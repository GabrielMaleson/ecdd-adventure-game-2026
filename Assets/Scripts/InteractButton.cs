using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// The single interact prompt: a TMP label that shows "E" while something interactable
// is in range and is blank otherwise, driven by a centralized E keypress instead of a
// clickable UI Button. Every interactable (DialogueStarter, InteractDialogue,
// Teleporter, SceneLoadTrigger, StatueSwitch, Pickup) registers itself here instead of
// polling the key and writing the label directly — that used to mean six scripts all
// wrote the SAME label with no coordination, so one could silently stomp another's
// prompt. Now there's exactly one registration at a time, with an explicit owner.
public class InteractButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    public static InteractButton Instance { get; private set; }

    // Fired on every press, AFTER the registered action (if any) already ran. Lets
    // other systems react to any interaction happening without needing to own the
    // prompt themselves (e.g. BarkDirector ducking the player's bark on interact).
    public event System.Action OnPressed;

    // Whichever caller currently owns the prompt — pass `this` from any MonoBehaviour.
    // Used only for identity checks in ClearInteraction, never dereferenced.
    private object owner;
    private System.Action onPress;

    private void Awake()
    {
        Instance = this;
        SetLabel(string.Empty);
    }

    private void Update()
    {
        if (onPress == null)
            return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Press();
    }

    // Registers what's currently interactable: who's registering (pass `this`), the
    // label to show, and what happens when E is pressed. Calling this again — e.g. a
    // different interactable now in range — simply replaces whatever was registered
    // before, no need to clear first.
    public void SetInteraction(object caller, string labelText, System.Action action)
    {
        owner = caller;
        onPress = action;
        SetLabel(labelText);
    }

    // Only clears if the caller is still the current owner — an old registration's
    // late exit (its trigger fires after a NEWER interactable already took over the
    // prompt) must not blank out someone else's active prompt.
    public void ClearInteraction(object caller)
    {
        if (owner != caller)
            return;

        owner = null;
        onPress = null;
        SetLabel(string.Empty);
    }

    private void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    private void Press()
    {
        // Blank and drop the registration immediately, so a held/spammed E can't
        // re-trigger mid-interaction. Whatever registered this is responsible for
        // calling SetInteraction again once it's ready to be interacted with again
        // (e.g. once dialogue ends and the player is still in range).
        System.Action action = onPress;
        owner = null;
        onPress = null;
        SetLabel(string.Empty);

        action?.Invoke();
        OnPressed?.Invoke();
    }
}
