using UnityEngine;

// Um asset so decide como o jogo inteiro resolve "quem esta na frente de quem".
// Vive em Assets/Resources/YSortSettings.asset e e achado por Resources.Load, entao nada
// precisa ser arrastado para campo nenhum.
//
// POR QUE ISTO EXISTE
// Hoje a ordem de desenho e digitada na mao: centenas de valores de Sorting Order
// espalhados pela cena. Isso nao e so trabalhoso — e IMPOSSIVEL de acertar, porque quem
// esta na frente muda conforme o jogador anda. Uma arvore precisa cobrir o Josh quando ele
// passa atras dela e ser coberta quando ele passa na frente, e um numero fixo so consegue
// uma das duas.
//
// A REGRA, em uma linha: quem tem o Y menor (esta mais para baixo na tela) desenha na
// frente. Sorting Order = -Y * Precision.
//
// Quem faz a conta acontecer e o YSortWorld. Aqui so ficam os numeros.
[CreateAssetMenu(fileName = "YSortSettings", menuName = "ECDD/Y-Sort Settings")]
public class YSortSettings : ScriptableObject
{
    [Header("Precisao")]
    [Tooltip("Quantos degraus de Sorting Order valem UMA unidade de mundo. 100 = 1 pixel " +
             "de diferenca ja separa dois objetos (a arte e 100 pixels por unidade).\n\n" +
             "Nao aumente sem necessidade: Sorting Order e um inteiro de 16 bits, entao " +
             "Precision x (maior Y do mapa) tem que caber em 32767.")]
    public int precision = 100;

    [Tooltip("Somado a TODA ordem calculada, para o resultado ficar num intervalo positivo.\n\n" +
             "Isto conserta um problema real: a conta crua e -Y * Precision, entao dentro de " +
             "uma casa em Y=137 o personagem recebe -13700, enquanto o chao e o tapete tem " +
             "ordens perto de zero (ou valores antigos como -11963). Numero menor desenha " +
             "atras — o personagem sumia debaixo do proprio chao da casa.\n\n" +
             "Com o deslocamento, tudo que e ordenado por Y sobe para uma faixa positiva e " +
             "passa a ficar SEMPRE acima de qualquer arte de chao, que e o que se quer. " +
             "A ordem relativa entre os objetos ordenados nao muda.")]
    public int orderBase = 20000;


    [Tooltip("Altura maxima, em unidades, de uma coisa que conta como UM objeto ordenavel.\n\n" +
             "Existe por causa de um erro real: o chao da casa do Elder e um sprite, e o " +
             "agrupamento subia dos moveis ate ele — tapete, escada, sofa, estante e mesa " +
             "viravam UM objeto so, com uma ordem so, desenhada por cima dos personagens.\n\n" +
             "Arvore, movel e personagem tem poucos metros. Um comodo tem dezenas: voce anda " +
             "DENTRO dele, nao atras dele. Acima desta altura o objeto para de ser tratado " +
             "como unidade — ele nao e ordenado, e os filhos passam a ser ordenados cada um " +
             "por si, que e o que se quer.")]
    public float maxObjectHeight = 6f;

    [Header("Camada")]
    [Tooltip("Ligado: todo objeto ordenado por Y e movido para a camada abaixo.\n\n" +
             "Isto e o que faz o sistema funcionar de verdade. Sorting Layer manda MAIS que " +
             "Sorting Order: hoje o Josh esta em 'Objects', o Marcus e a Erika em 'NPCs' e " +
             "as arvores em 'Default', e por isso um personagem nunca consegue passar atras " +
             "de uma arvore por mais que o Y diga que deveria. Tudo que precisa se " +
             "entrelacar tem que estar na MESMA camada.")]
    public bool forceSortingLayer = true;

    [Tooltip("A camada em que o mundo inteiro e ordenado por Y. Os Tilemaps (chao, penhasco) " +
             "ficam em Default, abaixo desta.")]
    public string sortingLayerName = "Objects";

    [Header("Varredura")]
    [Tooltip("De quantos em quantos segundos o sistema procura objetos novos (NPC que " +
             "aparece, item instanciado). 0 desliga: so varre ao carregar a cena.\n\n" +
             "Nao confunda com a ordenacao em si, que roda todo quadro e e barata.")]
    public float rescanInterval = 2f;

