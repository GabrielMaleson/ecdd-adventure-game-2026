using TMPro;
using UnityEngine;

// Os ajustes da faixa de tutorial, num asset em vez de campos num componente.
//
// A faixa nao tem objeto na cena de proposito — ela se cria em runtime, porque UI escrita
// no arquivo da cena por fora some no instante em que o editor salva. So que isso tirou o
// Inspector junto, e sem Inspector nao ha como acertar tamanho, altura e cor por olho.
//
// Este asset devolve o Inspector sem devolver o objeto de cena. E o mesmo padrao do
// TextStyle.asset e do YSortSettings.asset: fica em Assets/Resources, e achado por
// Resources.Load, sem nada arrastado em campo nenhum.
//
// Se o asset nao existir, a faixa continua funcionando com os padroes daqui.
[CreateAssetMenu(fileName = "TutorialHintSettings", menuName = "Settings/Tutorial Hint")]
public class TutorialHintSettings : ScriptableObject
{
    [Header("Aparencia")]
    [Tooltip("Tamanho da letra, em pixels de tela. A faixa e instrucao de sistema, entao " +
             "ela pode e deve ser maior que a fala dos personagens.")]
    public float fontSize = 46f;

    [Tooltip("Fonte da faixa. Vazio = usa a mesma do prompt de interacao (TextStyle).")]
    public TMP_FontAsset font;

    public Color cor = Color.white;

    [Header("Contorno")]
    [Tooltip("A faixa aparece por cima do cenario. Sem contorno, ela some em fundo claro " +
             "ou em fundo escuro, dependendo da cor — nunca fica legivel nos dois.")]
    [Range(0f, 1f)]
    public float espessuraDoContorno = 0.2f;

    public Color corDoContorno = Color.black;

    [Header("Posicao")]
    [Tooltip("Distancia do topo da tela, em pixels. Aumente se ela estiver batendo em " +
             "alguma outra UI.")]
    public float distanciaDoTopo = 60f;

    [Tooltip("Deslocamento horizontal a partir do centro. 0 = centralizada.")]
    public float deslocamentoX = 0f;

    [Header("Textos")]
    [Tooltip("Ficam aqui e nao no .yarn porque sao instrucao de sistema, nao fala de " +
             "personagem — e o .yarn e do roteirista.")]
    public string textoAssumirHaze = "Press Q to control Haze and get to the statue";

    [Tooltip("Mostrado quando a Haze chega ao alcance da estatua. O Q aqui a DEIXA ali " +
             "parada e devolve camera e controle ao Josh — e por isso o texto fala em " +
             "deixar, e nao em largar ou sair: ela fica de proposito, esperando ser usada.")]
    public string textoEstacionarHaze = "Press Q to leave Haze in position";

    public string textoVoltarAoJosh = "Press Q again for Haze to come back";

    [Tooltip("Dica mostrada perto da estatua. O StatueSwitch tem um campo proprio que, se " +
             "preenchido, manda mais que este — util quando uma estatua especifica precisa " +
             "de outra instrucao.")]
    public string textoUsarEstatua = "Press E to interact with the statue";

    [Tooltip("Ultima dica do tutorial. Vazio = nao mostra.")]
    public string textoDesfazer = "Press Z if you need to undo your last move";

    [Tooltip("Teto de seguranca da dica do Z, em segundos. Ela sai quando o jogador aperta " +
             "Z — este numero so existe para ela nao ficar na tela para sempre se ele nunca " +
             "apertar. " +
             "Deixe FOLGADO. Desfazer e a unica tecla do tutorial que nao tem uso imediato: " +
             "ele so vai querer usar quando errar, e ate errar pode levar um bom tempo. 25 " +
             "da tempo de ele voltar ao inicio do puzzle e experimentar. 0 = sem teto, so " +
             "sai com o Z.")]
    public float segundosDaDicaDoZ = 25f;

    [Header("Puzzle")]
    [Tooltip("No do .yarn tocado na PRIMEIRA vez que qualquer estatua do jogo for recusada " +
             "por ter algo no caminho — a fala da Haze explicando a regra. Vazio = nao toca " +
             "nada. " +
             "Preencher AQUI e nao em cada estatua e o ponto: a licao e do jogo, e uma " +
             "estatua nova criada daqui a um mes ja nasce sabendo dela, sem ninguem lembrar " +
             "de configurar. Uma estatua pode sobrescrever no campo proprio dela quando " +
             "precisar de outra fala.")]
    public string noDaPrimeiraRecusaDoPuzzle = "";

    [Tooltip("Progresso gravado quando o <<ghosttutorial>> termina. Vazio = nao grava nada. " +
             "E o que permite o gatilho do dialogo do puzzle NAO tocar de novo num pulo de " +
             "teste: no DialogueStarter dele, marque Has Conditions e Conditions Cancel e " +
             "ponha este mesmo nome. Assim a cena so roda quando o tutorial ainda nao foi " +
             "feito.")]
    public string progressoAoTerminar = "PuzzleTutorialDone";

    [Header("Transicao")]
    [Tooltip("Segundos para a faixa surgir e sumir. 0 = aparece e some seco.")]
    public float fade = 0.15f;

    // ------------------------------------------------------------------ acesso

    private const string ResourcePath = "TutorialHintSettings";

    private static TutorialHintSettings cached;
    private static bool lookedUp;

    public static TutorialHintSettings Current
    {
        get
        {
            if (!lookedUp)
            {
                cached = Resources.Load<TutorialHintSettings>(ResourcePath);
                lookedUp = true;
            }
            return cached;
        }
    }

    // O editor recarrega assemblies o tempo todo; sem isto o asset editado so passa a valer
    // depois de sair e voltar do Play.
    private void OnValidate()
    {
        cached = this;
        lookedUp = true;
        if (Application.isPlaying) TutorialHint.Reaplicar();
    }
}
