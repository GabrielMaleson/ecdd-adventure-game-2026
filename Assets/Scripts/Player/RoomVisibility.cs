using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

// Uma sala que aparece/some num fade. Só UMA sala fica visível por vez: entrar na
// zona de uma apaga todas as outras. É o que deixa duas salas ocuparem o mesmo
// espaço visual sem nunca aparecerem juntas na tela.
//
// COMO ELE DECIDE EM QUE SALA VOCÊ ESTÁ
// Cada sala tem uma ZONA — o Collider2D deste objeto, cobrindo o chão dela. Todo
// frame o sistema pergunta em qual zona está UM PONTO: o pé do player (o pivô dele).
// Não usa OnTriggerEnter, não usa física, não usa tag nem layer, e a LARGURA do
// player não entra na conta — é um ponto contra uma forma, e pronto.
//
// AS ZONAS PODEM SE ENCOSTAR NA MESMA LINHA, E PODEM ATÉ SE SOBREPOR. Duas coisas
// seguram isso:
//   Commit Time — a sala nova só assume depois que o ponto ficou dentro dela por N
//     segundos SEGUIDOS. Ficar bambeando em cima da divisa não troca nada: cada
//     vaivém zera a contagem. Só uma travessia de verdade passa do tempo. É isto que
//     permite a divisa ser uma linha de um pixel.
//   Recência — dentro de duas zonas ao mesmo tempo, ganha a que ele acabou de entrar.
//     Ao sair dela, cai de volta pra mais recente que ainda o contém. Ou seja: uma
//     alcova desenhada por cima do salão funciona sozinha, sem recortar zona e sem
//     configurar nada.
// E o fade continua de onde estava (não reinicia do zero), então nem uma troca
// legítima no meio de outra dá estouro de brilho.
//
// Fora de qualquer zona, NADA muda: continua valendo a última sala em que o player
// entrou. Por isso corredor, buraco na parede e vão de porta podem não pertencer a
// zona nenhuma, sem efeito colateral.
//
// AUTORAÇÃO
//   1. Este componente vai na RAIZ da sala (o pai dos tilemaps/sprites dela).
//   2. Collider2D nessa mesma raiz, cobrindo o chão. Is Trigger é recomendado (pra
//      não empurrar ninguém), mas o teste funciona de qualquer jeito.
//   3. Sala onde o player começa: Start Visible LIGADO. As outras: DESLIGADO. Se
//      NINGUÉM tiver ligado, todas nascem apagadas e a sala em que o player estiver
//      acende sozinha depois do Commit Time — não trava, só começa com a tela preta
//      por um instante.
//   4. O que pertence à sala mas não é filho da raiz (caixas, estátua, tochas) vai em
//      Also Fade — senão fica boiando no preto quando a sala apagar.
//
// NUNCA ponha o player nem o Fragmento embaixo da raiz de uma sala (nem em Also Fade):
// eles sumiriam junto.
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class RoomVisibility : MonoBehaviour
{
    [Header("Estado inicial")]
    [Tooltip("LIGADO na sala onde o player começa o jogo. DESLIGADO em todas as outras (nascem invisíveis). No editor a sala continua visível normalmente — isto só vale ao dar Play.")]
    [SerializeField] bool startVisible = false;

    [Header("Zona")]
    [Tooltip("Segundos que o pé do player precisa ficar SEGUIDOS dentro desta zona pra ela assumir. É o que impede o piscar quando a divisa entre duas salas é uma linha fina. 0.12–0.2 é o normal. Aumente se ainda piscar; diminua se a troca parecer atrasada.")]
    [SerializeField] float commitTime = 0.15f;

    [Header("Fade")]
    [Tooltip("Segundos para esta sala aparecer. 0 = na hora.")]
    [SerializeField] float fadeInDuration = 0.45f;

    [Tooltip("Segundos para esta sala sumir. Um pouco MAIOR que o fade in faz a sala antiga demorar mais a ir embora, e a troca fica menos seca.")]
    [SerializeField] float fadeOutDuration = 0.6f;

    [Header("O que entra no fade")]
    [Tooltip("Objetos que pertencem a esta sala mas não são filhos desta raiz — caixas, estátua, tochas, pickups. Sem isto eles ficam visíveis boiando no preto depois que a sala apaga. NUNCA ponha o player ou o Fragmento aqui.")]
    [SerializeField] Transform[] alsoFade;

    [Header("Extras")]
    [Tooltip("Desliga os renderers quando a sala termina invisível (economiza draw calls). Colliders e zona continuam ativos, então dá pra voltar pra sala normalmente.")]
    [SerializeField] bool disableRenderersWhenHidden = true;

    [Tooltip("Dispara toda vez que o player entra nesta sala — música de zona, ambiência.")]
    public UnityEvent onEntered;

    [Tooltip("Dispara toda vez que o player sai desta sala (quando outra assume).")]
    public UnityEvent onExited;

    [Tooltip("Dispara só na PRIMEIRA vez que o player entra — o som de revelação da cripta, um diálogo, uma flag de save.")]
    public UnityEvent onFirstEntered;

    public bool IsCurrent => current == this;

    // ---------------------------------------------------------------- detecção

    static readonly List<RoomVisibility> all = new List<RoomVisibility>();
    static RoomVisibility current;      // a sala acesa
    static RoomVisibility pending;      // a candidata que está cumprindo o Commit Time
    static float pendingSince;
    static int lastResolvedFrame = -1;
    static Transform player;
    static float nextPlayerSearch;

    bool wasInside;             // o ponto estava dentro desta zona no frame passado
    float insideSince;          // quando ele entrou nela (usado pra "a mais recente")

    // Limpa estado velho no começo de todo Play (sobrevive ao Fast Play Mode).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        current = null;
        pending = null;
        lastResolvedFrame = -1;
        player = null;
        nextPlayerSearch = 0f;
    }

    // A decisão é UMA por frame pra cena inteira, não uma por sala: quem chegar
    // primeiro no frame resolve por todo mundo.
    void Update() => Resolve();

    static void Resolve()
    {
        if (lastResolvedFrame == Time.frameCount) return;
        lastResolvedFrame = Time.frameCount;

        if (!TryGetPlayerPoint(out Vector2 point)) return;

        // Carimba o instante em que o ponto ENTROU em cada zona (transição de fora
        // pra dentro), não em que está dentro. É esse carimbo que define "acabou de
        // entrar".
        foreach (var room in all)
        {
            bool inside = room.ContainsPoint(point);
            if (inside && !room.wasInside) room.insideSince = Time.time;
            room.wasInside = inside;
        }

        // Candidata = das zonas que contêm o ponto, a que ele entrou POR ÚLTIMO.
        // Começa pela sala atual pra que ela ganhe empates (dois carimbos no mesmo
        // frame não devem provocar troca nenhuma).
        RoomVisibility candidate = (current != null && current.wasInside) ? current : null;
        foreach (var room in all)
        {
            if (!room.wasInside) continue;
            if (candidate == null || room.insideSince > candidate.insideSince)
                candidate = room;
        }

        // Fora de tudo (vão de porta, corredor): mantém a sala atual e desarma
        // qualquer candidatura em andamento.
        if (candidate == null) { pending = null; return; }

        if (candidate == current) { pending = null; return; }

        // Nova candidata: começa a contar do zero. Bambear em cima da divisa reentra
        // aqui toda hora e nunca chega no Commit Time — que é exatamente o ponto.
        if (candidate != pending)
        {
            pending = candidate;
            pendingSince = Time.time;
            return;
        }

        if (Time.time - pendingSince < candidate.commitTime) return;

        pending = null;
        candidate.Enter();
    }

    // Cacheado no Awake: GetComponents aloca um array a cada chamada, e isto roda todo
    // frame. Vários colliders no mesmo objeto são permitidos (sala em L, dois pedaços)
    // — a zona é a união deles.
    Collider2D[] zone;

    bool ContainsPoint(Vector2 point)
    {
        foreach (var col in zone)
            if (col.enabled && col.OverlapPoint(point)) return true;
        return false;
    }

    // O PÉ do player (o pivô), não o centro do sprite nem o collider: um ponto só.
    // É o que torna a divisa milimétrica viável — sem largura, não tem como encostar
    // em duas zonas ao mesmo tempo.
    //
    // Retorna false enquanto não houver player (cena carregando, player destruído).
    // A busca é limitada a uma a cada meio segundo: sem isso, uma cena SEM player
    // faria duas varreduras da cena inteira TODO frame, para sempre.
    static bool TryGetPlayerPoint(out Vector2 point)
    {
        point = default;

        // '== null' aqui é o do Unity: pega também um player que foi destruído,
        // e a busca recomeça sozinha.
        if (player == null)
        {
            if (Time.time < nextPlayerSearch) return false;
            nextPlayerSearch = Time.time + 0.5f;

            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) player = pc.transform;
            else
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            if (player == null) return false;
        }

        point = player.position;
        return true;
    }

    // ------------------------------------------------------------------- fade

    // Cor autorada de cada renderer: o fade termina exatamente no que você pintou no
    // editor, e não num alpha 1 chapado que ignoraria transparência de propósito.
    readonly List<SpriteRenderer> sprites       = new List<SpriteRenderer>();
    readonly List<Color>          spriteColors  = new List<Color>();
    readonly List<Tilemap>        tilemaps      = new List<Tilemap>();
    readonly List<Color>          tilemapColors = new List<Color>();
    readonly List<Renderer>       renderers     = new List<Renderer>();

    bool everEntered;
    Coroutine fade;
    float shown;                 // 0 = invisível, 1 = totalmente visível

    void Awake()
    {
        zone = GetComponents<Collider2D>();

        Collect(transform);
        foreach (var extra in alsoFade)
            if (extra != null) Collect(extra);
    }

    // Inclui filhos inativos: uma sala pode ter variantes desligadas que só ligam
    // depois, e elas também precisam nascer com o alpha certo.
    void Collect(Transform root)
    {
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprites.Add(sr);
            spriteColors.Add(sr.color);
            renderers.Add(sr);
        }
        foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
        {
            tilemaps.Add(tm);
            tilemapColors.Add(tm.color);
            var tr = tm.GetComponent<TilemapRenderer>();
            if (tr != null) renderers.Add(tr);
        }
    }

    void OnEnable() => all.Add(this);

    void OnDisable()
    {
        all.Remove(this);
        if (pending == this) pending = null;
        wasInside = false;
    }

    void Start()
    {
        if (startVisible)
        {
            if (current != null && current != this)
                Debug.LogWarning($"{name}: mais de uma sala com Start Visible ligado ('{current.name}' também). Só a última vale — deixe ligado só na sala onde o player começa.", this);
            current = this;
            everEntered = true;
        }

        ApplyProgress(startVisible ? 1f : 0f);
        SetRenderersEnabled(startVisible || !disableRenderersWhenHidden);
    }

    // Público para um UnityEvent poder forçar a troca sem o player andar (um diálogo,
    // um teleporte, uma cutscene). Ignora o Commit Time de propósito: é uma ordem.
    public void Enter()
    {
        if (current == this) return;

        // Qualquer contagem de Commit Time em andamento morre aqui: ela foi feita
        // contra a sala que acabou de deixar de ser a atual, e aproveitá-la deixaria
        // uma troca disparar no frame seguinte sem cumprir o tempo.
        pending = null;

        // Apaga quem estava acesa ANTES de acender esta: as duas coroutines rodam em
        // paralelo, então é um cross-fade de verdade, não um piscar sequencial.
        var previous = current;
        current = this;
        if (previous != null) previous.Exit();

        StartFade(1f, fadeInDuration);

        onEntered?.Invoke();
        if (!everEntered)
        {
            everEntered = true;
            onFirstEntered?.Invoke();
        }
    }

    void Exit()
    {
        StartFade(0f, fadeOutDuration);
        onExited?.Invoke();
    }

    void StartFade(float target, float duration)
    {
        if (fade != null) { StopCoroutine(fade); fade = null; }

        // Vai aparecer: religa os renderers ANTES do fade, senão ele roda invisível.
        if (target > 0f) SetRenderersEnabled(true);

        if (duration <= 0f || !gameObject.activeInHierarchy)
        {
            ApplyProgress(target);
            if (target <= 0f && disableRenderersWhenHidden) SetRenderersEnabled(false);
            return;
        }
        fade = StartCoroutine(Fade(target, duration));
    }

    IEnumerator Fade(float target, float duration)
    {
        // Parte do alpha ATUAL, não do zero: uma troca que pegue outra pela metade
        // continua de onde estava em vez de dar um salto.
        float from = shown;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            ApplyProgress(Mathf.Lerp(from, target, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        ApplyProgress(target);

        if (target <= 0f && disableRenderersWhenHidden) SetRenderersEnabled(false);
        fade = null;
    }

    void ApplyProgress(float progress)
    {
        shown = progress;

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;
            var c = spriteColors[i];
            sprites[i].color = new Color(c.r, c.g, c.b, c.a * progress);
        }
        for (int i = 0; i < tilemaps.Count; i++)
        {
            if (tilemaps[i] == null) continue;
            var c = tilemapColors[i];
            tilemaps[i].color = new Color(c.r, c.g, c.b, c.a * progress);
        }
    }

    void SetRenderersEnabled(bool on)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = on;
    }
}