    [Header("Editor")]
    [Tooltip("Ligado: a Scene View e o Game View mostram a ordem certa sem entrar em Play.\n\n" +
             "O preco: o Unity passa a considerar os Sorting Orders calculados como " +
             "alteracao da cena, entao salvar a cena grava esses numeros no arquivo. Nao " +
             "quebra nada (o sistema recalcula tudo do zero a cada quadro), mas engorda o " +
             "diff. Desligue se estiver atrapalhando o trabalho junto com o Gabriel.")]
    public bool previewInEditor = true;

    [Header("Seguranca")]
    [Tooltip("Avisa uma vez no Console se algum objeto passar do limite de 16 bits — o " +
             "sintoma seria um objeto la longe desenhando na frente de tudo, do nada.")]
    public bool warnOnOverflow = true;

    private const string ResourcePath = "YSortSettings";
    private const int OrderLimit = 32000;   // folga ate o teto real de 32767

    private static YSortSettings cached;
    private static bool lookedUp;

    // Null quando o asset nao existe, e todo mundo que le isto trata null — apagar o
    // arquivo devolve o jogo ao comportamento manual, sem erro e sem mexer em codigo.
    public static YSortSettings Current
    {
        get
        {
            if (!lookedUp)
            {
                cached = Resources.Load<YSortSettings>(ResourcePath);
                lookedUp = true;
            }
            return cached;
        }
    }

    private static bool warned;

    // A conta, em um lugar so, para o motor e a ferramenta de editor nunca divergirem.
    public static int OrderFor(float worldY)
    {
        YSortSettings s = Current;
        int p = s != null ? Mathf.Max(1, s.precision) : 100;

        int b = s != null ? s.orderBase : 20000;
        int order = b + Mathf.RoundToInt(-worldY * p);

        if (Mathf.Abs(order) > OrderLimit)
        {
            if (!warned && (s == null || s.warnOnOverflow))
            {
                warned = true;
                Debug.LogWarning(
                    $"Y-Sort: Y={worldY:F1} com Precision={p} da Sorting Order {order}, fora do " +
                    $"limite de 16 bits. Baixe o Precision ou traga o mapa para mais perto de Y=0. " +
                    "Sem isso, objetos muito longe da origem desenham na ordem errada.");
            }

            order = Mathf.Clamp(order, -OrderLimit, OrderLimit);
        }

        return order;
    }


    public static int LayerId
    {
        get
        {
            YSortSettings s = Current;
            if (s == null || !s.forceSortingLayer) return 0;
            return SortingLayer.NameToID(s.sortingLayerName);
        }
    }

    // Sem o asset, o Y-Sort inteiro fica desligado e o jogo volta a desenhar pelos Sorting
    // Orders escritos na mao. E o interruptor geral: apagar o arquivo desfaz o sistema.
    public static bool SystemEnabled => Current != null;

    // Nome de camada errado devolveria 0, que e Default — a MESMA camada dos Tilemaps, onde
    // metade dos objetos ficaria com Sorting Order negativo e sumiria debaixo do chao. Se a
    // camada nao existe, e mais seguro nao mover ninguem.
    public static float MaxObjectHeight
    {
        get
        {
            YSortSettings s = Current;
            return s != null && s.maxObjectHeight > 0f ? s.maxObjectHeight : float.MaxValue;
        }
    }

    public static bool ForcesLayer
    {
        get
        {
            YSortSettings s = Current;
            if (s == null || !s.forceSortingLayer) return false;

            foreach (SortingLayer l in SortingLayer.layers)
                if (l.name == s.sortingLayerName) return true;

            return false;
        }
    }

    public static float RescanInterval
    {
        get
        {
            YSortSettings s = Current;
            return s != null ? Mathf.Max(0f, s.rescanInterval) : 2f;
        }
    }

    public static bool PreviewInEditor => Current == null || Current.previewInEditor;

#if UNITY_EDITOR
    private void OnValidate()
    {
        cached = this;
        lookedUp = true;
        warned = false;

        if (!string.IsNullOrEmpty(sortingLayerName) &&
            SortingLayer.NameToID(sortingLayerName) == 0 && sortingLayerName != "Default")
        {
            Debug.LogWarning($"Y-Sort: nao existe Sorting Layer chamada '{sortingLayerName}'. " +
                             "Crie em Project Settings > Tags and Layers, ou use uma existente.", this);
        }

        // Mudar Precision ou a camada muda TODO objeto, e as entradas guardam a ordem
        // relativa lida na varredura anterior. Jogar tudo fora e remontar e a unica leitura
        // honesta.
        YSortWorld.Clear();
    }
#endif
}
