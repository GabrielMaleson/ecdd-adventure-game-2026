using UnityEngine;

// O AJUSTE do Y-Sort, nao o motor. Quem ordena o mundo e o YSortWorld, sozinho, sem
// componente em objeto nenhum — a maioria esmagadora dos objetos da cena nao tem nem
// precisa ter este componente.
//
// Coloque um YSort so quando o padrao errar, que sao dois casos:
//
//  1. A LINHA DO CHAO ESTA NO LUGAR ERRADO. O padrao mede a base do collider solido, e
//     quando nao ha collider, a base da arte. Isso acerta quase sempre. Erra quando o
//     collider nao e o pe do objeto — uma ponte cujo collider e o corrimao, um portal cujo
//     collider e um gatilho grande, uma arvore com a colisao no meio do tronco. Ai o gizmo
//     amarelo mostra onde o sistema acha que esta o chao, e o Deslocamento arruma.
//
//  2. O OBJETO NAO DEVERIA SER ORDENADO. Marque "Nao ordenar". Nevoa, balao de fala e UI ja
//     saem de fora sozinhos (por nome e por camada) — isto e para o caso solto que escapou.
//
// Mudar qualquer campo aqui so tem efeito na proxima varredura (padrao: ate 2 segundos),
// ou na hora se voce sair e entrar em Play.
[DisallowMultipleComponent]
public class YSort : MonoBehaviour
{
    public enum Ancora
    {
        Automatico,   // base do collider solido; sem collider, base da arte
        Pivo          // a posicao do objeto, crua
    }

    [Header("Onde o objeto toca o chao")]
    [Tooltip("Automatico acerta quase sempre. Use Pivo quando o collider do objeto nao for " +
             "o pe dele — gatilho grande, corrimao, colisao no meio do tronco.")]
    public Ancora ancora = Ancora.Automatico;

    [Tooltip("Empurra a linha do chao para cima ou para baixo, em unidades de mundo. " +
             "Negativo desce. O gizmo amarelo mostra onde ela esta.")]
    public float anchorOffsetY;

    [Header("Excecao")]
    [Tooltip("Tira este objeto do Y-Sort por completo: os Sorting Orders dele ficam como " +
             "estao e ninguem mais escreve neles.")]
    public bool naoOrdenar;

    [Tooltip("So por Y-Sort em objeto SEM collider solido. Por padrao esses ficam de fora, " +
             "porque objeto sem colisao quase sempre e chao — estrada, poca, grama, " +
             "sprite de interior — e chao ordenado por Y passa por cima do personagem.\n\n" +
             "Use so para o caso raro do objeto que fica de PE, o personagem atravessa, mas " +
             "precisa se entrelacar mesmo assim.")]
    public bool incluirSemCollider;

    private void OnDrawGizmosSelected()
    {
        // A linha do chao, calculada do mesmo jeito que o motor calcula. Se ela nao esta nos
        // pes do personagem ou na base do tronco, e o Deslocamento que esta errado — e o
        // resto vai parecer aleatorio ate arrumar.
        float y = transform.position.y + YSortWorld.AnchorOffsetFor(transform, this);
        Vector3 a = new Vector3(transform.position.x, y, 0f);

        Gizmos.color = naoOrdenar ? Color.grey : Color.yellow;
        Gizmos.DrawLine(a + Vector3.left * 0.6f, a + Vector3.right * 0.6f);
        Gizmos.DrawLine(a + Vector3.down * 0.08f, a + Vector3.up * 0.08f);
    }
}
