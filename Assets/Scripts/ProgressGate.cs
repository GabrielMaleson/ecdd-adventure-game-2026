using UnityEngine;

// "Ninguem passa daqui antes de X."
//
// Uma parede que existe enquanto o jogador nao tem um progresso, e some quando ele tem.
// Feita para a entrada da floresta: sem ter lido as notas do Elder, o Josh nao tem por que
// ir para la, e sair andando adianta o jogo por cima do beat que devia manda-lo.
//
// COMO SE MONTA
//   Objeto vazio atravessado no caminho, com um Collider2D NORMAL (Is Trigger DESLIGADO)
//   cobrindo a passagem, e este componente. Preencha Progresso Que Libera.
//
// Nao ha nada a ligar do outro lado: quem grava o progresso e o <<progress>> do .yarn ou o
// campo Progresso de quem ja faz isso. A parede pergunta sozinha, quadro a quadro, e a
// pergunta e um Contains num HashSet — de graca para o punhado de paredes que um jogo destes
// tem.
//
// SIMETRICA de proposito: se o progresso for removido a parede volta. Isso e o que faz o
// F1/F2/F3 do DebugSceneJump continuarem honestos, e evita o estado em que a parede sumiu
// numa sessao e nunca mais volta.
[DisallowMultipleComponent]
public class ProgressGate : MonoBehaviour
{
    [Header("Quando abrir")]
    [Tooltip("Nome do progresso que faz a parede sumir. Tem de bater LETRA POR LETRA com o " +
             "que e gravado no <<progress ...>> do .yarn. Ex: ReadElderNotes")]
    public string progressoQueLibera = "";

    [Header("O que sumir")]
    [Tooltip("O que e desligado quando o progresso chega. Vazio = os Collider2D deste " +
             "objeto. Preencha se a parede tiver arte junto e tudo tiver de sumir.")]
    public GameObject[] some;

    [Header("Aviso (opcional)")]
    [Tooltip("No do .yarn tocado quando o jogador esbarra na parede fechada. Vazio = ela so " +
             "bloqueia, em silencio. Sem isto o jogador esbarra numa parede invisivel que " +
             "ninguem explica.")]
    public string noAoEsbarrar = "";

    [Tooltip("Segundos de espera antes de a fala poder repetir, para ela nao disparar em " +
             "rajada enquanto ele fica encostado empurrando.")]
    public float esperaEntreAvisos = 6f;

    private bool aberta;
    private bool jaAplicou;
    private float proximoAviso;

    private void Start() => Aplicar(Liberado(), true);

    private void Update()
    {
        bool livre = Liberado();
        if (livre != aberta) Aplicar(livre, false);
    }

    private bool Liberado()
    {
        if (string.IsNullOrEmpty(progressoQueLibera)) return false;
        return SaveManager.Instance != null && SaveManager.Instance.HasProgress(progressoQueLibera);
    }

    private void Aplicar(bool livre, bool inicio)
    {
        aberta = livre;

        if (some != null && some.Length > 0)
        {
            foreach (GameObject go in some)
                if (go != null) go.SetActive(!livre);
        }
        else
        {
            foreach (Collider2D c in GetComponents<Collider2D>())
                c.enabled = !livre;
        }

        if (!inicio && livre && !jaAplicou)
        {
            jaAplicou = true;
            Debug.Log($"[ProgressGate] '{name}': '{progressoQueLibera}' chegou, passagem " +
                      "liberada.", this);
        }
    }

    private void OnCollisionEnter2D(Collision2D col) => Esbarrou(col.collider);
    private void OnCollisionStay2D(Collision2D col) => Esbarrou(col.collider);

    private void Esbarrou(Collider2D quem)
    {
        if (aberta) return;
        if (string.IsNullOrEmpty(noAoEsbarrar)) return;
        if (Time.time < proximoAviso) return;
        if (quem == null || !quem.CompareTag("Player")) return;

        proximoAviso = Time.time + esperaEntreAvisos;

        // Pelo mesmo funil de todo dialogo do jogo: e ele que congela o jogador enquanto a
        // fala roda e o destrava no fim.
        DialogueStarter.EvaluateConditionsAndStart(noAoEsbarrar, false, null, false);
    }

    private void OnDrawGizmos()
    {
        Collider2D[] cs = GetComponents<Collider2D>();
        if (cs.Length == 0) return;

        Gizmos.color = aberta ? new Color(0.3f, 1f, 0.4f, 0.5f)
                              : new Color(1f, 0.45f, 0.2f, 0.9f);
        foreach (Collider2D c in cs)
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
