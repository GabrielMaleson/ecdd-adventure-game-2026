using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// "Chega perto, aperta E, acontece QUALQUER COISA."
//
// O projeto ja tinha o E, mas so amarrado a uma coisa: o DialogueStarter, que abre a caixa
// de dialogo e PARA o jogo. Para as falas marcadas AD no roteiro isso e o oposto do que se
// quer — elas sao bark, popup que nao para nada.
//
// Este componente e so a ponte que faltava: registra o prompt do E e dispara um UnityEvent.
// O que o E CAUSA fica no Inspector, nao aqui. Para uma fala AD, ligue no
// BarkTrigger.Fire() de um BarkTrigger em modo Manual.
//
// COMO MONTAR (o caso do Marcus na mesa)
//   Um objeto FILHO do Marcus, com:
//     - Collider2D marcado Is Trigger (o alcance da conversa)
//     - BarkTrigger:  Trigger = Manual,  Action = SingleLine,
//                     Speaker Id = marcus,  Line = a fala do roteiro
//     - InteractAction: On Interact -> esse mesmo BarkTrigger -> Fire()
//   E deixe o objeto DESLIGADO na cena: quem liga e o NpcEntrance quando ele chega.
[DisallowMultipleComponent]
public class InteractAction : MonoBehaviour
{
    [Header("Prompt")]
    [Tooltip("Letra mostrada no balaozinho. O resto do jogo usa E.")]
    public string label = "E";

    [Tooltip("Onde o E flutua. Vazio = descobre sozinho: se houver um BarkBook aqui, o E " +
             "vai para cima do PERSONAGEM que fala; sem isso, para cima deste objeto.\n\n" +
             "Descobrir sozinho importa porque o objeto de conversa costuma ficar parado " +
             "numa marca, e o personagem anda. Ancorar na marca deixava o E boiando longe " +
             "de quem fala assim que ele parava alguns passos adiante.")]
    public Transform promptAnchor;

    [Header("O que acontece")]
    public UnityEvent onInteract;

    [Tooltip("Depois de disparar uma vez, o E nao aparece mais aqui.")]
    public bool onceOnly;

    [Header("Condicao")]
    [Tooltip("So oferece o E com este progresso gravado (SaveManager). Vazio = sem condicao.")]
    public string requiresProgress = "";

    public string playerTag = "Player";

    private bool used;
    private bool playerInside;

    // OnTriggerEnter2D so dispara na ENTRADA. Este objeto costuma ser LIGADO no exato
    // momento em que o NPC chega ao lugar dele — e nessa hora o jogador ja esta parado do
    // lado. Ele nunca "entra", porque ja estava dentro quando o gatilho nasceu, e o E
    // simplesmente nunca aparecia. Aqui a chegada e conferida na mao.
    private void OnEnable() => StartCoroutine(CheckAlreadyInside());

