using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

// "Va ate ali, andando, e me avise quando chegar."
//
// O terceiro jeito de mover um NPC neste projeto, e existe porque os outros dois nao
// servem para isto:
//
//   NpcFollow      — anda atras do Josh para sempre. Nao tem destino.
//   NpcFormation   — TELETRANSPORTA o grupo para as marcas. Instantaneo, sem caminhada.
//   <<move>>       — cutscene do Gabriel, e anda so no eixo X (trava o Y). Serve para
//                    alguem atravessar a tela de lado, nao para entrar numa sala.
//   NpcWalkTo      — um destino, andando, em 2D, e o jogo NAO PARA.
//
// O jogo nao parar e a caracteristica principal, nao um detalhe: enquanto Marcus e Erika
// entram na casa do Elder e vao para os lugares deles, o Josh continua sob controle do
// jogador. Por isso isto nao e um comando bloqueante de cutscene — e disparado e esquecido,
// e quem quiser saber do fim escuta onArrived.
//
// COMO SE LIGA NA CENA
// Componente no NPC. O destino e um objeto vazio na cena (a marca). Ao chegar, ele para,
// vira para onde voce mandar, e dispara onArrived — e e no onArrived, pelo Inspector, que
// se liga "agora ele fica interagivel". Este script nao sabe o que e interacao, do mesmo
// jeito que o CrateTarget do sokoban nao sabe o que e a recompensa.
[DisallowMultipleComponent]
public class NpcWalkTo : MonoBehaviour
{
    // So lado, e nao as quatro direcoes, porque o CharacterFacing deste projeto so tem
    // pose PARADA para esquerda e direita — "parado olhando para baixo" nao existe na arte.
    // Oferecer Baixo/Cima aqui seria um botao que nao faz nada.
    public enum ArrivalFacing
    {
        Manter,    // fica no lado em que estava andando
        Esquerda,
        Direita
    }

    [Header("Identity")]
    [Tooltip("Nome usado no .yarn: <<walkto EsteNome NomeDaMarca>>. Vazio = o nome do objeto.")]
    public string walkerName;

    // Uma parada do caminho. Duas coisas: onde, e quanto tempo fica ali antes de seguir.
    [System.Serializable]
    public class Stop
    {
        [Tooltip("A marca. Um objeto vazio na cena.")]
        public Transform point;

        [Tooltip("Segundos parado AQUI antes de seguir para o proximo ponto. 0 = nao para, " +
                 "so dobra a esquina e continua. Um valor pequeno (0.3-0.8) num canto ja " +
                 "tira o ar de robo, porque pessoa de verdade hesita ao virar.")]
        public float waitHere;
    }

    [Header("Para onde")]
    [Tooltip("O caminho, em ordem. UM ponto = anda reto ate la. VARIOS = ele dobra as " +
             "esquinas passando por cada um.\n\n" +
             "Isto existe porque o movimento e em linha reta: com parede ou movel entre ele " +
             "e o destino, a reta encosta e ele fica empurrando. Ponha um ponto no vao da " +
             "porta, outro no meio da sala, e o ultimo onde ele deve parar.")]
    public List<Stop> path = new List<Stop>();

    [Tooltip("Comeca a andar sozinho assim que a cena carrega. Deixe DESLIGADO para " +
             "entradas disparadas por gatilho — que e o caso normal.")]
    public bool walkOnStart;

    [Header("Como")]
    [Tooltip("Unidades por segundo. Andando, nao correndo — o Josh anda a ~3.")]
    public float moveSpeed = 1.6f;

    [Tooltip("Folga de chegada. Grande demais e ele para longe da marca; pequena demais e " +
             "ele fica tremendo em cima dela sem nunca 'chegar'.")]
    public float arriveThreshold = 0.06f;

    [Tooltip("Desiste depois deste tempo mesmo sem chegar, e dispara onArrived assim mesmo. " +
             "E a rede de seguranca para um movel no caminho: sem isto o NPC empurra a " +
             "parede para sempre e a fala dele nunca destrava. 0 = sem limite.")]
    public float giveUpAfterSeconds = 20f;

    [Header("Ao chegar")]
    public ArrivalFacing arrivalFacing = ArrivalFacing.Manter;

    [Tooltip("Ligados quando ele chega. Uma lista simples de objetos, e nao um evento: " +
             "e aqui que vai o objeto de conversa do personagem.\n\n" +
             "Existe separado do evento abaixo porque UnityEvent nao sobrevive a edicao do " +
             "arquivo da cena fora do editor — uma lista de referencias sobrevive. Para o " +
             "caso comum (chegou, agora da para falar com ele) isto basta e nao depende de " +
             "arraste nenhum no On Arrived.")]
    public GameObject[] ativarAoChegar;

    [Tooltip("Disparado UMA vez, quando ele chega (ou desiste). Para qualquer coisa alem de " +
             "simplesmente ligar objetos.")]
    public UnityEvent onArrived;

