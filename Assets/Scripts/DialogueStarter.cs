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
        if (!collision.CompareTag("Player"))
            return;

        if (OnceTime && hasPlayed)
            return;

        if (Notification != null)
            Notification.SetActive(true);

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

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        if (Notification != null)
            Notification.SetActive(false);

        if (IsClickNPC)
            ClearInteractButton();
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(this, Dialogue, ConversantName, HasConditions, Conditions, ConditionsCancel);
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
                    return;

                if (!conditionsCancel && !hasProgress)
                {
                    if (condition.PlaysOtherDialogue)
                        StartAndFreezePlayer(condition.OtherDialogue);

                    return;
                }
            }
        }

        StartAndFreezePlayer(dialogue);
    }

    // Single choke point every dialogue start funnels through, so the player is
    // stopped the instant a dialogue actually begins (not when it's merely
    // requested — a cancelled condition or a failed StartDialogue call must not
    // freeze him with nothing left to unfreeze him).
    private static void StartAndFreezePlayer(string dialogueName)
    {
        DialogueManager manager = DialogueManager.Instance;
        manager?.StartDialogue(dialogueName);

        Yarn.Unity.DialogueRunner runner = manager?.dialogueRunner;
        if (runner == null || !runner.IsDialogueRunning)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        PlayerController player = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
        if (player == null)
            return;

        player.InputEnabled = false;

        void OnComplete()
        {
            player.InputEnabled = true;
            runner.onDialogueComplete.RemoveListener(OnComplete);
        }
        runner.onDialogueComplete.AddListener(OnComplete);
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