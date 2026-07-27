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
        InteractButton.Instance?.SetInteraction(Dialogue, ConversantName, HasConditions, Conditions, ConditionsCancel);
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
}