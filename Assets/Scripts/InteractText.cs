using UnityEngine;
using System.Collections.Generic;

// Objeto examinavel: chega perto, aperta E, e o JOSH comenta.
//
// ------------------------------------------------------------------ o rework, e por que
//
// Antes cada objeto examinavel desenhava a propria fala: tinha um TextMeshPro proprio, uma
// posicao propria, um fade proprio e ajustes de X e Y so dele. Eram DOIS sistemas de texto
// flutuante no jogo, e toda correcao precisava ser feita duas vezes — foi o que fez o texto
// do poco sair com tamanho, altura e posicao diferentes dos personagens, mesmo com os
// numeros iguais no asset.
//
// A percepcao que desfaz tudo isso: **quem fala nunca e o objeto, e sempre o Josh**. Um
// poco nao diz "nao ia querer cair ai" — o Josh diz, olhando para o poco. Entao isto nao e
// um sistema de texto: e um bark do Josh, disparado por um objeto.
//
// O que sobra aqui e a lista de falas e o gatilho. Fonte, tamanho, altura, cor, tempo e
// fade vem do CharacterDialogue do Josh, como qualquer outra fala dele. Ajustar o texto do
// jogo inteiro volta a ser um lugar so.
//
// ------------------------------------------------------------------ o prompt
//
// O E continua ancorado NO OBJETO, e nao no Josh, de proposito: ele marca o que voce vai
// examinar. So a fala e que sai do Josh.
public class InteractDialogue : MonoBehaviour
{
    [Header("Quem comenta")]
    [Tooltip("Bark Id de quem fala. Praticamente sempre o Josh — texto de objeto examinado " +
             "e ele comentando o que ve.")]
    public string speakerId = "josh";

    [Header("Falas")]
    [Tooltip("Ditas em ordem, uma por aperto de E. No fim, volta para a primeira.")]
    public List<string> dialogueLines = new List<string>();

    [Tooltip("Letra do prompt.")]
    public string interactLabel = "E";

    private bool isPlayerInRange;
    private int currentIndex;

    // ------------------------------------------------------------------ gatilho

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        isPlayerInRange = true;

        if (dialogueLines.Count > 0)
            SendToInteractButton();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        isPlayerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    private void OnDisable()
    {
        isPlayerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    private void SendToInteractButton()
    {
        InteractButton.Instance?.SetInteraction(this, interactLabel, OnInteractPressed);
    }

    // ------------------------------------------------------------------ falar

    public void OnInteractPressed()
    {
        if (!isPlayerInRange || dialogueLines.Count == 0) return;

        ShowNextDialogue();

        // O InteractButton solta o registro a cada aperto. Sem re-registrar, o E so voltava
        // depois de sair do collider e entrar de novo — e passar as linhas em sequencia
        // ficava impossivel.
        if (isPlayerInRange)
            SendToInteractButton();
    }

    public void ShowNextDialogue()
    {
        if (!enabled || dialogueLines.Count == 0) return;

        string line = dialogueLines[currentIndex];
        currentIndex = (currentIndex + 1) % dialogueLines.Count;

        CharacterDialogue speaker = ResolveSpeaker();
        if (speaker == null)
        {
            Debug.LogWarning($"'{name}': nao achei quem fala com o bark id '{speakerId}'. " +
                             "Texto de objeto examinado sai pela boca do Josh, entao ele " +
                             "precisa existir na cena com a tag Player.", this);
            return;
        }

        speaker.Show(line, BarkPriority.Scripted);
    }

    public void ResetSequence() => currentIndex = 0;

    // Acha o CharacterDialogue de quem fala — e cria um se o Josh ainda nao tiver.
    //
    // Criar na hora, em vez de exigir que alguem monte o componente no prefab do Player, e
    // o que faz isto funcionar sem nenhum passo manual. O balao nasce pelo TextStyle, igual
    // ao de qualquer personagem, entao ja sai com a fonte, o tamanho e a altura certos.
    private CharacterDialogue ResolveSpeaker()
    {
        CharacterDialogue found = BarkDirector.Instance != null
            ? BarkDirector.Instance.Find(speakerId)
            : null;

        if (found != null) return found;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        found = player.GetComponentInChildren<CharacterDialogue>();

        if (found == null)
        {
            found = player.AddComponent<CharacterDialogue>();
            found.barkId = speakerId;

            // DESLIGAR a conversa fiada. O campo nasce ligado por padrao, e nos prefabs do
            // Marcus e da Erika ele esta desligado — era essa a assimetria que fazia a fala
            // do Josh nao aparecer: a rotina ociosa nao tem nenhuma linha para dizer e
            // desliga o balao logo depois, por cima da fala que acabou de ser pedida.
            found.autoPlayRandomDialogue = false;

            // Registrar na mao: o OnEnable do componente ja rodou com o barkId ainda vazio,
            // entao o registro automatico dele nao pegou.
            BarkDirector.Register(found);

            Debug.Log($"'{name}': o Player nao tinha CharacterDialogue — criei um com bark id " +
                      $"'{speakerId}'. Para controlar as falas dele pelo Inspector, adicione o " +
                      "componente no prefab do Player.", player);
        }

        return found;
    }
}
