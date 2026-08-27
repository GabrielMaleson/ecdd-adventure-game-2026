using System.Collections.Generic;
using UnityEngine;

// "Esta arvore fica translucida quando o jogador passa atras dela."
//
// OPT-IN, um por objeto. Nao existe varredura, nao existe lista global, nada acontece numa
// arvore que nao tenha este componente. E de proposito: enquanto for so um punhado de
// arvores escolhidas a mao, o custo de ligar uma e arrastar um componente, e o de desligar
// e apagar ele. Se um dia virar regra para o cenario todo, ai sim vale um sistema como o
// Y-Sort, que varre sozinho.
//
// O QUE ELE NAO FAZ, E POR QUE
// Nao mexe em sorting order. O exemplar deste efeito que veio no ForestPixelLand
// (Scripts/Nature/Fader.cs) escreve `interactor.SpriteRenderer.sortingOrder` para empurrar
// o personagem para a frente da arvore. Aqui isso seria uma briga perdida: o YSortWorld
// recalcula a ordem de desenho de todo mundo a cada quadro, e ganharia. E nem precisa —
// quem desenha na frente ja esta resolvido. O que falta e so poder VER atraves da copa,
// que e alpha.
//
// COMO SE MONTA
//   1. Este componente na raiz da arvore (o pai da sombra, do tronco e da copa).
//   2. Um Collider2D A MAIS nessa mesma raiz, marcado IS TRIGGER, do tamanho que voce
//      quiser que seja a area de aproximacao. Pode ser bem maior que a arvore.
//
// O collider solido do tronco continua onde esta e nao e tocado. Os dois convivem: o
// YSortWorld ignora trigger tanto para decidir quem entra no sistema quanto para medir a
// ancora, entao a linha do chao da arvore continua saindo do tronco, e nao do gatilho.
[DisallowMultipleComponent]
public class FadeWhenBehind : MonoBehaviour
{
    [Header("Quanto")]
    [Tooltip("Opacidade com o jogador dentro da area, como FRACAO da opacidade autorada. " +
             "0.35-0.45 deixa ver o personagem e ainda le como arvore; 0 some por completo.")]
    [Range(0f, 1f)]
    public float fadedAlpha = 0.4f;

    [Tooltip("Velocidade da transicao, em opacidade por segundo. 3 leva ~0.2s para ir e " +
             "voltar; valores altos ficam duros, baixos ficam preguicosos.")]
    public float fadeSpeed = 3f;

    [Header("Quando")]
    [Tooltip("So apaga se o jogador estiver ATRAS da arvore — ou seja, mais para cima na " +
             "tela que o pe dela, que e a mesma regra que o Y-Sort usa para decidir quem " +
             "desenha na frente.\n\n" +
             "LIGADO e quase sempre o certo: passando na FRENTE da arvore ele nao esta " +
             "escondido por nada, e apagar a copa nesse caso so faz a arvore piscar sem " +
             "motivo. Desligue se quiser que ela reaja a aproximacao venha de onde vier.")]
    public bool onlyWhenBehind = true;

    public string playerTag = "Player";

    [Header("Quem apaga")]
    [Tooltip("Raiz dos sprites que somem. Vazio = este objeto. Todos os SpriteRenderer " +
             "abaixo dela entram.")]
    public Transform visualRoot;

    [Tooltip("Sprites que NAO devem apagar, mesmo estando abaixo da raiz. O caso tipico e " +
             "a sombra no chao: ela nao esconde ninguem, e ve-la sumir entrega o truque.")]
    public SpriteRenderer[] naoApagar;

    // A opacidade que cada sprite tinha na cena, guardada uma vez. O alvo e SEMPRE relativo
    // a ela, nunca 1: uma copa autorada a 0.8 tem de voltar para 0.8, e nao ganhar 20% de
    // opacidade na primeira vez que o jogador passa por perto.
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<float> authoredAlpha = new List<float>();

    private Transform player;
    private bool playerInArea;

    private void Start()
    {
        Transform root = visualRoot != null ? visualRoot : transform;

        var excluded = new HashSet<SpriteRenderer>();
        if (naoApagar != null)
            foreach (SpriteRenderer r in naoApagar)
                if (r != null) excluded.Add(r);

        foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (excluded.Contains(r)) continue;

            renderers.Add(r);
            authoredAlpha.Add(r.color.a);
        }

        if (renderers.Count == 0)
            Debug.LogWarning($"[FadeWhenBehind] '{name}': nao ha SpriteRenderer nenhum abaixo " +
                             "da raiz — nao ha o que apagar.", this);

        bool temGatilho = false;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
            if (c.isTrigger) { temGatilho = true; break; }

        if (!temGatilho)
            Debug.LogWarning($"[FadeWhenBehind] '{name}': nao ha nenhum Collider2D marcado " +
                             "IS TRIGGER aqui. O collider solido do tronco nao serve — ele e " +
                             "parede, nao area. Adicione um segundo collider, marcado como " +
                             "trigger, do tamanho da area de aproximacao.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag)) return;

        playerInArea = true;
        player = other.transform;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag)) return;

        playerInArea = false;
    }

    private void Update()
    {
        bool esconder = playerInArea && (!onlyWhenBehind || PlayerIsBehind());

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer r = renderers[i];
            if (r == null) continue;

            float alvo = esconder ? authoredAlpha[i] * fadedAlpha : authoredAlpha[i];

            Color c = r.color;
            if (Mathf.Approximately(c.a, alvo)) continue;

            c.a = Mathf.MoveTowards(c.a, alvo, fadeSpeed * Time.deltaTime);
            r.color = c;
        }
    }

    // Mesma definicao de "atras" que o Y-Sort usa, para as duas coisas nao discordarem: o
    // pe da arvore e a base do collider SOLIDO, e nao o pivo nem o centro da arte. Uma
    // copa de tres metros medida pelo meio poria a linha divisoria no ar.
    private bool PlayerIsBehind()
    {
        if (player == null) return true;

        float pe = transform.position.y;

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
        {
            if (c.isTrigger || !c.isActiveAndEnabled) continue;
            pe = c.bounds.min.y;
            break;
        }

        return player.position.y > pe;
    }

    // Desligar a arvore com o jogador dentro deixaria a copa apagada para sempre: o
    // OnTriggerExit nunca chega, e no proximo enable ela comeca meio transparente sem
    // ninguem por perto.
    private void OnDisable()
    {
        playerInArea = false;

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;

            Color c = renderers[i].color;
            c.a = authoredAlpha[i];
            renderers[i].color = c;
        }
    }
}
