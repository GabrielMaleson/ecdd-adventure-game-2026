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

    private void Awake()
    {
        Instance = this;

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(OnClicked);
    }

    // Called by DialogueStarter when the player enters a click-NPC's trigger.
    public void SetInteraction(string dialogueTitle, string conversantName, bool conditionsEnabled, List<DialogueStarter.DialogueCondition> dialogueConditions, bool cancelConditions)
    {
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

    private void OnClicked()
    {
        OnPressed?.Invoke();

        if (!string.IsNullOrEmpty(dialogue))
            DialogueStarter.EvaluateConditionsAndStart(dialogue, hasConditions, conditions, conditionsCancel);
    }
}