    [Header("Animation")]
    [Tooltip("Vazio = o Animator deste objeto ou dos filhos.")]
    public Animator animator;

    [Tooltip("Bool do Animator ligado enquanto anda. Vazio = nao mexe em bool nenhum.")]
    public string walkBoolParam = "";

    private Rigidbody2D body;
    private CharacterFacing facing;
    private bool walking;
    private float walkingSince;

    // Onde ele esta no caminho, e ate quando fica parado na parada atual.
    private int stopIndex;
    private float waitUntil;

    // A rota que esta sendo andada AGORA. Separada de `path` de proposito: `path` e o que a
    // pessoa montou no Inspector e nunca e escrito por codigo; esta aqui e descartavel.
    private readonly List<Stop> route = new List<Stop>();

    public bool IsWalking => walking;

    // Mesmo registro por nome que NpcFollow e NpcFormation usam, para <<walkto>> ser uma
    // busca por nome e nenhuma cutscene precisar de referencia a um objeto especifico.
    private static readonly List<NpcWalkTo> allInstances = new List<NpcWalkTo>();

    private string Name => string.IsNullOrEmpty(walkerName) ? name : walkerName;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        facing = GetComponent<CharacterFacing>();
        if (facing == null) facing = GetComponentInChildren<CharacterFacing>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable() => allInstances.Add(this);
    private void OnDisable() => allInstances.Remove(this);

    private void Start()
    {
        if (walkOnStart && path.Count > 0) GoPath(path);   // copia para `route`
    }

    // ------------------------------------------------------------------ API

    // Um ponto so — o caso simples, e o que o Yarn usa.
    public void Go(Transform where)
    {
        if (where == null) return;

        route.Clear();
        route.Add(new Stop { point = where, waitHere = 0f });
        Begin();
    }

    // O caminho inteiro.
    public void GoPath(List<Stop> stops)
    {
        if (stops == null || stops.Count == 0) return;

        // Copiar para a rota de execucao, NUNCA escrever no campo `path`.
        //
        // Antes isto escrevia direto em `path`, e era destrutivo de verdade: um unico
        // Go(ponto) apagava o caminho que a pessoa montou no Inspector, e no editor a
        // cena ainda era marcada como suja e o apagamento virava permanente. Era isso que
        // fazia "o pathing sumir" ao disparar o teste.
        //
        // `path` e AUTORIA e so a pessoa escreve nele. `route` e execucao e so o codigo
        // escreve nela.
        if (!ReferenceEquals(stops, route))
        {
            route.Clear();
            route.AddRange(stops);
        }

        Begin();
    }

    private void Begin()
    {
        // O corpo fisico e o transform podem estar em lugares diferentes neste instante:
        // quem acabou de ser teletransportado escreveu no transform, e a fisica so acerta
        // no proximo passo. Andar a partir do corpo dessincronizado faz o NPC sair do lugar
        // errado — ou nao sair. Alinhar aqui custa nada e fecha a janela.
        if (body != null) body.position = transform.position;

        stopIndex = 0;
        waitUntil = 0f;
        walking = true;
        walkingSince = Time.time;
        SetWalkAnim(true);
    }

    // Para onde esta, sem disparar onArrived — para cancelar uma entrada pela metade.
    public void Cancel()
    {
        if (!walking) return;
        walking = false;
        SetWalkAnim(false);
        if (facing != null) facing.SetIdle();
    }

    // Yarn: <<walkto Marcus MarcusSpot>> — NAO bloqueia, o jogo segue enquanto ele anda.
    [YarnCommand("walkto")]
    public static void WalkTo(string who, string markName)
    {
        NpcWalkTo walker = Find(who);
        if (walker == null)
        {
            Debug.LogWarning($"<<walkto {who}>>: nao existe NpcWalkTo com esse nome, ou o " +
                             "objeto esta desligado. Nomes ligados agora: " + KnownNames());
            return;
        }

        GameObject mark = GameObject.Find(markName);
        if (mark == null)
        {
            Debug.LogWarning($"<<walkto {who} {markName}>>: nao achei objeto chamado " +
                             $"'{markName}' na cena.");
            return;
        }

        walker.Go(mark.transform);
    }

    private static NpcWalkTo Find(string who)
    {
        foreach (NpcWalkTo w in allInstances)
            if (string.Equals(w.Name, who, System.StringComparison.OrdinalIgnoreCase))
                return w;
        return null;
    }

    private static string KnownNames()
    {
        var names = new List<string>();
        foreach (NpcWalkTo w in allInstances) names.Add(w.Name);
        return names.Count > 0 ? string.Join(", ", names) : "(nenhum)";
    }

    // ------------------------------------------------------------------ movimento

