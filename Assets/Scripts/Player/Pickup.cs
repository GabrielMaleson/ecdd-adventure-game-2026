using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A collectible item: key, lore fragment, whatever.
//
// Two grab modes (pick per instance in the Inspector):
//   OnTouch  — walk onto it and it's collected. Good for keys.
//   PressE   — walk into range, then press E. Good for lore ("examine this").
//
// For now it does ONE thing on pickup — swap GameObjects: hide some, show others.
// That covers this puzzle's case (the grid crypt turns into a non-grid crypt) and
// simple "a door opens" cases. When a pickup needs a different KIND of effect,
// that's a deliberate add — ask and it gets its own handling rather than being
// guessed at up front.
//
// Needs a Collider2D with Is Trigger ON marking the grab area, and the player must
// have the "Player" tag.
[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    public enum GrabMode { OnTouch, PressE }

    [Header("How it's grabbed")]
    [Tooltip("OnTouch: collected by walking onto it. PressE: walk into range, then press E.")]
    [SerializeField] GrabMode grabMode = GrabMode.OnTouch;

    [Tooltip("PressE only — prompt shown on the interact label while in range.")]
    [SerializeField] string interactLabel = "E";

    [Header("On pickup — swap GameObjects")]
    [Tooltip("Objects switched OFF when collected (e.g. the grid crypt, the blocking bush).")]
    [SerializeField] GameObject[] hide;

    [Tooltip("Objects switched ON when collected (e.g. the non-grid crypt, an open-door sprite).")]
    [SerializeField] GameObject[] show;

    [Header("The item itself")]
    [Tooltip("Hide this whole object once grabbed, so the key visually disappears. Leave on for almost everything.")]
    [SerializeField] bool removeSelfOnPickup = true;

    [Tooltip("Extra hook for anything the swap above doesn't cover — sound, a dialogue trigger, a save flag. Optional; leave empty if unused.")]
    public UnityEvent onCollected;

    [Header("Antes de coletar")]
    [Tooltip("No do .yarn tocado ANTES de pegar o item. Vazio = pega direto. " +
             "Serve para o item que tem uma cena em cima dele: a chave com o corvo em " +
             "cima, por exemplo. O item so e coletado quando a fala FECHA — senao o mundo " +
             "muda (arbusto some, cripta abre) por tras de uma caixa de dialogo, e o jogador " +
             "perde a unica coisa que a cena existia para mostrar.")]
    public string noAntesDeColetar = "";

    [Header("Depois de coletar")]
    [Tooltip("Progresso gravado no SaveManager ao pegar o item. Vazio = nao grava nada. " +
             "E o que permite o resto do jogo reagir a este item sem referencia a ele: o " +
             "gatilho que faz os amigos voltarem a seguir exige este progresso, e assim ele " +
             "nao dispara se o jogador atravessar a saida antes de pegar a chave.")]
    public string progressoAoColetar = "";

    [Tooltip("Panoramica disparada logo apos pegar o item. Referencia direta e nao " +
             "UnityEvent, para sobreviver a edicao do arquivo da cena por fora.")]
    public CameraPanTo panoramicaAoColetar;

    bool collected;
    bool playerInRange;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (grabMode == GrabMode.OnTouch)
        {
            Collect();
            return;
        }

        // PressE: register the prompt, InteractButton handles the keypress.
        playerInRange = true;
        InteractButton.Instance?.SetInteraction(this, interactLabel, Pressionado);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    void OnDisable()
    {
        playerInRange = false;
        InteractButton.Instance?.ClearInteraction(this);
    }

    // O E foi apertado. Com fala configurada, ela vem primeiro e o item so e pego depois.
    void Pressionado()
    {
        if (collected) return;

        if (string.IsNullOrEmpty(noAntesDeColetar)) { Collect(); return; }

        // Marcar ANTES de abrir a fala: o E fica livre enquanto a caixa esta na tela, e sem
        // isto um segundo aperto abriria a mesma cena de novo por cima dela.
        collected = true;
        StartCoroutine(FalarDepoisColetar());
    }

    IEnumerator FalarDepoisColetar()
    {
        bool comecou = DialogueStarter.EvaluateConditionsAndStart(
            noAntesDeColetar, false, null, false);

        if (comecou)
        {
            Yarn.Unity.DialogueRunner runner = DialogueManager.Instance?.dialogueRunner;
            while (runner != null && runner.IsDialogueRunning)
                yield return null;
        }
        else
        {
            Debug.LogWarning($"[Pickup] '{name}': o no '{noAntesDeColetar}' nao comecou. " +
                             "Confira se o nome esta igual ao title: do .yarn. Pegando o " +
                             "item assim mesmo para nao travar o jogador.", this);
        }

        collected = false;   // o Collect() abaixo faz a marcacao de verdade
        Collect();
    }

    void Collect()
    {
        if (collected) return;                 // grab once, never re-fire
        collected = true;

        foreach (var go in hide) if (go != null) go.SetActive(false);
        foreach (var go in show) if (go != null) go.SetActive(true);

        // Progresso ANTES do resto: o que vier depois — evento, panoramica, uma fala — pode
        // consultar hasprogress() logo na primeira linha, e a condicao tem de ja valer.
        if (!string.IsNullOrEmpty(progressoAoColetar))
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.AddProgress(progressoAoColetar);
            else
                Debug.LogWarning($"[Pickup] '{name}': sem SaveManager na cena — o progresso " +
                                 $"'{progressoAoColetar}' nao foi gravado.", this);
        }

        onCollected?.Invoke();

        // A panoramica roda num objeto SEPARADO, e nao neste: a linha abaixo desliga este
        // aqui, e uma corrotina morre junto com o objeto que a hospeda.
        if (panoramicaAoColetar != null) panoramicaAoColetar.Executar();

        InteractButton.Instance?.ClearInteraction(this);

        // Last — deactivating this object stops its own code, so anything above must
        // already have run.
        if (removeSelfOnPickup) gameObject.SetActive(false);
    }
}
