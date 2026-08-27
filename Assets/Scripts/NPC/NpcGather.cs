using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// "Chega neste ponto e chama os amigos para conversar AQUI."
//
// O que este componente resolve, e que nenhum dos outros resolve: o lugar da conversa NAO
// e um ponto fixo da cena. O jogador entra no gatilho andando, e para alguns centimetros
// diferentes a cada partida — as vezes vindo pela esquerda, as vezes descendo a escada. Se
// as marcas dos NPCs fossem objetos fixos na cena, uma hora o Marcus pararia em cima do
// Josh e outra hora a dois metros dele.
//
// Entao o ponto de referencia e o PROPRIO JOGADOR, medido no instante em que ele para. As
// marcas sao offsets a partir dele: "um pouco acima, um pouco para a esquerda". A roda de
// conversa sai igual de onde quer que ele tenha parado.
//
// Os outros tres jeitos de mover NPC neste projeto, e por que nenhum serve aqui:
//   NpcFollow     — anda atras do Josh para sempre, nao tem destino.
//   NpcFormation  — TELETRANSPORTA. Aparecer do nada ao lado do jogador nao e uma cena.
//   NpcWalkTo     — anda ate uma MARCA FIXA da cena. E o motor certo, mas a marca fixa e
//                   justamente o problema. Este componente e so quem cria a marca, na
//                   hora, no lugar certo. Toda a caminhada continua sendo do NpcWalkTo.
//
// COMO SE MONTA NA CENA
//   Um GameObject vazio no lugar da conversa, com:
//     - Collider2D marcado IS TRIGGER (a area onde o Josh precisa pisar)
//     - este componente
//   Em "Quem vem", uma linha por NPC: arrasta o NpcWalkTo dele e escolhe o offset.
//   Os gizmos desenham as marcas na Scene View — da para acertar por olho, sem entrar em
//   Play. Y positivo = mais para cima na tela = atras do Josh, que e onde eles cabem sem
//   tapar o balao de fala dele.
[DisallowMultipleComponent]
public class NpcGather : MonoBehaviour
{
    // So esquerda e direita, e nao as quatro direcoes, pelo mesmo motivo que no NpcWalkTo:
    // a arte deste projeto nao tem pose PARADA olhando para cima ou para baixo. Oferecer
    // Cima/Baixo aqui seria um botao que nao faz nada.
    public enum Facing
    {
        Esquerda,
        Direita,
        Manter    // fica no lado em que a caminhada o deixou
    }

    [System.Serializable]
    public class Guest
    {
        [Tooltip("O NpcWalkTo do personagem. E ele que anda, vira e anima — isto aqui so " +
                 "diz PARA ONDE.")]
        public NpcWalkTo walker;

        [Tooltip("Onde ele para, medido a partir do JOGADOR. X negativo = a esquerda dele, " +
                 "Y positivo = acima (atras) dele.")]
        public Vector2 offset = new Vector2(-1.2f, 0.8f);

        [Tooltip("Para que lado ele fica olhando depois de parar. Aplicado DEPOIS da " +
                 "chegada, entao manda mais que o Arrival Facing do NpcWalkTo dele — que " +
                 "pertence a outra cena e nao pode ser reescrito por esta.")]
        public Facing arrivalFacing = Facing.Esquerda;

        [Tooltip("Pontos de passagem OPCIONAIS, percorridos nesta ordem ANTES do offset.\n\n" +
                 "So precisa se houver parede ou movel entre ele e o Josh: o NpcWalkTo anda " +
                 "em linha reta e encosta na quina. Um ponto no vao da porta resolve.")]
        public Transform[] via;

