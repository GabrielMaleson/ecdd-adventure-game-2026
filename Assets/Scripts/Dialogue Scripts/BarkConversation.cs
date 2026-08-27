using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Uma conversa em BARK: dois ou mais personagens se revezando em popups flutuantes,
// enquanto o jogo continua rodando.
//
// E o "AD" do roteiro — a marcacao que o Olavo usa para dizer "isto e bark, nao dialogo".
// A abertura da casa do Elder e exatamente isto:
//
//     Josh: What are we looking for, exactly?
//     Haze: No idea.
//     Josh: Great.
//
// ------------------------------------------------------------------ por que nao e Yarn
//
// O BarkDirector tem um caminho para rodar um no .yarn como bark (<<barkconvo>>), mas ele
// exige um SEGUNDO DialogueRunner na cena com um BarkPresenter, e esse runner nao esta
// montado. Enquanto nao estiver, uma conversa escrita aqui funciona hoje e nao depende de
// nada. Quando o barkRunner existir, os dois caminhos convivem: este para trocas curtas de
// duas ou tres linhas, o Yarn para o que ja vive no roteiro.
//
// ------------------------------------------------------------------ o ritmo
//
// Cada linha espera a ANTERIOR terminar, e nao um tempo fixo: o proprio CharacterDialogue
// ja calcula quanto uma fala fica na tela pelo tamanho dela. Uma frase longa segura mais,
// uma curta passa rapido, e a conversa respira sozinha sem ninguem cronometrar nada.
[DisallowMultipleComponent]
public class BarkConversation : MonoBehaviour
{
    public enum StartMode
    {
        Manual,          // so quando algo chama Play()
        PlayerEnters,    // ao pisar no trigger deste objeto
        PlayerPressesE,  // chega perto e aperta E — moveis examinaveis
        OnStart          // assim que a cena carrega
    }

    [System.Serializable]
    public class Line
    {
        [Tooltip("Bark Id de quem fala: josh, haze, marcus, erika.")]
        public string speakerId;

        [TextArea(1, 3)]
        public string text;

        [Tooltip("Pausa DEPOIS desta fala, alem do tempo que ela fica na tela. Use para " +
                 "dar peso a um silencio — o '...' do Haze pede um respiro maior.")]
        public float gapAfter = 0.35f;

        [Tooltip("Sai AO MESMO TEMPO que a linha de cima, em vez de esperar ela terminar.\n\n" +
                 "Serve para reacao coletiva: dois personagens vendo a mesma coisa e " +
                 "falando por cima um do outro. Em sequencia isso vira revezamento educado " +
                 "e perde o susto.\n\n" +
                 "O grupo inteiro conta como uma fala so: a proxima linha que NAO tem isto " +
                 "marcado espera a mais longa do grupo terminar.")]
        public bool aoMesmoTempoQueAnterior;
    }

    [Header("A conversa")]
    public List<Line> lines = new List<Line>();

    [Header("Quando")]
    public StartMode startMode = StartMode.PlayerEnters;

    [Tooltip("Letra do prompt, no modo PlayerPressesE.")]
    public string interactLabel = "E";

    [Tooltip("Roda uma vez so. Deixe ligado para beat de roteiro.")]
    public bool onceOnly = true;

    [Tooltip("Espera antes da primeira fala. Um beat curto depois do gatilho evita que a " +
             "conversa comece em cima de outra coisa que acabou de acontecer.")]
    public float startDelay = 0.4f;

    [Tooltip("Segura a conversa enquanto QUALQUER outra fala estiver na tela, e comeca no " +
             "instante em que o ar fica livre.\n\n" +
             "O Start Delay sozinho nao resolve isso: ele e um tempo fixo, e nao tem como " +
             "adivinhar quanto falta da fala que ja esta rolando. Sem esta espera, uma " +
             "conversa disparada por gatilho de passagem sai por cima de quem estava " +
             "falando, e o jogador perde as duas.\n\n" +
             "Ela nao adia para sempre: assim que o ultimo balao sai da tela, a conversa " +
             "comeca. Se nada estiver acontecendo, comeca na hora.")]
    public bool esperarFalaEmAndamento = true;

