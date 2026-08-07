using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Optional goal marker for a Sokoban puzzle (the rug). Put one on each target
// cell. Targets that share a Puzzle Id form ONE puzzle: when every target with
// that id is covered by a crate, that puzzle's onAllTargetsCovered fires once —
// INDEPENDENTLY of any other puzzle in the same scene. This is what lets one
// scene (e.g. the main scene) hold several separate crate puzzles.
//
// Leave Puzzle Id blank to drop a target into the default (unnamed) puzzle; a
// scene with a single puzzle can ignore the field entirely and behaves exactly
// as before.
//
// Entirely optional — if a puzzle has no CrateTarget in the scene, crates still
// push normally and nothing win-related happens.
//
// It does NOT occupy its cell (it isn't a GridOccupant): a crate has to be able
// to move onto it.
public class CrateTarget : GridObject
{
    [Tooltip("Targets sharing this id are ONE puzzle and fire together, independently of other puzzles in the scene. Leave blank for the default puzzle.")]
    [SerializeField] string puzzleId = "";

    [Tooltip("Fires once when every target sharing this target's Puzzle Id is covered by a crate.")]
    public UnityEvent onAllTargetsCovered;

    public string PuzzleId => puzzleId ?? "";

    // Lets other systems ask whether a puzzle is currently solved. Used by the statue
    // to DROP a group/cycle out of its sweep once that sub-puzzle's rug is covered —
    // a "cleared" sub-puzzle stops moving and stops blocking the rest. Re-arms
    // automatically if the crate is later pulled off (solvedByGroup flips back).
    public static bool IsSolved(string id)
        => solvedByGroup.TryGetValue(id ?? "", out bool s) && s;

    // --- Board truth, no ids involved -------------------------------------------
    //
    // IsSolved() only answers for a puzzle id someone TYPED into a group/cycle and
    // that matches a rug actually present in this scene. When it doesn't match
    // anything the answer is a silent `false` forever, the group never clears, and a
    // crate parked on a rug freezes the whole statue with no error. The three helpers
    // below let a group ask about the BOARD instead, which can't be mistyped.

    // Is there a rug on this cell?
    public static bool IsTargetCell(Vector2Int cell)
    {
        foreach (var t in all)
            if (t != null && t.LiveCell() == cell) return true;
        return false;
    }

    // Is a crate parked on a rug on this cell? Same two-step answer EvaluateGroup
    // trusts: the occupancy map first, then where the artwork really is.
    public static bool CrateParkedOnTarget(Vector2Int cell)
    {
        if (!IsTargetCell(cell)) return false;
        if (GridOccupant.TryGetOccupant(cell, out var occ) && occ is PushableCrate) return true;
        return CrateSittingOn(cell);
    }

    // Does any rug in this scene actually carry this id? Lets a group shout at
    // startup when its Cleared When Puzzle Solved names a puzzle that isn't here —
    // the misconfiguration that otherwise costs a debugging session.
    public static bool PuzzleExists(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (var t in all)
            if (t != null && t.PuzzleId == id) return true;
        return false;
    }

    // Fires ONLY when a puzzle's solved-latch actually flips: (id, true) the moment it
    // becomes solved, (id, false) if a crate is later pulled off. This is the hook
    // PuzzleGate listens to, so a gate doesn't need a reference to any particular rug
    // — it just names the puzzle ids it waits for.
    public static event System.Action<string, bool> PuzzleStateChanged;

    // Disparado sempre que o tabuleiro muda (caixa deslizou, estátua girou), sem
    // nenhuma condição. É o gancho do PuzzleGate.
    public static event System.Action BoardChanged;

    protected override Color DebugColor => Color.green;

    static readonly List<CrateTarget> all = new List<CrateTarget>();

    // One solved flag PER puzzle id, so each puzzle latches (and can re-arm if a
    // crate is later pulled off) on its own.
    static readonly Dictionary<string, bool> solvedByGroup = new Dictionary<string, bool>();