        [Tooltip("Velocidade SO nesta aproximacao. 0 = nao mexe, usa a do NpcWalkTo do " +
                 "personagem (hoje 2). O Josh anda a ~3.\n\n" +
                 "Vale por evento, entao a mesma pessoa pode chegar correndo numa cena e " +
                 "devagar em outra. Dar valores DIFERENTES aos dois — 2.2 e 1.8, por " +
                 "exemplo — faz eles chegarem desencontrados em vez de em formacao, e isso " +
                 "sozinho tira o ar de dois NPCs executando um comando.")]
        public float moveSpeed;
    }

    [Header("Quando")]
    [Tooltip("So dispara com este progresso gravado (SaveManager). Vazio = sem condicao.\n\n" +
             "E este campo que amarra a cena a mesa de escrita: o <<progress>> no fim do no " +
             "do .yarn e a chave, e isto e a fechadura.")]
    public string requiresProgress = "ReadElderNotes";

    [Tooltip("Depois de acontecer uma vez, nunca mais.")]
    public bool onceOnly = true;

    public string playerTag = "Player";

    [Header("Quem vem")]
    public List<Guest> guests = new List<Guest>();

    [Header("Como")]
    [Tooltip("Trava o jogador enquanto eles se aproximam.\n\n" +
             "LIGADO e o certo em quase todo caso, e nao e so estetica: as marcas sao " +
             "medidas a partir de onde ele esta AGORA. Se ele continuar andando, os amigos " +
             "vao para um ponto que o Josh ja deixou, e a roda de conversa nasce torta.")]
    public bool freezePlayer = true;

    [Tooltip("Para que lado o jogador fica olhando quando a conversa comeca.\n\n" +
             "E um lado FIXO e nao 'vire para o grupo' de proposito: ele chega aqui andando " +
             "para a esquerda, e essa e a pose em que ele ja esta. Calcular a direcao do " +
             "grupo daria virar para cima — que na arte deste jogo nao tem pose parada — ou " +
             "um giro no ultimo quadro, que so chama atencao para si mesmo.")]
    public Facing playerFacing = Facing.Esquerda;

    [Tooltip("Respiro entre o ultimo chegar e a fala comecar. Zero fica seco — a fala " +
             "estoura no mesmo quadro do ultimo passo.")]
    public float pauseBeforeDialogue = 0.35f;

    [Tooltip("Rede de seguranca. Se alguem nao chegar neste tempo, a fala comeca assim " +
             "mesmo, em vez de a cena travar para sempre. O NpcWalkTo tem o proprio limite " +
             "(Give Up After Seconds); este cobre o caso de ele nem ter comecado a andar.")]
    public float giveUpAfterSeconds = 25f;

    [Header("O que acontece")]
    [Tooltip("No do .yarn com a CHAMADA — a fala que o Josh solta ANTES de eles virem.\n\n" +
             "E o que poe a cena na ordem certa: ele para, chama, e so entao eles " +
             "atravessam a sala. Sem isto os dois vem sozinhos, sem motivo nenhum, e a fala " +
             "dele chega depois de eles ja estarem parados do lado — que e o efeito ao " +
             "contrario.\n\n" +
             "Vazio = ninguem chama, eles vem direto.")]
    public string callNode = "elders_house_first_floor_call";

    [Tooltip("No do .yarn disparado quando todos chegam. Vazio = nao dispara fala nenhuma, " +
             "so junta o grupo (o resto fica no evento abaixo).")]
    public string dialogueNode = "elders_house_first_floor";

    [Tooltip("Conversa em BARK tocada quando todos chegam — a alternativa ao no do .yarn.\n\n" +
             "Serve para o beat que nao merece caixa de dialogo: os dois comentam o lugar " +
             "enquanto o jogo continua desenhado normalmente. Deixe o Dialogue Node vazio ao " +
             "usar isto; os dois juntos seriam duas falas por cima uma da outra.\n\n" +
             "O jogador continua PARADO ate a conversa terminar, mesmo o bark nao sendo " +
             "bloqueante — nesta cena ele parou para ouvir, e sair andando no meio " +
             "desmancharia a roda que acabou de se formar.\n\n" +
             "Ponha o BarkConversation em Start Mode = Manual, senao ele dispara sozinho " +
             "antes da hora.")]
    public BarkConversation conversaAoJuntar;

