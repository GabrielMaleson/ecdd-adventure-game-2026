using System;
using UnityEngine;
using Yarn.Unity;

// Matches DialoguePresenterBase, which is compiled in a nullable-enabled context — without
// this the RunOptionsAsync override's DialogueOption? annotation raises CS8632.
#nullable enable

// Presents Yarn lines as floating barks instead of the dialogue box.
//
// Put this on the SECOND DialogueRunner (the one wired into BarkDirector.barkRunner),
// as its ONLY Dialogue Presenter. Any node started on that runner then plays overhead
// while the player keeps walking — no dialogue box, no frozen input.
//
// That is the whole trick: an overheard exchange is an ordinary Yarn node. Nothing about
// the writing changes, no second data format, and every existing Yarn command still works
// inside it. Which runner starts the node is what decides how it looks.
//
// Routing: a line's CHARACTER NAME is matched against CharacterDialogue.barkId, so
//   Marcus: Joshy, tá ouvindo isso?
// floats above whichever character registered the id "marcus".
public class BarkPresenter : DialoguePresenterBase
{
    [Header("Routing")]
    [Tooltip("Bark id used for lines that have no character name at all.")]
    public string narratorFallbackId = "josh";

    [Tooltip("Log a warning when a line's character name matches no registered bark id.")]
    public bool warnOnUnknownSpeaker = true;

    [Header("Pacing")]
    [Tooltip("Gap between one line finishing and the next starting.")]
    public float delayBetweenLines = 0.25f;

    [Tooltip("Fallback time to wait when a line has nowhere to be displayed.")]
    public float orphanLineDuration = 1.5f;

    [Header("Bail Out")]
    [Tooltip("Abandon the conversation if the player walks further than this from the speaker. 0 = never.")]
    public float abandonDistance = 18f;

    [Header("Options")]
    [Tooltip("Bark nodes have no chooser UI. On by default: take the first option and carry on. " +
             "Off: refuse to choose, which stalls the node — use only while hunting for stray options.")]
    public bool autoPickFirstOption = true;

    public override YarnTask OnDialogueStartedAsync()
    {
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        // Nothing to tear down: each bark hides itself on its own timer, and leaving the
        // last line to finish its float-out is what makes the exchange feel unhurried.
        return YarnTask.CompletedTask;
    }

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        string text = line.TextWithoutCharacterName.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        string speakerId = string.IsNullOrEmpty(line.CharacterName) ? narratorFallbackId : line.CharacterName;

        CharacterDialogue? speaker = BarkDirector.Instance != null
            ? BarkDirector.Instance.Find(speakerId)
            : null;

        if (speaker == null)
        {
            // The line still has to consume time, otherwise the whole node races past in
            // one frame and the rest of the exchange is unreadable.
            if (warnOnUnknownSpeaker)
                Debug.LogWarning($"BarkPresenter: no character with bark id '{speakerId}' — line skipped: \"{text}\"");

            await Wait(orphanLineDuration, token);
            return;
        }

        if (ShouldAbandon(speaker))
        {
            BarkDirector.StopConversation();
            return;
        }

        float duration = speaker.Show(text, BarkPriority.Scripted);

        // Show() refuses only when something of equal-or-higher priority is already up.
        // Waiting the normal beat keeps the exchange's rhythm instead of dumping the
        // remaining lines instantly.
        if (duration <= 0f)
            duration = orphanLineDuration;

        await Wait(duration + delayBetweenLines, token);
    }

    public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        if (dialogueOptions == null || dialogueOptions.Length == 0)
            return null;

        if (!autoPickFirstOption)
        {
            Debug.LogError("BarkPresenter: a bark node reached a set of options and Auto Pick First Option is off. " +
                           "Bark nodes have no chooser — the node will stall here.");
            return null;
        }

        Debug.LogWarning($"BarkPresenter: bark node hit {dialogueOptions.Length} options; taking the first " +
                         $"(\"{dialogueOptions[0].Line.TextWithoutCharacterName.Text}\"). " +
                         "Player choices belong in the blocking dialogue runner.");

        await YarnTask.Yield();
        return dialogueOptions[0];
    }

    private bool ShouldAbandon(CharacterDialogue? speaker)
    {
        if (abandonDistance <= 0f)
            return false;

        Transform? player = BarkDirector.PlayerTransform;
        if (player == null || speaker == null)
            return false;

        return (player.position - speaker.transform.position).sqrMagnitude > abandonDistance * abandonDistance;
    }

    // Cancellation is normal here — it's how the runner says "the player left" or
    // "the dialogue box just took over" — so it ends the line quietly rather than throwing.
    private static async YarnTask Wait(float seconds, LineCancellationToken token)
    {
        if (seconds <= 0f)
            return;

        try
        {
            await YarnTask.Delay(TimeSpan.FromSeconds(seconds), token.NextContentToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
