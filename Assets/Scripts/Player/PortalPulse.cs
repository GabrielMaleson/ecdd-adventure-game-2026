using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;

// "Uma caixa parou em cima do portal -> o pulso apaga."
//
// Put this on the PORTAL ROOT (the object whose cell the crate lands on). It watches
// the board and kills the Visual Effect Graph a moment after a crate settles on its
// cell — the delay is the point, not a workaround: the statue lands, you get a beat,
// and THEN the portal goes out. An instant cut reads as a glitch; half a second reads
// as the portal reacting.
//
// Costs nothing per frame. It wakes on CrateTarget.BoardChanged, which fires exactly
// when the board actually changes (end of a crate's slide, a cycle step, an undo) —
// the same hook PuzzleGate uses.
//
// Deliberately does NOT require a CrateTarget. It asks "is a crate standing on my
// cell", which is true whether or not this object is also a goal — so the portal can
// stop being a rug later without this script caring.
public class PortalPulse : MonoBehaviour
{
    [Header("O que apagar")]
    [Tooltip("O Visual Effect do pulso. Vazio = procura um VisualEffect nos filhos.")]
    [SerializeField] VisualEffect pulse;

    [Header("Timing")]
    [Tooltip("Segundos entre a caixa parar em cima e o pulso apagar. É a batida dramática — 0 apaga no mesmo frame e parece bug.")]
    [SerializeField] float stopDelay = 0.5f;

    [Tooltip("Segundos entre a caixa sair e o pulso voltar.")]
    [SerializeField] float resumeDelay = 0f;

    [Tooltip("Desligado (recomendado): zera a taxa de spawn e as partículas que já existem terminam de viver, então o pulso morre suave. Ligado: desliga o componente na hora e tudo some no mesmo frame.")]
    [SerializeField] bool hardCut = false;

    [Tooltip("Nome da propriedade EXPOSTA do graph que controla a taxa de spawn. É por aqui que o pulso é apagado de verdade — Stop() sozinho só funciona se o spawner do graph escutar o evento OnStop, e a maioria não escuta. No portal atual: smokeRate.")]
    [SerializeField] string spawnRateProperty = "smokeRate";

    [Tooltip("Imprime no console a célula do portal, se ele vê uma caixa em cima, e o que fez. Liga isso quando 'não funcionou' e o console passa a dizer QUAL metade falhou.")]
    [SerializeField] bool debugLog = false;

    // Taxa original, lida uma vez. Sem isto o "voltar" teria que adivinhar um número.
    float spawnRateWhenLit;
    bool  hasSpawnRate;

    [Header("Ganchos")]
    [Tooltip("Dispara quando o pulso apaga — som de portal fechando, poeira, o que for.")]
    public UnityEvent onPulseStopped;

    [Tooltip("Dispara quando o pulso volta.")]
    public UnityEvent onPulseResumed;

    // O estado que este script acredita estar na tela. Só age quando ele MUDA, senão
    // cada BoardChanged reiniciaria a contagem e o pulso nunca apagaria.
    bool covered;
    Coroutine pending;

    void Reset()      => AutoFill();
    void OnValidate() => AutoFill();

    void AutoFill()
    {
        if (pulse == null) pulse = GetComponentInChildren<VisualEffect>();
    }

    void OnEnable()  => CrateTarget.BoardChanged += OnBoardChanged;
    void OnDisable()
    {
        CrateTarget.BoardChanged -= OnBoardChanged;
        pending = null;                      // a corrotina já morreu junto com o OnDisable
    }

    void Start()
    {
        AutoFill();
        if (pulse == null)
        {
            Debug.LogError($"{name}: PortalPulse não achou nenhum VisualEffect neste objeto " +
                           $"nem nos filhos. Arrasta o objeto do VFX no campo Pulse.", this);
            enabled = false;
            return;
        }

        // A taxa de spawn é lida ANTES de qualquer coisa apagar o portal, senão o valor
        // guardado pra "acender de novo" seria zero.
        hasSpawnRate = !string.IsNullOrEmpty(spawnRateProperty) && pulse.HasFloat(spawnRateProperty);
        if (hasSpawnRate) spawnRateWhenLit = pulse.GetFloat(spawnRateProperty);
        else
            Debug.LogWarning(
                $"{name}: o graph '{pulse.visualEffectAsset?.name}' não expõe um float chamado " +
                $"\"{spawnRateProperty}\". Sem ele só resta Stop(), que NÃO apaga nada se o spawner do " +
                $"graph não escutar o evento OnStop. Confere o nome da propriedade exposta no VFX Graph " +
                $"(Blackboard) e põe o nome certo em Spawn Rate Property — ou liga Hard Cut.", this);

        // Um tabuleiro que já NASCE com a caixa em cima não deve acender o portal só
        // pra apagar meio segundo depois. Aqui o estado inicial é aplicado seco.
        covered = CrateOnMyCell();
        Apply(covered, instant: true);
    }

