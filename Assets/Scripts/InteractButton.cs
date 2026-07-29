using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public static InteractButton Instance { get; private set; }

    // Fired on every press, regardless of what (if anything) is currently
    // wired up via SetInteraction. Other systems (e.g. PushableCrate) can
    // subscribe to react to presses without going through the dialogue path.
    public event System.Action OnPressed;

    private string dialogue;
    private bool hasConditions;
    private bool conditionsCancel;
    private List<DialogueStarter.DialogueCondition> conditions;
    private DialogueStarter source; // Which DialogueStarter set this interaction, so it can be marked as played

    private void Awake()
    {
        Instance = this;

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(OnClicked);
    }

    // Called by DialogueStarter when the player enters a click-NPC's trigger.
    public void SetInteraction(DialogueStarter starter, string dialogueTitle, string conversantName, bool conditionsEnabled, List<DialogueStarter.DialogueCondition> dialogueConditions, bool cancelConditions)
    {
        source = starter;
        dialogue = dialogueTitle;
        hasConditions = conditionsEnabled;
        conditions = dialogueConditions;
        conditionsCancel = cancelConditions;

        SetLabel($"Interact ({conversantName})");
    }

    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text;
    }

    // Clears the interaction when the player leaves the trigger area.
    public void ClearInteraction()
    {
        source = null;
        dialogue = null;
        hasConditions = false;
        conditions = null;
        conditionsCancel = false;

        SetLabel(string.Empty);
    }

    private void OnClicked()
    {
        OnPressed?.Invoke();

        if (string.IsNullOrEmpty(dialogue))
            return;

        DialogueStarter.EvaluateConditionsAndStart(dialogue, hasConditions, conditions, conditionsCancel);

        if (source != null && source.OnceTime)
            source.MarkAsPlayed();
    }
}