    // Clears stale state at the start of every Play (survives Fast Play Mode).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        solvedByGroup.Clear();
        PuzzleStateChanged = null;       // gates from the previous Play must not linger
        BoardChanged = null;
        cratesCache = new PushableCrate[0];
        cratesFrame = -1;
    }

    // Registra SEMPRE. A versão antiga saía fora se o PuzzleGrid não estivesse pronto
    // neste instante — e um tapete que não entra na lista nunca é checado, sem erro
    // nenhum: o puzzle simplesmente não fecha, para sempre, em silêncio.
    void OnEnable() => all.Add(this);

    // Célula lida NA HORA da checagem, não guardada no OnEnable. Se o tapete foi
    // movido, snapado, reparentado ou avaliado antes do grid existir, o valor antigo
    // estaria errado e ninguém saberia.
    Vector2Int LiveCell()
    {
        var g = PuzzleGrid.Active;
        if (g == null || !g.IsReady) return Cell;
        Cell = g.WorldToCell(VisualCenter);      // mantém o gizmo de ocupação honesto
        return Cell;
    }

    void OnDisable() => all.Remove(this);

    // Runs after every target and crate has registered, so a puzzle authored
    // already-solved reports it instead of waiting for a push that never comes.
    void Start() => EvaluateWin();

    // Called by PushableCrate after each push (and by the statue's undo). Re-checks
    // EVERY puzzle group; each one fires, latches, or re-arms on its own.
    public static void EvaluateWin()
    {
        // ANTES de qualquer condição: "o tabuleiro mudou". Quem quiser reagir a isso
        // (PuzzleGate) faz a própria conta, e não depende de tapete registrado, de
        // Puzzle Id nem de latch. Disparado nos dois únicos momentos em que o
        // tabuleiro muda: fim do deslize de uma caixa e passo da estátua.
        BoardChanged?.Invoke();

        if (all.Count == 0) return;

        // Distinct puzzle ids present. Small allocation, but puzzle scale is tiny
        // and building the set here keeps the pass reentrancy-safe.
        var groups = new HashSet<string>();
        foreach (var target in all) groups.Add(target.PuzzleId);

        foreach (var id in groups) EvaluateGroup(id);
    }

    // Existe alguma caixa cujo DESENHO cai nesta célula? Independe do dicionário de
    // ocupação — é a pergunta que os olhos fazem.
    static bool CrateSittingOn(Vector2Int cell)
    {
        var g = PuzzleGrid.Active;
        if (g == null || !g.IsReady) return false;

        // Varre a cena no máximo uma vez por frame, não uma por tapete.
        if (cratesFrame != Time.frameCount)
        {
            cratesFrame = Time.frameCount;
            cratesCache = FindObjectsByType<PushableCrate>(FindObjectsSortMode.None);
        }

        foreach (var crate in cratesCache)
            if (crate != null && g.WorldToCell(crate.VisualCenter) == cell) return true;

        return false;
    }

    static PushableCrate[] cratesCache = new PushableCrate[0];
    static int cratesFrame = -1;

    static void EvaluateGroup(string id)
    {
        bool allCovered = true;
        foreach (var target in all)
        {
            if (target.PuzzleId != id) continue;

            // An obstacle sitting on a target must not count as a solve — only a
            // crate does.
            Vector2Int cell = target.LiveCell();

            bool covered = GridOccupant.TryGetOccupant(cell, out var occupant)
                           && occupant is PushableCrate;

            // Rede de segurança: a caixa pode estar EM CIMA do tapete e mesmo assim o
            // mapa de ocupação apontar outra coisa (registro velho de quando o grid
            // ainda não existia, caixa movida na mão no editor, arbusto que reivindicou
            // a célula e saiu). Antes de dizer que não fechou, pergunta às caixas onde
            // elas realmente estão — a posição do desenho é a verdade final.
            if (!covered) covered = CrateSittingOn(cell);

            if (!covered) { allCovered = false; break; }
        }

        solvedByGroup.TryGetValue(id, out bool wasSolved);

        if (!allCovered)
        {
            solvedByGroup[id] = false;       // re-arm: an uncovered puzzle can fire again
            if (wasSolved) PuzzleStateChanged?.Invoke(id, false);
            return;
        }

        if (wasSolved) return;               // don't re-fire while it stays solved
        solvedByGroup[id] = true;

        foreach (var target in all)
            if (target.PuzzleId == id)
                target.onAllTargetsCovered?.Invoke();

        PuzzleStateChanged?.Invoke(id, true);
    }
}
