using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public static InteractButton Instance { get; private set; }

    public event System.Action OnPressed;

    private InteractDialogue currentInteractDialogue;
    private DialogueStarter currentDialogueStarter;

    private void Awake()
    {
        Instance = this;

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(OnClicked);
    }

    public void SetInteraction(DialogueStarter starter, string dialogueTitle, string conversantName, bool conditionsEnabled, List<DialogueStarter.DialogueCondition> dialogueConditions, bool cancelConditions)
    {
        currentDialogueStarter = starter;
        currentInteractDialogue = null;
        SetLabel($"Interact ({conversantName})");
    }

    public void SetInteraction(InteractDialogue interactDialogue, string labelText)
    {
        currentInteractDialogue = interactDialogue;
        currentDialogueStarter = null;
        SetLabel(labelText);
    }

    // What the label currently READS. Six unrelated scripts write to this one label with
    // no coordination (DialogueStarter, InteractText, Pickup, StatueSwitch, Teleporter,
    // SceneLoadTrigger), so a caller that only writes when its own state changes can be
    // silently overwritten and never notice. Exposing the text lets such a caller check
    // whether it still owns the label and re-assert if not.
    public string CurrentLabel => label != null ? label.text : string.Empty;

    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    public void ClearInteraction()
    {
        currentDialogueStarter = null;
        currentInteractDialogue = null;
        SetLabel(string.Empty);
    }

    private void OnClicked()
    {
        OnPressed?.Invoke();

        if (currentInteractDialogue != null)
        {
            currentInteractDialogue.OnInteractPressed();
            return;
        }

        if (currentDialogueStarter != null)
        {
            string dialogue = currentDialogueStarter.Dialogue;
            bool hasConditions = currentDialogueStarter.HasConditions;
            var conditions = currentDialogueStarter.Conditions;
            bool conditionsCancel = currentDialogueStarter.ConditionsCancel;

            DialogueStarter.EvaluateConditionsAndStart(dialogue, hasConditions, conditions, conditionsCancel);

            if (currentDialogueStarter.OnceTime)
                currentDialogueStarter.MarkAsPlayed();
        }
    }
}