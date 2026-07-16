using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
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
    private List<DialogueStarter.DialogueCondition> conditions;

    private void Awake()
    {
        Instance = this;

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(OnClicked);
    }

    // Called by DialogueStarter when the player enters a click-NPC's trigger.
    public void SetInteraction(string dialogueTitle, bool conditionsEnabled, List<DialogueStarter.DialogueCondition> dialogueConditions)
    {
        dialogue = dialogueTitle;
        hasConditions = conditionsEnabled;
        conditions = dialogueConditions;

        SetLabel($"Interact ({GetConversantName(dialogueTitle)})");
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
            DialogueStarter.EvaluateConditionsAndStart(dialogue, hasConditions, conditions);
    }

    private string GetConversantName(string dialogueTitle)
    {
        if (DialogueManager.masterDatabase == null) return string.Empty;

        Conversation conversation = DialogueManager.masterDatabase.GetConversation(dialogueTitle);
        if (conversation == null) return string.Empty;

        Actor conversant = DialogueManager.masterDatabase.GetActor(conversation.ConversantID);
        return conversant != null ? conversant.Name : string.Empty;
    }
}
