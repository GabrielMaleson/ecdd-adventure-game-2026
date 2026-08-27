using System.Collections.Generic;
using UnityEngine;

// "Caixa nenhuma passa DAQUI."
//
// Um trigger em cima do vao por onde a caixa escaparia do tabuleiro. Empurrar para dentro
// dele e recusado, e a Haze comenta.
//
// A alternativa seria uma fileira de GridObstacle contornando o puzzle: mais trabalho de
// autoria, refeito toda vez que o formato do tabuleiro muda, e no jogo aparece como parede
// invisivel que ninguem explica. Aqui e um objeto so, tapando a unica saida, e a recusa tem
// voz.
//
// COMO SE MONTA
//   Objeto vazio no vao, com Collider2D marcado IS TRIGGER cobrindo a passagem, e este
//   componente. Nada precisa ser ligado nas caixas — elas perguntam sozinhas.
//
// Sem nenhum CrateBounds na cena, nada muda: o sokoban da cripta e qualquer puzzle antigo
// continuam funcionando sem ninguem desenhar nada para eles.
[DisallowMultipleComponent]
public class CrateBounds : MonoBehaviour
{
    [Header("A area bloqueada")]
    [Tooltip("O collider que marca o vao. Vazio = o primeiro Collider2D deste objeto. " +
             "Arraste sempre, para nao depender de qual vem primeiro num objeto com mais " +
             "de um.")]
    public Collider2D area;

    [Header("Aviso")]
    [Tooltip("No do .yarn tocado quando o jogador tenta empurrar uma caixa para dentro do " +
             "vao. Vazio = recusa em silencio. " +
             "E sempre o MESMO no, entao a escalada da fala mora no roteiro: um " +
             "<<if $crateTries == 0>> ... <<set $crateTries = $crateTries + 1>> dentro dele " +
             "faz a Haze dizer coisas diferentes na primeira, na segunda e nas seguintes, " +
             "sem este componente precisar contar nada.")]
    public string noAoTentarSair = "";

    // Todas as areas ativas. Registro estatico para a caixa nao precisar de referencia
    // nenhuma: ela pergunta ao tipo, e nao a um objeto que alguem teve de arrastar.
    private static readonly List<CrateBounds> ativas = new List<CrateBounds>();

    private void OnEnable()
    {
        if (area == null) area = GetComponent<Collider2D>();

        if (area == null)
        {
            Debug.LogError($"[CrateBounds] '{name}': nao ha area — nem no campo Area, nem um " +
                           "Collider2D neste objeto. Nada vai ser bloqueado.", this);
            return;
        }

        if (!area.isTrigger)
            Debug.LogWarning($"[CrateBounds] '{name}': a area nao esta marcada Is Trigger. " +
                             "Assim ela vira parede de verdade e as caixas esbarram nela em " +
                             "vez de serem recusadas com fala.", this);

        ativas.Add(this);
    }

    private void OnDisable() => ativas.Remove(this);

    // A caixa pode ocupar este ponto? Nao, se ele cair dentro de qualquer vao bloqueado.
    public static bool Permitido(Vector2 ponto)
    {
        for (int i = ativas.Count - 1; i >= 0; i--)
        {
            CrateBounds b = ativas[i];
            if (b == null || b.area == null) { ativas.RemoveAt(i); continue; }
            if (b.area.OverlapPoint(ponto)) return false;
        }

        return true;
    }

    // Qual vao recusou, para a fala dele ser tocada.
    public static CrateBounds QueBloqueia(Vector2 ponto)
    {
        foreach (CrateBounds b in ativas)
        {
            if (b == null || b.area == null) continue;
            if (b.area.OverlapPoint(ponto)) return b;
        }

        return null;
    }

    public void Avisar()
    {
        if (string.IsNullOrEmpty(noAoTentarSair)) return;

        // Pelo mesmo funil de todo dialogo do jogo: e ele que congela o jogador enquanto a
        // fala roda e o destrava no fim.
        DialogueStarter.EvaluateConditionsAndStart(noAoTentarSair, false, null, false);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = area != null ? area : GetComponent<Collider2D>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
