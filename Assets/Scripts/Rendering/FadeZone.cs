using System.Collections.Generic;
using UnityEngine;

// "Estas arvores AQUI ficam translucidas quando o jogador passa atras delas. O resto do
// jogo nao muda."
//
// O PROBLEMA QUE ISTO RESOLVE, e por que nao e nenhuma das duas solucoes obvias:
//
//   Componente em cada arvore — sao 180 arvores na MainScene. Inviavel de arrastar, e o
//     resultado nao sairia padronizado.
//   Componente no PREFAB — sao so 13 prefabs, entao daria pouco trabalho... e ligaria o
//     efeito nas 180 instancias do jogo inteiro, que e exatamente o que nao se quer agora.
//
// A escolha nao e "qual arvore" nem "qual tipo de arvore": e QUAL PEDACO DO MAPA. Entao o
// que seleciona tambem tem de ser um pedaco do mapa. Voce desenha um poligono por cima da
// mata e acabou; nenhuma arvore e tocada, nenhum prefab e alterado, e nada fora do
// poligono entra.
//
// E O FADE CONTINUA SENDO POR ARVORE. A zona so decide QUEM esta sob o sistema — a decisao
// de apagar e de cada arvore, com a propria distancia e o proprio "o jogador esta atras de
// mim". Nao existe momento em que a mata inteira apaga junto: visualmente e identico a ter
// posto o componente em cada uma, so que sem a arrastacao.
//
// COMO SE MONTA
//   1. Um GameObject vazio, em qualquer lugar da cena.
//   2. Um Collider2D nele marcado IS TRIGGER — um PolygonCollider2D acompanha o formato
//      irregular de uma mata muito melhor que uma caixa.
//   3. Este componente.
//
// Ao dar Play ele diz no Console quantas arvores pegou. Se o numero nao bater com o que
// voce ve, o poligono e que esta errado — nao ha nada escondido para configurar.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FadeZone : MonoBehaviour
{
    [Header("Quanto")]
    [Tooltip("Opacidade quando o jogador esta atras, como FRACAO da opacidade autorada. " +
             "0.35-0.45 deixa ver o personagem e ainda le como arvore.")]
    [Range(0f, 1f)]
    public float fadedAlpha = 0.4f;

    [Tooltip("Velocidade da transicao, em opacidade por segundo.")]
    public float fadeSpeed = 3f;

    [Header("Quando")]
    [Tooltip("A que distancia do PE de cada arvore o jogador precisa chegar para ela " +
             "apagar. Este e o raio que faz o efeito ser por arvore: so as duas ou tres " +
             "em volta dele reagem, nunca a mata toda.")]
    public float raio = 2.2f;

    [Tooltip("So apaga se o jogador estiver ATRAS da arvore — mais para cima na tela que o " +
             "pe dela, a mesma regra que o Y-Sort usa para decidir quem desenha na frente. " +
             "Passando na FRENTE ele nao esta escondido por nada, e apagar a copa ai so faz " +
             "a arvore piscar sem motivo.")]
    public bool onlyWhenBehind = true;

    public string playerTag = "Player";

    [Header("Descoberta")]
    [Tooltip("Objetos que estao dentro do poligono mas NAO devem apagar nunca. Arraste a " +
             "raiz deles.")]
    public Transform[] naoIncluir;

    [Tooltip("Diz no Console, ao entrar em Play, quantos objetos a zona pegou e o nome de " +
             "cada um. Deixe ligado ate confiar no poligono.")]
    public bool relatorioAoIniciar = true;

    // Uma arvore. Os sprites dela sao varios (sombra, tronco, copa) e apagam JUNTOS — e o
    // mesmo conceito de grupo do Y-Sort, e de proposito: "uma arvore" tem de significar a
    // mesma coisa nos dois sistemas, senao um deles vai discordar do outro em algum caso.
    private class Group
    {
        public Transform root;
        public SpriteRenderer[] renderers;
        public float[] authoredAlpha;

        // O alpha ATUAL, em memoria gerenciada. Existe para nao perguntar ao renderer.
        //
        // `SpriteRenderer.color` e uma chamada nativa: ler os 540 sprites desta zona a cada
        // quadro custava 540 travessias de fronteira mesmo com a mata inteira parada, sem
        // nada para fazer. Guardando o valor aqui, um quadro em repouso nao encosta em
        // renderer nenhum — so compara floats.
        public float[] currentAlpha;
        public float footY;        // base do collider solido — o pe
        public Vector2 footPos;

        // Ha transicao em andamento. Enquanto for false e o jogador estiver longe, a arvore
        // e pulada inteira, sem tocar em renderer nenhum.
        public bool fading;
    }

    private readonly List<Group> groups = new List<Group>();
    private Collider2D zone;
    private Transform player;

    private void Start()
    {
        zone = GetComponent<Collider2D>();

        if (!zone.isTrigger)
            Debug.LogWarning($"[FadeZone] '{name}': o Collider2D nao esta marcado IS TRIGGER. " +
                             "Sem isso ele vira parede e o jogador nao consegue entrar na mata.", this);

        Discover();
    }

    private void Discover()
    {
        groups.Clear();

        var excluidos = new HashSet<Transform>();
        if (naoIncluir != null)
            foreach (Transform t in naoIncluir)
                if (t != null) excluidos.Add(t);

        var vistos = new HashSet<Transform>();

        // Contadores de RECUSA. "Nada aconteceu" tem cinco causas que de fora sao iguais, e
        // adivinhar qual delas e custa mais que contar.
        int recExcluido = 0, recTemDono = 0, recSemCollider = 0, recRigidbody = 0, recForaDoPoligono = 0;
        Vector2 exemploFora = Vector2.zero; string exemploForaNome = null;

        foreach (SpriteRenderer r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            Transform root = YSortWorld.RootOf(r.transform);

            if (!vistos.Add(root)) continue;
            if (excluidos.Contains(root)) { recExcluido++; continue; }

            // Quem manda em si mesmo fica de fora. Uma arvore com FadeWhenBehind proprio ja
            // tem dono; dois componentes escrevendo a mesma opacidade brigariam a cada
            // quadro e o resultado dependeria da ordem de execucao.
            if (root.GetComponentInChildren<FadeWhenBehind>() != null) { recTemDono++; continue; }

            // MESMO discriminador fisico do Y-Sort, e pelo mesmo motivo: collider solido
            // significa "ocupa espaco no mundo, o personagem esbarra nisto, logo passa
            // atras disto". Estrada, grama e poca nao tem collider e por isso nao entram —
            // e ainda bem, porque apagar o chao seria um desastre silencioso.
            //
            // Nao se filtra por NOME. Num jogo com 13 prefabs de arvore com nomes
            // diferentes, e num projeto onde ja se descobriu que filtrar por nome tira um
            // personagem principal do sistema sem avisar, o sinal tem de ser fisico.
            Collider2D solido = null;
            foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>())
            {
                if (c.isTrigger || !c.isActiveAndEnabled) continue;
                solido = c;
                break;
            }
            if (solido == null) { recSemCollider++; continue; }

            // PERSONAGEM fica de fora, senao o Josh parado dentro do poligono viraria
            // "arvore" e apagaria sozinho.
            //
            // Aqui havia um teste por Rigidbody2D, na ideia de que "quem anda pelo mundo tem
            // corpo fisico". Nesta cena isso e simplesmente FALSO: os prefabs de arvore do
            // ForestPixelLand tem Rigidbody2D — e Dynamic (BodyType 0) — enquanto Marcus,
            // Erika e Haze sao Kinematic (1). O teste recusava 195 objetos, quase todos
            // arvore, e nem inverte-lo funcionaria: nem o tipo de corpo separa os dois.
            //
            // O sinal que separa de verdade e o que MOVE o objeto. Personagem neste projeto
            // tem um destes; cenario nao tem nenhum. Continua sendo estrutura, e nao palpite
            // por nome do GameObject.
            if (root.GetComponentInChildren<PlayerController>(true) != null
             || root.GetComponentInChildren<CharacterFacing>(true) != null
             || root.GetComponentInChildren<FragmentFollow>(true) != null
             || root.CompareTag(playerTag))
            {
                recRigidbody++;
                continue;
            }

            Vector2 pe = new Vector2(solido.bounds.center.x, solido.bounds.min.y);

            // O teste e feito no PE, e num PONTO, nao nos bounds: com um poligono seguindo
            // o contorno da mata, uma arvore da borda tem a copa passando por fora sem que
            // ela deva ficar de fora. Onde o objeto TOCA O CHAO e que decide, que e a mesma
            // pergunta que o Y-Sort faz.
            if (!zone.OverlapPoint(pe))
            {
                recForaDoPoligono++;
                if (exemploForaNome == null) { exemploForaNome = root.name; exemploFora = pe; }
                continue;
            }

            var sprites = new List<SpriteRenderer>(root.GetComponentsInChildren<SpriteRenderer>(true));
            if (sprites.Count == 0) continue;

            var g = new Group
            {
                root = root,
                renderers = sprites.ToArray(),
                authoredAlpha = new float[sprites.Count],
                currentAlpha = new float[sprites.Count],
                footY = pe.y,
                footPos = pe
            };

            // A opacidade autorada e guardada UMA vez. O alvo e sempre relativo a ela e
            // nunca 1: uma copa desenhada a 0.8 tem de voltar para 0.8, e nao ganhar
            // opacidade na primeira vez que alguem passa por perto.
            for (int i = 0; i < sprites.Count; i++)
            {
                g.authoredAlpha[i] = sprites[i].color.a;
                g.currentAlpha[i] = g.authoredAlpha[i];
            }

            groups.Add(g);
        }

        // Um zero aqui e SEMPRE um erro de montagem, nunca um estado normal — entao ele
        // grita, mesmo com o relatorio desligado, e diz o que recusou e por que.
        if (groups.Count == 0)
        {
            Debug.LogError(
                $"[FadeZone] '{name}': NENHUM objeto entrou na zona. Recusados: " +
                $"{recForaDoPoligono} com o pe FORA do poligono, " +
                $"{recSemCollider} sem collider solido, " +
                $"{recRigidbody} personagens, " +
                $"{recTemDono} com FadeWhenBehind proprio, " +
                $"{recExcluido} na lista Nao Incluir. " +
                (exemploForaNome != null
                    ? $"Exemplo do primeiro recusado por posicao: '{exemploForaNome}', com o pe em {exemploFora}. " +
                      $"O collider da zona cobre {zone.bounds.min} ate {zone.bounds.max}. "
                    : "") +
                "Se o numero grande for 'fora do poligono', o poligono esta no lugar errado ou " +
                "em Z diferente; se for 'sem collider solido', as arvores desta cena nao tem " +
                "collider e o discriminador fisico nao as reconhece.", this);
            return;
        }

        if (!relatorioAoIniciar) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[FadeZone] '{name}': {groups.Count} objetos dentro da zona " +
                      $"(recusados: {recForaDoPoligono} fora do poligono, {recSemCollider} sem " +
                      $"collider solido, {recRigidbody} personagens, {recTemDono} com dono).");
        foreach (Group g in groups) sb.AppendLine($"   {g.root.name}  pe em {g.footPos}");
        Debug.Log(sb.ToString(), this);
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p == null) return;
            player = p.transform;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F9)) DumpAroundPlayer();