    private void FixedUpdate()
    {
        if (!walking) return;

        // Parado numa parada, esperando o tempo dela passar.
        if (waitUntil > 0f)
        {
            if (Time.time < waitUntil) return;

            waitUntil = 0f;
            stopIndex++;

            // Era a ultima: chegou de vez.
            if (stopIndex >= route.Count) { Arrive(); return; }

            // O relogio do "desisti" reinicia a cada trecho. Senao um caminho longo com
            // varias esperas estoura o limite so por ser longo, sem nada estar travado.
            walkingSince = Time.time;
            SetWalkAnim(true);
            return;
        }

        Stop stop = CurrentStop();
        if (stop == null || stop.point == null) { Arrive(); return; }

        Vector2 here = body != null ? body.position : (Vector2)transform.position;
        Vector2 toGoal = (Vector2)stop.point.position - here;

        if (toGoal.magnitude <= arriveThreshold) { ReachedStop(stop); return; }

        if (giveUpAfterSeconds > 0f && Time.time - walkingSince > giveUpAfterSeconds)
        {
            Debug.LogWarning($"NpcWalkTo: {Name} desistiu de chegar em " +
                             $"'{stop.point.name}' depois de {giveUpAfterSeconds}s — " +
                             "provavelmente tem parede ou movel no caminho. Ponha um ponto " +
                             "a mais no vao da porta para ele dobrar a esquina. Disparando " +
                             "onArrived assim mesmo para nao travar a cena.", this);
            Arrive();
            return;
        }

        Vector2 step = toGoal.normalized * moveSpeed * Time.fixedDeltaTime;

        // Nao passar do alvo no ultimo passo, senao ele oscila em volta da marca.
        if (step.magnitude > toGoal.magnitude) step = toGoal;

        // MovePosition e nao transform.position: e assim que o PlayerController e o
        // NpcFollow andam neste projeto, e e o que faz o corpo PARAR ao encostar numa
        // parede em vez de atravessar. O rigidbody precisa ser Dynamic (gravidade 0,
        // rotacao Z travada) para isto valer.
        if (body != null) body.MovePosition(here + step);
        else transform.position = here + step;

        if (facing != null) facing.Set(step);
    }

    private Stop CurrentStop()
    {
        if (stopIndex < 0 || stopIndex >= route.Count) return null;
        return route[stopIndex];
    }

    private void ReachedStop(Stop stop)
    {
        // Ultimo ponto: nao interessa o tempo de espera dele, a viagem acabou.
        if (stopIndex >= route.Count - 1) { Arrive(); return; }

        if (stop.waitHere > 0f)
        {
            waitUntil = Time.time + stop.waitHere;
            SetWalkAnim(false);
            if (facing != null) facing.SetIdle();
            return;
        }

        // Sem espera: emenda direto no proximo trecho, sem um quadro de idle no meio — e
        // isso que faz a esquina parecer uma curva e nao duas caminhadas coladas.
        stopIndex++;
        walkingSince = Time.time;
    }

    private void Arrive()
    {
        walking = false;
        SetWalkAnim(false);

        if (facing != null)
        {
            // Set() decide o LADO, SetIdle() poe a pose parada desse lado. Nesta ordem:
            // ao contrario, o idle roda antes e o lado novo nunca aparece.
            if (arrivalFacing == ArrivalFacing.Esquerda) facing.Set(Vector2.left);
            else if (arrivalFacing == ArrivalFacing.Direita) facing.Set(Vector2.right);

            facing.SetIdle();
        }

        // Lista simples primeiro: e a que sempre funciona.
        if (ativarAoChegar != null)
            foreach (GameObject g in ativarAoChegar)
                if (g != null) g.SetActive(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int dir = animator != null ? animator.GetInteger("Direction") : -1;
        Debug.Log($"[NpcWalkTo] {Name} chegou. arrivalFacing={arrivalFacing}, " +
                  $"CharacterFacing={(facing != null ? "ok" : "AUSENTE")}, " +
                  $"Direction do Animator={dir} (0=idle-dir 3=side-dir 4=side-esq 5=idle-esq).", this);
#endif

        onArrived?.Invoke();
    }

    private void SetWalkAnim(bool on)
    {
        if (animator == null || string.IsNullOrEmpty(walkBoolParam)) return;
        animator.SetBool(walkBoolParam, on);
    }

    private void OnDrawGizmosSelected()
    {
        // O caminho inteiro desenhado, para dar para ver de relance se algum trecho corta
        // uma parede — que e exatamente o erro que este componente nao percebe sozinho.
        Gizmos.color = Color.cyan;

        Vector3 from = transform.position;

        foreach (Stop s in path)
        {
            if (s == null || s.point == null) continue;

            Gizmos.DrawLine(from, s.point.position);
            Gizmos.DrawWireSphere(s.point.position, arriveThreshold);

            // Parada COM espera ganha um circulo a mais, para as paradas se distinguirem
            // dos pontos de passagem sem precisar abrir o Inspector.
            if (s.waitHere > 0f)
                Gizmos.DrawWireSphere(s.point.position, arriveThreshold + 0.14f);

            from = s.point.position;
        }
    }
}
