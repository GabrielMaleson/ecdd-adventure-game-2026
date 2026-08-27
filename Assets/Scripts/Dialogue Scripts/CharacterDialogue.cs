using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// How much right a line has to be on screen. A line only interrupts one of STRICTLY
// lower priority — so idle chatter can never stomp something the story asked for.
public enum BarkPriority
{
    Idle = 0,       // ambient chatter on a timer
    Reactive = 1,   // responses to something the player did
    Scripted = 2    // asked for by name from .yarn or a BarkTrigger
}

// Floating world-space text above a character. Filled three ways:
//   - idle chatter on a timer (a BarkSet asset)
//   - a specific line pushed in by BarkDirector / BarkTrigger / a UnityEvent
//   - a line of a Yarn conversation, routed here by BarkPresenter
// Whichever it is, the line holds, then floats up and fades.
public class CharacterDialogue : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Id this character answers to from .yarn — <<bark marcus \"...\">>. Case-insensitive. " +
             "Should match the CHARACTER NAME used in the .yarn lines so bark conversations can find it.")]
    public string barkId;

    [Header("Text")]
    [Tooltip("World-space TextMeshPro positioned above the character. Assign in the Inspector.")]
    public TextMeshPro dialogueText;

    [Tooltip("Somado a altura global do TextStyle, so para ESTE objeto. Existe porque a "
           + "altura global e um numero ABSOLUTO somado ao topo da arte, e as artes tem "
           + "alturas muito diferentes: um personagem em escala 4 tem varios units (com "
           + "espaco vazio acima da cabeca), um poco tem dois. O valor que acerta um erra "
           + "o outro. Use isto para a excecao, sem mexer no global.")]
    public float heightOffset;

    [Header("Estilo")]
    [Tooltip("Ligado: fonte, cor, tamanho e posicao vem do TextStyle global "
           + "(Assets/Resources/TextStyle.asset). Desligado: este objeto ignora o global "
           + "e mantem o que estiver escrito nele — util para uma placa ou uma fala que "
           + "precisa ser diferente de todo o resto. Os dois deslocamentos valem nos dois casos.")]
    public bool useUniversalSettings = true;

    [Tooltip("Deslocamento HORIZONTAL so deste objeto, somado ao global. Positivo vai para a direita.")]
    public float offsetX;

    [Header("Idle Dialogue")]
    [Tooltip("De onde saem as falas de conversa fiada deste personagem.")]
    public BarkSet barkSet;

    public bool autoPlayRandomDialogue = true;
    [Tooltip("Random delay range between idle lines.")]
    public float minInterval = 3f;
    public float maxInterval = 8f;

    [Header("Idle Gating")]
    [Tooltip("Idle chatter only runs while the player is within this distance. 0 = no distance check.")]
    public float idleRange = 14f;
    [Tooltip("Idle chatter also requires the character to be on screen. Needs a Renderer somewhere on this object.")]
    public bool idleRequiresVisible = true;

    [Header("Display Timing")]
    [Tooltip("Minimum time a line stays fully visible before it floats up and fades.")]
    public float holdDuration = 1.5f;
    [Tooltip("Long lines stay up longer instead of every line getting the same slot.")]
    public bool scaleDurationWithLength = true;
    public float secondsPerCharacter = 0.045f;
    public float maxHoldDuration = 6f;

    [Header("Float & Fade")]
    [Tooltip("Duracao do fade out, em segundos. A fala nao se move — so apaga no lugar.")]
    public float fadeOutDuration = 0.15f;

    private Vector3 originalLocalPosition;
    private Color originalColor;
    // QUEM esta com fala na tela agora, sem passar por registro nenhum.
    //
    // O BarkDirector respondia isso varrendo o dicionario de speakers, que e indexado por
    // barkId — dois personagens com o mesmo id e so um cabe la. Se quem esta falando for o
    // outro, a busca nao acha ninguem e o prompt de E continua na tela por cima da fala.
    // Aqui e o proprio falante que se anuncia, entao nao ha como escapar.
    private static readonly HashSet<CharacterDialogue> mostrando = new HashSet<CharacterDialogue>();

    public static bool AlgumMostrando => mostrando.Count > 0;

    private Coroutine displayRoutineBacking;

    // Propriedade, e nao campo, de proposito: todas as atribuicoes que ja existiam pelo
    // arquivo passam a manter o conjunto acima em dia sozinhas.
    private Coroutine displayRoutine
    {
        get => displayRoutineBacking;
        set
        {
            displayRoutineBacking = value;
            if (value != null) mostrando.Add(this);
            else mostrando.Remove(this);
        }
    }
    private Coroutine randomRoutine;
    private Renderer visibilityRenderer;

    // Priority of whatever is on screen right now. Meaningless while nothing is showing.
    private BarkPriority currentPriority = BarkPriority.Idle;

    // Shuffled bag so a line can't come up twice in a row while others go unused.
    private readonly List<int> bag = new List<int>();

    public bool IsShowing => displayRoutine != null;

    private void Awake()
    {
        if (dialogueText == null)
            dialogueText = TextStyle.CreateWorldLabel(transform, name + "Bark");
        else
            TextStyle.PlaceWorldLabel(transform, dialogueText, offsetX, heightOffset);

        ApplyTextStyle();

        originalLocalPosition = dialogueText.transform.localPosition;
        // Keep only the RGB. Alpha is driven entirely by the fade, so re-reading a
        // mid-fade colour here can never leave the line stuck semi-transparent.
        originalColor = new Color(dialogueText.color.r, dialogueText.color.g, dialogueText.color.b, 1f);
        dialogueText.gameObject.SetActive(false);

        visibilityRenderer = GetComponentInChildren<Renderer>();
    }

    // Font and size come from the single Assets/Resources/TextStyle.asset, so barks match
    // the interact prompt and everything else without being tuned per character.
    public void ApplyTextStyle()
    {
        // Desligado, o objeto e deixado inteiramente em paz: fonte, cor e tamanho ficam
        // como foram autorados. So os dois deslocamentos continuam valendo.
        if (useUniversalSettings)
            TextStyle.Apply(dialogueText, TextStyle.Role.WorldText);

        // A ALTURA tambem, e nao so fonte/cor/tamanho. Sem esta linha, mexer em
        // World Text Height no asset nao movia nada: a altura so era aplicada no Awake, ou
        // seja, so ao entrar em Play — no editor o campo parecia morto.
        if (dialogueText != null)
        {
            TextStyle.PlaceWorldLabel(transform, dialogueText, offsetX, heightOffset);

            // A animacao sobe a partir de uma posicao de repouso guardada no Awake. Mudar a
            // altura sem atualizar essa referencia faria a fala voltar para a altura velha
            // no primeiro float. So enquanto ninguem esta falando, para nao teleportar uma
            // fala no meio da subida.
            if (displayRoutine == null)
                originalLocalPosition = dialogueText.transform.localPosition;
        }

        TextStyle style = TextStyle.Current;
        if (style == null || !useUniversalSettings) return;

        if (style.holdDuration > 0f)    holdDuration    = style.holdDuration;
        if (style.fadeOutDuration > 0f) fadeOutDuration = style.fadeOutDuration;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // F4: a medida da arte de TODO mundo que fala, lado a lado, com o balao de cada um.
    // Existe porque "a altura nao bate entre dois personagens" so se resolve comparando os
    // numeros dos dois no MESMO instante — separado, cada um parece plausivel.
    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb[UnityEngine.InputSystem.Key.F4].wasPressedThisFrame) return;
        if (BarkDirector.Instance == null || !ReferenceEquals(this, FirstSpeaker())) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Bark F4] altura global={TextStyle.Current?.worldTextHeight}");

        foreach (CharacterDialogue c in FindObjectsByType<CharacterDialogue>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string art = VisibleArt.TryGetBounds(c.transform, out Bounds b)
                ? $"arte y[{b.min.y:F2}..{b.max.y:F2}] (altura {b.size.y:F2})"
                : "SEM arte";

            string lbl = c.dialogueText != null
                ? $"balao world={c.dialogueText.transform.position.y:F2} local={c.dialogueText.transform.localPosition.y:F2}"
                : "sem balao";

            sb.AppendLine($"   {c.barkId,-8} pivo y={c.transform.position.y:F2}  {art}  {lbl}");

            foreach (SpriteRenderer sr in c.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sprite != null)
                    sb.AppendLine($"        sprite '{sr.name}' y[{sr.bounds.min.y:F2}..{sr.bounds.max.y:F2}] enabled={sr.enabled}");
        }

        Debug.Log(sb.ToString());
    }

    private static CharacterDialogue FirstSpeaker()
    {
        CharacterDialogue[] all = FindObjectsByType<CharacterDialogue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }
