using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Undo for the crate puzzle (test build). Press Z to step the board back to how it
// was before the last crate was pushed.
//
// This is per-INTERACTION, not per-tile: the player moves freely, not on the grid,
// so there's nothing to rewind for the player — only the crates snap to discrete
// cells, so only they are recorded. Undo never moves the player.
//
// Each push pushes a closure onto a stack that puts that one crate back. Undoing is
// strict last-in-first-out, which is what keeps the board consistent: a cell a
// crate came from can only have been filled by a LATER push, and that later push is
// always undone first — so by the time we restore this crate, its origin is free
// again.
//
// Put one PuzzleUndo anywhere in the scene (the PuzzleGrid object is fine).
public class PuzzleUndo : MonoBehaviour
{
    static readonly Stack<Action> history = new Stack<Action>();

    // Clears the stack at the start of every Play (survives Fast Play Mode), so a
    // previous run's moves can't be "undone" into the new one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => history.Clear();

    // Called BEFORE a move commits. `undo` restores the pre-move state.
    public static void Record(Action undo)
    {
        if (undo != null) history.Push(undo);
    }

    public static int Count => history.Count;

    public static void UndoLast()
    {
        if (history.Count == 0) return;
        history.Pop()?.Invoke();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.zKey.wasPressedThisFrame)
            UndoLast();
    }
}
