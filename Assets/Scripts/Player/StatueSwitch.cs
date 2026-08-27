using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The statue: an interactable that turns one or more ObstacleGroups by 90 degrees
// every time it's used, reshaping the board mid-puzzle.
//
// Driven by the FRAGMENT (ghost), not the MC. Needs a TRIGGER Collider2D marking
// the range at which the ghost can use it. Whenever the ghost is inside that range
// — whether you're piloting it or it's parked there — pressing E turns the statue.
// The MC alone can never use it, and it never fires from across the map.
[RequireComponent(typeof(Collider2D))]
public class StatueSwitch : MonoBehaviour
{
    // One rotating group + its own direction override. The little "Counter Clockwise"
    // checkbox next to each group flips THAT group against the statue's base direction,
    // leaving the others alone.
    [System.Serializable]
    public class RotatingGroup
    {
        public ObstacleGroup group;

        [Tooltip("Turn this specific group the OTHER way from the statue's base direction.")]
        public bool counterClockwise;
    }

    [Tooltip("Rotating bush groups this statue turns. All of them turn together, or none do. Each has its own Counter Clockwise toggle.")]
    [SerializeField] List<RotatingGroup> groups = new List<RotatingGroup>();

    [Tooltip("Tile cycles this statue advances one step. Turn together with the groups, all-or-nothing.")]
    [SerializeField] List<TileCycle> cycles = new List<TileCycle>();

    [Tooltip("Prompt shown on the interact label while the parked ghost can use this.")]
    [SerializeField] string interactLabel = "E";

    [Tooltip("Fires when the statue is used successfully — rumble, dust, stone-grinding SFX.")]
    public UnityEvent onActivated;

    [Tooltip("Fires when the statue is used but nothing can turn (something's in the way). Fires EVERY time — hook the grind/clunk sound and a shake here.")]
    public UnityEvent onRefused;

    [Tooltip("Fires only the FIRST time this statue is refused. Hook Haze's explanatory line here: the first refusal teaches the rule, and repeating the line on every later bump would just be noise.")]
    public UnityEvent onRefusedFirstTime;

    [Tooltip("No do .yarn tocado na PRIMEIRA recusa. Vazio = nao toca nada.\n\n" +
             "Existe ao lado do evento acima, e nao no lugar dele, pelo mesmo motivo que o " +
             "NpcWalkTo tem a lista 'Ativar Ao Chegar' junto do On Arrived: UnityEvent NAO " +
             "sobrevive a edicao do arquivo da cena fora do editor, e neste projeto a cena " +
             "e editada por YAML. Um campo de texto sobrevive.\n\n" +
             "Os dois convivem — o evento continua servindo para som e tremida, arrastados " +
             "no Inspector; este campo cobre a fala, que e o que se quer ligar de fora.")]
    public string noAoRecusarPrimeiraVez = "";

    [Tooltip("Flash whatever is in the way when a turn is refused. Needs nothing on the crates — see BlockedFlash.")]
    [SerializeField] bool flashBlocker = true;

    [Tooltip("Conversa em bark tocada quando esta estatua e usada com sucesso. Vazio = nao " +
             "toca nada. Ponha o BarkConversation em Start Mode = Manual. " +
             "Referencia direta e nao UnityEvent, pelo mesmo motivo do Ativar Ao Chegar do " +
             "NpcWalkTo: campo de objeto sobrevive a edicao do arquivo da cena por fora.")]
    public BarkConversation conversaAoAtivar;

    [Tooltip("Toca a conversa acima so na PRIMEIRA vez que esta estatua e usada. " +
             "Desmarcado, ela repete a cada giro — que vira ruido depois da segunda vez.")]
    public bool conversaSoNaPrimeiraVez = true;

    // Quantas vezes QUALQUER estatua foi usada com sucesso na sessao.
    //
    // Existe para o tutorial saber que "o puzzle se mexeu pela primeira vez" sem precisar
    // de referencia a uma estatua especifica: o <<ghosttutorial>> anota o valor ao comecar
    // e espera ele mudar. Assim o mesmo tutorial serve para qualquer estatua do jogo.
    public static int AtivacoesTotais { get; private set; }

    // Quantas vezes ESTA estatua foi usada. Separado do total porque a conversa de estreia
    // e desta estatua, e nao do puzzle inteiro.
    public int AtivacoesDesta { get; private set; }

    // Quantas estatuas tem a Haze dentro do alcance AGORA.
    //
    // Contador e nao bool porque duas estatuas podem ter alcances que se tocam: com bool, a
    // segunda a receber a saida zeraria o estado enquanto a Haze ainda esta dentro da
    // primeira. O tutorial usa isto para saber que ela chegou em alguma, sem precisar de
    // referencia a nenhuma especifica.
    public static int FantasmasEmAlcance { get; private set; }

