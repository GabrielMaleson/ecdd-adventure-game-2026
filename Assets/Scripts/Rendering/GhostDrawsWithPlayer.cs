using UnityEngine;

// "A Haze desenha junto com o Josh, sempre."
//
// ------------------------------------------------------------------ por que ela e excecao
//
// O Y-Sort ordena todo mundo pela linha do chao, e isso funciona para quem TEM chao: o
// personagem tem collider solido, a arvore tem o pe da sombra, a casa tem a base da parede.
//
// A Haze nao tem nenhum dos dois. Ela e um fantasma: atravessa tudo, so tem collider de
// gatilho, e ainda FLUTUA acima do Josh — o FragmentFollow a coloca 0,87 acima dele, e
// inverte esse sinal conforme ele sobe ou desce. Qualquer linha do chao que se invente para
// ela e um numero derivado de outro numero: da base da arte (que muda a cada quadro da
// animacao), ou do pivo mais um ajuste que depende do offset do follow continuar o mesmo.
// Cada tentativa acerta um caso e erra outro.
//
// A regra certa nao e geometrica, e narrativa: ela acompanha o Josh. Onde ele passa na
// frente, ela passa na frente. Entao a ordem dela nao e calculada — e COPIADA da dele, um
// degrau a frente.
//
// ------------------------------------------------------------------ o que isto NAO faz
//
// Nao mexe em posicao, nem em GhostControl, nem em cutscene, nem liga ou desliga nada. A
// unica coisa que escreve sao o Sorting Order e a Sorting Layer dos proprios renderers
// dela. Se o Josh nao estiver na cena, nao escreve nada e some do caminho.
//
// Para o Y-Sort nao brigar por esses mesmos campos, o YSort da Haze fica em "Nao ordenar".
[DisallowMultipleComponent]
public class GhostDrawsWithPlayer : MonoBehaviour
{
    [Tooltip("Tag de quem ela acompanha.")]
    public string playerTag = "Player";

    [Tooltip("Quantos degraus a FRENTE do jogador ela desenha. 1 = logo na frente dele.")]
    public int degrausAFrente = 1;

    private SpriteRenderer[] meus;
    private SpriteRenderer[] doJogador;
    private Transform jogador;

    private void Awake() => Recolher();

    private void OnEnable()
    {
        // A cena pode ter recarregado com outro Player: a referencia velha vira nula e
        // precisa ser procurada de novo.
        jogador = null;
        doJogador = null;
        if (meus == null || meus.Length == 0) Recolher();
    }

    private void Recolher()
    {
        SpriteRenderer[] todos = GetComponentsInChildren<SpriteRenderer>(true);

        int n = 0;
        foreach (SpriteRenderer r in todos)
            if (!YSortWorld.ShouldSkip(r)) n++;

        meus = new SpriteRenderer[n];
        n = 0;
        foreach (SpriteRenderer r in todos)
            if (!YSortWorld.ShouldSkip(r)) meus[n++] = r;
    }

    // LateUpdate, como o Y-Sort: depois de tudo ter se mexido e o jogador ja ter a ordem
    // dele deste quadro. Antes disso, ela copiaria a ordem do quadro passado e ficaria um
    // quadro atrasada toda vez que ele andasse.
    private void LateUpdate()
    {
        if (meus == null || meus.Length == 0) return;

        if (jogador == null || doJogador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p == null) return;

            jogador = p.transform;
            doJogador = p.GetComponentsInChildren<SpriteRenderer>(true);
        }

        int maior = int.MinValue;
        int camada = 0;

        foreach (SpriteRenderer r in doJogador)
        {
            if (r == null || YSortWorld.ShouldSkip(r)) continue;
            if (r.sortingOrder > maior) { maior = r.sortingOrder; camada = r.sortingLayerID; }
        }

        // Jogador sem nenhum sprite utilizavel: nao ha o que copiar, e chutar um numero
        // seria voltar exatamente ao problema que isto existe para resolver.
        if (maior == int.MinValue) return;

        for (int i = 0; i < meus.Length; i++)
        {
            SpriteRenderer r = meus[i];
            if (r == null) continue;

            // A camada primeiro: ela manda MAIS que a ordem. Estar na camada errada nao e
            // ficar alguns lugares atras, e sumir atras de tudo que esta na camada de cima.
            if (r.sortingLayerID != camada) r.sortingLayerID = camada;

            // O "+ i" preserva a ordem relativa entre os sprites dela, se um dia ela tiver
            // mais de um — a mesma ideia de GRUPO que o Y-Sort usa.
            int querido = maior + degrausAFrente + i;
            if (r.sortingOrder != querido) r.sortingOrder = querido;
        }
    }
}
