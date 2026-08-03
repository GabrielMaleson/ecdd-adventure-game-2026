using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Undo for the crate puzzle (test build). Press Z to step the board back to how it
// was before the last crate was pushed.
//
// Undo desfaz SÓ empurrão de caixa — a caixa volta pra célula anterior e o player
// volta pra onde estava empurrando (a ser tematizado como o Fragmento puxando ele).
// A ESTÁTUA NÃO ENTRA: girar obstáculo é jogada de tabuleiro, e desfazer isso é usar
// a estátua de novo, não apertar Z.
//
// Consequência disso: a estátua pode ter mexido no tabuleiro DEPOIS de um empurrão
// gravado. Se a célula de origem da caixa estiver ocupada na hora do Z, o próprio
// PushableCrate recusa aquele undo em vez de empilhar dois ocupantes na mesma célula.
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
