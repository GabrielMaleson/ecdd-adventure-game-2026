using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

public class DialogueStarter : MonoBehaviour
{
    [System.Serializable]
    public class DialogueCondition
    {
        public string Progress;
        public bool PlaysOtherDialogue;
        public string OtherDialogue;
    }

    public string Dialogue;
    public bool IsClickNPC;
    public bool OnceTime;
    public bool DoneOnce = false;
    public GameObject Notification;
    public Transform transformthing;

    public bool HasConditions;
    public List<DialogueCondition> Conditions;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            if (OnceTime)
            {
                return;
            }
            if (Notification != null)
            {
                Notification.SetActive(true);
            }
            if (IsClickNPC)
            {
                SendToInteractButton();
            }
            else
            {
                StartDialogue();
            }
        }
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(Dialogue, HasConditions, Conditions);
    }

    private void StartDialogue()
    {
        EvaluateConditionsAndStart(Dialogue, HasConditions, Conditions);
    }

    // Shared by DialogueStarter (non-click NPCs) and InteractButton (click NPCs) so
    // the condition check only lives in one place.
    public static void EvaluateConditionsAndStart(string dialogue, bool hasConditions, List<DialogueCondition> conditions)
    {
        if (hasConditions && conditions != null)
        {
            foreach (var condition in conditions)
            {
                if (SaveManager.Instance != null && SaveManager.Instance.HasProgress(condition.Progress))
                    continue;

                if (condition.PlaysOtherDialogue)
                    DialogueManager.StartConversation(condition.OtherDialogue);
                return;
            }
        }

        DialogueManager.StartConversation(dialogue);
    }
}
