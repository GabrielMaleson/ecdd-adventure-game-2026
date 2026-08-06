using System.Collections;
using UnityEngine;

// "This character says this, at this moment" — wired entirely in the Inspector, no code
// per beat. Fire() is public, so anything that already exposes a UnityEvent can drive it:
// CrateTarget.onAllTargetsCovered, Pickup.onCollected, a cutscene, another BarkTrigger.
public class BarkTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        PlayerEnters,   // needs a trigger Collider2D on this object
        PlayerExits,
        OnStart,
        Manual          // only fires when something calls Fire()
    }

    public enum BarkAction
    {
        SingleLine,       // one specific line, one specific character
        LineFromSet,      // a random line out of a BarkSet
        SwapIdleSet,      // change what that character chatters about from now on
        Conversation      // run a whole .yarn node as floating barks
    }

    [Header("When")]
    public TriggerMode trigger = TriggerMode.PlayerEnters;
    [Tooltip("Fire at most once for the whole session.")]
    public bool onceOnly = true;
    [Tooltip("Seconds to wait after the trigger before the bark actually happens.")]
    public float delay = 0f;

    [Header("Conditions (optional)")]
    [Tooltip("Only fire once SaveManager has this progress id.")]
    public string requiresProgress;
    [Tooltip("Only fire while this Yarn variable is true, e.g. $YarnTalkedElder.")]
    public string requiresYarnVariable;
    [Tooltip("Fire only while the conditions above are NOT met.")]
    public bool invertCondition;

    [Header("What")]
    public BarkAction action = BarkAction.SingleLine;

    [Tooltip("Bark id of the character who speaks. Must match their CharacterDialogue's Bark Id.")]
    public string speakerId;

    [Tooltip("Single Line — what they say.")]
    [TextArea(1, 3)]
    public string line;

    [Tooltip("Line From Set / Swap Idle Set — the set to use.")]
    public BarkSet set;

    [Tooltip("Conversation — the .yarn node to run as floating barks.")]
    public string conversationNode;

    [Tooltip("Scripted beats out-rank idle chatter. Drop to Reactive for background flavour " +
             "that may be interrupted.")]
    public BarkPriority priority = BarkPriority.Scripted;

    private bool hasFired;

    private void Start()
    {
        if (trigger == TriggerMode.OnStart)
            Fire();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (trigger != TriggerMode.PlayerEnters) return;
        if (!other.CompareTag("Player")) return;
        Fire();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (trigger != TriggerMode.PlayerExits) return;
        if (!other.CompareTag("Player")) return;
        Fire();
    }

    // The single entry point. Safe to call from a UnityEvent.
    public void Fire()
    {
        if (onceOnly && hasFired) return;
        if (!ConditionsMet()) return;

        hasFired = true;

        if (delay > 0f)
            StartCoroutine(FireAfterDelay());
        else
            Execute();
    }

    // Lets a once-only trigger be re-armed from a UnityEvent (e.g. a puzzle reset).
    public void Rearm()
    {
        hasFired = false;
    }

    private IEnumerator FireAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        Execute();
    }

    private bool ConditionsMet()
    {
        bool met = true;

        if (!string.IsNullOrEmpty(requiresProgress))
            met &= SaveManager.Instance != null && SaveManager.Instance.HasProgress(requiresProgress);

        if (met && !string.IsNullOrEmpty(requiresYarnVariable))
            met &= BarkDirector.GetYarnBool(requiresYarnVariable);

        return invertCondition ? !met : met;
    }

    private void Execute()
    {
        switch (action)
        {
            case BarkAction.SingleLine:
                if (string.IsNullOrEmpty(line))
                {
                    Debug.LogWarning($"{name}: BarkTrigger is set to Single Line but has no line.", this);
                    return;
                }
                BarkDirector.Bark(speakerId, line, priority);
                break;

            case BarkAction.LineFromSet:
                if (set == null || set.lines.Count == 0)
                {
                    Debug.LogWarning($"{name}: BarkTrigger is set to Line From Set but the set is empty.", this);
                    return;
                }
                BarkDirector.Bark(speakerId, set.lines[Random.Range(0, set.lines.Count)], priority);
                break;

            case BarkAction.SwapIdleSet:
                BarkDirector.Instance?.Find(speakerId)?.SetBarkSet(set);
                break;

            case BarkAction.Conversation:
                if (string.IsNullOrEmpty(conversationNode))
                {
                    Debug.LogWarning($"{name}: BarkTrigger is set to Conversation but has no node name.", this);
                    return;
                }
                // A refused start (dialogue box open, another exchange running) un-fires a
                // once-only trigger, so the beat isn't silently lost — it plays next time.
                if (!BarkDirector.RunConversation(conversationNode) && onceOnly)
                    hasFired = false;
                break;
        }
    }
}