    void OnBoardChanged()
    {
        bool now = CrateOnMyCell();

        if (debugLog)
        {
            var g = PuzzleGrid.Active;
            Debug.Log($"{name}: tabuleiro mudou — minha célula {(g != null && g.IsReady ? g.WorldToCell(MyCenter()).ToString() : "SEM GRID")}, " +
                      $"caixa em cima = {now} (antes {covered}).", this);
        }

        if (now == covered) return;          // nada mudou pra este portal
        covered = now;

        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(ApplyAfterDelay(now, now ? stopDelay : resumeDelay));
    }

    IEnumerator ApplyAfterDelay(bool nowCovered, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // A caixa pode ter sido empurrada de volta (ou o Z desfeito) durante a espera.
        // Sem esta conferida o portal apagaria por causa de uma caixa que não está mais
        // aqui.
        if (CrateOnMyCell() != nowCovered) { pending = null; yield break; }

        Apply(nowCovered, instant: false);
        pending = null;
    }

    void Apply(bool nowCovered, bool instant)
    {
        if (pulse == null) return;

        if (nowCovered)
        {
            if (hardCut)
            {
                pulse.enabled = false;
            }
            else
            {
                // Zerar a taxa é o que realmente apaga: independe de como o graph foi
                // montado. O Stop() vai junto por educação, pro caso de o graph escutar
                // OnStop — mas ele NUNCA é a única coisa acontecendo aqui.
                if (hasSpawnRate) pulse.SetFloat(spawnRateProperty, 0f);
                pulse.Stop();
            }
        }
        else
        {
            pulse.enabled = true;
            if (hasSpawnRate) pulse.SetFloat(spawnRateProperty, spawnRateWhenLit);
            pulse.Play();
        }

        if (debugLog)
            Debug.Log($"{name}: pulso {(nowCovered ? "APAGADO" : "ACESO")}" +
                      $"{(instant ? " (estado inicial)" : "")} — " +
                      $"{(hardCut ? "hard cut" : hasSpawnRate ? $"{spawnRateProperty} = {(nowCovered ? 0f : spawnRateWhenLit)}" : "só Stop()/Play()")}.", this);

        if (instant) return;                 // estado inicial não é um "evento"
        if (nowCovered) onPulseStopped?.Invoke();
        else            onPulseResumed?.Invoke();
    }

    // Tem uma caixa parada na MINHA célula? Mesma resposta em dois passos que o resto
    // do sistema usa: primeiro o mapa de ocupação, depois onde o desenho realmente
    // está — porque um registro velho não pode decidir se o portal acende ou não.
    bool CrateOnMyCell()
    {
        var g = PuzzleGrid.Active;
        if (g == null || !g.IsReady) return false;

        Vector2Int myCell = g.WorldToCell(MyCenter());

        if (GridOccupant.TryGetOccupant(myCell, out var occ)
            && occ is PushableCrate
            && g.WorldToCell(occ.VisualCenter) == myCell)
            return true;

        foreach (var crate in FindObjectsByType<PushableCrate>(FindObjectsSortMode.None))
            if (crate != null && crate.isActiveAndEnabled && g.WorldToCell(crate.VisualCenter) == myCell)
                return true;

        return false;
    }

    // A célula do portal é a do DESENHO, não a do pivot — igual a todo o resto do
    // sistema de grid. Se houver um GridObject aqui (CrateTarget), ele já sabe onde o
    // desenho está; senão cai no sprite, e por último no transform.
    Vector3 MyCenter()
    {
        var gridObject = GetComponent<GridObject>();
        if (gridObject != null) return gridObject.VisualCenter;

        var sr = GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds.center : transform.position;
    }
}
