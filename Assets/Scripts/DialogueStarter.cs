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
        // A trigger whose interaction covers SEVERAL characters gets a box that reaches all
        // of them, recomputed here rather than trusted from the scene: the friends are moved
        // to the elder's house by <<formation>> before this object is enabled, so the right
        // box can only be known now. Re-centring on one member instead is what made walking
        // up to Erika do nothing while Marcus worked.
        if (InteractSettings.TryGroupBox(transform, out Vector2 groupOffset, out Vector2 groupSize))
        {
            BoxCollider2D groupBox = GetComponent<BoxCollider2D>();
            if (groupBox != null)
            {
                groupBox.offset = groupOffset;
                groupBox.size = groupSize;
                return;
            }
        }

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // "Cheguei perto e nao apareceu nada" tem quatro causas que de fora sao iguais:
        // Is Click NPC desmarcado (dispara ao pisar, sem prompt), ja jogado com Once Time,
        // sem no de dialogo escrito, ou sem InteractButton na cena. Cada uma se identifica.
        if (!IsClickNPC)
            Debug.Log($"[DialogueStarter] '{name}': entrei no trigger, mas IS CLICK NPC esta " +
                      "DESMARCADO — ele dispara ao pisar, sem prompt de E.", this);
        else if (OnceTime && hasPlayed)
            Debug.Log($"[DialogueStarter] '{name}': ja foi jogado uma vez (Once Time).", this);
        else if (string.IsNullOrEmpty(Dialogue))
            Debug.LogWarning($"[DialogueStarter] '{name}': o campo Dialogue esta VAZIO — nao " +
                             "ha no de .yarn para disparar.", this);
        else if (InteractButton.Instance == null)
            Debug.LogWarning($"[DialogueStarter] '{name}': nao ha InteractButton na cena.", this);
        else
            Debug.Log($"[DialogueStarter] '{name}': prompt oferecido para o no '{Dialogue}'.", this);
#endif

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
        // transformthing is handed over as the prompt's anchor because this script's own
        // transform is very often NOT where the thing you're talking to is: the trigger is
        // a zone laid over a patch of ground, positioned by its collider offset. The
        // marker already means "where this zone really is" — the same field that
        // re-centres the collider in Start — so the floating E lands on the character
        // instead of on the corner of an invisible box. Null just falls back to this
        // object's transform, which is correct for a trigger sitting on its own subject.
        InteractButton.Instance?.SetInteraction(this, "E", OnInteractPressed, transformthing);
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