#endif

    private void OnEnable()
    {
        BarkDirector.Register(this);

        if (autoPlayRandomDialogue)
            StartRandomDialogue();
    }

    private void OnDisable()
    {
        mostrando.Remove(this);

        // Without this a character disabled mid-line leaves its text hanging on screen.
        BarkDirector.Unregister(this);
        StopRandomDialogue();
        HideDialogue();
    }

    // ---------------------------------------------------------------- showing lines

    // Kept parameterless-priority so existing Inspector UnityEvent wiring
    // (which can only pass one string) still works and comes in as Idle.
    public void ShowDialogue(string text)
    {
        Show(text, BarkPriority.Idle);
    }

    // Returns how long the line will be on screen, or 0 if it was refused.
    // BarkPresenter uses the return value to know how long to wait.
    public float Show(string text, BarkPriority priority)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        // Cada recusa daqui era um return 0 mudo: a fala simplesmente nao aparecia e nao
        // havia uma linha no Console para explicar. Agora cada motivo se identifica, porque
        // "o bark nao aparece" sem causa nomeada custa uma sessao inteira de tentativa e erro.
        if (!enabled)
        {
            Debug.LogWarning($"Bark de '{name}' ignorado: o componente CharacterDialogue esta " +
                             "DESMARCADO no Inspector.", this);
            return 0f;
        }

        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"Bark de '{name}' ignorado: o objeto (ou algum pai dele) esta " +
                             "desativado na cena.", this);
            return 0f;
        }

        // Sem balao nao ha onde escrever. Em vez de desistir calado, cria um agora — e a
        // mesma coisa que o Awake faria com o campo vazio. Isto elimina a falha em vez de
        // so denuncia-la.
        if (dialogueText == null)
        {
            dialogueText = TextStyle.CreateWorldLabel(transform, name + "Bark");

            if (dialogueText == null)
            {
                Debug.LogWarning($"Bark de '{name}' ignorado: o campo Dialogue Text esta vazio " +
                                 "e nao foi possivel criar um balao (falta o asset TextStyle " +
                                 "em Assets/Resources).", this);
                return 0f;
            }

            originalLocalPosition = dialogueText.transform.localPosition;
            originalColor = dialogueText.color;
            dialogueText.gameObject.SetActive(false);
        }

        // Fala nova SUBSTITUI a que esta na tela, salvo quando e menos importante que ela.
        //
        // Antes era `<=`: prioridade IGUAL tambem era descartada. Isso quebrava o caso mais
        // comum que existe — apertar E duas vezes seguidas no mesmo personagem. A segunda
        // linha chegava como Scripted, encontrava uma Scripted ainda na tela, e sumia sem
        // dizer nada. Continuar mudo quando o jogador acabou de pedir a proxima fala e o
        // oposto do que ele pediu.
        //
        // Menos importante continua sendo descartado: conversa fiada nunca atropela roteiro.
        if (displayRoutine != null && priority < currentPriority)
            return 0f;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        currentPriority = priority;
        float duration = DurationFor(text);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // "A fala nao aparece" tem varias causas que de fora sao identicas: balao fora da
        // tela, alpha zero, fonte minuscula, camada errada. Uma linha com os numeros reais
        // separa todas de uma vez.
        Renderer r = dialogueText.GetComponent<Renderer>();

        // A MEDIDA de onde o balao deveria estar, e nao so onde ele esta: a altura e somada
        // ao topo da arte de cada um, entao para saber por que dois personagens nao batem e
        // preciso ver o topo da arte dos dois lado a lado.
        string art = VisibleArt.TryGetBounds(transform, out Bounds ab)
            ? $"arte y[{ab.min.y:F2}..{ab.max.y:F2}] altura={ab.size.y:F2} centro.x={ab.center.x:F2}"
            : "SEM arte medivel";
        Debug.Log($"[Bark] '{barkId}' vai dizer \"{text}\" por {duration:F2}s. " +
                  $"Balao em {dialogueText.transform.position} (escala {dialogueText.transform.lossyScale}), " +
                  $"fontSize={dialogueText.fontSize}, alpha={dialogueText.color.a}, " +
                  $"camada={(r != null ? UnityEngine.SortingLayer.IDToName(r.sortingLayerID) : "?")}, " +
                  $"order={(r != null ? r.sortingOrder : 0)}, ativo={dialogueText.gameObject.activeSelf}.\n" +
                  $"      dono '{name}' em {transform.position}, {art}. " +
                  $"Altura global={(TextStyle.Current != null ? TextStyle.Current.worldTextHeight : 0f)}, offset local={heightOffset}.", this);
