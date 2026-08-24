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
//
// WHERE the prompt is drawn has two modes, and the field decides:
//   World Label EMPTY    → the old behaviour, a fixed label parked on the Canvas.
//   World Label ASSIGNED → the prompt floats in the world, over whatever is
//                          interactable. Clearing the field reverts, no code change.
//
// Nothing is configured per object. The thing to hover over is the `caller` every
// interactable ALREADY passes in (they all pass `this`, which is a Component, so it
// carries a transform), and the height comes from that object's own sprite bounds. An
// interactable only needs to say anything at all when its trigger sits somewhere other
// than its art — a walk-into ZONE rather than a thing — and for those DialogueStarter
// hands over its `transformthing` marker, which already means "where this zone really is".
public class InteractButton : MonoBehaviour
{
    [Header("Screen Prompt (legacy)")]
    [Tooltip("Label fixo no Canvas. Usado apenas quando World Label esta vazio.")]
    [SerializeField] private TMP_Text label;

    [Header("World Prompt")]
    [Tooltip("TextMeshPro em WORLD SPACE (nao o UGUI). Preenchido = o prompt flutua sobre " +
             "o objeto interagivel. Esvaziar volta pro label do Canvas.")]
    [SerializeField] private TextMeshPro worldLabel;

    [Tooltip("Folga entre o topo do sprite do objeto e o prompt, em unidades.")]
    [SerializeField] private float heightMargin = 0.35f;

    [Tooltip("Altura usada quando o objeto nao tem sprite nenhum — um marcador vazio, " +
             "por exemplo. Objetos com sprite ignoram este campo e usam os proprios bounds.")]
    [SerializeField] private float fallbackHeight = 1.2f;

    public static InteractButton Instance { get; private set; }

    // Fired on every press, AFTER the registered action (if any) already ran. Lets
    // other systems react to any interaction happening without needing to own the
    // prompt themselves (e.g. BarkDirector ducking the player's bark on interact).
    public event System.Action OnPressed;

    // Whichever caller currently owns the prompt — pass `this` from any MonoBehaviour.
    // Used only for identity checks in ClearInteraction, never dereferenced.
    private object owner;
    private System.Action onPress;

    // What the world prompt hovers over, resolved once per registration rather than
    // every frame — GetComponentsInChildren is not a per-frame cost worth paying for
    // something that cannot change while one interactable stays registered.
    private Transform anchor;
    private SpriteRenderer[] anchorSprites;

    private bool UsingWorldPrompt => worldLabel != null;

    private void Awake()
    {
        Instance = this;

        // Clears whatever was authored into the Canvas label, so a leftover "E" can't sit
        // in the corner for the whole game once the world prompt takes over.
        if (label != null)
            label.text = string.Empty;

        ShowWorldPrompt(false);
    }

    private void Update()
    {
        if (onPress == null)
            return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
            Press();
    }

    // After everything has moved for the frame, so the prompt can't lag a frame behind a
    // crate being pushed or an NPC walking out from under it.
    private void LateUpdate()
    {
        if (!UsingWorldPrompt || onPress == null)
            return;

        // The interactable was switched off while the prompt was up — <<disable ElderAmos>>
        // with the player standing next to him is exactly this. Its OnTriggerExit never
        // runs, so without this the prompt would hang in the air over nobody, still armed.
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
        {
            owner = null;
            onPress = null;
            anchor = null;
            anchorSprites = null;
            ShowWorldPrompt(false);
            return;
        }

        worldLabel.transform.position = PromptPosition();
    }

    // Registers what's currently interactable: who's registering (pass `this`), the
    // label to show, and what happens when E is pressed. Calling this again — e.g. a
    // different interactable now in range — simply replaces whatever was registered
    // before, no need to clear first.
    //
    // promptAnchor is only for an interactable whose OWN transform is not where its art
    // is: a trigger zone laid over a stretch of ground. Everything whose transform is
    // the object itself leaves it out and gets the right answer for free.
    public void SetInteraction(object caller, string labelText, System.Action action, Transform promptAnchor = null)
    {
        owner = caller;
        onPress = action;

        if (promptAnchor == null)
            promptAnchor = (caller as Component)?.transform;

        anchor = promptAnchor;
        anchorSprites = anchor != null ? anchor.GetComponentsInChildren<SpriteRenderer>() : null;

        if (UsingWorldPrompt)
        {
            worldLabel.text = labelText;
            ShowWorldPrompt(!string.IsNullOrEmpty(labelText) && anchor != null);
            if (anchor != null)
                worldLabel.transform.position = PromptPosition();
        }
        else
        {
            SetLabel(labelText);
        }
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
        anchor = null;
        anchorSprites = null;
        SetLabel(string.Empty);
        ShowWorldPrompt(false);
    }

    // Top-centre of the interactable's artwork, plus the margin. Measured from the SPRITE
    // and not the transform because a transform tells you nothing about how tall a thing
    // is: the pivot of a 64px character at scale 4 sits in the middle of its own body,
    // while a key lying on the floor is a few pixels tall. Reading the bounds is what
    // makes one setting look right on a statue, an NPC and a dropped key at once, with
    // nothing typed in per object.
    private Vector3 PromptPosition()
    {
        float z = worldLabel.transform.position.z;

        if (anchorSprites != null && anchorSprites.Length > 0)
        {
            bool any = false;
            Bounds bounds = default;

            foreach (SpriteRenderer sprite in anchorSprites)
            {
                // A disabled renderer contributes nothing visible, so letting it into the
                // bounds would push the prompt off into empty space.
                if (sprite == null || !sprite.enabled || sprite.sprite == null)
                    continue;

                if (!any) { bounds = sprite.bounds; any = true; }
                else bounds.Encapsulate(sprite.bounds);
            }

            if (any)
                return new Vector3(bounds.center.x, bounds.max.y + heightMargin, z);
        }

        // No artwork to measure — a bare marker Transform. Nothing better than a number.
        return new Vector3(anchor.position.x, anchor.position.y + fallbackHeight, z);
    }

    private void ShowWorldPrompt(bool visible)
    {
        if (worldLabel != null)
            worldLabel.gameObject.SetActive(visible);
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
        anchor = null;
        anchorSprites = null;
        SetLabel(string.Empty);
        ShowWorldPrompt(false);

        action?.Invoke();
        OnPressed?.Invoke();
    }
}
