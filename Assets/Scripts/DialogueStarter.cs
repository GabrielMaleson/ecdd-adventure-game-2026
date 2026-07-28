using System.Collections.Generic;
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
    public string ConversantName;
    public bool IsClickNPC;
    public bool OnceTime;
    public GameObject Notification;
    public Transform transformthing;

    public bool HasConditions;
    public bool ConditionsCancel;
    public List<DialogueCondition> Conditions;

    private bool hasPlayed = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            // Check if OnceTime is enabled and dialogue has already been played
            if (OnceTime && hasPlayed)
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
                MarkAsPlayed(); // Auto-played dialogue counts as played
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            // Hide the notification when player leaves the trigger area
            if (Notification != null)
            {
                Notification.SetActive(false);
            }

            // Clear the Interact Button text when player leaves
            if (IsClickNPC)
            {
                ClearInteractButton();
            }
        }
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(Dialogue, ConversantName, HasConditions, Conditions, ConditionsCancel);
        // Store reference to this instance in the button so it can mark as played
        // We need to access the button's currentDialogueStarter field
        // Since we can't directly set it, let's use a different approach - we'll mark as played when the button is clicked
    }

    private void ClearInteractButton()
    {
        InteractButton.Instance?.ClearInteraction();
    }

    private void StartDialogue()
    {
        EvaluateConditionsAndStart(Dialogue, HasConditions, Conditions, ConditionsCancel);
    }

    public static void EvaluateConditionsAndStart(string dialogue, bool hasConditions, List<DialogueCondition> conditions, bool conditionsCancel)
    {
        if (hasConditions && conditions != null)
        {
            foreach (var condition in conditions)
            {
                bool hasProgress = SaveManager.Instance != null && SaveManager.Instance.HasProgress(condition.Progress);

                if (conditionsCancel && hasProgress)
                {
                    return;
                }

                if (!conditionsCancel && !hasProgress)
                {
                    if (condition.PlaysOtherDialogue)
                    {
                        DialogueManager.Instance?.StartDialogue(condition.OtherDialogue);
                    }
                    return;
                }

                if (!conditionsCancel && hasProgress)
                {
                    continue;
                }
            }
        }

        DialogueManager.Instance?.StartDialogue(dialogue);
    }

    // Call this method when dialogue actually starts to mark it as played
    public void MarkAsPlayed()
    {
        if (OnceTime)
        {
            hasPlayed = true;
        }
    }
}