    // A conferencia espera UM passo de fisica antes de medir, e isso nao e cautela: os
    // bounds de um collider recem-ativado ainda nao existem no quadro do SetActive. O
    // motor de fisica so os calcula no proximo FixedUpdate, e ate la col.bounds vem
    // zerado. Medindo na hora, "o jogador esta dentro?" respondia sempre NAO, e o E so
    // aparecia quando algo mais tarde reavaliava — sair e voltar, ou um outro trigger.
    //
    // Era exatamente o "demora a aparecer depois que o NPC chega": o objeto acende no
    // instante certo, mas a medida feita nesse instante nao vale nada.
    private IEnumerator CheckAlreadyInside()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogWarning($"[InteractAction] '{name}': nao tem Collider2D nenhum — nao ha " +
                             "area de alcance, o E nunca vai aparecer.", this);
            yield break;
        }

        if (!col.isTrigger)
            Debug.LogWarning($"[InteractAction] '{name}': o Collider2D NAO esta marcado como " +
                             "Is Trigger. Sem isso ele vira parede e nao dispara nada.", this);

        yield return new WaitForFixedUpdate();

        if (this == null || !isActiveAndEnabled) yield break;

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null) yield break;

        Collider2D pc = p.GetComponent<Collider2D>();
        bool inside = pc != null ? col.bounds.Intersects(pc.bounds)
                                 : col.bounds.Contains(p.transform.position);

        Debug.Log($"[InteractAction] '{name}': ligado em {transform.position}, alcance " +
                  $"{col.bounds.size}. Jogador em {p.transform.position} — " +
                  $"{(inside ? "JA ESTA dentro, oferecendo o prompt agora" : "esta fora, espera ele entrar")}.", this);

        if (inside) OnTriggerEnter2D(pc != null ? pc : p.GetComponent<Collider2D>());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        if (!other.CompareTag(playerTag)) return;

        // Cada recusa daqui em diante fazia o E simplesmente nao aparecer, sem uma palavra.
        // "O prompt nao funciona" tem cinco causas possiveis e do lado de fora sao iguais.
        if (used && onceOnly)
        {
            Debug.Log($"[InteractAction] '{name}': jogador entrou, mas ja foi usado uma vez " +
                      "(Once Only ligado).", this);
            return;
        }

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null)
            {
                Debug.LogWarning($"[InteractAction] '{name}': exige o progresso " +
                                 $"'{requiresProgress}' mas nao ha SaveManager na cena.", this);
                return;
            }
            if (!SaveManager.Instance.HasProgress(requiresProgress))
            {
                Debug.Log($"[InteractAction] '{name}': jogador entrou, mas o progresso " +
                          $"'{requiresProgress}' ainda nao foi gravado.", this);
                return;
            }
        }

        if (InteractButton.Instance == null)
            Debug.LogWarning($"[InteractAction] '{name}': nao ha InteractButton na cena — " +
                             "nao existe onde desenhar o E.", this);
        else
            Debug.Log($"[InteractAction] '{name}': jogador entrou, prompt '{label}' oferecido.", this);

        playerInside = true;
        InteractButton.Instance?.SetInteraction(this, label, Press, ResolveAnchor());
    }

    // Quem tem arte manda. Um objeto de conversa vazio nao tem, e medir pela arte em que
    // ele esta em cima poe o E no topo-centro do tapete em vez de sobre quem fala.
    private Transform ResolveAnchor()
    {
        if (promptAnchor != null) return promptAnchor;

        BarkBook book = GetComponent<BarkBook>();
        if (book == null) book = GetComponentInParent<BarkBook>();

        if (book != null && BarkDirector.Instance != null)
        {
            CharacterDialogue speaker = BarkDirector.Instance.Find(book.SpeakerId);
            if (speaker != null) return speaker.transform;
        }

        return transform;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // Desligar o objeto com o jogador dentro deixaria o E preso na tela para sempre, porque
    // o OnTriggerExit nunca chega. Limpar aqui e o que impede isso.
    private void OnDisable()
    {
        if (playerInside) InteractButton.Instance?.ClearInteraction(this);
        playerInside = false;
    }

    private void Press()
    {
        used = true;

        int wired = onInteract != null ? onInteract.GetPersistentEventCount() : 0;

        // Sem nada ligado no Inspector, fala com o BarkBook que estiver AQUI mesmo. Este e
        // o caso normal — "chega perto do personagem, aperta E, ele fala" — e exigir um
        // arraste manual para isso ja custou tres tentativas perdidas: UnityEvent nao
        // sobrevive a escrita por YAML, entao o padrao tem que funcionar sem ele.
        //
        // Um On Interact preenchido continua mandando; isto so cobre o vazio.
        if (wired == 0)
        {
            BarkBook book = GetComponent<BarkBook>();
            if (book == null) book = GetComponentInParent<BarkBook>();

            if (book != null)
            {
                Debug.Log($"[InteractAction] '{name}': E apertado, falando pelo BarkBook " +
                          "deste objeto (On Interact esta vazio).", this);
                book.Speak();
                Rearm();
                return;
            }

            Debug.LogWarning($"[InteractAction] '{name}': E foi apertado, On Interact esta vazio " +
                             "e nao ha BarkBook neste objeto nem no pai — nada para fazer.", this);
        }
        else
        {
            Debug.Log($"[InteractAction] '{name}': E apertado, disparando {wired} acao(oes).", this);
        }

        onInteract?.Invoke();
        Rearm();
    }

    // O InteractButton descarta o registro a CADA aperto — de proposito, para um E segurado
    // nao disparar a interacao varias vezes no meio dela. Quem registrou e que tem de voltar
    // a se registrar. Sem isto era preciso sair do collider e entrar de novo para o E
    // reaparecer, o que na pratica parecia que o prompt tinha travado.
    private void Rearm()
    {
        if (onceOnly) { InteractButton.Instance?.ClearInteraction(this); return; }
        if (!playerInside) return;

        InteractButton.Instance?.SetInteraction(this, label, Press, ResolveAnchor());
    }
}
