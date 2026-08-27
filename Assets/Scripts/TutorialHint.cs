using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

// A faixa de tutorial: uma linha de texto grande no topo-centro da tela, dizendo qual
// tecla apertar agora.
//
// ------------------------------------------------------------------ nao tem objeto na cena
//
// Ela CRIA a propria Canvas em tempo de execucao e some com ela no fim. Isso e deliberado:
// objeto de UI adicionado ao arquivo da cena por fora desaparece no instante em que o
// editor salva uma cena que ele carregou antes do objeto existir — aconteceu varias vezes
// neste projeto. Nao possuindo objeto nenhum na cena, nao ha o que perder.
//
// O preco disso e nao ter Inspector, e o preco e pago pelo TutorialHintSettings.asset em
// Assets/Resources: tamanho, cor, altura e os textos ficam la, editaveis, do mesmo jeito
// que o TextStyle.asset e o YSortSettings.asset. Sem o asset, os padroes daqui valem.
//
// ------------------------------------------------------------------ o comando bloqueante
//
// <<ghosttutorial>> PARA o dialogo no meio e so devolve o controle ao roteiro quando o
// jogador cumpriu a sequencia. E isso que permite a fala do Haze "I feel it calling to me"
// ser seguida do tutorial, e o resto da conversa continuar depois do puzzle se mexer, sem
// partir o no em dois.
//
// O <<movement>> deste projeto ja e um comando bloqueante do mesmo tipo, entao o caminho
// esta comprovado.
public class TutorialHint : MonoBehaviour
{
    public static TutorialHint Instance { get; private set; }

    private TMP_Text label;
    private Canvas canvas;
    private Coroutine fade;

    private static TutorialHintSettings S => TutorialHintSettings.Current;

    // ------------------------------------------------------------------ montagem

    private static TutorialHint Ensure()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("~TutorialHint");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<TutorialHint>();
        Instance.Build();
        return Instance;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Acima de tudo. A faixa e instrucao de sistema: se ela ficar atras do balao de
        // fala ou do prompt, o jogador nao sabe o que fazer e nada na tela explica por que.
        canvas.sortingOrder = 32000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var textGo = new GameObject("Texto");
        textGo.transform.SetParent(transform, false);

        label = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(1600f, 90f);