    [Header("Condicoes")]
    [Tooltip("So roda com este progresso gravado (SaveManager). Vazio = sem condicao.")]
    public string requiresProgress = "";

    [Tooltip("Progresso gravado quando a conversa TERMINA. Vazio = nao grava nada.")]
    public string progressOnFinish = "";

    [Header("Ao terminar")]
    public UnityEvent onFinished;

    public string playerTag = "Player";

    private bool played;
    private bool playerInside;
    private Coroutine running;

    // Quem esta conversando AGORA. Conta a CONVERSA, e nao a fala: entre uma linha e a
    // proxima nao ha bark nenhum na tela, e um prompt que volta nesse buraco pisca a cada
    // troca de fala. Enquanto a conversa corre, o E fica fora do caminho.
    //
    // Uma LISTA e nao um contador, de proposito. Contador e um numero sem dono: se alguem
    // esquece de decrementar — objeto destruido no meio da fala, Play interrompido — ele
    // fica preso acima de zero e o E some do jogo INTEIRO, para sempre, sem nada no Console.
    // A lista se limpa sozinha, porque cada entrada pode ser conferida.
    private static readonly List<BarkConversation> runningNow = new List<BarkConversation>();

    public static bool AnyRunning
    {
        get
        {
            for (int i = runningNow.Count - 1; i >= 0; i--)
            {
                BarkConversation c = runningNow[i];
                if (c == null || c.running == null) { runningNow.RemoveAt(i); continue; }
                return true;
            }
            return false;
        }
    }

    // Estatico sobrevive entre sessoes de Play quando o domain reload esta desligado — que
    // e o padrao de "Enter Play Mode" rapido no Unity 6. Sem esta limpeza, parar o Play no
    // meio de uma conversa levava o estado sujo para a proxima sessao.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => runningNow.Clear();

    public bool IsRunning => running != null;

    private void Start()
    {
        if (startMode == StartMode.OnStart) Play();
    }