#endif

        Vector2 pos = player.position;
        float r2 = raio * raio;

        float passo = fadeSpeed * Time.deltaTime;

        foreach (Group g in groups)
        {
            if (g.root == null) continue;

            bool perto = ((Vector2)(pos - g.footPos)).sqrMagnitude <= r2;
            bool esconder = perto && (!onlyWhenBehind || pos.y > g.footY);

            // A arvore inteira decide de uma vez. Longe e ja no alpha autorado nao ha nada
            // a fazer, e esse e o caso de 99% das arvores em 99% dos quadros — o teste
            // abaixo e o que faz a zona custar quase nada com a mata parada.
            if (!esconder && !g.fading) continue;

            bool aindaMudando = false;

            for (int i = 0; i < g.renderers.Length; i++)
            {
                SpriteRenderer sr = g.renderers[i];
                if (sr == null) continue;

                float alvo = esconder ? g.authoredAlpha[i] * fadedAlpha : g.authoredAlpha[i];
                float atual = g.currentAlpha[i];

                if (atual == alvo) continue;

                atual = Mathf.MoveTowards(atual, alvo, passo);
                g.currentAlpha[i] = atual;

                Color c = sr.color;
                c.a = atual;
                sr.color = c;

                if (atual != alvo) aindaMudando = true;
            }

            g.fading = aindaMudando;
        }
    }

    // F9 em Play: por que ESTA arvore nao apagou.
    //
    // "Nao apagou" tem tres causas que de fora sao identicas — ela nao entrou na zona, ela
    // entrou mas o jogador esta longe do pe dela, ou ela entrou e esta perto mas o jogador
    // esta na FRENTE dela. Cada uma pede um conserto diferente (esticar o poligono, subir o
    // raio, desligar o Only When Behind), entao adivinhar qual e custa mais que medir.
    //
    // Lista os objetos perto do jogador, DENTRO e FORA da zona, para o caso mais provavel
    // aparecer: uma arvore que nem foi descoberta nao aparece em lista nenhuma de dentro.
    private void DumpAroundPlayer()
    {
        Vector2 pos = player.position;
        const float perto = 6f;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[FadeZone] F9 — jogador em {pos}. Raio={raio}, Only When Behind={onlyWhenBehind}.");

        var naZona = new HashSet<Transform>();
        foreach (Group g in groups) if (g.root != null) naZona.Add(g.root);

        sb.AppendLine("  NA ZONA:");
        int n = 0;
        foreach (Group g in groups)
        {
            if (g.root == null) continue;

            float d = Vector2.Distance(pos, g.footPos);
            if (d > perto) continue;

            n++;
            bool dentroDoRaio = d <= raio;
            bool atras = pos.y > g.footY;
            string motivo = dentroDoRaio && (atras || !onlyWhenBehind) ? "APAGANDO"
                          : !dentroDoRaio ? $"longe demais (precisa <= {raio})"
                          : "jogador esta NA FRENTE dela";

            sb.AppendLine($"    {g.root.name,-24} pe={g.footPos} dist={d:F2} " +
                          $"alpha={g.currentAlpha[0]:F2}  -> {motivo}");
        }
        if (n == 0) sb.AppendLine("    (nenhuma a menos de 6 unidades)");

        sb.AppendLine("  PERTO MAS FORA DA ZONA (nunca descobertas):");
        int fora = 0;
        var vistos = new HashSet<Transform>();
        foreach (SpriteRenderer r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            Transform root = YSortWorld.RootOf(r.transform);
            if (!vistos.Add(root) || naZona.Contains(root)) continue;
            if (Vector2.Distance(pos, root.position) > perto) continue;

            fora++;
            sb.AppendLine($"    {root.name,-24} pos={(Vector2)root.position} " +
                          $"— pe dentro do poligono? {zone.OverlapPoint(root.position)}");
        }
        if (fora == 0) sb.AppendLine("    (nenhum)");

        Debug.Log(sb.ToString(), this);
    }

    // Desligar a zona com arvores no meio de um fade as deixaria translucidas para sempre.
    private void OnDisable()
    {
        foreach (Group g in groups)
        {
            if (g.root == null) continue;

            for (int i = 0; i < g.renderers.Length; i++)
            {
                if (g.renderers[i] == null) continue;

                Color c = g.renderers[i].color;
                c.a = g.authoredAlpha[i];
                g.renderers[i].color = c;
                g.currentAlpha[i] = g.authoredAlpha[i];
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // O raio desenhado uma vez, no proprio objeto, so para dar escala: e ele que faz o
        // efeito ser por arvore, e no Inspector "2.2" nao diz nada sobre o tamanho de uma
        // copa nesta cena.
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, raio);
    }
}
