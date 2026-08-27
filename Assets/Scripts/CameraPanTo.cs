using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// "A camera vai ate ali, mostra, e volta."
//
// Serve para o beat em que algo acontece LONGE do jogador e ele precisa ver: a cripta se
// abrindo do outro lado do cemiterio enquanto ele esta parado na chave.
//
// ------------------------------------------------------------------ como ela mexe a camera
//
// Trocando o Tracking Target da CinemachineCamera, que e o mesmo mecanismo que o
// GhostControl usa para alternar entre o Josh e a Haze. Nao cria camera nova, nao mexe em
// prioridade, nao depende de blend do Brain.
//
// A suavidade vem do Damping do CinemachinePositionComposer, e a IDA e a VOLTA usam valores
// diferentes de proposito: ir e o que o jogador precisa acompanhar com os olhos, entao e
// lento; voltar e so devolve-lo ao lugar onde ele ja estava, e arrastar isso e tempo em que
// ele fica sem fazer nada esperando a camera.
//
// ------------------------------------------------------------------ o jogador durante isso
//
// Fica congelado. A camera esta noutro lugar: sair andando as cegas e a receita para ele
// terminar a panoramica em algum canto sem saber como foi parar la.
[DisallowMultipleComponent]
public class CameraPanTo : MonoBehaviour
{
    [Header("Para onde")]
    [Tooltip("O que a camera vai mostrar. Um objeto vazio na cripta serve.")]
    public Transform destino;

    [Tooltip("Segundos parados no destino DEPOIS de a camera chegar. E o tempo de o jogador " +
             "entender o que esta vendo — sem isto a camera vai e volta antes de ele ler a " +
             "imagem.")]
    public float esperaNoDestino = 1.2f;

    [Header("Suavidade")]
    [Tooltip("Damping da IDA. Alto = viagem lenta e cinematografica.")]
    public float dampingIda = 1.2f;

    [Tooltip("Damping da VOLTA. Menor que o da ida de proposito: voltar nao e um plano, e " +
             "so devolver o jogador ao lugar dele.")]
    public float dampingVolta = 0.35f;

    [Tooltip("Damping restaurado no fim — o normal de seguir alguem andando. Deixe igual ao " +
             "que esta no CinemachinePositionComposer da cena.")]
    public float dampingNormal = 0.35f;

    [Tooltip("Duracao da IDA, em segundos. " +
             "E tempo e nao distancia de proposito: o Cinemachine mantem o alvo deslocado na " +
             "tela (composicao, dead zone), entao a camera NUNCA fica em cima dele e uma " +
             "espera por distancia nao termina nunca. Tempo sempre termina.")]
    public float duracaoIda = 1.4f;

    [Tooltip("Duracao da VOLTA, em segundos. Menor que a ida: voltar nao e um plano, e so " +
             "devolver o jogador ao lugar dele.")]
    public float duracaoVolta = 0.6f;

    [Header("Ao voltar")]
    [Tooltip("Conversa em bark tocada quando a camera termina de voltar. Ponha o " +
             "BarkConversation em Start Mode = Manual.")]
    public BarkConversation conversaAoVoltar;

    [Tooltip("Congela o jogador durante a panoramica.")]
    public bool congelarJogador = true;

    private CinemachineCamera cam;
    private CinemachinePositionComposer composer;
    private bool rodando;

    // Publico e sem parametro para tambem dar para chamar de um UnityEvent no Inspector.
    public void Executar()
    {
        // Log sempre: sem ele nao da para distinguir "a panoramica travou no meio" de "a
        // panoramica nunca foi chamada", que sao problemas em lugares opostos do wiring.
        Debug.Log($"[CameraPanTo] '{name}': Executar() chamado" +
                  (rodando ? " — IGNORADO, ja esta rodando." : "."), this);

        if (rodando) return;
        StartCoroutine(Rodar());
    }

    private IEnumerator Rodar()
    {
        rodando = true;

        PlayerController pc = null;

        // TUDO dentro de try/finally: o descongelamento e a restauracao do damping tem de
        // acontecer mesmo se algo der errado no meio. Uma panoramica que morre pela metade
        // deixaria o jogador congelado para sempre, sem nada na tela explicando.
        try
        {
            if (cam == null) cam = FindFirstObjectByType<CinemachineCamera>();
            if (cam == null)
            {
                Debug.LogWarning($"[CameraPanTo] '{name}': nao ha CinemachineCamera na cena.", this);
                yield break;
            }

            if (composer == null) composer = cam.GetComponent<CinemachinePositionComposer>();

            if (destino == null)
            {
                Debug.LogWarning($"[CameraPanTo] '{name}': campo Destino vazio — nao ha para " +
                                 "onde ir.", this);
                yield break;
            }

            Transform voltarPara = cam.Target.TrackingTarget;

            if (congelarJogador)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                pc = p != null ? p.GetComponent<PlayerController>() : null;
                if (pc != null) pc.InputEnabled = false;
            }

            SetDamping(dampingIda);
            cam.Target.TrackingTarget = destino;
            yield return new WaitForSeconds(Mathf.Max(0f, duracaoIda));

            if (esperaNoDestino > 0f) yield return new WaitForSeconds(esperaNoDestino);

            SetDamping(dampingVolta);
            cam.Target.TrackingTarget = voltarPara;
            yield return new WaitForSeconds(Mathf.Max(0f, duracaoVolta));
        }
        finally
        {
            SetDamping(dampingNormal);
            if (pc != null) pc.InputEnabled = true;
            rodando = false;
        }

        if (conversaAoVoltar != null) conversaAoVoltar.Play();
    }

    private void SetDamping(float v)
    {
        if (composer == null) return;
        composer.Damping = new Vector3(v, v, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (destino == null) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destino.position);
        Gizmos.DrawWireSphere(destino.position, 0.5f);
    }
}
