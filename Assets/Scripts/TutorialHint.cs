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
// O InteractButton resolveu o mesmo problema pelo caminho oposto (move um rotulo que ja
// existe na Canvas). Os dois funcionam; este serve melhor aqui porque a faixa nao existe
// em lugar nenhum ainda e teria de ser montada a mao.
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
        label.fontSize = 46;
        label.color = Color.white;
        label.raycastTarget = false;

        // Contorno preto: a faixa aparece em cima do cenario, e cenario escuro com texto
        // escuro ou claro com texto claro sao os dois igualmente ilegiveis.
        label.fontMaterial.EnableKeyword("OUTLINE_ON");
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(0, 0, 0, 255);

        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta = new Vector2(1600f, 80f);

        // A fonte sai do mesmo asset que o resto do texto do jogo, quando ele existe.
        TextStyle.Apply(label, TextStyle.Role.InteractPrompt);
        label.fontSize = 46;

        label.enabled = false;
    }

    // ------------------------------------------------------------------ API

    public static void Mostrar(string texto)
    {
        TutorialHint h = Ensure();
        h.label.text = texto;
        h.label.enabled = !string.IsNullOrEmpty(texto);
    }

    public static void Esconder()
    {
        if (Instance == null || Instance.label == null) return;
        Instance.label.text = string.Empty;
        Instance.label.enabled = false;
    }

    // Chamavel de UnityEvent (um StatueSwitch, um trigger) sem passar por comando de yarn.
    public void MostrarTexto(string texto) => Mostrar(texto);
    public void EsconderTexto() => Esconder();

    // ------------------------------------------------------------------ yarn

    [YarnCommand("hint")]
    public static void Hint(string texto) => Mostrar(texto);

    [YarnCommand("hintoff")]
    public static void HintOff() => Esconder();

    [Tooltip("Textos das tres dicas. Ficam aqui e nao no .yarn porque sao instrucao de " +
             "sistema, nao fala de personagem.")]
    public static string TextoQ = "Press Q to control Haze";
    public static string TextoQVolta = "Press Q again to come back";

    // <<ghosttutorial>> — PARA o dialogo e conduz o jogador pela mecanica.
    //
    // A sequencia inteira mora aqui, e nao espalhada em gatilhos da cena, porque ela e uma
    // coisa so: um tutorial tem ordem, e ordem quebrada em cinco objetos diferentes e
    // impossivel de ler depois. Cada passo espera um FATO do jogo, nunca um tempo fixo:
    //
    //   1. mostra "aperte Q"        -> espera o jogador de fato assumir a Haze
    //   2. some com a faixa         -> a dica da estatua vem do proprio StatueSwitch,
    //                                  quando a Haze chega perto dela
    //   3. espera a estatua girar   -> mostra "aperte Q de novo"
    //   4. devolve o controle ao roteiro, e a conversa continua
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

        Mostrar(TextoQ);
        while (GhostControl.Instance != null &&
               GhostControl.Instance.State != GhostControl.Mode.Piloting)
            yield return null;

        Esconder();

        // A estatua girou: e o "puzzle se mexeu pela primeira vez" do roteiro.
        while (StatueSwitch.AtivacoesTotais == ativacoesNoInicio)
            yield return null;

        Mostrar(TextoQVolta);

        // Some sozinha quando ele obedece, mas sem travar o dialogo: daqui em diante a
        // conversa ja pode rolar.
        Ensure().StartCoroutine(EsconderQuandoVoltar());
    }

    private static IEnumerator EsconderQuandoVoltar()
    {
        while (GhostControl.Instance != null &&
               GhostControl.Instance.State == GhostControl.Mode.Piloting)
            yield return null;

        Esconder();
    }
}