#endif
        displayRoutine = StartCoroutine(DisplayRoutine(text, duration));
        return duration;
    }

    public void HideDialogue()
    {
        // Uma fala de roteiro em andamento nunca e apagada por quem so queria parar a
        // conversa fiada. Sem esta guarda, ligar o idle num personagem apaga o bark que
        // acabou de ser pedido.
        if (displayRoutine != null && currentPriority >= BarkPriority.Scripted) return;

        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(false);
    }

    // Total on-screen time: the hold plus the float-out, since the line is still
    // readable while it fades.
    private float DurationFor(string text)
    {
        float hold = holdDuration;

        if (scaleDurationWithLength)
            hold = Mathf.Clamp(text.Length * secondsPerCharacter, holdDuration, maxHoldDuration);

        return hold + fadeOutDuration;
    }

    // ---------------------------------------------------------------- idle chatter

    public void StartRandomDialogue()
    {
        if (!isActiveAndEnabled)
            return;

        // Sem BarkSet nao ha o que dizer, e a rotina so faz mal: ela acorda, nao acha linha
        // nenhuma, e esconde o balao — por cima da fala de roteiro que acabou de ser pedida.
        // Foi assim que a primeira fala do Josh sumiu. Um personagem que ganha o componente
        // no Inspector nasce com este campo LIGADO, entao a protecao tem de estar aqui e
        // nao no valor padrao.
        if (barkSet == null || barkSet.lines.Count == 0)
            return;

        if (randomRoutine != null)
            StopCoroutine(randomRoutine);
        randomRoutine = StartCoroutine(RandomDialogueRoutine());
    }

    public void StopRandomDialogue()
    {
        if (randomRoutine != null)
        {
            StopCoroutine(randomRoutine);
            randomRoutine = null;
        }
    }

    private IEnumerator RandomDialogueRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (!CanChatter())
                continue;

            string line = NextIdleLine();
            if (!string.IsNullOrEmpty(line))
                Show(line, BarkPriority.Idle);
        }
    }

    // Every reason idle chatter should keep its mouth shut.
    private bool CanChatter()
    {
        if (displayRoutine != null)
            return false;

        // Never talk over a real conversation — neither the blocking dialogue box
        // nor a bark conversation being run through BarkPresenter.
        if (BarkDirector.DialogueInProgress)
            return false;

        if (idleRequiresVisible && visibilityRenderer != null && !visibilityRenderer.isVisible)
            return false;

        if (idleRange > 0f)
        {
            Transform player = BarkDirector.PlayerTransform;
            if (player == null)
                return false;
            if ((player.position - transform.position).sqrMagnitude > idleRange * idleRange)
                return false;
        }

        return true;
    }

    // De onde saem as falas ociosas: o BarkSet, quando existe e a condicao dele passa.
    // Antes havia tambem uma lista digitada no proprio prefab (randomLines); foi removida
    // por nunca ter sido usada em personagem nenhum — o BarkSet e a unica fonte.
    private List<string> ActiveLines()
    {
        if (barkSet != null && barkSet.lines.Count > 0 && barkSet.ConditionsMet())
            return barkSet.lines;

        return null;
    }

    public void SetBarkSet(BarkSet set)
    {
        barkSet = set;
        bag.Clear();
    }

    // Shuffled bag: every line is used once before any repeats.
    private string NextIdleLine()
    {
        List<string> lines = ActiveLines();
        if (lines == null || lines.Count == 0)
            return null;

        if (bag.Count == 0)
        {
            for (int i = 0; i < lines.Count; i++)
                bag.Add(i);

            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            // Avoid the one case a bag still allows: the last line of the old bag
            // landing first in the new one.
            if (bag.Count > 1 && lines.Count > 1 && bag[0] == lastPlayedIndex)
                (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
        }

        int index = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);

        // The set can be swapped between picks, so an index from an older, longer
        // bag has to be treated as stale rather than thrown.
        if (index >= lines.Count)
        {
            bag.Clear();
            return null;
        }

        lastPlayedIndex = index;
        return lines[index];
    }

    private int lastPlayedIndex = -1;

    // ---------------------------------------------------------------- rendering

    private IEnumerator DisplayRoutine(string text, float totalDuration)
    {
        // Sem movimento nenhum: a fala aparece parada, fica, e apaga no lugar. O float
        // antigo (subir enquanto some) foi removido a pedido — sobrou so o fade out, e
        // curto.
        float hold = Mathf.Max(0f, totalDuration - fadeOutDuration);

        dialogueText.transform.localPosition = originalLocalPosition;
        dialogueText.color = originalColor;
        dialogueText.text = text;
        dialogueText.gameObject.SetActive(true);

        yield return new WaitForSeconds(hold);

        Color startColor = dialogueText.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            dialogueText.color = Color.Lerp(startColor, endColor, elapsed / fadeOutDuration);
            yield return null;
        }

        dialogueText.gameObject.SetActive(false);
        displayRoutine = null;
    }

    private void OnDestroy()
    {
        if (displayRoutine != null) StopCoroutine(displayRoutine);
        if (randomRoutine != null) StopCoroutine(randomRoutine);
    }
}