    // O caso de este objeto ser LIGADO com o jogador ja parado dentro do alcance.
    //
    // OnTriggerEnter2D so dispara na ENTRADA. Mas o uso normal deste componente no modo
    // PressesE e justamente nascer desligado e ser aceso por outra coisa — a chegada dos
    // NPCs num waypoint, o fim de uma caminhada, um puzzle resolvido. Nessa hora o Josh ja
    // esta parado no lugar: ele nunca "entra", porque ja estava dentro quando o gatilho
    // nasceu, e o E simplesmente nao aparece.
    //
    // O InteractAction ja tratava isso no proprio OnEnable, e pelo mesmo motivo. Aqui a
    // chegada e conferida na mao, do mesmo jeito.
    private void OnEnable()
    {
        if (startMode != StartMode.PlayerPressesE) return;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"[BarkConversation] '{name}': modo Player Presses E sem " +
                             "Collider2D — nao ha alcance, o E nunca vai aparecer.", this);
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null) return;

        Collider2D pc = p.GetComponent<Collider2D>();
        bool dentro = pc != null ? col.bounds.Intersects(pc.bounds)
                                 : col.bounds.Contains(p.transform.position);
        if (!dentro) return;

        playerInside = true;
        Offer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (startMode == StartMode.PlayerEnters) { Play(); return; }

        if (startMode == StartMode.PlayerPressesE)
        {
            playerInside = true;
            Offer();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (startMode != StartMode.PlayerPressesE) return;

        playerInside = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // Registra o E direto no InteractButton, sem passar por UnityEvent.
    //
    // Um armario examinavel e um objeto + collider + este componente, e mais nada. Depender
    // de um evento arrastado no Inspector para algo tao comum ja custou caro neste projeto:
    // UnityEvent nao sobrevive a edicao do arquivo da cena, e o campo aparece preenchido na
    // tela e vazio no disco.
    private void Offer()
    {
        if (!playerInside) return;
        if (played && onceOnly) return;

        if (!string.IsNullOrEmpty(requiresProgress) &&
            (SaveManager.Instance == null || !SaveManager.Instance.HasProgress(requiresProgress)))
            return;

        InteractButton.Instance?.SetInteraction(this, interactLabel, OnPressed);
    }

    private void OnPressed()
    {
        Play();

        // Re-oferecer: o InteractButton solta o registro a cada aperto. Sem isto o E so
        // voltaria depois de sair do alcance e entrar de novo.
        if (!onceOnly) Offer();
    }

    // Publico para dar para disparar de um UnityEvent — o fim de uma caminhada, um puzzle
    // resolvido, o onAllArrived de um NpcEntrance.
    public void Play()
    {
        if (played && onceOnly) return;
        if (running != null) return;
        if (lines.Count == 0) return;

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null || !SaveManager.Instance.HasProgress(requiresProgress))
                return;
        }

        played = true;
        if (!runningNow.Contains(this)) runningNow.Add(this);
        running = StartCoroutine(Run());
    }

    public void Stop()
    {
        if (running == null) return;
        StopCoroutine(running);
        running = null;
        runningNow.Remove(this);
    }

    // Permite repetir um beat que ja rodou — reset de puzzle, recarregar save.
    public void Rearm() => played = false;

    // Outra conversa rodando — esta nao conta, senao ela esperaria por si mesma para sempre.
    private bool OutraConversaRodando()
    {
        for (int i = runningNow.Count - 1; i >= 0; i--)
        {
            BarkConversation c = runningNow[i];
            if (c == null) { runningNow.RemoveAt(i); continue; }
            if (c != this) return true;
        }
        return false;
    }

    private IEnumerator Run()
    {
        // Esperar o ar ficar livre ANTES do startDelay, e nao depois: o delay e o respiro
        // desta cena, e ele so faz sentido contado a partir do silencio.
        if (esperarFalaEmAndamento)
            while (BarkDirector.AnyBarkShowing || OutraConversaRodando())
                yield return null;

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // O quanto falta esperar por causa das falas ja disparadas. Nao e aplicado na hora:
        // fica pendente ate aparecer uma linha que precise do palco limpo. E isso que
        // permite um grupo de falas simultaneas contar como uma fala so.
        float esperaPendente = 0f;

        foreach (Line line in lines)
        {
            if (line == null || string.IsNullOrEmpty(line.text)) continue;

            // Linha normal: paga o que ficou pendente do grupo anterior antes de abrir a boca.
            if (!line.aoMesmoTempoQueAnterior && esperaPendente > 0f)
            {
                yield return new WaitForSeconds(esperaPendente);
                esperaPendente = 0f;
            }

            float duration = BarkDirector.Bark(line.speakerId, line.text, BarkPriority.Scripted);

            // Duracao 0 significa que a fala NAO saiu — personagem fora de cena, id errado.
            // Esperar como se tivesse saido deixaria buracos mudos no meio da conversa; o
            // BarkDirector ja gritou no Console dizendo qual foi, entao aqui e so seguir.
            if (duration <= 0f) continue;

            // O grupo simultaneo termina quando a fala MAIS LONGA dele termina. Somar
            // deixaria o silencio depois de duas falas curtas parecer o dobro do que se ve.
            esperaPendente = Mathf.Max(esperaPendente, duration + Mathf.Max(0f, line.gapAfter));
        }

        if (esperaPendente > 0f) yield return new WaitForSeconds(esperaPendente);

        running = null;
        runningNow.Remove(this);

        if (!string.IsNullOrEmpty(progressOnFinish) && SaveManager.Instance != null)
            SaveManager.Instance.AddProgress(progressOnFinish);

        onFinished?.Invoke();
    }

    // Desligar o objeto no meio da conversa deixaria o contador preso em 1 para sempre, e
    // o E nunca mais voltaria. Este e o tipo de vazamento que so aparece uma hora depois.
    private void OnDisable()
    {
        Stop();
        if (playerInside) InteractButton.Instance?.ClearInteraction(this);
        playerInside = false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null || startMode == StartMode.Manual || startMode == StartMode.OnStart) return;

        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