    public static bool FantasmaPertoDeAlgumaEstatua => FantasmasEmAlcance > 0;

    [Tooltip("Progresso que marca 'a regra do bloqueio ja foi explicada'. Vazio = a fala " +
             "repete a cada primeira recusa de CADA estatua. " +
             "Fica no SaveManager e nao num bool do componente porque a licao e do JOGO, " +
             "nao desta estatua: uma vez que a Haze explicou que algo esta no caminho, " +
             "repetir isso na estatua seguinte, ou na cripta, e ruido. Como e progresso, " +
             "atravessa cena e sobrevive a save.")]
    public string progressoDaExplicacao = "PuzzleBlockExplained";

    bool ghostInRange;

    // Identify the ghost by its FragmentFollow component (on the root), so no tag
    // string has to be kept in sync. The collider may sit on a child.
    static bool IsGhost(Collider2D other) => other.GetComponentInParent<FragmentFollow>() != null;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsGhost(other)) return;

        ghostInRange = true;
        FantasmasEmAlcance++;
        RefreshPrompt();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsGhost(other)) return;

        ghostInRange = false;
        FantasmasEmAlcance = Mathf.Max(0, FantasmasEmAlcance - 1);
        RefreshPrompt();
    }

    // The ghost sitting inside the trigger while the statue is disabled or the
    // scene unloads would otherwise leave ghostInRange stuck true.
    void OnDisable()
    {
        // Desligar com a Haze dentro deixaria o contador alto para sempre.
        if (ghostInRange) FantasmasEmAlcance = Mathf.Max(0, FantasmasEmAlcance - 1);

        ghostInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // E works whenever the ghost is in range — piloted or parked, no parked-only
    // restriction. Only the ghost's presence in the trigger gates it.
    //
    // A excecao e o tutorial. Enquanto ele esta ensinando a mecanica, a instrucao vive na
    // faixa do topo ("Press E to interact with the statue") e o E flutuante fica de fora:
    // duas instrucoes dizendo a mesma coisa em lugares diferentes dividem a atencao no
    // exato momento em que o jogador ainda nao sabe onde olhar. Passado o tutorial, a
    // estatua volta a ser um objeto interagivel como qualquer outro e o E aparece em cima
    // dela normalmente.
    void RefreshPrompt()
    {
        if (!ghostInRange)
        {
            InteractButton.Instance?.ClearInteraction(this);
            return;
        }

        // O tutorial tem DOIS estados diferentes aqui, e confundi-los foi o bug:
        //
        //   RECUSANDO  — a faixa esta pedindo o Q. O E nao pode nem existir, senao o
        //                jogador usa a estatua antes de estacionar a Haze e pula o passo
        //                que esta escrito na tela. Nao registra nada.
        //
        //   SUPRIMINDO — a faixa ja esta pedindo o E. Ele FUNCIONA, mas o rotulo fica vazio:
        //                a instrucao ja esta na faixa, e o mesmo pedido em dois lugares
        //                divide a atencao de quem ainda nao sabe onde olhar.
        if (TutorialHint.EstatuaRecusandoE)
        {
            InteractButton.Instance?.ClearInteraction(this);
            return;
        }

        string rotulo = TutorialHint.SuprimindoPromptDaEstatua ? string.Empty : interactLabel;
        InteractButton.Instance?.SetInteraction(this, rotulo, OnInteractPressed);
    }

    // O tutorial libera o E no instante em que a estatua e usada. Sem alguem chamar isto,
    // o prompt so voltaria quando a Haze saisse do alcance e entrasse de novo.
    public void ReavaliarPrompt() => RefreshPrompt();

    // InteractButton blanks the prompt the instant E is pressed (so it can't be
    // spammed mid-activation); the statue's own use is instantaneous, so re-arm right
    // away if the ghost is still standing here.
    void OnInteractPressed()
    {
        Activate();
        RefreshPrompt();
    }

    void Activate()
    {
        if (groups.Count == 0 && cycles.Count == 0) return;

        // Check EVERYTHING before moving anything: a partial activation would leave
        // the board in a state the player never asked for and can't undo. The flip
        // side is that ONE bad group/cycle refuses the whole statue — so name it, or
        // a misconfigured second one looks like "the statue just stopped working".
        //
        // CLEARED sub-puzzles are skipped everywhere below: once a group/cycle's rug is
        // covered by a crate it drops out of the sweep, so it neither moves nor blocks
        // the rest. That's what stops a crate parked on a rug from freezing the statue.
        int active = 0;
        foreach (var entry in groups)
        {
            if (entry == null || entry.group == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Groups list — fill or remove it.", this);
                Refuse(null);
                return;
            }
            if (entry.group.IsCleared) continue;

            // Ainda girando NAO e bloqueio: e a animacao anterior terminando.
            //
            // O CanRotate devolve false com "it's still mid-spin", e tratar isso como
            // recusa fazia a Haze dizer que algo estava no caminho quando nao havia nada —
            // bastava apertar E duas vezes seguidas. O TryRotate ja separava os dois casos
            // ("silent: spam-clicking isn't blocked"); aqui faltava.
            if (entry.group.IsRotating) return;

            if (!entry.group.CanRotate(DirectionFor(entry), out string reason, out GameObject blocker))
            {
                Debug.Log($"{name}: won't turn because group '{entry.group.name}' can't rotate — {reason}.", entry.group);
                Refuse(blocker);
                return;
            }
            active++;
        }
        foreach (var c in cycles)
        {
            if (c == null)
            {
                Debug.LogWarning($"{name}: an empty slot is in the Cycles list — fill or remove it.", this);
                Refuse(null);
                return;
            }
            if (c.IsCleared) continue;

            // Mesma coisa do grupo: meio-passo e animacao, nao obstaculo.
            if (c.IsStepping) return;

            if (!c.CanStep(out string reason, out GameObject blocker))
            {
                Debug.Log($"{name}: won't turn because cycle '{c.name}' can't step — {reason}.", c);
                Refuse(blocker);
                return;
            }
            active++;
        }

        // Everything the statue drives is cleared — nothing left to move, so it just
        // sits (no refuse buzz: this is a solved state, not a blocked one).
        if (active == 0) return;

        // A estátua NÃO entra no undo. O Z desfaz só o que o player fez com o próprio
        // corpo — empurrar caixa. Girar a estátua é uma jogada do tabuleiro, e voltar
        // atrás nela é usar a estátua de novo, não apertar Z.
        foreach (var entry in groups) if (!entry.group.IsCleared) entry.group.TryRotate(DirectionFor(entry));
        foreach (var c in cycles) if (!c.IsCleared) c.TryStep();
        bool primeiraVezDesta = AtivacoesDesta == 0;
        AtivacoesDesta++;
        AtivacoesTotais++;

        if (conversaAoAtivar != null && (!conversaSoNaPrimeiraVez || primeiraVezDesta))
            conversaAoAtivar.Play();

        onActivated?.Invoke();
    }

    // One refusal, three audiences: the world (sound/shake on every refusal), the player's
    // eye (the thing in the way lights up), and Haze (who explains the rule once and then
    // shuts up about it).
    //
    // The blocker may be null — an empty Inspector slot or a mid-spin refusal has no
    // single culprit to point at — and only the flash cares, so the rest still fires.
    // A explicacao do bloqueio e uma licao unica do jogo inteiro. Sem progresso configurado,
    // cai para o comportamento antigo: uma vez por estatua.
    bool JaExplicado()
    {
        if (string.IsNullOrEmpty(progressoDaExplicacao)) return recusouAntesNesta;
        return SaveManager.Instance != null &&
               SaveManager.Instance.HasProgress(progressoDaExplicacao);
    }

    void MarcarExplicado()
    {
        recusouAntesNesta = true;
        if (string.IsNullOrEmpty(progressoDaExplicacao)) return;
        if (SaveManager.Instance != null)
            SaveManager.Instance.AddProgress(progressoDaExplicacao);
    }

    bool recusouAntesNesta;

    void Refuse(GameObject blocker)
    {
        onRefused?.Invoke();

        if (flashBlocker && blocker != null)
            BlockedFlash.Play(blocker);

        if (!JaExplicado())
        {
            MarcarExplicado();
            onRefusedFirstTime?.Invoke();

            // Pelo mesmo funil que todo dialogo do jogo usa, e nao pelo DialogueManager na
            // mao: e ele que trava o jogador enquanto a fala roda e o destrava no fim.
            // O campo desta estatua manda; sem ele, o do asset, que vale para o jogo todo.
            string no = !string.IsNullOrEmpty(noAoRecusarPrimeiraVez)
                ? noAoRecusarPrimeiraVez
                : (TutorialHintSettings.Current != null
                       ? TutorialHintSettings.Current.noDaPrimeiraRecusaDoPuzzle
                       : null);

            if (!string.IsNullOrEmpty(no))
                DialogueStarter.EvaluateConditionsAndStart(no, false, null, false);
        }
    }

    // Groups turn the default direction unless a group opts into the other way with
    // its Counter Clockwise toggle. (This preserves the exact behaviour from when the
    // statue's global direction was left unchecked — no global base anymore.)
    bool DirectionFor(RotatingGroup entry) => entry.counterClockwise;
}