    [Tooltip("Disparado junto com a fala, para qualquer coisa alem dela.")]
    public UnityEvent onGathered;

    private bool fired;
    private bool running;
    private bool playerInside;
    private Transform player;

    private void OnEnable()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogWarning($"[NpcGather] '{name}': nao tem Collider2D — nao existe area " +
                             "para o jogador pisar, isto nunca vai disparar.", this);
            return;
        }

        if (!col.isTrigger)
            Debug.LogWarning($"[NpcGather] '{name}': o Collider2D NAO esta marcado como Is " +
                             "Trigger. Sem isso ele vira parede e nao dispara nada.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag)) return;

        playerInside = true;
        player = other.transform;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag)) return;
        playerInside = false;
    }

    // A condicao e conferida a cada quadro enquanto ele esta dentro, e nao uma vez so na
    // entrada, porque as duas ordens acontecem de verdade: ele pode passar por aqui ANTES
    // de ler as notas (e ai nada pode acontecer), e pode estar parado exatamente em cima do
    // gatilho quando a condicao ficar verdadeira. Conferindo so na entrada, o segundo caso
    // exige sair e voltar — que de dentro do jogo parece um gatilho quebrado.
    private void Update()
    {
        if (!playerInside || running) return;
        if (onceOnly && fired) return;

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null)
            {
                Debug.LogWarning($"[NpcGather] '{name}': exige o progresso " +
                                 $"'{requiresProgress}' mas nao ha SaveManager na cena.", this);
                enabled = false;
                return;
            }

            if (!SaveManager.Instance.HasProgress(requiresProgress)) return;
        }

        if (player == null) return;

        fired = true;
        StartCoroutine(Gather());
    }

    private IEnumerator Gather()
    {
        running = true;

        PlayerController pc = player.GetComponent<PlayerController>();

        if (freezePlayer && pc != null) pc.InputEnabled = false;

        // A posicao e lida DEPOIS do congelamento e de um passo de fisica, e nao no quadro
        // da entrada no gatilho. Entre uma coisa e outra o corpo ainda desliza um pouco, e e
        // essa posicao final — onde ele de fato para — que as marcas precisam usar.
        yield return new WaitForFixedUpdate();

        Vector2 anchor = player.position;

        // O lado do jogador e acertado AQUI, antes da chamada, e nao no fim junto com o dos
        // outros: e nesta fala que a camera esta nele sozinho. Virar depois seria virar
        // enquanto ninguem olha.
        if (pc != null && playerFacing != Facing.Manter)
            pc.SetFacing(playerFacing == Facing.Esquerda ? Vector2.left : Vector2.right);

        // A CHAMADA. Bloqueante de proposito: e o motivo de eles virem, entao tem de acabar
        // antes de o primeiro passo sair.
        if (!string.IsNullOrEmpty(callNode))
        {
            if (DialogueStarter.EvaluateConditionsAndStart(callNode, false, null, false))
            {
                Yarn.Unity.DialogueRunner runner = DialogueManager.Instance?.dialogueRunner;

                while (runner != null && runner.IsDialogueRunning)
                    yield return null;

                // Um quadro a mais ANTES de recongelar. IsDialogueRunning cai e o
                // onDialogueComplete dispara, mas nada garante que seja no mesmo quadro —
                // e e o onDialogueComplete que descongela. Recongelando cedo demais, o
                // descongelamento dele viria depois e mandaria.
                yield return null;

                // O funil de dialogo devolve o controle ao jogador quando a fala fecha —
                // ele nao sabe que isto aqui e o meio de uma cena, nao o fim dela. Sem
                // retomar o congelamento, o Josh volta a andar enquanto os dois atravessam
                // a sala, e as marcas (medidas de onde ele estava) ficam para tras.
                if (freezePlayer && pc != null) pc.InputEnabled = false;
            }
            else
            {
                Debug.LogWarning($"[NpcGather] '{name}': o no de chamada '{callNode}' nao " +
                                 "comecou — seguindo direto para a caminhada. Confira se o " +
                                 "nome esta escrito igual ao title: do .yarn.", this);
            }
        }

        var marks = new List<GameObject>();
        var walking = new List<NpcWalkTo>();

        // A velocidade original de cada um, para ser DEVOLVIDA quando a cena acabar.
        //
        // O campo moveSpeed do NpcWalkTo pertence ao personagem e vale em todas as
        // caminhadas dele. Escrever nele e sair sem restaurar faria a escolha desta cena
        // vazar para todas as seguintes — e o vazamento so apareceria muito depois, numa
        // cena que ninguem associaria a esta.
        var velocidadeOriginal = new Dictionary<NpcWalkTo, float>();

        foreach (Guest g in guests)
        {
            if (g == null || g.walker == null)
            {
                Debug.LogWarning($"[NpcGather] '{name}': ha uma linha em Quem Vem sem " +
                                 "NpcWalkTo arrastado — essa linha nao faz nada.", this);
                continue;
            }

            // O NpcFollow briga com o NpcWalkTo: um puxa para a trilha do Josh, o outro para
            // a marca, e o NPC fica tremendo entre os dois. Hoje o Follow esta desligado no
            // projeto todo, mas isto e o que impede a cena de quebrar em silencio no dia em
            // que ele for religado.
            NpcFollow follow = g.walker.GetComponent<NpcFollow>();
            if (follow != null && follow.enabled) follow.enabled = false;

            // A marca nasce filha DESTE objeto para o Hierarchy mostrar de onde ela veio, se
            // alguem der pause no meio da caminhada.
            var mark = new GameObject($"GatherMark_{g.walker.name}");
            mark.transform.SetParent(transform, false);
            mark.transform.position = anchor + g.offset;
            marks.Add(mark);

            var route = new List<NpcWalkTo.Stop>();

            if (g.via != null)
                foreach (Transform v in g.via)
                    if (v != null) route.Add(new NpcWalkTo.Stop { point = v, waitHere = 0f });

            route.Add(new NpcWalkTo.Stop { point = mark.transform, waitHere = 0f });

            // Zero significa "nao mexe", e nao "parado" — mesma convencao do NpcEntrance.
            if (g.moveSpeed > 0f)
            {
                velocidadeOriginal[g.walker] = g.walker.moveSpeed;
                g.walker.moveSpeed = g.moveSpeed;
            }

            // GoPath copia para a rota interna dele — o caminho montado no Inspector do
            // NpcWalkTo nao e tocado.
            g.walker.GoPath(route);
            walking.Add(g.walker);
        }

        // Um quadro de folga: GoPath marca IsWalking na hora, mas quem ja estava em cima da
        // marca so descobre que chegou no primeiro FixedUpdate. Perguntar antes disso da
        // "ninguem esta andando" e a fala comecaria com os dois ainda parados longe.
        yield return new WaitForFixedUpdate();

        float deadline = Time.time + Mathf.Max(0.1f, giveUpAfterSeconds);
        bool timedOut = true;

        while (Time.time < deadline)
        {
            bool anyWalking = false;
            foreach (NpcWalkTo w in walking)
                if (w != null && w.IsWalking) { anyWalking = true; break; }

            if (!anyWalking) { timedOut = false; break; }
            yield return null;
        }

        if (timedOut)
            Debug.LogWarning($"[NpcGather] '{name}': alguem nao chegou em " +
                             $"{giveUpAfterSeconds}s — comecando a fala assim mesmo para nao " +
                             "travar a cena. Provavelmente tem movel no caminho: ponha um " +
                             "ponto em Via para ele dobrar a esquina.", this);

        foreach (var kv in velocidadeOriginal)
            if (kv.Key != null) kv.Key.moveSpeed = kv.Value;

        foreach (Guest g in guests)
        {
            if (g == null || g.walker == null) continue;
            if (g.arrivalFacing == Facing.Manter) continue;

            CharacterFacing f = g.walker.GetComponent<CharacterFacing>();
            if (f == null) f = g.walker.GetComponentInChildren<CharacterFacing>();
            if (f == null) continue;

            f.FaceHorizontal(g.arrivalFacing == Facing.Esquerda);
        }

        if (pauseBeforeDialogue > 0f)
            yield return new WaitForSeconds(pauseBeforeDialogue);

        foreach (GameObject m in marks)
            if (m != null) Destroy(m);

        onGathered?.Invoke();

        // A conversa em bark, quando e ela que carrega o beat.
        //
        // O jogo nao para — o bark e popup, esse e o ponto dele — mas o JOGADOR fica parado
        // ate o fim. Nao e o mesmo que travar o jogo: e a diferenca entre "o mundo congelou"
        // e "ele parou para ouvir". Sair andando no meio desmancharia a roda que os tres
        // acabaram de formar, e as falas terminariam sozinhas no meio do nada.
        if (conversaAoJuntar != null)
        {
            conversaAoJuntar.Play();

            // Um quadro antes de perguntar: o Play() marca a conversa como rodando dentro
            // de uma corrotina, e no mesmo quadro ela ainda pode nao constar.
            yield return null;

            while (conversaAoJuntar != null && conversaAoJuntar.IsRunning)
                yield return null;

            if (freezePlayer && pc != null) pc.InputEnabled = true;
        }

        bool started = false;

        if (!string.IsNullOrEmpty(dialogueNode))
        {
            // Passa pelo mesmo funil que todo dialogo do jogo usa, em vez de chamar o
            // DialogueManager na mao: e ele que trava o jogador enquanto a fala roda e o
            // destrava no fim. Chamar por fora deixaria o Josh andando durante a conversa.
            started = DialogueStarter.EvaluateConditionsAndStart(dialogueNode, false, null, false);

            if (!started)
                Debug.LogWarning($"[NpcGather] '{name}': o no '{dialogueNode}' nao comecou. " +
                                 "Confira se o nome esta escrito igual ao title: do .yarn.", this);
        }

        // Se a fala comecou, ela mesma devolve o controle no fim. Se nao comecou — nome
        // errado, runner ausente — quem congelou tem de descongelar, senao o jogador fica
        // preso para sempre por causa de um erro de digitacao.
        if (!started && freezePlayer && pc != null) pc.InputEnabled = true;

        running = false;
    }

    // O gizmo desenha as marcas a partir do CENTRO DO GATILHO, que e o palpite honesto de
    // onde o jogador vai estar. Em Play ele para alguns centimetros diferente disso, mas
    // para acertar o offset por olho a diferenca nao importa — e para isso que serve.
    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        Vector3 anchor = col != null ? (Vector3)col.bounds.center : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(anchor, 0.18f);

        Gizmos.color = Color.yellow;

        foreach (Guest g in guests)
        {
            if (g == null) continue;

            Vector3 spot = anchor + (Vector3)g.offset;
            Gizmos.DrawLine(anchor, spot);
            Gizmos.DrawWireSphere(spot, 0.22f);

            // Risquinho para o lado em que ele vai ficar olhando, para o Arrival Facing se
            // conferir por olho junto com o offset em vez de so no Inspector.
            if (g.arrivalFacing != Facing.Manter)
            {
                float side = g.arrivalFacing == Facing.Esquerda ? -1f : 1f;
                Gizmos.DrawLine(spot, spot + new Vector3(side * 0.45f, 0f, 0f));
            }
        }
    }
}
