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
    private bool playerInRange = false;

    // The trigger is a collider on THIS object, so "where the cutscene starts" used to
    // be a Box Collider offset — invisible in the Hierarchy and impossible to line up
    // with anything by eye. With a marker dropped in transformthing, the collider is
    // re-centred on that marker at Start instead, so dragging the marker in the scene
    // is what decides where the scene fires. Nothing else moves and nobody walks: the
    // player reaches the spot himself and the dialogue catches him there.
    private void Start()
    {
        if (transformthing == null) return;

        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger == null)
        {
            Debug.LogWarning($"DialogueStarter on '{name}' has a marker assigned but no Collider2D to centre on it.", this);
            return;
        }

        trigger.offset = transform.InverseTransformPoint(transformthing.position);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        playerInRange = true;

        if (OnceTime && hasPlayed)
            return;

        if (Notification != null)
            Notification.SetActive(true);

        if (IsClickNPC)
        {
            SendToInteractButton();
        }
        else if (StartDialogue())
        {
            // ONLY when the dialogue really began. Marking unconditionally killed
            // cutscenes outright: crossing a trigger whose Progress condition is not
            // met yet is refused by EvaluateConditionsAndStart, but it still counted
            // as "played", so the scene could never fire once the condition was
            // finally satisfied. With a OnceTime trigger laid over a road the player
            // walks early, that is a guaranteed loss, not an edge case.
            MarkAsPlayed();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        playerInRange = false;

        if (Notification != null)
            Notification.SetActive(false);

        if (IsClickNPC)
            ClearInteractButton();
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(this, "E", OnInteractPressed);
    }

    private void ClearInteractButton()
    {
        InteractButton.Instance?.ClearInteraction(this);
    }

    private void OnInteractPressed()
    {
        bool started = StartDialogue();

        if (OnceTime && started)
        {
            MarkAsPlayed(); // never needs the prompt again
            return;
        }

        // Repeatable dialogue: InteractButton already blanked the prompt so E can't be
        // spammed mid-conversation — re-arm it once the dialogue actually ends, if the
        // player is still standing here.
        Yarn.Unity.DialogueRunner runner = DialogueManager.Instance?.dialogueRunner;
        if (runner == null)
            return;

        void OnComplete()
        {
            runner.onDialogueComplete.RemoveListener(OnComplete);
            if (playerInRange)
                SendToInteractButton();
        }
        runner.onDialogueComplete.AddListener(OnComplete);
    }

    // Returns whether THIS trigger's dialogue actually started, so a refused start is
    // never recorded as having played.
    private bool StartDialogue()
    {
        return EvaluateConditionsAndStart(Dialogue, HasConditions, Conditions, ConditionsCancel);
    }

    public static bool EvaluateConditionsAndStart(string dialogue, bool hasConditions, List<DialogueCondition> conditions, bool conditionsCancel)
    {
        if (hasConditions && conditions != null)
        {
            foreach (var condition in conditions)
            {
                bool hasProgress = SaveManager.Instance != null && SaveManager.Instance.HasProgress(condition.Progress);

                if (conditionsCancel && hasProgress)
                    return false;

                if (!conditionsCancel && !hasProgress)
                {
                    // The fallback is a DIFFERENT dialogue — playing it does not mean
                    // this trigger's own dialogue happened, so still false.
                    if (condition.PlaysOtherDialogue)
                        StartAndFreezePlayer(condition.OtherDialogue);

                    return false;
                }
            }
        }

        return StartAndFreezePlayer(dialogue);
    }

    // Single choke point every dialogue start funnels through, so the player is
    // stopped the instant a dialogue actually begins (not when it's merely
    // requested — a cancelled condition or a failed StartDialogue call must not
    // freeze him with nothing left to unfreeze him).
    private static bool StartAndFreezePlayer(string dialogueName)
    {
        DialogueManager manager = DialogueManager.Instance;
        manager?.StartDialogue(dialogueName);

        Yarn.Unity.DialogueRunner runner = manager?.dialogueRunner;
        if (runner == null || !runner.IsDialogueRunning)
            return false;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        PlayerController player = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
        if (player == null)
            return true;   // the dialogue IS running, only the freeze could not be applied

        player.InputEnabled = false;

        void OnComplete()
        {
            player.InputEnabled = true;
            runner.onDialogueComplete.RemoveListener(OnComplete);
        }
        runner.onDialogueComplete.AddListener(OnComplete);
        return true;
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