        Aplicar();
        label.enabled = false;
    }

    // Le o asset e escreve na faixa. Separado do Build para o OnValidate do asset poder
    // chamar isto em Play e a mudanca aparecer sem sair e voltar.
    private void Aplicar()
    {
        if (label == null) return;

        TutorialHintSettings s = S;

        float tamanho = s != null ? s.fontSize : 46f;
        float topo = s != null ? s.distanciaDoTopo : 60f;
        float dx = s != null ? s.deslocamentoX : 0f;

        // A fonte vem do asset proprio; sem ela, a mesma do prompt de interacao, para a
        // faixa nao destoar do resto da UI.
        if (s != null && s.font != null) label.font = s.font;
        else TextStyle.Apply(label, TextStyle.Role.InteractPrompt);

        label.fontSize = tamanho;
        label.color = s != null ? s.cor : Color.white;

        label.outlineWidth = s != null ? s.espessuraDoContorno : 0.2f;
        label.outlineColor = s != null ? (Color32)s.corDoContorno : new Color32(0, 0, 0, 255);

        label.rectTransform.anchoredPosition = new Vector2(dx, -topo);
    }

    public static void Reaplicar()
    {
        if (Instance != null) Instance.Aplicar();
    }

    // ------------------------------------------------------------------ API

    public static void Mostrar(string texto)
    {
        TutorialHint h = Ensure();
        h.Aplicar();

        h.label.text = texto;

        if (string.IsNullOrEmpty(texto)) { h.label.enabled = false; return; }

        h.label.enabled = true;
        h.IniciarFade(1f);
    }

    public static void Esconder()
    {
        if (Instance == null || Instance.label == null) return;
        Instance.IniciarFade(0f);
    }

    private void IniciarFade(float alvo)
    {
        float duracao = S != null ? S.fade : 0.15f;

        if (fade != null) StopCoroutine(fade);

        if (duracao <= 0f)
        {
            Color c = label.color; c.a = alvo; label.color = c;
            if (alvo <= 0f) { label.text = string.Empty; label.enabled = false; }
            return;
        }

        fade = StartCoroutine(Fade(alvo, duracao));
    }

    private IEnumerator Fade(float alvo, float duracao)
    {
        Color c = label.color;
        float inicio = c.a;

        for (float t = 0f; t < duracao; t += Time.unscaledDeltaTime)
        {
            c.a = Mathf.Lerp(inicio, alvo, t / duracao);
            label.color = c;
            yield return null;
        }

        c.a = alvo;
        label.color = c;

        // Desligar o rotulo depois de sumir, e nao so deixa-lo transparente: um TMP
        // invisivel continua montando a malha dele todo quadro.
        if (alvo <= 0f) { label.text = string.Empty; label.enabled = false; }

        fade = null;
    }

    // Chamaveis de UnityEvent (um StatueSwitch, um trigger) sem passar por comando de yarn.
    public void MostrarTexto(string texto) => Mostrar(texto);
    public void EsconderTexto() => Esconder();

    // ------------------------------------------------------------------ yarn

    // As estatuas nao tem como perceber sozinhas que o estado do tutorial mudou: o
    // OnTriggerEnter delas ja passou faz tempo. Quem muda o estado avisa.
    private static void ReavaliarEstatuas()
    {
        foreach (StatueSwitch st in FindObjectsByType<StatueSwitch>(FindObjectsSortMode.None))
            st.ReavaliarPrompt();
    }

    [YarnCommand("hint")]
    public static void Hint(string texto) => Mostrar(texto);

    [YarnCommand("hintoff")]
    public static void HintOff() => Esconder();

    // Enquanto isto e true, o StatueSwitch NAO oferece o E flutuante.
    //
    // A estatua e um objeto interagivel como qualquer outro e o E dela e o normal do jogo.
    // A excecao dura so o tutorial: ali a instrucao ja esta escrita na faixa do topo, e o
    // mesmo pedido aparecendo em dois lugares divide a atencao justamente no momento em que
    // o jogador ainda nao sabe onde olhar. Assim que ele usa a estatua, o E volta.
    public static bool SuprimindoPromptDaEstatua { get; private set; }

    // O E da estatua esta RECUSADO neste instante?
    //
    // Diferente de SuprimindoPromptDaEstatua, que so esconde o rotulo. Aqui a interacao nao
    // e nem registrada: apertar E nao faz nada. Existe porque o tutorial ensina uma ORDEM —
    // levar a Haze, estacionar, e so entao usar a estatua — e deixar o E funcionar antes do
    // Q permitiria pular o passo que a faixa esta pedindo naquele exato momento.
    public static bool EstatuaRecusandoE { get; private set; }

    // O tutorial esta parado esperando o jogador apertar alguma coisa.
    //
    // Existe por causa de uma colisao entre duas regras boas: teclas de fantasma ficam
    // travadas enquanto ha dialogo, e o <<ghosttutorial>> e um comando BLOQUEANTE — ou
    // seja, para o Yarn o dialogo esta rodando o tutorial inteiro. Sem esta excecao, o
    // tutorial pediria Q e o Q estaria travado por ele mesmo.
    public static bool EsperandoInputDoJogador { get; private set; }

    // Dialogo de verdade na tela: o Yarn rodando FORA de um comando do tutorial.
    public static bool DialogoBloqueandoInput
    {
        get
        {
            if (EsperandoInputDoJogador) return false;
            Yarn.Unity.DialogueRunner runner = DialogueManager.Instance?.dialogueRunner;
            return runner != null && runner.IsDialogueRunning;
        }
    }

    // <<ghosttutorial>> — PARA o dialogo e conduz o jogador pela mecanica inteira.
    //
    // A sequencia toda mora aqui, e nao espalhada em gatilhos da cena, porque ela e uma
    // coisa so: tutorial tem ORDEM, e ordem quebrada em cinco objetos diferentes fica
    // impossivel de ler depois. Cada passo espera um FATO do jogo, nunca um tempo fixo:
    //
    //   1. "aperte Q"                  -> espera ele de fato assumir a Haze
    //   2. "aperte E na estatua"       -> espera QUALQUER estatua girar
    //   3. camera e controle voltam ao Josh na hora, faixa some
    //   4. RETORNA: o resto da conversa roda, com a camera nele
    //   5. conversa acabou -> devolve a Haze e ensina o Q de volta
    //
    // O passo 5 nao cabe aqui dentro: enquanto este comando nao retornar, a conversa nao
    // anda, e ele so faz sentido DEPOIS dela. Por isso sai numa corrotina solta.
    //
    // O E flutuante da estatua fica suprimido do passo 1 ao 5 inteiro — so volta quando o
    // jogador aperta o Q final. Durante o tutorial a instrucao ja esta na faixa, e o mesmo
    // pedido em dois lugares divide a atencao de quem ainda nao sabe onde olhar.
    [YarnCommand("ghosttutorial")]
    public static IEnumerator GhostTutorial()
    {
        Ensure();

        if (GhostControl.Instance == null)
        {
            Debug.LogWarning("[TutorialHint] <<ghosttutorial>>: nao ha GhostControl na cena. " +
                             "Sem ele o jogador nunca assume a Haze e o dialogo travaria aqui " +
                             "para sempre — seguindo direto.");
            yield break;
        }

        int ativacoesNoInicio = StatueSwitch.AtivacoesTotais;

        // 1 a 3. Um LACO de estado, e nao tres passos em fila.
        //
        // A diferenca importa: em fila, sair do alcance da estatua depois de a faixa ja ter
        // mudado deixaria na tela um pedido que nao vale mais — "deixe a Haze aqui" com a
        // Haze longe. O jogador fica olhando uma instrucao impossivel sem entender por que.
        //
        // Aqui a faixa e recalculada a cada quadro a partir de onde as coisas ESTAO. Andar
        // para tras volta a dica para tras sozinho, quantas vezes ele quiser.
        //
        // O texto so e reescrito quando MUDA — Mostrar() reinicia o fade, e chamar todo
        // quadro faria a faixa piscar sem parar.
        GhostControl.Instance.LiberarQ();
        SuprimindoPromptDaEstatua = true;
        EsperandoInputDoJogador = true;

        string ultimo = null;

        while (StatueSwitch.AtivacoesTotais == ativacoesNoInicio)
        {
            GhostControl gc = GhostControl.Instance;
            if (gc == null) break;

            bool perto = StatueSwitch.FantasmaPertoDeAlgumaEstatua;

            // O E so vale com a Haze ESTACIONADA ao alcance — que e exatamente o unico
            // estado em que a faixa pede o E. Nos outros dois ela esta pedindo o Q, e o E
            // tem de ser inerte.
            bool recusar = !(gc.State == GhostControl.Mode.Parked && perto);

            if (recusar != EstatuaRecusandoE)
            {
                EstatuaRecusandoE = recusar;
                ReavaliarEstatuas();
            }

            string querido;

            if (gc.State == GhostControl.Mode.Parked && perto)
                // Estacionada ao alcance: e a hora do E.
                querido = S != null ? S.textoUsarEstatua : "Press E to interact with the statue";

            else if (gc.State == GhostControl.Mode.Piloting && perto)
                // Pilotando e ja ao alcance: so falta deixar ela ali.
                querido = S != null ? S.textoEstacionarHaze : "Press Q to leave Haze in position";

            else
                // Todo o resto — nao assumiu ainda, ou assumiu e saiu do alcance, ou
                // estacionou longe da estatua — e a mesma instrucao: leve a Haze ate la.
                querido = S != null ? S.textoAssumirHaze : "Press Q to control Haze and get to the statue";

            if (querido != ultimo)
            {
                Mostrar(querido);
                ultimo = querido;
            }

            yield return null;
        }

        EsperandoInputDoJogador = false;
        EstatuaRecusandoE = false;
        ReavaliarEstatuas();
        Esconder();

        // 4. Devolve o controle ao roteiro: as falas dos NPCs e da Haze continuam no
        //    dialogo, daqui em diante.
        //
        //    O ultimo passo NAO cabe aqui dentro: enquanto este comando nao retornar, a
        //    conversa nao anda. Por isso ele sai numa corrotina solta.
        Ensure().StartCoroutine(EnsinarOQuandoAFalaAcabar());
    }

    // 5. Fim da conversa: ensina a ultima tecla. O Q agora leva PARKED de volta a FOLLOW —
    //    e e literalmente isso que "a Haze volta" significa, ela larga a estatua e volta a
    //    seguir o Josh.
    private static IEnumerator EnsinarOQuandoAFalaAcabar()
    {
        Yarn.Unity.DialogueRunner runner = DialogueManager.Instance?.dialogueRunner;

        // Um quadro antes de olhar: quem chamou isto ainda esta DENTRO do comando, e o
        // runner so volta a andar depois que ele retorna.
        yield return null;

        while (runner != null && runner.IsDialogueRunning)
            yield return null;

        EsperandoInputDoJogador = true;
        Mostrar(S != null ? S.textoVoltarAoJosh : "Press Q again for Haze to come back");

        while (GhostControl.Instance != null &&
               GhostControl.Instance.State != GhostControl.Mode.Follow)
            yield return null;

        EsperandoInputDoJogador = false;

        Esconder();

        // 6. A saida de emergencia, por ultimo.
        //
        //    Ela sai quando ele aperta Z. O tempo e so um teto de seguranca, para a faixa
        //    nao ficar eterna se ele nunca apertar — e ele e folgado de proposito, porque
        //    desfazer e a unica tecla do tutorial sem uso imediato: ele so vai querer usar
        //    quando errar, e ate errar pode demorar.
        string zTexto = S != null ? S.textoDesfazer : "Press Z if you need to undo your last move";
        float zTempo = S != null ? S.segundosDaDicaDoZ : 5f;

        if (!string.IsNullOrEmpty(zTexto))
        {
            Mostrar(zTexto);

            for (float t = 0f; zTempo <= 0f || t < zTempo; t += Time.deltaTime)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.zKey.wasPressedThisFrame) break;
                yield return null;
            }

            Esconder();
        }

        // Fim do tutorial. Daqui em diante a estatua e um objeto interagivel como qualquer
        // outro, e o E volta a aparecer em cima dela.
        //
        // As estatuas com a Haze AINDA dentro do alcance precisam ser avisadas: o
        // OnTriggerEnter delas ja passou, e sem isto o prompt so voltaria se ela saisse e
        // entrasse de novo.
        SuprimindoPromptDaEstatua = false;
        EstatuaRecusandoE = false;
        ReavaliarEstatuas();

        // Marca o tutorial como feito. Sem isto, um pulo de teste que cai direto no puzzle
        // reabre a cena inteira do tutorial — o gatilho do dialogo nao tem como saber que o
        // jogador ja aprendeu.
        string feito = S != null ? S.progressoAoTerminar : "PuzzleTutorialDone";
        if (!string.IsNullOrEmpty(feito) && SaveManager.Instance != null)
            SaveManager.Instance.AddProgress(feito);
    }
}
